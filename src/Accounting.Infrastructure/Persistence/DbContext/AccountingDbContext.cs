using Microsoft.EntityFrameworkCore;
using Accounting.Infrastructure.Persistence.Entities;
using Accounting.Infrastructure.Persistence.Outbox;

namespace Accounting.Infrastructure.Persistence.DbContext;

/// <summary>
/// Entity Framework Core database context for the Accounting system.
/// Implements multi-tenant isolation through global query filters.
/// </summary>
public class AccountingDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    private readonly Guid? _tenantId;

    /// <summary>
    /// Initializes a new instance of the AccountingDbContext.
    /// </summary>
    /// <param name="options">The DbContext options.</param>
    /// <param name="tenantId">The current tenant ID for global query filtering.</param>
    public AccountingDbContext(
        DbContextOptions<AccountingDbContext> options,
        Guid? tenantId = null)
        : base(options)
    {
        _tenantId = tenantId;
    }

    /// <summary>
    /// DbSet for ledger entries (User Story 1).
    /// </summary>
    public DbSet<LedgerEntryEntity> LedgerEntries => Set<LedgerEntryEntity>();

    /// <summary>
    /// DbSet for accounts (User Story 2).
    /// </summary>
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();

    /// <summary>
    /// DbSet for invoices (User Story 3).
    /// </summary>
    public DbSet<InvoiceEntity> Invoices => Set<InvoiceEntity>();

    /// <summary>
    /// DbSet for invoice line items (User Story 3).
    /// </summary>
    public DbSet<InvoiceLineItemEntity> InvoiceLineItems => Set<InvoiceLineItemEntity>();

    /// <summary>
    /// DbSet for outbox events (T145: Outbox pattern for integration events).
    /// </summary>
    public DbSet<OutboxEventEntity> OutboxEvents => Set<OutboxEventEntity>();

    /// <summary>
    /// Configures the model and applies conventions.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply snake_case naming convention for tables and columns
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Table names: Convert PascalCase to snake_case
            var tableName = ToSnakeCase(entity.GetTableName() ?? entity.DisplayName());
            entity.SetTableName(tableName);

            // Column names: Convert PascalCase to snake_case
            foreach (var property in entity.GetProperties())
            {
                var columnName = ToSnakeCase(property.Name);
                property.SetColumnName(columnName);
            }

            // Foreign key names: Convert PascalCase to snake_case
            foreach (var key in entity.GetKeys())
            {
                var keyName = ToSnakeCase(key.GetName() ?? $"pk_{tableName}");
                key.SetName(keyName);
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                var fkName = ToSnakeCase(foreignKey.GetConstraintName() ?? 
                    $"fk_{tableName}_{foreignKey.PrincipalEntityType.GetTableName()}");
                foreignKey.SetConstraintName(fkName);
            }

            foreach (var index in entity.GetIndexes())
            {
                var indexName = ToSnakeCase(index.GetDatabaseName() ?? $"ix_{tableName}");
                index.SetDatabaseName(indexName);
            }
        }

        // Configure timestamps to use UTC
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(
                        new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
                            v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
                            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(
                        new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
                            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null,
                            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null));
                }
            }
        }

        // Apply global query filter for tenant isolation
        if (_tenantId.HasValue)
        {
            modelBuilder.Entity<AccountEntity>()
                .HasQueryFilter(e => e.TenantId == _tenantId.Value);

            modelBuilder.Entity<InvoiceEntity>()
                .HasQueryFilter(e => e.TenantId == _tenantId.Value);
        }

        // Apply entity configurations from separate files
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountingDbContext).Assembly);
    }

    /// <summary>
    /// Converts a PascalCase string to snake_case.
    /// </summary>
    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var result = new System.Text.StringBuilder();
        result.Append(char.ToLowerInvariant(input[0]));

        for (int i = 1; i < input.Length; i++)
        {
            char c = input[i];
            if (char.IsUpper(c))
            {
                result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Saves changes to the database, ensuring timestamps are in UTC.
    /// </summary>
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    /// <summary>
    /// Saves changes to the database asynchronously, ensuring timestamps are in UTC.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Updates created_at and modified_at timestamps for entities.
    /// </summary>
    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            var createdAtProperty = entry.Properties.FirstOrDefault(p => 
                p.Metadata.Name == "CreatedAt" && p.Metadata.ClrType == typeof(DateTime));
            
            var modifiedAtProperty = entry.Properties.FirstOrDefault(p => 
                p.Metadata.Name == "ModifiedAt" && p.Metadata.ClrType == typeof(DateTime?));

            if (entry.State == EntityState.Added && createdAtProperty != null)
            {
                createdAtProperty.CurrentValue = DateTime.UtcNow;
            }

            if (entry.State == EntityState.Modified && modifiedAtProperty != null)
            {
                modifiedAtProperty.CurrentValue = DateTime.UtcNow;
            }
        }
    }
}
