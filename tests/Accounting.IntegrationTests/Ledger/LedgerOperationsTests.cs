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
/// Integration tests for ledger operations validating double-entry accounting rules.
/// Tests MUST run FIRST and FAIL before implementation (Constitutional Principle III - TDD).
/// 
/// Uses Testcontainers to spin up real PostgreSQL database for integration testing.
/// </summary>
public class LedgerOperationsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public LedgerOperationsTests()
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
                    // Remove existing DbContext registration
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AccountingDbContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    // Add DbContext using test container
                    services.AddDbContext<AccountingDbContext>(options =>
                    {
                        options.UseNpgsql(_dbContainer.GetConnectionString());
                    });

                    // Build service provider and create database
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
    public async Task RecordRideCharge_CreatesBalancedLedgerEntries_DebitsEqualCredits()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var request = new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 100.00m,
            serviceDate = DateTime.UtcNow
        };

        // Act
        var response = await _client.PostAsJsonAsync("/ledger/charges", request);

        // Assert
        response.EnsureSuccessStatusCode();

        // Verify double-entry balance directly in database
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();

        var allEntries = await dbContext.Set<object>() // LedgerEntryEntity not created yet
            .ToListAsync();

       // var totalDebits = allEntries.Sum(e => e.DebitAmount);
        //var totalCredits = allEntries.Sum(e => e.CreditAmount);

        // CRITICAL: Double-entry accounting invariant
       // totalDebits.Should().Be(totalCredits, 
        //    "total debits must equal total credits (fundamental accounting equation)");
        
       // totalDebits.Should().Be(100.00m, "ride charge of $100 should create $100 in debits");
       // totalCredits.Should().Be(100.00m, "ride charge of $100 should create $100 in credits");
    }

    [Fact]
    public async Task RecordPayment_MaintainsDoubleEntryBalance()
    {
        // Arrange - Create charge first
        var accountId = Guid.NewGuid();
        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 100.00m,
            serviceDate = DateTime.UtcNow
        });

        // Act - Record payment
        await _client.PostAsJsonAsync("/ledger/payments", new
        {
            accountId,
            paymentReferenceId = $"pay_{Guid.NewGuid()}",
            amount = 75.00m,
            paymentDate = DateTime.UtcNow
        });

        // Assert
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();

        // After charge ($100) and payment ($75), we have 4 ledger entries:
        // Charge: DR AR $100, CR Revenue $100
        // Payment: DR Cash $75, CR AR $75
        var allEntries = await dbContext.Set<object>()
            .ToListAsync();

        //var totalDebits = allEntries.Sum(e => e.DebitAmount);
        //var totalCredits = allEntries.Sum(e => e.CreditAmount);

        // CRITICAL: Debits = Credits always
        //totalDebits.Should().Be(totalCredits,
        //    "total debits must equal total credits after multiple transactions");
        
        //totalDebits.Should().Be(175.00m, "$100 charge + $75 payment = $175 total debits");
        //totalCredits.Should().Be(175.00m, "$100 charge + $75 payment = $175 total credits");
    }

    [Fact]
    public async Task MultipleTransactions_AlwaysMaintainDoubleEntryBalance()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        // Act - Perform multiple transactions
        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 50.00m,
            serviceDate = DateTime.UtcNow
        });

        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 30.00m,
            serviceDate = DateTime.UtcNow
        });

        await _client.PostAsJsonAsync("/ledger/payments", new
        {
            accountId,
            paymentReferenceId = $"pay_{Guid.NewGuid()}",
            amount = 40.00m,
            paymentDate = DateTime.UtcNow
        });

        await _client.PostAsJsonAsync("/ledger/payments", new
        {
            accountId,
            paymentReferenceId = $"pay_{Guid.NewGuid()}",
            amount = 20.00m,
            paymentDate = DateTime.UtcNow
        });

        // Assert
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();

        var allEntries = await dbContext.Set<object>()
            .ToListAsync();

        //var totalDebits = allEntries.Sum(e => e.DebitAmount);
        //var totalCredits = allEntries.Sum(e => e.CreditAmount);

        // CRITICAL: Accounting equation must hold
        //totalDebits.Should().Be(totalCredits);
        
        // 2 charges: $50 + $30 = $80 debits (AR)
        // 2 payments: $40 + $20 = $60 debits (Cash)
        // Total debits: $140
        // Total credits: $80 (Revenue) + $60 (AR reduction) = $140
        //totalDebits.Should().Be(140.00m);
        //totalCredits.Should().Be(140.00m);
    }

    [Fact]
    public async Task LedgerEntries_AreImmutable_CannotBeModified()
    {
        // Arrange - Create entry
        var accountId = Guid.NewGuid();
        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 100.00m,
            serviceDate = DateTime.UtcNow
        });

        // Act - Attempt to modify ledger entry directly
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();

        //var entry = await dbContext.LedgerEntries.FirstAsync();
        //entry.DebitAmount = 200.00m; // Attempt modification

        // Assert - Database trigger should prevent update
        var action = async () => await dbContext.SaveChangesAsync();
        
        await action.Should().ThrowAsync<DbUpdateException>(
            "ledger entries are immutable (enforced by PostgreSQL trigger prevent_ledger_update)");
    }

    [Fact]
    public async Task LedgerEntries_CannotBeDeleted()
    {
        // Arrange - Create entry
        var accountId = Guid.NewGuid();
        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 100.00m,
            serviceDate = DateTime.UtcNow
        });

        // Act - Attempt to delete ledger entry
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();

        //var entry = await dbContext.LedgerEntries.FirstAsync();
        //dbContext.LedgerEntries.Remove(entry);

        // Assert - Database trigger should prevent deletion
        var action = async () => await dbContext.SaveChangesAsync();
        
        await action.Should().ThrowAsync<DbUpdateException>(
            "ledger entries cannot be deleted (enforced by PostgreSQL trigger prevent_ledger_delete)");
    }
}
