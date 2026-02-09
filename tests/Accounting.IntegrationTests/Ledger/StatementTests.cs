using Accounting.API;
using Accounting.Application.Commands;
using Accounting.Domain.Enums;
using Accounting.Infrastructure.Persistence.DbContext;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Testcontainers.PostgreSql;
using Xunit;

namespace Accounting.IntegrationTests.Ledger;

/// <summary>
/// Integration tests for account statement functionality (User Story 5)
/// </summary>
public class StatementTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("accounting_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private readonly Guid _tenantId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove existing DbContext registration
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AccountingDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    // Add test database
                    services.AddDbContext<AccountingDbContext>(options =>
                        options.UseNpgsql(_postgres.GetConnectionString()));
                });
            });

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", _tenantId.ToString());

        // Run migrations
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task GetAccountStatement_WithTransactions_ReturnsAccurateStatement()
    {
        // Arrange: Create account
        var createAccountRequest = new
        {
            name = "Test Taxi Company",
            type = "Customer",
            status = "Active",
            invoiceFrequency = "Monthly"
        };

        var createResponse = await _client.PostAsJsonAsync("/accounts", createAccountRequest);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateAccountResponse>();
        var accountId = createResult!.Id;

        // Arrange: Record 3 ride charges over 3 days
        var baseDate = new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc);
        
        var charge1 = new RecordRideChargeCommand
        {
            RideId = Guid.NewGuid().ToString(),
            AccountId = accountId,
            FareAmount = 25.50m,
            ServiceDate = baseDate,
            Description = "Ride charge 1"
        };
        await _client.PostAsJsonAsync("/ledger/record-ride-charge", charge1);

        // 1 day later
        var charge2 = new RecordRideChargeCommand
        {
            RideId = Guid.NewGuid().ToString(),
            AccountId = accountId,
            FareAmount = 30.00m,
            ServiceDate = baseDate.AddDays(1),
            Description = "Ride charge 2"
        };
        await _client.PostAsJsonAsync("/ledger/record-ride-charge", charge2);

        // 2 days later
        var charge3 = new RecordRideChargeCommand
        {
            RideId = Guid.NewGuid().ToString(),
            AccountId = accountId,
            FareAmount = 15.75m,
            ServiceDate = baseDate.AddDays(2),
            Description = "Ride charge 3"
        };
        await _client.PostAsJsonAsync("/ledger/record-ride-charge", charge3);

        // Act: Get statement for entire period
        var statementResponse = await _client.GetAsync(
            $"/accounts/{accountId}/statements?startDate={baseDate:yyyy-MM-dd}&endDate={baseDate.AddDays(3):yyyy-MM-dd}");

        // Assert: Verify statement
        Assert.Equal(HttpStatusCode.OK, statementResponse.StatusCode);
        
        var statement = await statementResponse.Content.ReadFromJsonAsync<AccountStatementResponse>();
        Assert.NotNull(statement);
        
        // Verify opening balance (should be 0)
        Assert.Equal(0m, statement.OpeningBalance);
        
        // Verify closing balance (should be sum of all charges)
        Assert.Equal(71.25m, statement.ClosingBalance); // 25.50 + 30.00 + 15.75
        
        // Verify transaction count (2 entries per ride charge = 6 total)
        Assert.Equal(6, statement.Transactions.Count);
        
        // Verify transactions are chronologically ordered
        Assert.True(statement.Transactions[0].TransactionDate <= statement.Transactions[1].TransactionDate);
        
        // Verify accounts receivable entries sum correctly
        var arTotal = statement.Transactions
            .Where(t => t.LedgerAccount == "AccountsReceivable")
            .Sum(t => t.DebitAmount);
        Assert.Equal(71.25m, arTotal);
    }

    [Fact]
    public async Task GetAccountStatement_NoTransactionsInPeriod_ReturnsEmptyStatement()
    {
        // Arrange: Create account
        var createAccountRequest = new
        {
            name = "Empty Statement Account",
            type = "Customer",
            status = "Active",
            invoiceFrequency = "Monthly"
        };

        var createResponse = await _client.PostAsJsonAsync("/accounts", createAccountRequest);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateAccountResponse>();
        var accountId = createResult!.Id;

        // Don't record any charges

        // Act: Request statement for a period with no transactions
        var futureDate = DateTime.UtcNow.AddMonths(1);
        var statementResponse = await _client.GetAsync(
            $"/accounts/{accountId}/statements?startDate={futureDate:yyyy-MM-dd}&endDate={futureDate.AddDays(7):yyyy-MM-dd}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, statementResponse.StatusCode);
        
        var statement = await statementResponse.Content.ReadFromJsonAsync<AccountStatementResponse>();
        Assert.NotNull(statement);
        Assert.Equal(0m, statement.OpeningBalance);
        Assert.Equal(0m, statement.ClosingBalance);
        Assert.Empty(statement.Transactions);
    }

    [Fact]
    public async Task GetAccountStatement_WithPagination_ReturnsPagedResults()
    {
        // Arrange: Create account and record multiple transactions
        var createAccountRequest = new
        {
            name = "Pagination Test Account",
            type = "Customer",
            status = "Active",
            invoiceFrequency = "Monthly"
        };

        var createResponse = await _client.PostAsJsonAsync("/accounts", createAccountRequest);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateAccountResponse>();
        var accountId = createResult!.Id;

        // Record 5 ride charges (10 ledger entries total)
        for (int i = 0; i < 5; i++)
        {
            var charge = new RecordRideChargeCommand
            {
                RideId = Guid.NewGuid().ToString(),
                AccountId = accountId,
                FareAmount = 10.00m,
                ServiceDate = DateTime.UtcNow,
                Description = $"Ride charge {i + 1}"
            };
            await _client.PostAsJsonAsync("/ledger/record-ride-charge", charge);
        }

        // Act: Request first page with page size 5
        var statementResponse = await _client.GetAsync(
            $"/accounts/{accountId}/statements?startDate={DateTime.UtcNow.AddDays(-1):yyyy-MM-dd}&endDate={DateTime.UtcNow:yyyy-MM-dd}&page=1&pageSize=5");

        // Assert
        Assert.Equal(HttpStatusCode.OK, statementResponse.StatusCode);
        
        var statement = await statementResponse.Content.ReadFromJsonAsync<AccountStatementResponse>();
        Assert.NotNull(statement);
        Assert.Equal(5, statement.Transactions.Count); // First 5 of 10 entries
        Assert.Equal(10, statement.TotalCount); // Total is 10 entries
    }
}

public record AccountStatementResponse(
    Guid AccountId,
    string AccountName,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal OpeningBalance,
    decimal ClosingBalance,
    List<StatementTransactionDto> Transactions,
    int TotalCount,
    int Page,
    int PageSize
);

public record StatementTransactionDto(
    Guid Id,
    DateTime TransactionDate,
    string LedgerAccount,
    decimal DebitAmount,
    decimal CreditAmount,
    string Description,
    string SourceType,
    Guid? SourceReferenceId
);
