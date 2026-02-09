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
/// Integration tests for balance calculation accuracy.
/// Tests MUST run FIRST and FAIL before implementation (Constitutional Principle III - TDD).
/// 
/// Validates that account balances are calculated correctly using double-entry accounting:
/// Balance = Sum(Debits to AR) - Sum(Credits to AR)
/// </summary>
public class BalanceCalculationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public BalanceCalculationTests()
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
    public async Task GetAccountBalance_NoTransactions_ReturnsZero()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/accounts/{accountId}/balance");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        
        result.Should().NotBeNull();
        result!.Balance.Should().Be(0.00m, "account with no transactions should have zero balance");
    }

    [Fact]
    public async Task GetAccountBalance_SingleCharge_ReturnsPositiveBalance()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        
        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 100.00m,
            serviceDate = DateTime.UtcNow
        });

        // Act
        var response = await _client.GetAsync($"/accounts/{accountId}/balance");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        
        result!.Balance.Should().Be(100.00m, "single charge of $100 should result in $100 balance");
    }

    [Fact]
    public async Task GetAccountBalance_ChargeAndFullPayment_ReturnsZero()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        
        // Charge $100
        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 100.00m,
            serviceDate = DateTime.UtcNow
        });

        // Pay $100 (full payment)
        await _client.PostAsJsonAsync("/ledger/payments", new
        {
            accountId,
            paymentReferenceId = $"pay_{Guid.NewGuid()}",
            amount = 100.00m,
            paymentDate = DateTime.UtcNow
        });

        // Act
        var response = await _client.GetAsync($"/accounts/{accountId}/balance");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        
        result!.Balance.Should().Be(0.00m, "full payment should zero out balance");
    }

    [Fact]
    public async Task GetAccountBalance_ChargeAndPartialPayment_ReturnsRemainingBalance()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        
        // Charge $100
        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 100.00m,
            serviceDate = DateTime.UtcNow
        });

        // Pay $60 (partial payment)
        await _client.PostAsJsonAsync("/ledger/payments", new
        {
            accountId,
            paymentReferenceId = $"pay_{Guid.NewGuid()}",
            amount = 60.00m,
            paymentDate = DateTime.UtcNow
        });

        // Act
        var response = await _client.GetAsync($"/accounts/{accountId}/balance");

        // Assert - CRITICAL: Balance calculation accuracy
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        
        result!.Balance.Should().Be(40.00m, 
            "partial payment of $60 on $100 charge should leave $40 balance");
    }

    [Fact]
    public async Task GetAccountBalance_MultiplePartialPayments_AccumulatesCorrectly()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        
        // Charge $100
        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 100.00m,
            serviceDate = DateTime.UtcNow
        });

        // Pay $30
        await _client.PostAsJsonAsync("/ledger/payments", new
        {
            accountId,
            paymentReferenceId = $"pay_{Guid.NewGuid()}",
            amount = 30.00m,
            paymentDate = DateTime.UtcNow
        });

        // Pay $20
        await _client.PostAsJsonAsync("/ledger/payments", new
        {
            accountId,
            paymentReferenceId = $"pay_{Guid.NewGuid()}",
            amount = 20.00m,
            paymentDate = DateTime.UtcNow
        });

        // Act
        var response = await _client.GetAsync($"/accounts/{accountId}/balance");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        
        result!.Balance.Should().Be(50.00m, 
            "two partial payments ($30 + $20) on $100 charge should leave $50 balance");
    }

    [Fact]
    public async Task GetAccountBalance_Overpayment_ReturnsNegativeBalance()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        
        // Charge $50
        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 50.00m,
            serviceDate = DateTime.UtcNow
        });

        // Pay $75 (overpayment)
        await _client.PostAsJsonAsync("/ledger/payments", new
        {
            accountId,
            paymentReferenceId = $"pay_{Guid.NewGuid()}",
            amount = 75.00m,
            paymentDate = DateTime.UtcNow
        });

        // Act
        var response = await _client.GetAsync($"/accounts/{accountId}/balance");

        // Assert - CRITICAL: Overpayment creates credit balance (negative)
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        
        result!.Balance.Should().Be(-25.00m, 
            "overpayment of $75 on $50 charge should create -$25 credit balance");
    }

    [Fact]
    public async Task GetAccountBalance_MultipleChargesAndPayments_CalculatesAccurately()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        
        // Charge $50
        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 50.00m,
            serviceDate = DateTime.UtcNow
        });

        // Charge $30
        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 30.00m,
            serviceDate = DateTime.UtcNow
        });

        // Pay $40
        await _client.PostAsJsonAsync("/ledger/payments", new
        {
            accountId,
            paymentReferenceId = $"pay_{Guid.NewGuid()}",
            amount = 40.00m,
            paymentDate = DateTime.UtcNow
        });

        // Charge $25
        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 25.00m,
            serviceDate = DateTime.UtcNow
        });

        // Pay $20
        await _client.PostAsJsonAsync("/ledger/payments", new
        {
            accountId,
            paymentReferenceId = $"pay_{Guid.NewGuid()}",
            amount = 20.00m,
            paymentDate = DateTime.UtcNow
        });

        // Act
        var response = await _client.GetAsync($"/accounts/{accountId}/balance");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        
        // Charges: $50 + $30 + $25 = $105
        // Payments: $40 + $20 = $60
        // Balance: $105 - $60 = $45
        result!.Balance.Should().Be(45.00m, 
            "complex transaction history should calculate correctly: ($50+$30+$25) - ($40+$20) = $45");
    }

    [Fact]
    public async Task GetAccountBalance_DecimalPrecision_MaintainsCentAccuracy()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        
        // Charge with fractional cents
        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 33.33m,
            serviceDate = DateTime.UtcNow
        });

        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 33.33m,
            serviceDate = DateTime.UtcNow
        });

        await _client.PostAsJsonAsync("/ledger/charges", new
        {
            accountId,
            rideId = $"R-{Guid.NewGuid()}",
            fareAmount = 33.34m,
            serviceDate = DateTime.UtcNow
        });

        // Act
        var response = await _client.GetAsync($"/accounts/{accountId}/balance");

        // Assert - CRITICAL: Decimal precision to avoid rounding errors
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        
        result!.Balance.Should().Be(100.00m, 
            "fractional cents should sum correctly ($33.33 + $33.33 + $33.34 = $100.00)");
    }

    [Fact]
    public async Task GetAccountBalance_LargeVolume_PerformanceUnder100ms()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        
        // Create 50 transactions
        for (int i = 0; i < 25; i++)
        {
            await _client.PostAsJsonAsync("/ledger/charges", new
            {
                accountId,
                rideId = $"R-{Guid.NewGuid()}",
                fareAmount = 100.00m,
                serviceDate = DateTime.UtcNow
            });
        }

        for (int i = 0; i < 25; i++)
        {
            await _client.PostAsJsonAsync("/ledger/payments", new
            {
                accountId,
                paymentReferenceId = $"pay_{Guid.NewGuid()}",
                amount = 50.00m,
                paymentDate = DateTime.UtcNow
            });
        }

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await _client.GetAsync($"/accounts/{accountId}/balance");
        stopwatch.Stop();

        // Assert - CRITICAL: Performance requirement from spec
        response.EnsureSuccessStatusCode();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, 
            "balance calculation should complete in under 100ms (spec requirement)");

        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        // 25 charges * $100 = $2500
        // 25 payments * $50 = $1250
        // Balance: $2500 - $1250 = $1250
        result!.Balance.Should().Be(1250.00m);
    }

    private class BalanceResponse
    {
        public Guid AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime AsOf { get; set; }
    }
}
