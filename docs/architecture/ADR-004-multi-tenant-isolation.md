# ADR-004: Multi-Tenant Data Isolation Strategy

**Status**: Accepted  
**Date**: 2026-02-05  
**Deciders**: Engineering Team, Security Team  
**Technical Story**: Accounting Ledger System - Multi-Tenancy (User Story 2)

## Context

The accounting system serves multiple ride-hailing companies (tenants) from a single database. We must ensure tenant data is completely isolated to prevent cross-tenant data leakage, while maintaining query performance and development simplicity.

## Decision

Use **EF Core Global Query Filters** with `TenantId` in every table.

### Implementation Details

**1. Database Schema** (Tenant column in all tables):
```sql
CREATE TABLE ledger_entries (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    account_id UUID NOT NULL,
    ...
);

CREATE INDEX ix_ledger_entries_tenant_id ON ledger_entries(tenant_id);
```

**2. EF Core Global Query Filter**:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Apply to all entities with TenantId
    modelBuilder.Entity<LedgerEntryEntity>()
        .HasQueryFilter(e => e.TenantId == _tenantId);

    modelBuilder.Entity<AccountEntity>()
        .HasQueryFilter(e => e.TenantId == _tenantId);

    modelBuilder.Entity<InvoiceEntity>()
        .HasQueryFilter(e => e.TenantId == _tenantId);
}
```

**3. Middleware Extracts Tenant from Header**:
```csharp
public class TenantContext
{
    public Guid? GetTenantId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantId))
        {
            return Guid.Parse(tenantId);
        }
        return null; // Reject request in middleware
    }
}
```

**4. DbContext Receives Tenant via Constructor**:
```csharp
public class AccountingDbContext : DbContext
{
    private readonly Guid _tenantId;

    public AccountingDbContext(
        DbContextOptions<AccountingDbContext> options,
        IHttpContextAccessor httpContextAccessor) : base(options)
    {
        _tenantId = httpContextAccessor.HttpContext?.GetTenantId() ?? Guid.Empty;
    }
}
```

**5. Automatic WHERE Clause Injection**:
```csharp
// Developer writes:
var balance = await _dbContext.LedgerEntries
    .Where(e => e.AccountId == accountId)
    .SumAsync(e => e.DebitAmount);

// EF Core generates SQL:
SELECT SUM(debit_amount)
FROM ledger_entries
WHERE account_id = @accountId
  AND tenant_id = @tenantId  -- ← Automatically added by Global Query Filter
```

## Consequences

### Positive

- **Zero Developer Errors**: Cannot forget to filter by tenant (automatic WHERE clause)
- **Performance**: Database indexes on `tenant_id` eliminate full table scans
- **Security**: Row-Level Security enforced at EF Core layer
- **Query Simplicity**: Developers write normal LINQ queries
- **Testability**: Easy to test with different tenant IDs

### Negative

- **Global Filter Bypass Risk**: `.IgnoreQueryFilters()` bypasses tenant isolation
- **Performance Overhead**: Extra JOIN/WHERE clause on every query (mitigated by indexes)
- **Migration Complexity**: Difficult to move to separate databases per tenant

### Mitigation

- **Code Review**: Search for `.IgnoreQueryFilters()` in PRs (should be extremely rare)
- **Integration Tests**: Validate tenant isolation (T062: TenantIsolationTests.cs)
- **Monitoring**: Alert on cross-tenant query patterns (OpenTelemetry tracing)

## Alternatives Considered

### Alternative 1: Separate Database Per Tenant

**Description**: Each tenant gets own PostgreSQL database (e.g., `tenant_abc`, `tenant_xyz`)

**Rejected Because**:
- **Operational Complexity**: Managing 100s of databases, migrations
- **Cost**: N databases vs 1 database (infrastructure overhead)
- **Backup/Restore**: 100x more backup jobs to manage
- **Schema Changes**: Must apply migration to every database

**When to Reconsider**: If tenants exceed 10,000 or compliance requires physical isolation

### Alternative 2: PostgreSQL Row-Level Security (RLS)

**Description**: Use PostgreSQL's built-in RLS with policies:
```sql
CREATE POLICY tenant_isolation ON ledger_entries
    USING (tenant_id = current_setting('app.current_tenant')::uuid);
```

**Rejected Because**:
- Requires setting session variable on every connection (`SET app.current_tenant = '...'`)
- EF Core doesn't natively support session variables
- Global Query Filters provide same protection at application layer
- RLS better suited for database-level tenancy (not application-level)

**When to Reconsider**: If direct database access (non-EF Core) is required

### Alternative 3: Tenant-Specific Schema

**Description**: Each tenant gets own schema (e.g., `tenant_abc.ledger_entries`, `tenant_xyz.ledger_entries`)

**Rejected Because**:
- EF Core doesn't support dynamic schema selection well
- Migration complexity (N schemas vs 1 schema)
- Cross-tenant reporting becomes difficult (requires UNION across schemas)

## Related Decisions

- **ADR-005**: Outbox pattern (integration events must include `TenantId`)

## Security Considerations

**Threat Model**:
1. **Developer Accidentally Queries Wrong Tenant**: ✓ Prevented by Global Query Filters
2. **SQL Injection Bypasses Tenant Filter**: ✓ Prevented by EF Core parameterized queries
3. **Malicious Developer Uses `.IgnoreQueryFilters()`**: ⚠️ Requires code review + monitoring
4. **Database Direct Access (non-EF Core)**: ⚠️ Requires database connection restrictions

**Mitigation**:
- Restrict database credentials (application service account only)
- Audit logs for `.IgnoreQueryFilters()` usage
- Integration tests validate tenant isolation (T062)

## Implementation Checklist

- [x] Add `TenantId` column to all tables
- [x] Create composite indexes `(tenant_id, <primary_key>)`
- [x] Configure EF Core Global Query Filters
- [x] Middleware extracts tenant from `X-Tenant-Id` header
- [x] Integration tests validate cross-tenant isolation (T062)
- [ ] Production monitoring for `.IgnoreQueryFilters()` usage (future story)

## References

- [EF Core Global Query Filters](https://learn.microsoft.com/en-us/ef/core/querying/filters)
- [Multi-Tenancy Patterns - Microsoft Docs](https://learn.microsoft.com/en-us/azure/architecture/patterns/sharding)
- [PostgreSQL Row-Level Security](https://www.postgresql.org/docs/17/ddl-rowsecurity.html)
- Vaughn Vernon: [Implementing Domain-Driven Design - Multi-Tenancy](https://www.amazon.com/Implementing-Domain-Driven-Design-Vaughn-Vernon/dp/0321834577)
