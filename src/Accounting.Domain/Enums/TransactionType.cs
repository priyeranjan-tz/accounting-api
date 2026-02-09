namespace Accounting.Domain.Enums;

/// <summary>
/// Represents the type of transaction that creates ledger entries.
/// </summary>
public enum TransactionType
{
    /// <summary>
    /// Transaction originates from a completed ride.
    /// Creates double-entry: Debit AR, Credit Revenue.
    /// </summary>
    RideCharge = 1,

    /// <summary>
    /// Transaction originates from a payment received.
    /// Creates double-entry: Debit Cash, Credit AR.
    /// </summary>
    Payment = 2
}
