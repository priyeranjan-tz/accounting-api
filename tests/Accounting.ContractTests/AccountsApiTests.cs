using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Accounting.ContractTests;

/// <summary>
/// Contract tests for Accounts API endpoints (User Story 2).
/// These tests validate the API contract according to accounts-api.yaml specification.
/// Tests MUST run FIRST and FAIL before implementation (Constitutional Principle III - TDD).
/// </summary>
public class AccountsApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public AccountsApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region POST /accounts (T058)

    [Fact]
    public async Task CreateAccount_ValidRequest_Returns201Created()
    {
        // Arrange
        var request = new
        {
            name = $"Test Account {Guid.NewGuid()}",
            type = "Organization",
            status = "Active"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/accounts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "valid account creation should return 201 Created");

        var result = await response.Content.ReadFromJsonAsync<AccountResponse>();
        result.Should().NotBeNull("response should contain account details");
        result!.Id.Should().NotBeEmpty("account ID should be assigned");
        result.Name.Should().Be(request.name, "response should contain original account name");
        result.Type.Should().Be(request.type, "response should contain account type");
        result.Status.Should().Be(request.status, "response should contain account status");
        result.Currency.Should().Be("USD", "currency should default to USD");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5),
            "creation timestamp should be recent");

        response.Headers.Location.Should().NotBeNull("Location header should be present");
        response.Headers.Location.ToString().Should().Contain($"/accounts/{result.Id}",
            "Location header should point to the created account");
    }

    [Fact]
    public async Task CreateAccount_DuplicateName_Returns409Conflict()
    {
        // Arrange - Create account once
        var accountName = $"Duplicate Test Account {Guid.NewGuid()}";
        var request = new
        {
            name = accountName,
            type = "Organization",
            status = "Active"
        };

        await _client.PostAsJsonAsync("/accounts", request);

        // Act - Attempt duplicate account creation
        var duplicateResponse = await _client.PostAsJsonAsync("/accounts", request);

        // Assert
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "duplicate account name should be rejected with 409 Conflict");

        var problemDetails = await duplicateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull("error response should use RFC 9457 Problem Details");
        problemDetails!.Status.Should().Be(409, "status code in problem details should match HTTP status");
        problemDetails.Title.Should().Contain("Conflict", "title should indicate conflict");
        problemDetails.Detail.Should().Contain(accountName, "detail should reference the conflicting account name");
        problemDetails.TraceId.Should().NotBeNullOrEmpty("traceId should be present for correlation");
    }

    [Fact]
    public async Task CreateAccount_InvalidName_Returns400BadRequest()
    {
        // Arrange
        var request = new
        {
            name = "", // Invalid: empty name
            type = "Organization",
            status = "Active"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/accounts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "invalid account name should be rejected with 400 Bad Request");

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
    }

    #endregion

    #region GET /accounts (T059)

    [Fact]
    public async Task ListAccounts_WithPagination_Returns200OK()
    {
        // Arrange - Create multiple accounts
        for (int i = 0; i < 3; i++)
        {
            await _client.PostAsJsonAsync("/accounts", new
            {
                name = $"Test Account {Guid.NewGuid()}",
                type = "Organization",
                status = "Active"
            });
        }

        // Act
        var response = await _client.GetAsync("/accounts?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "listing accounts should return 200 OK");

        var result = await response.Content.ReadFromJsonAsync<ListAccountsResponse>();
        result.Should().NotBeNull("response should contain accounts list");
        result!.Accounts.Should().NotBeNull("accounts array should be present");
        result.Accounts.Count.Should().BeGreaterThanOrEqualTo(3, "should have at least the accounts we created");
        result.Pagination.Should().NotBeNull("pagination metadata should be present");
        result.Pagination.CurrentPage.Should().Be(1, "current page should match request");
        result.Pagination.PageSize.Should().Be(10, "page size should match request");
    }

    [Fact]
    public async Task ListAccounts_WithStatusFilter_ReturnsFilteredResults()
    {
        // Arrange - Create active and inactive accounts
        var activeAccount = new
        {
            name = $"Active Account {Guid.NewGuid()}",
            type = "Organization",
            status = "Active"
        };
        await _client.PostAsJsonAsync("/accounts", activeAccount);

        // Act
        var response = await _client.GetAsync("/accounts?status=Active");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ListAccountsResponse>();
        result!.Accounts.Should().AllSatisfy(a =>
            a.Status.Should().Be("Active", "all returned accounts should have Active status"));
    }

    [Fact]
    public async Task ListAccounts_WithTypeFilter_ReturnsFilteredResults()
    {
        // Arrange
        var orgAccount = new
        {
            name = $"Organization Account {Guid.NewGuid()}",
            type = "Organization",
            status = "Active"
        };
        await _client.PostAsJsonAsync("/accounts", orgAccount);

        // Act
        var response = await _client.GetAsync("/accounts?accountType=Organization");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ListAccountsResponse>();
        result!.Accounts.Should().AllSatisfy(a =>
            a.Type.Should().Be("Organization", "all returned accounts should be Organization type"));
    }

    #endregion

    #region GET /accounts/{id} (T060)

    [Fact]
    public async Task GetAccount_ExistingId_Returns200OK()
    {
        // Arrange - Create an account
        var createRequest = new
        {
            name = $"Test Account {Guid.NewGuid()}",
            type = "Individual",
            status = "Active"
        };
        var createResponse = await _client.PostAsJsonAsync("/accounts", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();

        // Act
        var response = await _client.GetAsync($"/accounts/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "getting existing account should return 200 OK");

        var result = await response.Content.ReadFromJsonAsync<AccountDetailResponse>();
        result.Should().NotBeNull("response should contain account details");
        result!.Id.Should().Be(created.Id, "should return the requested account");
        result.Name.Should().Be(createRequest.name, "account name should match");
        result.Type.Should().Be(createRequest.type, "account type should match");
        result.Status.Should().Be(createRequest.status, "account status should match");
        result.CreatedBy.Should().NotBeNullOrEmpty("created by field should be populated");
    }

    [Fact]
    public async Task GetAccount_NonExistentId_Returns404NotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/accounts/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "non-existent account should return 404 Not Found");

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(404);
        problemDetails.Detail.Should().Contain(nonExistentId.ToString(),
            "error message should reference the account ID");
    }

    #endregion

    #region PATCH /accounts/{id} (T061)

    [Fact]
    public async Task UpdateAccountStatus_ActiveToInactive_Returns200OK()
    {
        // Arrange - Create active account
        var createRequest = new
        {
            name = $"Test Account {Guid.NewGuid()}",
            type = "Organization",
            status = "Active"
        };
        var createResponse = await _client.PostAsJsonAsync("/accounts", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();

        var updateRequest = new
        {
            status = "Inactive"
        };

        // Act
        var response = await _client.PatchAsJsonAsync($"/accounts/{created!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "valid status update should return 200 OK");

        var result = await response.Content.ReadFromJsonAsync<UpdateAccountStatusResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id, "response should contain account ID");
        result.Status.Should().Be("Inactive", "status should be updated to Inactive");
        result.ModifiedAt.Should().NotBeNull("modified timestamp should be set");
        result.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5),
            "modification timestamp should be recent");
    }

    [Fact]
    public async Task UpdateAccountStatus_InactiveToActive_Returns200OK()
    {
        // Arrange - Create inactive account
        var createRequest = new
        {
            name = $"Test Account {Guid.NewGuid()}",
            type = "Individual",
            status = "Inactive"
        };
        var createResponse = await _client.PostAsJsonAsync("/accounts", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();

        var updateRequest = new
        {
            status = "Active"
        };

        // Act
        var response = await _client.PatchAsJsonAsync($"/accounts/{created!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UpdateAccountStatusResponse>();
        result!.Status.Should().Be("Active", "status should be updated to Active");
    }

    [Fact]
    public async Task UpdateAccountStatus_NonExistentId_Returns404NotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateRequest = new
        {
            status = "Inactive"
        };

        // Act
        var response = await _client.PatchAsJsonAsync($"/accounts/{nonExistentId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "updating non-existent account should return 404 Not Found");
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

    private record AccountDetailResponse(
        Guid Id,
        string Name,
        string Type,
        string Status,
        string Currency,
        DateTime CreatedAt,
        string CreatedBy,
        DateTime? ModifiedAt,
        string? ModifiedBy);

    private record ListAccountsResponse(
        List<AccountDto> Accounts,
        PaginationMetadata Pagination);

    private record AccountDto(
        Guid Id,
        string Name,
        string Type,
        string Status,
        string Currency,
        DateTime CreatedAt,
        DateTime? ModifiedAt);

    private record PaginationMetadata(
        int CurrentPage,
        int PageSize,
        int TotalPages,
        int TotalCount);

    private record UpdateAccountStatusResponse(
        Guid Id,
        string Name,
        string Type,
        string Status,
        DateTime? ModifiedAt);

    private record ProblemDetails(
        string? Type,
        string? Title,
        int? Status,
        string? Detail,
        string? Instance,
        string? TraceId);

    #endregion
}
