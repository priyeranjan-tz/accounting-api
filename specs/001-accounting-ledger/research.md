# Research: Dual-Entry Accounting & Invoicing Service

**Feature**: [spec.md](spec.md)  
**Date**: 2026-02-06  
**Status**: Complete

## Research Questions

Based on Technical Context and Constitution Check, the following areas require research:

1. Double-entry accounting patterns in .NET
2. Fixed-point decimal handling for monetary values
3. EF Core optimistic concurrency for ledger immutability
4. Outbox pattern implementation
5. Multi-tenant data isolation strategies

---

## 1. Double-Entry Accounting Patterns in .NET

### Decision
Use **explicit debit/credit columns** with database check constraint to enforce balance.

### Rationale
- **Explicit Columns**: `debit_amount NUMERIC(19,4) NOT NULL`, `credit_amount NUMERIC(19,4) NOT NULL` with check constraint `CHECK ((debit_amount > 0 AND credit_amount = 0) OR (credit_amount > 0 AND debit_amount = 0))`
- **Database-Level Enforcement**: Prevents impossible states (both debit and credit non-zero)
- **Query Simplicity**: Balance = SUM(debit_amount) - SUM(credit_amount) is straightforward
- **Audit Trail**: Each entry clearly shows which side of the equation it represents
- **Domain Model**: `LedgerEntry` value object with factory methods `Debit(amount)` and `Credit(amount)` that enforce single-side population

### Alternatives Considered
- **Single Amount Column with Type Flag**: Rejected because it requires application-level validation and doesn't leverage database constraints
- **Separate Debit/Credit Tables**: Rejected due to query complexity and no compelling benefit over check constraints
- **Event Sourcing**: Considered but rejected for MVP; append-only ledger provides sufficient audit trail without complexity of event store

### Implementation Pattern
```csharp
// Domain layer
public sealed class LedgerEntry
{
    public required Guid Id { get; init; }
    public required AccountId AccountId { get; init; }
    public required LedgerAccount Account { get; init; } // AR, Revenue, Cash
    public required Money DebitAmount { get; init; }
    public required Money CreditAmount { get; init; }
    public required TransactionSource Source { get; init; }
    public required DateTime CreatedAt { get; init; }
    
    // Factory methods enforce rule: exactly one side must be non-zero
    public static LedgerEntry Debit(AccountId accountId, LedgerAccount account, Money amount, TransactionSource source)
    {
        if (amount.Amount <= 0) throw new DomainException("Debit amount must be positive");
        return new LedgerEntry 
        { 
            DebitAmount = amount, 
            CreditAmount = Money.Zero,
            // ... other properties
        };
    }
    
    public static LedgerEntry Credit(AccountId accountId, LedgerAccount account, Money amount, TransactionSource source)
    {
        if (amount.Amount <= 0) throw new DomainException("Credit amount must be positive");
        return new LedgerEntry 
        { 
            DebitAmount = Money.Zero,
            CreditAmount = amount,
            // ... other properties
        };
    }
}

// Infrastructure layer - EF Core configuration
modelBuilder.Entity<LedgerEntryEntity>()
    .ToTable("ledger_entries", "accounting", 
        t => t.HasCheckConstraint("ck_ledger_entries_single_side",
            "(debit_amount > 0 AND credit_amount = 0) OR (credit_amount > 0 AND debit_amount = 0)"));
```

---

## 2. Fixed-Point Decimal Handling for Monetary Values

### Decision
Use **C# `decimal` type** (128-bit) with explicit precision `decimal(19,4)` in PostgreSQL.

### Rationale
- **Native .NET Type**: No additional dependencies, excellent performance
- **Sufficient Precision**: 19 digits total, 4 decimal places supports up to $999,999,999,999,999.9999
- **No Floating Point Errors**: `decimal` is base-10, avoids binary floating point rounding issues
- **EF Core Support**: First-class mapping to PostgreSQL NUMERIC
- **Constitutional Requirement**: "Fixed-point monetary calculations" mandated in FR-012

### Alternatives Considered
- **NodaTime Money/BigDecimal**: Rejected because `decimal` is sufficient for USD-only system with known range
- **Integer Cents (long)**: Rejected due to loss of readability and arithmetic complexity
- **float/double**: FORBIDDEN - introduces rounding errors (e.g., 0.1 + 0.2 ≠ 0.3)

### Implementation Pattern
```csharp
// Domain layer - Money value object
public readonly record struct Money
{
    private const int DecimalPlaces = 4;  // Match PostgreSQL NUMERIC(19,4)
    
    public decimal Amount { get; init; }
    
    public Money(decimal amount)
    {
        Amount = decimal.Round(amount, DecimalPlaces, MidpointRounding.AwayFromZero);
    }
    
    public static Money Zero => new Money(0m);
    public static Money FromDollars(decimal dollars) => new Money(dollars);
    
    public static Money operator +(Money a, Money b) => new Money(a.Amount + b.Amount);
    public static Money operator -(Money a, Money b) => new Money(a.Amount - b.Amount);
    
    public bool IsPositive => Amount > 0;
    public bool IsNegative => Amount < 0;
    public Money Abs() => new Money(Math.Abs(Amount));
}

// Infrastructure layer - PostgreSQL mapping
modelBuilder.Entity<LedgerEntryEntity>()
    .Property(e => e.DebitAmount)
    .HasColumnType("NUMERIC(19,4)")
    .HasColumnName("debit_amount");
```

### Validation
- ✅ Precision: 4 decimal places matches currency standards (supports sub-cent calculations if needed)
- ✅ Range: $999 trillion max - exceeds any realistic ride fare or account balance
- ✅ Performance: `decimal` arithmetic is fast in .NET (faster than BigDecimal libraries)
- ✅ Constitutional Compliance: "Fixed-point monetary calculations" satisfied

---

## 3. EF Core Optimistic Concurrency for Ledger Immutability

### Decision
Use **RowVersion (timestamp) + database trigger** to enforce append-only immutability.

### Rationale
- **Optimistic Concurrency Control**: EF Core's `[Timestamp]` attribute (maps to PostgreSQL `xmin` or custom `row_version` column)
- **Prevents Updates**: Database trigger `BEFORE UPDATE ON ledger_entries FOR EACH ROW EXECUTE FUNCTION prevent_ledger_update()` raises exception
- **Prevents Deletes**: Database trigger `BEFORE DELETE ON ledger_entries FOR EACH ROW EXECUTE FUNCTION prevent_ledger_delete()` raises exception
- **Defense in Depth**: Even if application code attempts update/delete, database rejects it
- **Audit Trail**: `created_at` timestamp never changes, `row_version` detects concurrent modification attempts

### Alternatives Considered
- **Application-Level Only**: Rejected because it doesn't prevent direct SQL manipulation
- **Database Permissions**: Could revoke UPDATE/DELETE at role level but complicates migrations
- **Event Sourcing**: Rejected for MVP complexity

### Implementation Pattern
```sql
-- Migration: Create immutability triggers
CREATE OR REPLACE FUNCTION prevent_ledger_update()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'Ledger entries are immutable and cannot be updated';
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION prevent_ledger_delete()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'Ledger entries are immutable and cannot be deleted';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_prevent_ledger_update
    BEFORE UPDATE ON accounting.ledger_entries
    FOR EACH ROW
    EXECUTE FUNCTION prevent_ledger_update();

CREATE TRIGGER trg_prevent_ledger_delete
    BEFORE DELETE ON accounting.ledger_entries
    FOR EACH ROW
    EXECUTE FUNCTION prevent_ledger_delete();
```

```csharp
// Infrastructure layer - EF Core configuration
modelBuilder.Entity<LedgerEntryEntity>()
    .Property(e => e.RowVersion)
    .IsRowVersion()  // Maps to PostgreSQL xmin or custom column
    .IsConcurrencyToken();

// Application layer - Optimistic concurrency handling
public async Task<Result<LedgerEntry>> RecordChargeAsync(RideCharge charge, CancellationToken ct)
{
    // EF Core tracks row version
    var entry = new LedgerEntryEntity { /* ... */ };
    await _context.LedgerEntries.AddAsync(entry, ct);
    
    try
    {
        await _context.SaveChangesAsync(ct);
        return Result.Success(entry.ToDomain());
    }
    catch (DbUpdateConcurrencyException)
    {
        // Another transaction modified the same row
        return Result.Failure<LedgerEntry>(LedgerErrors.ConcurrencyConflict);
    }
}
```

### Edge Case Handling
- **Concurrent Appends**: Allowed - different rows, no conflict
- **Concurrent Balance Reads**: Allowed - reads don't hold locks
- **Update Attempt**: Database trigger rejects with exception
- **Delete Attempt**: Database trigger rejects with exception

---

## 4. Outbox Pattern Implementation

### Decision
Use **dedicated outbox table + background processor** with Quartz.NET for reliable event publishing.

### Rationale
- **Transactional Integrity**: Outbox messages saved in same transaction as ledger entries (atomic)
- **At-Least-Once Delivery**: Background processor retries failed deliveries
- **Order Preservation**: Process messages in `created_at` order per aggregate
- **Idempotency**: Consumers must handle duplicate events (constitutional requirement)
- **No Distributed Transaction**: Avoids 2PC, aligns with Principle V (Eventual Consistency)

### Alternatives Considered
- **Direct Event Publishing**: Rejected because it risks event loss if publish fails after database commit
- **CDC (Change Data Capture)**: Considered but over-engineered for MVP
- **Message Broker Transaction**: Rejected because it introduces distributed transaction complexity

### Implementation Pattern
```csharp
// Infrastructure layer - Outbox table
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; }  // "RideChargeRecorded", "InvoiceGenerated"
    public string Payload { get; set; }    // JSON serialized event
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
}

// Domain event handler - Save to outbox
public class LedgerEventHandler
{
    private readonly AccountingDbContext _context;
    
    public async Task HandleAsync(RideChargeRecorded domainEvent, CancellationToken ct)
    {
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = nameof(RideChargeRecorded),
            Payload = JsonSerializer.Serialize(domainEvent),
            CreatedAt = DateTime.UtcNow,
        };
        
        await _context.OutboxMessages.AddAsync(outboxMessage, ct);
        // Saved in same transaction as ledger entry
    }
}

// Background processor - Quartz.NET job
public class OutboxProcessorJob : IJob
{
    private readonly AccountingDbContext _context;
    private readonly IEventPublisher _publisher;
    
    public async Task Execute(IJobExecutionContext context)
    {
        var pendingMessages = await _context.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(100)
            .ToListAsync();
        
        foreach (var message in pendingMessages)
        {
            try
            {
                await _publisher.PublishAsync(message.EventType, message.Payload);
                message.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                _logger.LogWarning(ex, "Failed to process outbox message {Id}, retry {Retry}", 
                    message.Id, message.RetryCount);
                
                if (message.RetryCount >= 5)
                {
                    // Move to dead-letter queue
                    await _context.OutboxDeadLetters.AddAsync(/* ... */);
                    _context.OutboxMessages.Remove(message);
                }
            }
        }
        
        await _context.SaveChangesAsync();
    }
}
```

### Configuration
- **Job Schedule**: Every 30 seconds (configurable)
- **Batch Size**: 100 messages per execution
- **Retry Policy**: 5 attempts with exponential backoff
- **Dead Letter Queue**: After 5 failures, move to DLQ for manual investigation

---

## 5. Multi-Tenant Data Isolation Strategies

### Decision
Use **TenantId column + row-level filtering** with EF Core Global Query Filters.

### Rationale
- **Shared Database, Shared Schema**: Simplest deployment (single database, single connection pool)
- **Row-Level Security**: PostgreSQL RLS optional but adds defense in depth
- **EF Core Global Filters**: Automatic TenantId filtering on all queries
- **Constitutional Requirement**: "Zero tenant data leakage" (FR-031)
- **Performance**: Single database allows efficient cross-account analytics within tenant

### Alternatives Considered
- **Database Per Tenant**: Rejected due to operational complexity (thousands of databases, migrations, backups)
- **Schema Per Tenant**: Rejected due to connection pool fragmentation and query complexity
- **Separate Tables**: Rejected - same issues as schema-per-tenant

### Implementation Pattern
```csharp
// Infrastructure layer - DbContext with Global Query Filter
public class AccountingDbContext : DbContext
{
    private readonly ICurrentTenantService _tenantService;
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountEntity>()
            .HasQueryFilter(a => a.TenantId == _tenantService.GetCurrentTenantId());
        
        modelBuilder.Entity<LedgerEntryEntity>()
            .HasQueryFilter(l => l.TenantId == _tenantService.GetCurrentTenantId());
        
        modelBuilder.Entity<InvoiceEntity>()
            .HasQueryFilter(i => i.TenantId == _tenantService.GetCurrentTenantId());
    }
}

// API layer - Middleware extracts TenantId from JWT
public class TenantIsolationMiddleware
{
    public async Task InvokeAsync(HttpContext context, ICurrentTenantService tenantService)
    {
        var tenantIdClaim = context.User.FindFirst("tenant_id");
        if (tenantIdClaim == null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Missing tenant_id claim" });
            return;
        }
        
        tenantService.SetCurrentTenant(Guid.Parse(tenantIdClaim.Value));
        await _next(context);
    }
}

// Infrastructure - Current tenant service (scoped lifetime)
public interface ICurrentTenantService
{
    Guid GetCurrentTenantId();
    void SetCurrentTenant(Guid tenantId);
}

public class CurrentTenantService : ICurrentTenantService
{
    private Guid? _tenantId;
    
    public Guid GetCurrentTenantId() => _tenantId ?? throw new InvalidOperationException("Tenant not set");
    public void SetCurrentTenant(Guid tenantId) => _tenantId = tenantId;
}
```

### PostgreSQL Row-Level Security (Optional Defense in Depth)
```sql
-- Enable RLS on tables
ALTER TABLE accounting.accounts ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounting.ledger_entries ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounting.invoices ENABLE ROW LEVEL SECURITY;

-- Create policy: Only allow access to rows matching current tenant
CREATE POLICY tenant_isolation_policy ON accounting.accounts
    USING (tenant_id = current_setting('app.current_tenant')::UUID);
```

### Validation Strategy
- **Integration Tests**: Verify EF Global Filters prevent cross-tenant queries
- **Manual Testing**: Attempt queries with different tenant JWTs, verify isolation
- **Code Review**: ALL queries must use DbContext (no raw SQL without TenantId filter)
- **Monitoring**: Alert on any query not using indexed TenantId column (performance anomaly)

---

## Summary of Decisions

| Research Topic | Decision | Key Technology |
|---------------|----------|---------------|
| Double-Entry Accounting | Explicit debit/credit columns + check constraint | PostgreSQL CHECK constraint |
| Monetary Precision | C# `decimal` type with NUMERIC(19,4) | .NET decimal, PostgreSQL NUMERIC |
| Ledger Immutability | RowVersion + database triggers | EF Core IsConcurrencyToken(), PostgreSQL triggers |
| Reliable Events | Outbox pattern + background processor | Quartz.NET, transactional outbox table |
| Multi-Tenancy | TenantId column + EF Global Query Filters | EF Core HasQueryFilter(), JWT tenant claim |

All decisions align with constitutional principles and support the feature requirements defined in spec.md.
