using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Accounting.Infrastructure.Persistence.DbContext;
using Testcontainers.PostgreSql;
using Xunit;

namespace Accounting.IntegrationTests.Accounts;

/// <summary>
/// Integration tests for account management lifecycle.
/// Tests MUST run FIRST and FAIL before implementation (Constitutional Principle III - TDD).
/// 
/// Validates complete account lifecycle: create, activate, deactivate, and business rules.
/// </summary>
public class AccountManagementTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public AccountManagementTests()
    {
        _dbContainer = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("accounting_test")
            .WithUsername("postgres")
            .WithPassword("Pass@123")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AccountingDbContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddDbContext<AccountingDbContext>(options =>
                    {
                        options.UseNpgsql(_dbContainer.GetConnectionString());
                    });

                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();
                    db.Database.EnsureCreated();
                });
            });

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
        await _dbContainer.DisposeAsync();
    }

    [Fact]
    public async Task AccountLifecycle_CreateActivateDeactivate_CompleteCycleWorks()
    {
        // Arrange - Create account
        var createRequest = new
        {
            name = $"Lifecycle Test Account {Guid.NewGuid()}",
            type = "Organization",
            status = "Active"
        };

        // Act 1 - Create account
        var createResponse = await _client.PostAsJsonAsync("/accounts", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created, "account creation should succeed");

        var createdAccount = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();
        createdAccount.Should().NotBeNull("created account should be returned");
        createdAccount!.Name.Should().Be(createRequest.name, "account name should match");
        createdAccount.Type.Should().Be(createRequest.type, "account type should match");
        createdAccount.Status.Should().Be("Active", "account should be created as Active");
        createdAccount.Currency.Should().Be("USD", "currency should default to USD");

        // Act 2 - Deactivate account
        var deactivateRequest = new { status = "Inactive" };
        var deactivateResponse = await _client.PatchAsJsonAsync($"/accounts/{createdAccount.Id}", deactivateRequest);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK, "deactivation should succeed");

        var deactivatedAccount = await deactivateResponse.Content.ReadFromJsonAsync<UpdateAccountStatusResponse>();
        deactivatedAccount.Should().NotBeNull();
        deactivatedAccount!.Status.Should().Be("Inactive", "account should be deactivated");
        deactivatedAccount.ModifiedAt.Should().NotBeNull("modified timestamp should be set");

        // Act 3 - Reactivate account
        var reactivateRequest = new { status = "Active" };
        var reactivateResponse = await _client.PatchAsJsonAsync($"/accounts/{createdAccount.Id}", reactivateRequest);
        reactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK, "reactivation should succeed");

        var reactivatedAccount = await reactivateResponse.Content.ReadFromJsonAsync<UpdateAccountStatusResponse>();
        reactivatedAccount.Should().NotBeNull();
        reactivatedAccount!.Status.Should().Be("Active", "account should be reactivated");

        // Assert - Verify final state
        var getResponse = await _client.GetAsync($"/accounts/{createdAccount.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var finalAccount = await getResponse.Content.ReadFromJsonAsync<AccountDetailResponse>();
        finalAccount.Should().NotBeNull();
        finalAccount!.Status.Should().Be("Active", "final status should be Active");
        finalAccount.ModifiedAt.Should().NotBeNull("modified timestamp should be present");
        finalAccount.CreatedBy.Should().NotBeNullOrEmpty("created by should be set");
    }

    [Fact]
    public async Task CreateAccount_DuplicateName_ReturnsConflict()
    {
        // Arrange
        var accountName = $"Duplicate Name Test {Guid.NewGuid()}";
        var request = new
        {
            name = accountName,
            type = "Organization",
            status = "Active"
        };

        // Act - Create account first time
        var firstResponse = await _client.PostAsJsonAsync("/accounts", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created, "first account creation should succeed");

        // Act - Create account with same name
        var duplicateResponse = await _client.PostAsJsonAsync("/accounts", request);

        // Assert - Duplicate should be rejected
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "duplicate account name should return 409 Conflict");

        var problemDetails = await duplicateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(409);
        problemDetails.Detail.Should().Contain(accountName, "error should reference the duplicate account name");
    }

    [Fact]
    public async Task AccountStatusTransitions_Idempotent_MultipleCallsSucceed()
    {
        // Arrange - Create active account
        var createRequest = new
        {
            name = $"Idempotent Test Account {Guid.NewGuid()}",
            type = "Individual",
            status = "Active"
        };

        var createResponse = await _client.PostAsJsonAsync("/accounts", createRequest);
        var createdAccount = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();

        var deactivateRequest = new { status = "Inactive" };

        // Act - Deactivate account twice
        var firstDeactivate = await _client.PatchAsJsonAsync($"/accounts/{createdAccount!.Id}", deactivateRequest);
        var secondDeactivate = await _client.PatchAsJsonAsync($"/accounts/{createdAccount.Id}", deactivateRequest);

        // Assert - Both should succeed (idempotent)
        firstDeactivate.StatusCode.Should().Be(HttpStatusCode.OK, "first deactivation should succeed");
        secondDeactivate.StatusCode.Should().Be(HttpStatusCode.OK, "second deactivation should succeed (idempotent)");

        var firstResult = await firstDeactivate.Content.ReadFromJsonAsync<UpdateAccountStatusResponse>();
        var secondResult = await secondDeactivate.Content.ReadFromJsonAsync<UpdateAccountStatusResponse>();

        firstResult!.Status.Should().Be("Inactive");
        secondResult!.Status.Should().Be("Inactive");

        // Act - Activate account twice
        var activateRequest = new { status = "Active" };
        var firstActivate = await _client.PatchAsJsonAsync($"/accounts/{createdAccount.Id}", activateRequest);
        var secondActivate = await _client.PatchAsJsonAsync($"/accounts/{createdAccount.Id}", activateRequest);

        // Assert - Both should succeed (idempotent)
        firstActivate.StatusCode.Should().Be(HttpStatusCode.OK);
        secondActivate.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InactiveAccount_PreventRideCharges_Returns400BadRequest()
    {
        // Arrange - Create account
        var createRequest = new
        {
            name = $"Test Account for Charges {Guid.NewGuid()}",
            type = "Organization",
            status = "Active"
        };

        var createResponse = await _client.PostAsJsonAsync("/accounts", createRequest);
        var createdAccount = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();

        // Act - Deactivate account
        var deactivateRequest = new { status = "Inactive" };
        await _client.PatchAsJsonAsync($"/accounts/{createdAccount!.Id}", deactivateRequest);

        // Act - Try to record ride charge to inactive account
        var rideChargeRequest = new
        {
            accountId = createdAccount.Id,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 50.00m,
            serviceDate = DateTime.UtcNow
        };

        var chargeResponse = await _client.PostAsJsonAsync("/ledger/charges", rideChargeRequest);

        // Assert - Charge should be rejected
        chargeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "ride charges to inactive accounts should be rejected with 400 Bad Request");

        var problemDetails = await chargeResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Detail.Should().Contain("inactive", "error should indicate account is inactive");
    }

    [Fact]
    public async Task GetAccount_AfterCreation_ReturnsCompleteDetails()
    {
        // Arrange - Create account
        var createRequest = new
        {
            name = $"Details Test Account {Guid.NewGuid()}",
            type = "Individual",
            status = "Active"
        };

        var createResponse = await _client.PostAsJsonAsync("/accounts", createRequest);
        var createdAccount = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();

        // Act - Retrieve account details
        var getResponse = await _client.GetAsync($"/accounts/{createdAccount!.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var accountDetails = await getResponse.Content.ReadFromJsonAsync<AccountDetailResponse>();

        // Assert - Verify all details are present
        accountDetails.Should().NotBeNull();
        accountDetails!.Id.Should().Be(createdAccount.Id);
        accountDetails.Name.Should().Be(createRequest.name);
        accountDetails.Type.Should().Be(createRequest.type);
        accountDetails.Status.Should().Be("Active");
        accountDetails.Currency.Should().Be("USD");
        accountDetails.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        accountDetails.CreatedBy.Should().NotBeNullOrEmpty("created by user should be recorded");
        accountDetails.ModifiedAt.Should().BeNull("newly created account should not have modification timestamp");
        accountDetails.ModifiedBy.Should().BeNull("newly created account should not have modifier");
    }

    [Fact]
    public async Task ListAccounts_WithFilters_ReturnsFilteredResults()
    {
        // Arrange - Create accounts with different types and statuses
        var orgActive = new { name = $"Org Active {Guid.NewGuid()}", type = "Organization", status = "Active" };
        var orgInactive = new { name = $"Org Inactive {Guid.NewGuid()}", type = "Organization", status = "Inactive" };
        var indActive = new { name = $"Ind Active {Guid.NewGuid()}", type = "Individual", status = "Active" };

        await Task.WhenAll(
            _client.PostAsJsonAsync("/accounts", orgActive),
            _client.PostAsJsonAsync("/accounts", orgInactive),
            _client.PostAsJsonAsync("/accounts", indActive)
        );

        // Act - Filter by status (Active)
        var activeResponse = await _client.GetAsync("/accounts?status=Active&pageSize=100");
        var activeResult = await activeResponse.Content.ReadFromJsonAsync<ListAccountsResponse>();

        // Assert - Only active accounts returned
        activeResult.Should().NotBeNull();
        activeResult!.Accounts.Should().OnlyContain(a => a.Status == "Active",
            "status filter should return only Active accounts");
        activeResult.Accounts.Should().HaveCountGreaterOrEqualTo(2,
            "should have at least our 2 active accounts");

        // Act - Filter by type (Organization)
        var orgResponse = await _client.GetAsync("/accounts?accountType=Organization&pageSize=100");
        var orgResult = await orgResponse.Content.ReadFromJsonAsync<ListAccountsResponse>();

        // Assert - Only Organization accounts returned
        orgResult.Should().NotBeNull();
        orgResult!.Accounts.Should().OnlyContain(a => a.Type == "Organization",
            "type filter should return only Organization accounts");
        orgResult.Accounts.Should().HaveCountGreaterOrEqualTo(2,
            "should have at least our 2 organization accounts");
    }

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
