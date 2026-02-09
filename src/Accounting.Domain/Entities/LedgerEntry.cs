using Accounting.Domain.Enums;
using Accounting.Domain.ValueObjects;

namespace Accounting.Domain.Entities;

/// <summary>
/// Represents a single line in the double-entry ledger.
/// Each financial transaction produces exactly two ledger entries: one debit and one credit.
/// 
/// CRITICAL: Ledger entries are IMMUTABLE once created (append-only ledger).
/// Updates and deletes are prevented by database triggers.
/// </summary>
public class LedgerEntry
{
    /// <summary>
    /// Unique identifier for this ledger entry.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Account this entry affects.
    /// </summary>
    public AccountId AccountId { get; private set; }

    /// <summary>
    /// Which account in the ledger was affected (AR, Revenue, Cash).
    /// </summary>
    public LedgerAccount LedgerAccount { get; private set; }

    /// <summary>
    /// Amount debited (zero if this entry is a credit).
    /// </summary>
    public Money DebitAmount { get; private set; }

    /// <summary>
    /// Amount credited (zero if this entry is a debit).
    /// </summary>
    public Money CreditAmount { get; private set; }

    /// <summary>
    /// Type of source transaction (RideCharge, Payment).
    /// </summary>
    public TransactionType SourceType { get; private set; }

    /// <summary>
    /// External reference ID (RideId for charges, PaymentId for payments).
    /// Used for idempotency enforcement.
    /// </summary>
    public string SourceReferenceId { get; private set; }

    /// <summary>
    /// Multi-tenant isolation identifier.
    /// </summary>
    public Guid TenantId { get; private set; }

    /// <summary>
    /// UTC timestamp when entry was created (immutable).
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// User or system that created the entry (immutable).
    /// </summary>
    public string CreatedBy { get; private set; }

    /// <summary>
    /// Optional human-readable description.
    /// </summary>
    public string? Description { get; private set; }

    // Private constructor - enforce factory methods for entry creation
    private LedgerEntry(
        Guid id,
        AccountId accountId,
        LedgerAccount ledgerAccount,
        Money debitAmount,
        Money creditAmount,
        TransactionType sourceType,
        string sourceReferenceId,
        Guid tenantId,
        DateTime createdAt,
        string createdBy,
        string? description)
    {
        Id = id;
        AccountId = accountId;
        LedgerAccount = ledgerAccount;
        DebitAmount = debitAmount;
        CreditAmount = creditAmount;
        SourceType = sourceType;
        SourceReferenceId = sourceReferenceId;
        TenantId = tenantId;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        Description = description;

        ValidateEntry();
    }

    /// <summary>
    /// Factory method to create a debit entry.
    /// </summary>
    /// <param name="accountId">Account to debit</param>
    /// <param name="ledgerAccount">Which ledger account to debit (AR, Revenue, Cash)</param>
    /// <param name="amount">Amount to debit (must be positive)</param>
    /// <param name="sourceType">Type of source transaction</param>
    /// <param name="sourceReferenceId">External reference ID for idempotency</param>
    /// <param name="tenantId">Tenant owning this entry</param>
    /// <param name="createdBy">User or system creating the entry</param>
    /// <param name="description">Optional description</param>
    /// <returns>New debit ledger entry</returns>
    public static LedgerEntry Debit(
        AccountId accountId,
        LedgerAccount ledgerAccount,
        Money amount,
        TransactionType sourceType,
        string sourceReferenceId,
        Guid tenantId,
        string createdBy,
        string? description = null)
    {
        if (amount.Amount <= 0)
        {
            throw new ArgumentException("Debit amount must be positive", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(sourceReferenceId))
        {
            throw new ArgumentException("Source reference ID cannot be empty", nameof(sourceReferenceId));
        }

        if (string.IsNullOrWhiteSpace(createdBy))
        {
            throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));
        }

        return new LedgerEntry(
            id: Guid.NewGuid(),
            accountId: accountId,
            ledgerAccount: ledgerAccount,
            debitAmount: amount,
            creditAmount: Money.Zero,
            sourceType: sourceType,
            sourceReferenceId: sourceReferenceId,
            tenantId: tenantId,
            createdAt: DateTime.UtcNow,
            createdBy: createdBy,
            description: description);
    }

    /// <summary>
    /// Factory method to create a credit entry.
    /// </summary>
    /// <param name="accountId">Account to credit</param>
    /// <param name="ledgerAccount">Which ledger account to credit (AR, Revenue, Cash)</param>
    /// <param name="amount">Amount to credit (must be positive)</param>
    /// <param name="sourceType">Type of source transaction</param>
    /// <param name="sourceReferenceId">External reference ID for idempotency</param>
    /// <param name="tenantId">Tenant owning this entry</param>
    /// <param name="createdBy">User or system creating the entry</param>
    /// <param name="description">Optional description</param>
    /// <returns>New credit ledger entry</returns>
    public static LedgerEntry Credit(
        AccountId accountId,
        LedgerAccount ledgerAccount,
        Money amount,
        TransactionType sourceType,
        string sourceReferenceId,
        Guid tenantId,
        string createdBy,
        string? description = null)
    {
        if (amount.Amount <= 0)
        {
            throw new ArgumentException("Credit amount must be positive", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(sourceReferenceId))
        {
            throw new ArgumentException("Source reference ID cannot be empty", nameof(sourceReferenceId));
        }

        if (string.IsNullOrWhiteSpace(createdBy))
        {
            throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));
        }

        return new LedgerEntry(
            id: Guid.NewGuid(),
            accountId: accountId,
            ledgerAccount: ledgerAccount,
            debitAmount: Money.Zero,
            creditAmount: amount,
            sourceType: sourceType,
            sourceReferenceId: sourceReferenceId,
            tenantId: tenantId,
            createdAt: DateTime.UtcNow,
            createdBy: createdBy,
            description: description);
    }

    /// <summary>
    /// Validates that the entry satisfies double-entry accounting invariants.
    /// </summary>
    private void ValidateEntry()
    {
        // CRITICAL: Exactly one of debit or credit must be non-zero (single-sided entry)
        var hasDebit = DebitAmount.Amount > 0;
        var hasCredit = CreditAmount.Amount > 0;

        if (hasDebit && hasCredit)
        {
            throw new InvalidOperationException(
                "Invalid ledger entry: both debit and credit amounts are non-zero. " +
                "Each entry must be single-sided (either debit OR credit).");
        }

        if (!hasDebit && !hasCredit)
        {
            throw new InvalidOperationException(
                "Invalid ledger entry: both debit and credit amounts are zero. " +
                "Entry must have an effect on the ledger.");
        }

        // Validate source reference
        if (string.IsNullOrWhiteSpace(SourceReferenceId))
        {
            throw new InvalidOperationException(
                "Invalid ledger entry: source reference ID cannot be empty.");
        }
    }

    /// <summary>
    /// Returns the net effect of this entry (positive for debit, negative for credit).
    /// </summary>
    public Money GetNetEffect()
    {
        return DebitAmount - CreditAmount;
    }

    /// <summary>
    /// Determines if this entry is a debit.
    /// </summary>
    public bool IsDebit() => DebitAmount.Amount > 0;

    /// <summary>
    /// Determines if this entry is a credit.
    /// </summary>
    public bool IsCredit() => CreditAmount.Amount > 0;

    /// <summary>
    /// Returns the amount of this entry (regardless of debit/credit side).
    /// </summary>
    public Money GetAmount() => IsDebit() ? DebitAmount : CreditAmount;
}
