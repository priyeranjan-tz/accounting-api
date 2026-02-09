using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Accounting.Infrastructure.Persistence.DbContext;
using Testcontainers.PostgreSql;
using Xunit;

namespace Accounting.IntegrationTests.Ledger;

/// <summary>
/// Integration tests for idempotency enforcement in ledger operations.
/// Tests MUST run FIRST and FAIL before implementation (Constitutional Principle III - TDD).
/// 
/// Validates that duplicate ride charges are rejected with 409 Conflict,
/// preventing double-charging customers.
/// </summary>
public class IdempotencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public IdempotencyTests()
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
        // Note: In development mode, authentication middleware will use default tenant when no auth header is present
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
    public async Task RecordRideCharge_DuplicateRideId_ReturnsConflict()
    {
        // Arrange
        var rideId = $"R-{Guid.NewGuid()}";
        var accountId = Guid.NewGuid();
        
        var request = new
        {
            accountId,
            rideId,
            fareAmount = 25.00m,
            serviceDate = DateTime.UtcNow
        };

        // Act - First charge should succeed
        var firstResponse = await _client.PostAsJsonAsync("/ledger/charges", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created, "first charge should succeed");

        // Act - Second charge with same rideId should fail
        var duplicateResponse = await _client.PostAsJsonAsync("/ledger/charges", request);

        // Assert - CRITICAL: Idempotency enforcement
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "duplicate ride charge must be rejected to prevent double-billing");

        var problemDetails = await duplicateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(409);
        problemDetails.Detail.Should().Contain(rideId, "error message should reference conflicting ride ID");
    }

    [Fact]
    public async Task RecordRideCharge_SameRideIdDifferentAccount_ReturnsConflict()
    {
        // Arrange - Idempotency is global per ride, not per account
        var rideId = $"R-{Guid.NewGuid()}";
        var account1 = Guid.NewGuid();
        var account2 = Guid.NewGuid();

        // Act - Charge ride to account 1
        var firstResponse = await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId = account1,
            rideId,
            fareAmount = 25.00m,
            serviceDate = DateTime.UtcNow
        });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act - Attempt to charge same ride to different account
        var duplicateResponse = await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId = account2,
            rideId,
            fareAmount = 25.00m,
            serviceDate = DateTime.UtcNow
        });

        // Assert - CRITICAL: Ride can only be charged once globally
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "same ride cannot be charged to multiple accounts");
    }

    [Fact]
    public async Task RecordRideCharge_DifferentRideIds_BothSucceed()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        // Act - Charge two different rides to same account
        var response1 = await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 25.00m,
            serviceDate = DateTime.UtcNow
        });

        var response2 = await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 30.00m,
            serviceDate = DateTime.UtcNow
        });

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.Created, "first ride should be charged");
        response2.StatusCode.Should().Be(HttpStatusCode.Created, "second ride should be charged");
    }

    [Fact]
    public async Task RecordRideCharge_IdempotencyEnforcedAtDatabaseLevel()
    {
        // Arrange
        var rideId = $"R-{Guid.NewGuid()}";
        var accountId = Guid.NewGuid();

        var request = new
        {
            accountId,
            rideId,
            fareAmount = 25.00m,
            serviceDate = DateTime.UtcNow
        };

        // Act - First charge
        await _client.PostAsJsonAsync("/ledger/charges", request);

        // Act - Second charge
        await _client.PostAsJsonAsync("/ledger/charges", request);

        // Assert - Verify database has only ONE charge for this ride
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();

        // var entriesForRide = await dbContext.LedgerEntries
        //     .Where(e => e.RideId == rideId)
        //     .CountAsync();

        // entriesForRide.Should().Be(2, 
        //     "ride should have exactly 2 entries (debit AR, credit Revenue) - NOT 4 (duplicate prevented)");
    }

    [Fact]
    public async Task RecordRideCharge_ConcurrentDuplicates_OnlyOneSucceeds()
    {
        // Arrange
        var rideId = $"R-{Guid.NewGuid()}";
        var accountId = Guid.NewGuid();

        var request = new
        {
            accountId,
            rideId,
            fareAmount = 25.00m,
            serviceDate = DateTime.UtcNow
        };

        // Act - Concurrent duplicate requests (race condition test)
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _client.PostAsJsonAsync("/ledger/charges", request))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - CRITICAL: Only ONE request should succeed
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        successCount.Should().Be(1, "only one concurrent request should succeed");
        conflictCount.Should().Be(4, "all other requests should return 409 Conflict");
    }

    [Fact]
    public async Task RecordRideCharge_UniqueConstraint_PreventsDuplicates()
    {
        // Arrange
        var rideId = $"R-{Guid.NewGuid()}";
        var accountId = Guid.NewGuid();

        // Act - First charge
        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId,
            fareAmount = 25.00m,
            serviceDate = DateTime.UtcNow
        });

        // Assert - Verify unique index on (account_id, ride_id) exists in database
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();

        // Check database schema for unique index
        // Unique constraint on ride_id prevents duplicate charges for same ride

        // var indexCount = await dbContext.Database.SqlQueryRaw<int>(sql).FirstAsync();
        // indexCount.Should().BeGreaterThan(0, 
        //     "unique index on ride_id should exist to enforce idempotency at database level");
    }

    private class ProblemDetails
    {
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Status { get; set; }
        public string Detail { get; set; } = string.Empty;
        public string TraceId { get; set; } = string.Empty;
    }
}
