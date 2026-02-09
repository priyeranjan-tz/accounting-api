using System.Net;
using System.Net.Http.Json;
using Accounting.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Accounting.IntegrationTests.Invoicing;

/// <summary>
/// Integration tests for automated invoice generation across different frequencies (US4)
/// Tests: T114 (per-ride), T115 (daily), T116 (weekly), T117 (monthly), T118 (skip when no charges)
/// </summary>
public class AutomatedInvoicingTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private readonly Guid _tenantId = Guid.NewGuid();

    public AutomatedInvoicingTests()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("accounting_test")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:AccountingDb", _postgresContainer.GetConnectionString());
            });

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", _tenantId.ToString());

        // Apply migrations
        await ApplyMigrationsAsync();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

    private async Task ApplyMigrationsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Accounting.Infrastructure.Persistence.DbContext.AccountingDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// T114: Integration test for per-ride invoice generation
    /// GIVEN an account with per-ride invoice frequency
    /// WHEN a ride charge is recorded
    /// THEN an invoice is generated immediately
    /// </summary>
    [Fact]
    public async Task PerRideInvoicing_AfterRideCharge_GeneratesInvoiceImmediately()
    {
        // Arrange: Create account with per-ride frequency
        var accountId = await CreateAccountAsync(
            name: "Acme Transport - Per Ride",
            invoiceFrequency: (int)InvoiceFrequency.PerRide);

        var rideId = Guid.NewGuid();
        var fareAmount = 25.50m;
        var rideDate = DateTime.UtcNow.Date;

        // Act: Record ride charge
        var chargeResponse = await _client.PostAsJsonAsync("/ledger/charges", new
        {
            rideId = rideId,
            accountId = accountId,
            fareAmount = fareAmount,
            tipAmount = 0m,
            tollAmount = 0m,
            otherFees = 0m,
            serviceDate = rideDate
        });

        chargeResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert: Invoice generated immediately
        var invoicesResponse = await _client.GetAsync($"/invoices?accountId={accountId}");
        invoicesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invoicesResult = await invoicesResponse.Content.ReadFromJsonAsync<ListInvoicesResponse>();
        invoicesResult.Should().NotBeNull();
        invoicesResult!.Invoices.Should().HaveCount(1, "per-ride frequency should generate invoice immediately");

        var invoice = invoicesResult.Invoices.First();
        invoice.AccountId.Should().Be(accountId);
        invoice.TotalAmount.Should().Be(fareAmount);
        invoice.BillingPeriodStart.Date.Should().Be(rideDate);
        invoice.BillingPeriodEnd.Date.Should().Be(rideDate);
        invoice.LineItems.Should().HaveCount(1);
        invoice.LineItems.First().RideId.Should().Be(rideId);
    }

    /// <summary>
    /// T115: Integration test for daily invoice generation
    /// GIVEN an account with daily invoice frequency and multiple ride charges over 2 days
    /// WHEN scheduled invoice generation runs for yesterday
    /// THEN an invoice is generated with charges from yesterday only
    /// </summary>
    [Fact]
    public async Task DailyInvoicing_WithMultipleCharges_GeneratesInvoiceForYesterday()
    {
        // Arrange: Create account with daily frequency
        var accountId = await CreateAccountAsync(
            name: "Daily Billing Corp",
            invoiceFrequency: (int)InvoiceFrequency.Daily);

        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var twoDaysAgo = DateTime.UtcNow.Date.AddDays(-2);
        var today = DateTime.UtcNow.Date;

        // Record charges across multiple days
        await RecordTestChargeAsync(accountId, Guid.NewGuid(), 10.00m, twoDaysAgo);
        await RecordTestChargeAsync(accountId, Guid.NewGuid(), 15.00m, yesterday);
        await RecordTestChargeAsync(accountId, Guid.NewGuid(), 20.00m, yesterday);
        await RecordTestChargeAsync(accountId, Guid.NewGuid(), 25.00m, today);

        // Act: Trigger scheduled invoice generation for yesterday (daily frequency)
        var generateResponse = await _client.PostAsJsonAsync("/invoices/scheduled", new
        {
            frequency = (int)InvoiceFrequency.Daily,
            generationDate = today
        });

        // Assert
        generateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var generateResult = await generateResponse.Content.ReadFromJsonAsync<GenerateScheduledInvoicesResponse>();
        generateResult.Should().NotBeNull();
        generateResult!.InvoicesGenerated.Should().Be(1, "one invoice should be generated for daily frequency");

        // Verify invoice contains only yesterday's charges
        var invoicesResponse = await _client.GetAsync($"/invoices?accountId={accountId}");
        var invoicesResult = await invoicesResponse.Content.ReadFromJsonAsync<ListInvoicesResponse>();

        var invoice = invoicesResult!.Invoices.Should().ContainSingle().Subject;
        invoice.TotalAmount.Should().Be(35.00m, "invoice should contain yesterday's charges (15 + 20)");
        invoice.BillingPeriodStart.Date.Should().Be(yesterday);
        invoice.BillingPeriodEnd.Date.Should().Be(yesterday);
        invoice.LineItems.Should().HaveCount(2, "two charges from yesterday");
    }

    /// <summary>
    /// T116: Integration test for weekly invoice generation
    /// GIVEN an account with weekly invoice frequency and charges over 8 days
    /// WHEN scheduled invoice generation runs on Sunday
    /// THEN an invoice is generated with charges from the last 7 days
    /// </summary>
    [Fact]
    public async Task WeeklyInvoicing_OnSunday_GeneratesInvoiceForLastWeek()
    {
        // Arrange: Create account with weekly frequency
        var accountId = await CreateAccountAsync(
            name: "Weekly Billing Inc",
            invoiceFrequency: (int)InvoiceFrequency.Weekly);

        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);
        var twoDaysAgo = today.AddDays(-2);
        var sixDaysAgo = today.AddDays(-6);
        var eightDaysAgo = today.AddDays(-8);

        // Record charges across different days
        await RecordTestChargeAsync(accountId, Guid.NewGuid(), 5.00m, eightDaysAgo);  // Outside period
        await RecordTestChargeAsync(accountId, Guid.NewGuid(), 10.00m, sixDaysAgo);   // Within period
        await RecordTestChargeAsync(accountId, Guid.NewGuid(), 15.00m, twoDaysAgo);   // Within period
        await RecordTestChargeAsync(accountId, Guid.NewGuid(), 20.00m, yesterday);   // Within period

        // Act: Trigger weekly invoice generation
        var generateResponse = await _client.PostAsJsonAsync("/invoices/scheduled", new
        {
            frequency = (int)InvoiceFrequency.Weekly,
            generationDate = today
        });

        // Assert
        generateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var generateResult = await generateResponse.Content.ReadFromJsonAsync<GenerateScheduledInvoicesResponse>();
        generateResult.Should().NotBeNull();
        generateResult!.InvoicesGenerated.Should().Be(1);

        // Verify invoice contains last 7 days of charges (excluding 8 days ago)
        var invoicesResponse = await _client.GetAsync($"/invoices?accountId={accountId}");
        var invoicesResult = await invoicesResponse.Content.ReadFromJsonAsync<ListInvoicesResponse>();

        var invoice = invoicesResult!.Invoices.Should().ContainSingle().Subject;
        invoice.TotalAmount.Should().Be(45.00m, "invoice should contain last 7 days charges (10 + 15 + 20)");
        invoice.BillingPeriodStart.Date.Should().Be(today.AddDays(-7));
        invoice.BillingPeriodEnd.Date.Should().Be(yesterday);
        invoice.LineItems.Should().HaveCount(3);
    }

    /// <summary>
    /// T117: Integration test for monthly invoice generation
    /// GIVEN an account with monthly invoice frequency and charges in last month
    /// WHEN scheduled invoice generation runs on the 1st of the month
    /// THEN an invoice is generated with charges from the previous month
    /// </summary>
    [Fact]
    public async Task MonthlyInvoicing_OnFirstOfMonth_GeneratesInvoiceForLastMonth()
    {
        // Arrange: Create account with monthly frequency
        var accountId = await CreateAccountAsync(
            name: "Monthly Billing LLC",
            invoiceFrequency: (int)InvoiceFrequency.Monthly);

        // Calculate last month's dates
        var today = DateTime.UtcNow.Date;
        var firstOfThisMonth = new DateTime(today.Year, today.Month, 1);
        var lastMonth = firstOfThisMonth.AddMonths(-1);
        var firstDayLastMonth = new DateTime(lastMonth.Year, lastMonth.Month, 1);
        var lastDayLastMonth = firstDayLastMonth.AddMonths(1).AddDays(-1);
        var twoMonthsAgo = firstDayLastMonth.AddDays(-1);

        // Record charges
        await RecordTestChargeAsync(accountId, Guid.NewGuid(), 5.00m, twoMonthsAgo);        // Outside period
        await RecordTestChargeAsync(accountId, Guid.NewGuid(), 100.00m, firstDayLastMonth); // Within period
        await RecordTestChargeAsync(accountId, Guid.NewGuid(), 150.00m, lastMonth.AddDays(15)); // Within period
        await RecordTestChargeAsync(accountId, Guid.NewGuid(), 200.00m, lastDayLastMonth);  // Within period

        // Act: Trigger monthly invoice generation (simulating running on first of this month)
        var generateResponse = await _client.PostAsJsonAsync("/invoices/scheduled", new
        {
            frequency = (int)InvoiceFrequency.Monthly,
            generationDate = firstOfThisMonth
        });

        // Assert
        generateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var generateResult = await generateResponse.Content.ReadFromJsonAsync<GenerateScheduledInvoicesResponse>();
        generateResult.Should().NotBeNull();
        generateResult!.InvoicesGenerated.Should().Be(1);

        // Verify invoice contains last month's charges only
        var invoicesResponse = await _client.GetAsync($"/invoices?accountId={accountId}");
        var invoicesResult = await invoicesResponse.Content.ReadFromJsonAsync<ListInvoicesResponse>();

        var invoice = invoicesResult!.Invoices.Should().ContainSingle().Subject;
        invoice.TotalAmount.Should().Be(450.00m, "invoice should contain last month's charges (100 + 150 + 200)");
        invoice.BillingPeriodStart.Date.Should().Be(firstDayLastMonth);
        invoice.BillingPeriodEnd.Date.Should().Be(lastDayLastMonth);
        invoice.LineItems.Should().HaveCount(3);
    }

    /// <summary>
    /// T118: Integration test for skipping invoice when no charges in period
    /// GIVEN an account with daily invoice frequency and NO charges yesterday
    /// WHEN scheduled invoice generation runs
    /// THEN no invoice is generated (skip empty periods)
    /// </summary>
    [Fact]
    public async Task ScheduledInvoicing_NoChargesInPeriod_SkipsInvoiceGeneration()
    {
        // Arrange: Create account with daily frequency
        var accountId = await CreateAccountAsync(
            name: "No Charges Account",
            invoiceFrequency: (int)InvoiceFrequency.Daily);

        var today = DateTime.UtcNow.Date;
        var twoDaysAgo = today.AddDays(-2);

        // Record charge 2 days ago (outside yesterday's billing period)
        await RecordTestChargeAsync(accountId, Guid.NewGuid(), 50.00m, twoDaysAgo);

        // Act: Trigger daily invoice generation (should find no charges for yesterday)
        var generateResponse = await _client.PostAsJsonAsync("/invoices/scheduled", new
        {
            frequency = (int)InvoiceFrequency.Daily,
            generationDate = today
        });

        // Assert
        generateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var generateResult = await generateResponse.Content.ReadFromJsonAsync<GenerateScheduledInvoicesResponse>();
        generateResult.Should().NotBeNull();
        generateResult!.AccountsProcessed.Should().Be(1, "account was processed");
        generateResult.InvoicesGenerated.Should().Be(0, "no invoice generated for empty period");

        // Verify no invoices exist for this account
        var invoicesResponse = await _client.GetAsync($"/invoices?accountId={accountId}");
        var invoicesResult = await invoicesResponse.Content.ReadFromJsonAsync<ListInvoicesResponse>();

        invoicesResult!.Invoices.Should().BeEmpty("no invoices should be generated for periods without charges");
    }

    #region Helper Methods

    private async Task<Guid> CreateAccountAsync(string name, int invoiceFrequency)
    {
        var response = await _client.PostAsJsonAsync("/accounts", new
        {
            name = name,
            accountType = (int)AccountType.Organization,
            billingContact = "billing@example.com",
            invoiceFrequency = invoiceFrequency
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateAccountResponse>();
        return result!.AccountId;
    }

    private async Task RecordTestChargeAsync(Guid accountId, Guid rideId, decimal amount, DateTime serviceDate)
    {
        var response = await _client.PostAsJsonAsync("/ledger/charges", new
        {
            rideId = rideId,
            accountId = accountId,
            fareAmount = amount,
            tipAmount = 0m,
            tollAmount = 0m,
            otherFees = 0m,
            serviceDate = serviceDate
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    #endregion

    #region Response DTOs (minimal definitions for deserialization)

    private record ListInvoicesResponse(List<InvoiceDto> Invoices, int TotalCount, int Page, int PageSize);
    private record InvoiceDto(
        Guid Id,
        string InvoiceNumber,
        Guid AccountId,
        DateTime BillingPeriodStart,
        DateTime BillingPeriodEnd,
        decimal TotalAmount,
        List<InvoiceLineItemDto> LineItems);
    private record InvoiceLineItemDto(Guid RideId, DateTime ServiceDate, decimal Amount);
    private record CreateAccountResponse(Guid AccountId);
    private record GenerateScheduledInvoicesResponse(
        int AccountsProcessed,
        int InvoicesGenerated,
        int FailedAccounts,
        DateTime GenerationDate);

    #endregion
}
