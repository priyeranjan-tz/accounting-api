using Accounting.Infrastructure.Persistence.DbContext;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Accounting.ContractTests;

/// <summary>
/// Custom WebApplicationFactory for contract tests.
/// Configures the application to use the existing test database.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
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

            // Add DbContext with test database connection
            // Using the same database as Development (accounting_dev) since migration is already applied
            var connectionString = "Host=localhost;Port=5432;Database=accounting_dev;Username=postgres;Password=Pass@123";
            
            services.AddDbContext<AccountingDbContext>(options =>
            {
                options.UseNpgsql(connectionString);
                options.EnableSensitiveDataLogging(); // For better error messages in tests
            });

            // Note: Database and migrations already applied manually, no need to EnsureCreated
            // EnsureCreated() would bypass migrations and NOT create our triggers!
        });

        builder.UseEnvironment("Development");
    }
}
