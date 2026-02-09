namespace Accounting.Infrastructure.Persistence.Entities;

/// <summary>
/// Persistence entity for ledger entries.
/// Separate from domain entity per Constitutional Principle VII (Domain-Persistence Separation).
/// 
/// Maps to table: ledger_entries (snake_case per EF Core convention in AccountingDbContext)
/// </summary>
public class LedgerEntryEntity
{
    /// <summary>
    /// Unique identifier for this ledger entry.
    /// Maps to column: id
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Account this entry affects.
    /// Maps to column: account_id
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Which account in the ledger was affected (AR, Revenue, Cash).
    /// Maps to column: ledger_account
    /// Stored as integer enum value.
    /// </summary>
    public int LedgerAccount { get; set; }

    /// <summary>
    /// Amount debited (zero if this entry is a credit).
    /// Maps to column: debit_amount
    /// Database type: NUMERIC(19,4)
    /// </summary>
    public decimal DebitAmount { get; set; }

    /// <summary>
    /// Amount credited (zero if this entry is a debit).
    /// Maps to column: credit_amount
    /// Database type: NUMERIC(19,4)
    /// </summary>
    public decimal CreditAmount { get; set; }

    /// <summary>
    /// Type of source transaction (RideCharge, Payment).
    /// Maps to column: source_type
    /// Stored as integer enum value.
    /// </summary>
    public int SourceType { get; set; }

    /// <summary>
    /// External reference ID (RideId for charges, PaymentId for payments).
    /// Maps to column: source_reference_id
    /// Used for idempotency enforcement via unique index.
    /// </summary>
    public string SourceReferenceId { get; set; } = string.Empty;

    /// <summary>
    /// Multi-tenant isolation identifier.
    /// Maps to column: tenant_id
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// UTC timestamp when entry was created (immutable).
    /// Maps to column: created_at
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// User or system that created the entry (immutable).
    /// Maps to column: created_by
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Optional human-readable description.
    /// Maps to column: description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Concurrency token for optimistic locking.
    /// Maps to column: row_version
    /// PostgreSQL type: xmin (transaction ID)
    /// </summary>
    //public uint RowVersion { get; set; }
}
