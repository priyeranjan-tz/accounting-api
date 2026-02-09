using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Accounting.ContractTests;

/// <summary>
/// Contract tests for Invoices API endpoints (User Story 3).
/// These tests validate the API contract according to invoices-api.yaml specification.
/// Tests MUST run FIRST and FAIL before implementation (Constitutional Principle III - TDD).
/// </summary>
public class InvoicesApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public InvoicesApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region POST /invoices (T085)

    [Fact]
    public async Task GenerateInvoice_ValidRequest_Returns201Created()
    {
        // Arrange - Create an active account
        var accountRequest = new
        {
            name = $"Test Account {Guid.NewGuid()}",
            type = "Organization",
            status = "Active"
        };
        var accountResponse = await _client.PostAsJsonAsync("/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountResponse>();

        // Record some ride charges
        var rideId1 = Guid.NewGuid();
        var rideId2 = Guid.NewGuid();
        await _client.PostAsJsonAsync("/ledger/ride-charges", new
        {
            rideId = rideId1,
            accountId = account!.Id,
            amount = 25.50m,
            rideDate = DateTime.UtcNow.AddDays(-20)
        });
        await _client.PostAsJsonAsync("/ledger/ride-charges", new
        {
            rideId = rideId2,
            accountId = account.Id,
            amount = 30.00m,
            rideDate = DateTime.UtcNow.AddDays(-15)
        });

        var invoiceRequest = new
        {
            accountId = account.Id,
            billingPeriodStart = DateTime.UtcNow.AddDays(-30).Date,
            billingPeriodEnd = DateTime.UtcNow.Date,
            issueDate = DateTime.UtcNow.Date,
            paymentTermsDays = 30
        };

        // Act
        var response = await _client.PostAsJsonAsync("/invoices", invoiceRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "valid invoice generation should return 201 Created");

        var result = await response.Content.ReadFromJsonAsync<GenerateInvoiceResponse>();
        result.Should().NotBeNull("response should contain invoice details");
        result!.InvoiceId.Should().NotBeEmpty("invoice ID should be assigned");
        result.InvoiceNumber.Should().MatchRegex(@"^INV-\d{6}-\d{6}$",
            "invoice number should follow INV-YYYYMM-NNNNNN format");
        result.AccountId.Should().Be(account.Id, "invoice should reference the account");
        result.TotalAmount.Should().BeGreaterThan(0, "invoice should have positive total");
        result.LineItemCount.Should().BeGreaterThanOrEqualTo(2,
            "invoice should include both ride charges");

        response.Headers.Location.Should().NotBeNull("Location header should be present");
        response.Headers.Location.ToString().Should().Contain($"/invoices/{result.InvoiceNumber}",
            "Location header should point to the created invoice");
    }

    [Fact]
    public async Task GenerateInvoice_NoUnbilledCharges_Returns400BadRequest()
    {
        // Arrange - Create account but no charges
        var accountRequest = new
        {
            name = $"Test Account {Guid.NewGuid()}",
            type = "Organization",
            status = "Active"
        };
        var accountResponse = await _client.PostAsJsonAsync("/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountResponse>();

        var invoiceRequest = new
        {
            accountId = account!.Id,
            billingPeriodStart = DateTime.UtcNow.AddDays(-30).Date,
            billingPeriodEnd = DateTime.UtcNow.Date
        };

        // Act
        var response = await _client.PostAsJsonAsync("/invoices", invoiceRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "invoice generation without unbilled charges should return 400 Bad Request");

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Detail.Should().Contain("unbilled", "error should indicate no unbilled charges");
    }

    [Fact]
    public async Task GenerateInvoice_InactiveAccount_Returns400BadRequest()
    {
        // Arrange - Create inactive account
        var accountRequest = new
        {
            name = $"Test Account {Guid.NewGuid()}",
            type = "Individual",
            status = "Inactive"
        };
        var accountResponse = await _client.PostAsJsonAsync("/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountResponse>();

        var invoiceRequest = new
        {
            accountId = account!.Id,
            billingPeriodStart = DateTime.UtcNow.AddDays(-30).Date,
            billingPeriodEnd = DateTime.UtcNow.Date
        };

        // Act
        var response = await _client.PostAsJsonAsync("/invoices", invoiceRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "invoice generation for inactive account should return 400 Bad Request");

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Detail.Should().Contain("inactive", "error should indicate account is inactive");
    }

    [Fact]
    public async Task GenerateInvoice_NonExistentAccount_Returns404NotFound()
    {
        // Arrange
        var nonExistentAccountId = Guid.NewGuid();
        var invoiceRequest = new
        {
            accountId = nonExistentAccountId,
            billingPeriodStart = DateTime.UtcNow.AddDays(-30).Date,
            billingPeriodEnd = DateTime.UtcNow.Date
        };

        // Act
        var response = await _client.PostAsJsonAsync("/invoices", invoiceRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "invoice generation for non-existent account should return 404 Not Found");

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(404);
    }

    #endregion

    #region GET /invoices (T086)

    [Fact]
    public async Task ListInvoices_WithPagination_Returns200OK()
    {
        // Arrange - Create account and invoice
        var accountRequest = new
        {
            name = $"Test Account {Guid.NewGuid()}",
            type = "Organization",
            status = "Active"
        };
        var accountResponse = await _client.PostAsJsonAsync("/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountResponse>();

        // Record ride charge and generate invoice
        await _client.PostAsJsonAsync("/ledger/ride-charges", new
        {
            rideId = Guid.NewGuid(),
            accountId = account!.Id,
            amount = 50.00m,
            rideDate = DateTime.UtcNow.AddDays(-10)
        });

        await _client.PostAsJsonAsync("/invoices", new
        {
            accountId = account.Id,
            billingPeriodStart = DateTime.UtcNow.AddDays(-30).Date,
            billingPeriodEnd = DateTime.UtcNow.Date
        });

        // Act
        var response = await _client.GetAsync("/invoices?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "listing invoices should return 200 OK");

        var result = await response.Content.ReadFromJsonAsync<ListInvoicesResponse>();
        result.Should().NotBeNull("response should contain invoices list");
        result!.Invoices.Should().NotBeNull("invoices array should be present");
        result.Invoices.Count.Should().BeGreaterThan(0, "should have at least one invoice");
        result.Pagination.Should().NotBeNull("pagination metadata should be present");
        result.Pagination.CurrentPage.Should().Be(1, "current page should match request");
        result.Pagination.PageSize.Should().Be(10, "page size should match request");
    }

    [Fact]
    public async Task ListInvoices_WithAccountFilter_ReturnsFilteredResults()
    {
        // Arrange - Create account and invoice
        var accountRequest = new
        {
            name = $"Test Account {Guid.NewGuid()}",
            type = "Organization",
            status = "Active"
        };
        var accountResponse = await _client.PostAsJsonAsync("/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountResponse>();

        // Record ride charge and generate invoice
        await _client.PostAsJsonAsync("/ledger/ride-charges", new
        {
            rideId = Guid.NewGuid(),
            accountId = account!.Id,
            amount = 75.00m,
            rideDate = DateTime.UtcNow.AddDays(-5)
        });

        await _client.PostAsJsonAsync("/invoices", new
        {
            accountId = account.Id,
            billingPeriodStart = DateTime.UtcNow.AddDays(-30).Date,
            billingPeriodEnd = DateTime.UtcNow.Date
        });

        // Act
        var response = await _client.GetAsync($"/invoices?accountId={account.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ListInvoicesResponse>();
        result!.Invoices.Should().AllSatisfy(i =>
            i.AccountId.Should().Be(account.Id, "all returned invoices should belong to the account"));
    }

    #endregion

    #region GET /invoices/{invoiceNumber} (T087)

    [Fact]
    public async Task GetInvoice_ExistingInvoiceNumber_Returns200OK()
    {
        // Arrange - Create account, record charges, and generate invoice
        var accountRequest = new
        {
            name = $"Test Account {Guid.NewGuid()}",
            type = "Organization",
            status = "Active"
        };
        var accountResponse = await _client.PostAsJsonAsync("/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountResponse>();

        var rideId = Guid.NewGuid();
        await _client.PostAsJsonAsync("/ledger/ride-charges", new
        {
            rideId = rideId,
            accountId = account!.Id,
            amount = 100.00m,
            rideDate = DateTime.UtcNow.AddDays(-7)
        });

        var invoiceResponse = await _client.PostAsJsonAsync("/invoices", new
        {
            accountId = account.Id,
            billingPeriodStart = DateTime.UtcNow.AddDays(-30).Date,
            billingPeriodEnd = DateTime.UtcNow.Date
        });
        var createdInvoice = await invoiceResponse.Content.ReadFromJsonAsync<GenerateInvoiceResponse>();

        // Act
        var response = await _client.GetAsync($"/invoices/{createdInvoice!.InvoiceNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "getting existing invoice should return 200 OK");

        var result = await response.Content.ReadFromJsonAsync<GetInvoiceResponse>();
        result.Should().NotBeNull("response should contain invoice details");
        result!.InvoiceId.Should().Be(createdInvoice.InvoiceId, "should return the requested invoice");
        result.InvoiceNumber.Should().Be(createdInvoice.InvoiceNumber, "invoice number should match");
        result.AccountId.Should().Be(account.Id, "account ID should match");
        result.TotalAmount.Should().Be(100.00m, "total amount should match");
        result.LineItems.Should().NotBeNull("line items should be included");
        result.LineItems.Count.Should().BeGreaterThan(0, "should have at least one line item");
        result.LineItems.Should().AllSatisfy(line =>
        {
            line.RideId.Should().NotBeEmpty("line item should have ride ID");
            line.Amount.Should().BeGreaterThan(0, "line item amount should be positive");
            line.Description.Should().NotBeNullOrEmpty("line item should have description");
        });
    }

    [Fact]
    public async Task GetInvoice_NonExistentInvoiceNumber_Returns404NotFound()
    {
        // Arrange
        var nonExistentInvoiceNumber = "INV-999999-999999";

        // Act
        var response = await _client.GetAsync($"/invoices/{nonExistentInvoiceNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "non-existent invoice should return 404 Not Found");

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(404);
        problemDetails.Detail.Should().Contain(nonExistentInvoiceNumber,
            "error message should reference the invoice number");
    }

    #endregion

    #region Integration Tests (T088, T089, T090)

    [Fact]
    public async Task InvoiceLifecycle_GenerateAndRetrieve_SuccessfulFlow()
    {
        // Arrange - Complete flow from account creation to invoice retrieval
        var accountRequest = new
        {
            name = $"Integration Test Account {Guid.NewGuid()}",
            type = "Organization",
            status = "Active"
        };
        var accountResponse = await _client.PostAsJsonAsync("/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountResponse>();

        // Record multiple ride charges
        var rides = new[]
        {
            new { rideId = Guid.NewGuid(), amount = 35.00m, date = DateTime.UtcNow.AddDays(-14) },
            new { rideId = Guid.NewGuid(), amount = 42.50m, date = DateTime.UtcNow.AddDays(-10) },
            new { rideId = Guid.NewGuid(), amount = 28.75m, date = DateTime.UtcNow.AddDays(-5) }
        };

        foreach (var ride in rides)
        {
            await _client.PostAsJsonAsync("/ledger/ride-charges", new
            {
                rideId = ride.rideId,
                accountId = account!.Id,
                amount = ride.amount,
                rideDate = ride.date
            });
        }

        // Act - Generate invoice
        var invoiceRequest = new
        {
            accountId = account!.Id,
            billingPeriodStart = DateTime.UtcNow.AddDays(-30).Date,
            billingPeriodEnd = DateTime.UtcNow.Date,
            paymentTermsDays = 15
        };
        var invoiceResponse = await _client.PostAsJsonAsync("/invoices", invoiceRequest);
        var generatedInvoice = await invoiceResponse.Content.ReadFromJsonAsync<GenerateInvoiceResponse>();

        // Assert - Verify invoice was created correctly
        invoiceResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        generatedInvoice!.TotalAmount.Should().Be(106.25m, "total should sum all ride charges");
        generatedInvoice.LineItemCount.Should().Be(3, "should have 3 line items");

        // Act - Retrieve invoice details
        var getResponse = await _client.GetAsync($"/invoices/{generatedInvoice.InvoiceNumber}");
        var retrievedInvoice = await getResponse.Content.ReadFromJsonAsync<GetInvoiceResponse>();

        // Assert - Verify retrieved invoice matches generated invoice
        retrievedInvoice!.InvoiceId.Should().Be(generatedInvoice.InvoiceId);
        retrievedInvoice.TotalAmount.Should().Be(106.25m);
        retrievedInvoice.LineItems.Count.Should().Be(3);
        retrievedInvoice.LineItems.Should().OnlyHaveUniqueItems(l => l.RideId,
            "each ride should appear only once");

        // Act - Attempt to generate duplicate invoice (should have no new charges)
        var duplicateInvoiceResponse = await _client.PostAsJsonAsync("/invoices", invoiceRequest);

        // Assert - Duplicate invoice should fail (no unbilled charges)
        duplicateInvoiceResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "duplicate invoice generation should fail when no unbilled charges exist");
    }

    [Fact]
    public async Task InvoiceTraceability_LineItemsMatchLedgerEntries_ValidRelationship()
    {
        // Arrange
        var accountRequest = new
        {
            name = $"Traceability Test Account {Guid.NewGuid()}",
            type = "Individual",
            status = "Active"
        };
        var accountResponse = await _client.PostAsJsonAsync("/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountResponse>();

        // Record specific ride with known details
        var knownRideId = Guid.NewGuid();
        var knownAmount = 89.99m;
        var knownDate = DateTime.UtcNow.AddDays(-12);

        await _client.PostAsJsonAsync("/ledger/ride-charges", new
        {
            rideId = knownRideId,
            accountId = account!.Id,
            amount = knownAmount,
            rideDate = knownDate
        });

        // Act - Generate invoice
        var invoiceResponse = await _client.PostAsJsonAsync("/invoices", new
        {
            accountId = account.Id,
            billingPeriodStart = DateTime.UtcNow.AddDays(-30).Date,
            billingPeriodEnd = DateTime.UtcNow.Date
        });
        var generatedInvoice = await invoiceResponse.Content.ReadFromJsonAsync<GenerateInvoiceResponse>();

        // Retrieve invoice with line items
        var getResponse = await _client.GetAsync($"/invoices/{generatedInvoice!.InvoiceNumber}");
        var invoice = await getResponse.Content.ReadFromJsonAsync<GetInvoiceResponse>();

        // Assert - Verify line item traces back to ledger entry
        var lineItem = invoice!.LineItems.Should().ContainSingle(l => l.RideId == knownRideId,
            "invoice should contain line item for the recorded ride").Subject;

        lineItem.Amount.Should().Be(knownAmount,
            "line item amount should match ledger entry amount");
        lineItem.RideDate.Date.Should().Be(knownDate.Date,
            "line item ride date should match ledger entry ride date");
        lineItem.Description.Should().Contain("Ride",
            "line item description should indicate ride charge");
    }

    [Fact]
    public async Task InvoiceImmutability_ModificationAttempts_ShouldFail()
    {
        // Arrange - Create invoice
        var accountRequest = new
        {
            name = $"Immutability Test Account {Guid.NewGuid()}",
            type = "Organization",
            status = "Active"
        };
        var accountResponse = await _client.PostAsJsonAsync("/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountResponse>();

        await _client.PostAsJsonAsync("/ledger/ride-charges", new
        {
            rideId = Guid.NewGuid(),
            accountId = account!.Id,
            amount = 65.00m,
            rideDate = DateTime.UtcNow.AddDays(-8)
        });

        var invoiceResponse = await _client.PostAsJsonAsync("/invoices", new
        {
            accountId = account.Id,
            billingPeriodStart = DateTime.UtcNow.AddDays(-30).Date,
            billingPeriodEnd = DateTime.UtcNow.Date
        });
        var generatedInvoice = await invoiceResponse.Content.ReadFromJsonAsync<GenerateInvoiceResponse>();

        // Act - Attempt to modify invoice (PUT not supported)
        var updateAttempt = await _client.PutAsJsonAsync($"/invoices/{generatedInvoice!.InvoiceNumber}", new
        {
            totalAmount = 100.00m // Attempt to change amount
        });

        // Assert - Modification should be rejected (405 Method Not Allowed or 404 endpoint not found)
        var allowedStatuses = new[] { HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound };
        allowedStatuses.Should().Contain(updateAttempt.StatusCode,
            "PUT endpoint should not exist for invoice modification");

        // Verify invoice remains unchanged
        var getResponse = await _client.GetAsync($"/invoices/{generatedInvoice.InvoiceNumber}");
        var unchangedInvoice = await getResponse.Content.ReadFromJsonAsync<GetInvoiceResponse>();
        unchangedInvoice!.TotalAmount.Should().Be(65.00m,
            "invoice total should remain unchanged after modification attempt");
    }

    #endregion

    #region Response DTOs

    private record AccountResponse(
        Guid Id,
        string Name,
        string Type,
        string Status,
        string Currency,
        DateTime CreatedAt);

    private record GenerateInvoiceResponse(
        Guid InvoiceId,
        string InvoiceNumber,
        Guid AccountId,
        decimal TotalAmount,
        int LineItemCount);

    private record GetInvoiceResponse(
        Guid InvoiceId,
        string InvoiceNumber,
        Guid AccountId,
        DateTime BillingPeriodStart,
        DateTime BillingPeriodEnd,
        DateTime IssueDate,
        DateTime DueDate,
        decimal TotalAmount,
        string Currency,
        List<InvoiceLineItemDto> LineItems);

    private record InvoiceLineItemDto(
        Guid RideId,
        DateTime RideDate,
        string Description,
        decimal Amount);

    private record ListInvoicesResponse(
        List<InvoiceDto> Invoices,
        PaginationMetadata Pagination);

    private record InvoiceDto(
        Guid InvoiceId,
        string InvoiceNumber,
        Guid AccountId,
        DateTime BillingPeriodStart,
        DateTime BillingPeriodEnd,
        DateTime IssueDate,
        DateTime DueDate,
        decimal TotalAmount,
        string Currency,
        DateTime CreatedAt);

    private record PaginationMetadata(
        int CurrentPage,
        int PageSize,
        int TotalPages,
        int TotalCount);

    private record ProblemDetails(
        string? Type,
        string? Title,
        int? Status,
        string? Detail,
        string? Instance,
        string? TraceId);

    #endregion
}
