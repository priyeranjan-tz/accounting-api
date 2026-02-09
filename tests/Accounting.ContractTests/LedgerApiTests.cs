using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Accounting.Application.Queries;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Accounting.ContractTests;

/// <summary>
/// Contract tests for Ledger API endpoints.
/// These tests validate the API contract according to ledger-api.yaml specification.
/// Tests MUST run FIRST and FAIL before implementation (Constitutional Principle III - TDD).
/// </summary>
public class LedgerApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public LedgerApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        
        // Note: In development mode, authentication middleware will use default tenant when no auth header is present
    }

    #region POST /ledger/charges

    [Fact]
    public async Task RecordRideCharge_ValidRequest_Returns201Created()
    {
        // Arrange
        var request = new
        {
            accountId = Guid.NewGuid(),
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 25.00m,
            serviceDate = DateTime.UtcNow,
            description = "Ride from 123 Main St to 456 Oak Ave"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/ledger/charges", request);

        // Assert - Contract validation
        response.StatusCode.Should().Be(HttpStatusCode.Created, 
            "valid ride charge should be recorded successfully");

        var result = await response.Content.ReadFromJsonAsync<LedgerTransactionResponse>();
        result.Should().NotBeNull("response should contain transaction details");
        result!.TransactionId.Should().NotBeEmpty("transaction ID should be assigned");
        result.AccountId.Should().Be(request.accountId, "response should contain original account ID");
        result.Entries.Should().HaveCount(2, "double-entry accounting requires 2 entries (debit AR, credit Revenue)");
        result.NewBalance.Should().BeGreaterThan(0, "ride charge should increase account balance");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5), 
            "creation timestamp should be recent");

        // Validate double-entry structure: AR debit + Revenue credit
        var arEntry = result.Entries.FirstOrDefault(e => e.LedgerAccount == "AccountsReceivable");
        var revenueEntry = result.Entries.FirstOrDefault(e => e.LedgerAccount == "ServiceRevenue");

        arEntry.Should().NotBeNull("should have Accounts Receivable entry");
        arEntry!.DebitAmount.Should().Be(request.fareAmount, "AR should be debited with fare amount");
        arEntry.CreditAmount.Should().Be(0, "AR should not be credited");

        revenueEntry.Should().NotBeNull("should have Service Revenue entry");
        revenueEntry!.CreditAmount.Should().Be(request.fareAmount, "Revenue should be credited with fare amount");
        revenueEntry.DebitAmount.Should().Be(0, "Revenue should not be debited");
    }

    [Fact]
    public async Task RecordRideCharge_DuplicateRideId_Returns409Conflict()
    {
        // Arrange - Record charge once
        var rideId = $"R-{Guid.NewGuid()}";
        var accountId = Guid.NewGuid();
        var request = new
        {
            accountId,
            rideId,
            fareAmount = 25.00m,
            serviceDate = DateTime.UtcNow
        };

        await _client.PostAsJsonAsync("/ledger/charges", request);

        // Act - Attempt duplicate charge (idempotency violation)
        var duplicateResponse = await _client.PostAsJsonAsync("/ledger/charges", request);

        // Assert - Contract validation
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict, 
            "duplicate ride charge should be rejected with 409 Conflict");

        var problemDetails = await duplicateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull("error response should use RFC 9457 Problem Details");
        problemDetails!.Status.Should().Be(409, "status code in problem details should match HTTP status");
        problemDetails.Title.Should().Contain("Duplicate", "title should indicate duplicate ride");
        problemDetails.Detail.Should().Contain(rideId, "detail should reference the conflicting ride ID");
        problemDetails.TraceId.Should().NotBeNullOrEmpty("traceId should be present for correlation");
    }

    #endregion

    #region GET /accounts/{accountId}/statements

    [Fact]
    public async Task GetAccountStatement_ValidRequest_Returns200WithTransactions()
    {
        // Arrange - Create account and record charges
        var accountId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        // Record some charges to generate transactions
        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 25.00m,
            serviceDate = DateTime.UtcNow.AddDays(-5)
        });

        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 30.00m,
            serviceDate = DateTime.UtcNow.AddDays(-3)
        });

        // Act
        var response = await _client.GetAsync(
            $"/accounts/{accountId}/statements?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}&page=1&pageSize=20");

        // Assert - Contract validation
        response.StatusCode.Should().Be(HttpStatusCode.OK, 
            "valid statement request should return 200 OK");

        var statement = await response.Content.ReadFromJsonAsync<GetAccountStatementResponse>();
        statement.Should().NotBeNull("response should contain statement details");
        statement!.AccountId.Should().Be(accountId, "statement should be for the requested account");
        statement.PeriodStart.Should().Be(startDate.Date, "statement should start at requested date");
        statement.PeriodEnd.Date.Should().Be(endDate.Date, "statement should end at requested date");
        statement.OpeningBalance.Should().BeGreaterThanOrEqualTo(0, "opening balance should be valid");
        statement.ClosingBalance.Should().BeGreaterThan(0, "closing balance should reflect transactions");
        statement.Transactions.Should().NotBeEmpty("statement should contain transaction list");
        statement.Transactions.Count.Should().BeLessOrEqualTo(20, "page size should be respected");
        statement.TotalCount.Should().BeGreaterThan(0, "total count should include all transactions");
        statement.Page.Should().Be(1, "response should reflect current page");
        statement.PageSize.Should().Be(20, "response should reflect page size");

        // Validate transaction structure
        var firstTransaction = statement.Transactions.First();
        firstTransaction.Id.Should().NotBeEmpty("transaction should have ID");
        firstTransaction.TransactionDate.Should().BeAfter(DateTime.MinValue, "transaction should have valid date");
        firstTransaction.LedgerAccount.Should().NotBeNullOrEmpty("transaction should specify ledger account");
        firstTransaction.Description.Should().NotBeNullOrEmpty("transaction should have description");
    }

    [Fact]
    public async Task GetAccountStatement_InvalidDateRange_Returns400BadRequest()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var startDate = DateTime.UtcNow;
        var endDate = DateTime.UtcNow.AddDays(-7); // Invalid: end before start

        // Act
        var response = await _client.GetAsync(
            $"/accounts/{accountId}/statements?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

        // Assert - Contract validation
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, 
            "invalid date range should return 400 Bad Request");

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull("error response should use RFC 9457 Problem Details");
        problemDetails!.Status.Should().Be(400, "status code should be 400");
        problemDetails.Detail.Should().ContainEquivalentOf("date", 
            "error message should indicate date validation issue");
    }

    #endregion

    [Fact]
    public async Task RecordRideCharge_AnyAccountId_Succeeds()
    {
        // Arrange
        var request = new
        {
            accountId = Guid.NewGuid(), // User Story 1 accepts any valid GUID (no Account entity validation yet)
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 25.00m,
            serviceDate = DateTime.UtcNow
        };

        // Act
        var response = await _client.PostAsJsonAsync("/ledger/charges", request);

        // Assert - Contract validation
        // TODO (User Story 2): Add Account entity validation - reject if account doesn't exist
        response.StatusCode.Should().Be(HttpStatusCode.Created, 
            "User Story 1 MVP allows charges to any account ID (Account entity validation in User Story 2)");
    }

    [Fact]
    public async Task RecordRideCharge_ZeroAmount_Returns400BadRequest()
    {
        // Arrange
        var request = new
        {
            accountId = Guid.NewGuid(),
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 0.00m, // Invalid: amount must be > 0
            serviceDate = DateTime.UtcNow
        };

        // Act
        var response = await _client.PostAsJsonAsync("/ledger/charges", request);

        // Assert - Contract validation
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, 
            "zero fare amount violates validation rules (minimum 0.01)");

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Errors.Should().ContainKey("FareAmount", 
            "validation error should reference fareAmount property");
    }

    #region POST /ledger/payments

    [Fact]
    public async Task RecordPayment_ValidRequest_Returns201Created()
    {
        // Arrange
        var request = new
        {
            accountId = Guid.NewGuid(),
            paymentReferenceId = $"pay_{Guid.NewGuid()}",
            amount = 50.00m,
            paymentDate = DateTime.UtcNow,
            paymentMode = "Credit Card",
            description = "Payment via Stripe"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/ledger/payments", request);

        // Assert - Contract validation
        response.StatusCode.Should().Be(HttpStatusCode.Created, 
            "valid payment should be recorded successfully");

        var result = await response.Content.ReadFromJsonAsync<LedgerTransactionResponse>();
        result.Should().NotBeNull("response should contain transaction details");
        result!.TransactionId.Should().NotBeEmpty("transaction ID should be assigned");
        result.AccountId.Should().Be(request.accountId, "response should contain original account ID");
        result.Entries.Should().HaveCount(2, "double-entry accounting requires 2 entries (debit Cash, credit AR)");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Validate double-entry structure: Cash debit + AR credit
        var cashEntry = result.Entries.FirstOrDefault(e => e.LedgerAccount == "Cash");
        var arEntry = result.Entries.FirstOrDefault(e => e.LedgerAccount == "AccountsReceivable");

        cashEntry.Should().NotBeNull("should have Cash entry");
        cashEntry!.DebitAmount.Should().Be(request.amount, "Cash should be debited with payment amount");
        cashEntry.CreditAmount.Should().Be(0, "Cash should not be credited");

        arEntry.Should().NotBeNull("should have Accounts Receivable entry");
        arEntry!.CreditAmount.Should().Be(request.amount, "AR should be credited with payment amount (reduces receivable)");
        arEntry.DebitAmount.Should().Be(0, "AR should not be debited");
    }

    [Fact]
    public async Task RecordPayment_PartialPayment_ReducesBalance()
    {
        // Arrange - Record a charge first to create balance
        var accountId = Guid.NewGuid();
        var chargeRequest = new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 100.00m,
            serviceDate = DateTime.UtcNow
        };
        await _client.PostAsJsonAsync("/ledger/charges", chargeRequest);

        // Act - Make partial payment
        var paymentRequest = new
        {
            accountId,
            paymentReferenceId = $"pay_{Guid.NewGuid()}",
            amount = 40.00m, // Partial payment
            paymentDate = DateTime.UtcNow
        };
        var response = await _client.PostAsJsonAsync("/ledger/payments", paymentRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<LedgerTransactionResponse>();
        result!.NewBalance.Should().Be(60.00m, "partial payment should reduce balance (100 - 40 = 60)");
    }

    [Fact]
    public async Task RecordPayment_Overpayment_CreatesNegativeBalance()
    {
        // Arrange - Record a charge first
        var accountId = Guid.NewGuid();
        var chargeRequest = new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 50.00m,
            serviceDate = DateTime.UtcNow
        };
        await _client.PostAsJsonAsync("/ledger/charges", chargeRequest);

        // Act - Make overpayment
        var paymentRequest = new
        {
            accountId,
            paymentReferenceId = $"pay_{Guid.NewGuid()}",
            amount = 75.00m, // Overpayment
            paymentDate = DateTime.UtcNow
        };
        var response = await _client.PostAsJsonAsync("/ledger/payments", paymentRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created, "overpayments should be accepted");
        var result = await response.Content.ReadFromJsonAsync<LedgerTransactionResponse>();
        result!.NewBalance.Should().Be(-25.00m, "overpayment should create negative balance (credit)");
    }

    [Fact]
    public async Task RecordPayment_AnyAccountId_Succeeds()
    {
        // Arrange
        var request = new
        {
            accountId = Guid.NewGuid(), // User Story 1 accepts any valid GUID (no Account entity validation yet)
            paymentReferenceId = $"pay_{Guid.NewGuid()}",
            amount = 50.00m,
            paymentDate = DateTime.UtcNow
        };

        // Act
        var response = await _client.PostAsJsonAsync("/ledger/payments", request);

        // Assert
        // TODO (User Story 2): Add Account entity validation - reject if account doesn't exist
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "User Story 1 MVP allows payments to any account ID (Account entity validation in User Story 2)");
    }

    #endregion

    #region GET /accounts/{accountId}/balance

    [Fact]
    public async Task GetAccountBalance_ExistingAccount_Returns200OK()
    {
        // Arrange - Create account with transactions
        var accountId = Guid.NewGuid();
        
        // Record charge
        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 100.00m,
            serviceDate = DateTime.UtcNow
        });

        // Record payment
        await _client.PostAsJsonAsync("/ledger/payments", new
        {
            accountId,
            paymentReferenceId = $"pay_{Guid.NewGuid()}",
            amount = 60.00m,
            paymentDate = DateTime.UtcNow
        });

        // Act
        var response = await _client.GetAsync($"/accounts/{accountId}/balance");

        // Assert - Contract validation
        response.StatusCode.Should().Be(HttpStatusCode.OK, 
            "balance query for existing account should succeed");

        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        result.Should().NotBeNull("response should contain balance details");
        result!.AccountId.Should().Be(accountId, "response should contain queried account ID");
        result.Balance.Should().Be(40.00m, "balance should be calculated correctly (100 charge - 60 payment = 40)");
        result.Currency.Should().Be("USD", "currency should be USD");
        result.AsOf.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5), 
            "timestamp should be recent");
    }

    [Fact]
    public async Task GetAccountBalance_NonExistentAccount_ReturnsZeroBalance()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/accounts/{accountId}/balance");

        // Assert
        // User Story 1 (Ledger MVP) doesn't have Account entity validation
        // Account validation will be added in User Story 2
        // For now, return zero balance for accounts with no transactions
        response.StatusCode.Should().Be(HttpStatusCode.OK, 
            "balance query should return zero for accounts with no ledger entries");

        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        result.Should().NotBeNull();
        result!.Balance.Should().Be(0, "account with no transactions should have zero balance");
    }

    [Fact]
    public async Task GetAccountBalance_NewAccount_ReturnsZeroBalance()
    {
        // Arrange - Create account but no transactions
        var accountId = Guid.NewGuid();
        // NOTE: This test assumes account is auto-created on first transaction
        //       OR requires account creation endpoint (User Story 2)

        // Act
        var response = await _client.GetAsync($"/accounts/{accountId}/balance");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        result!.Balance.Should().Be(0.00m, "new account with no transactions should have zero balance");
    }

    #endregion

    #region Helper DTOs (matching contract spec)

    private class LedgerTransactionResponse
    {
        public Guid TransactionId { get; set; }
        public Guid AccountId { get; set; }
        public List<LedgerEntryDto> Entries { get; set; } = new();
        public decimal NewBalance { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private class LedgerEntryDto
    {
        public Guid Id { get; set; }
        public string LedgerAccount { get; set; } = string.Empty;
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
    }

    private class BalanceResponse
    {
        public Guid AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime AsOf { get; set; }
    }

    private class ProblemDetails
    {
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Status { get; set; }
        public string Detail { get; set; } = string.Empty;
        public string TraceId { get; set; } = string.Empty;
        public Dictionary<string, string[]>? Errors { get; set; }
    }

    #endregion
}
