using Accounting.Application.Interfaces;
using Accounting.Application.Common;

namespace Accounting.Application.Commands;

/// <summary>
/// Command to record a payment received from a customer using double-entry accounting.
/// Creates two ledger entries: Debit Cash, Credit AR.
/// Supports partial, full, and overpayments.
/// </summary>
public record RecordPaymentCommand : ICommand<Result<RecordPaymentResult>>
{
    /// <summary>
    /// Account receiving the payment.
    /// </summary>
    public required Guid AccountId { get; init; }

    /// <summary>
    /// Unique payment identifier (external reference, e.g., Stripe payment ID).
    /// Used for idempotency - prevents duplicate payment recording.
    /// </summary>
    public required string PaymentReferenceId { get; init; }

    /// <summary>
    /// Payment amount in USD (must be > 0).
    /// Supports partial, full, and overpayments.
    /// </summary>
    public required decimal Amount { get; init; }

    /// <summary>
    /// Date payment was received (UTC).
    /// </summary>
    public required DateTime PaymentDate { get; init; }

    /// <summary>
    /// Optional payment method (e.g., "Credit Card", "Bank Transfer", "Check").
    /// </summary>
    public string? PaymentMode { get; init; }

    /// <summary>
    /// Optional description/notes.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Result of recording a payment.
/// </summary>
public record RecordPaymentResult
{
    /// <summary>
    /// Unique transaction identifier (not individual ledger entry IDs).
    /// </summary>
    public required Guid TransactionId { get; init; }

    /// <summary>
    /// Account that received the payment.
    /// </summary>
    public required Guid AccountId { get; init; }

    /// <summary>
    /// Ledger entries created (always 2 for double-entry).
    /// </summary>
    public required List<LedgerEntryDto> Entries { get; init; } = new();

    /// <summary>
    /// New account balance after this payment.
    /// Positive = amount owed, Negative = credit balance (overpayment).
    /// </summary>
    public required decimal NewBalance { get; init; }

    /// <summary>
    /// UTC timestamp when transaction was recorded.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
}
