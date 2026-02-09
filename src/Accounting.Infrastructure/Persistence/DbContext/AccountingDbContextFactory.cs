using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Accounting.Infrastructure.Persistence.DbContext;

/// <summary>
/// Design-time factory for the AccountingDbContext.
/// Required for EF Core migrations when the DbContext has custom constructor parameters.
/// This factory is used by the EF Core tools (dotnet ef) during migration operations.
/// </summary>
public class AccountingDbContextFactory : IDesignTimeDbContextFactory<AccountingDbContext>
{
    /// <summary>
    /// Creates a new instance of AccountingDbContext for design-time operations (migrations).
    /// </summary>
    /// <param name="args">Command-line arguments (not used in this implementation).</param>
    /// <returns>A new AccountingDbContext instance configured for design-time use.</returns>
    public AccountingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AccountingDbContext>();
        
        // Use connection string from environment variable or default for development
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AccountingDb")
            ?? "Host=localhost;Port=5432;Database=accounting_dev;Username=postgres;Password=Pass@123";
        
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
            npgsqlOptions.CommandTimeout(30);
        });

        // Create DbContext without tenantId for migration operations
        // Migrations should not be filtered by tenant
        return new AccountingDbContext(optionsBuilder.Options, tenantId: null);
    }
}
