namespace Accounting.Domain.Enums;

/// <summary>
/// Represents a ledger account in the chart of accounts.
/// </summary>
public enum LedgerAccount
{
    /// <summary>
    /// Asset account - amounts owed by customers for rides.
    /// Debit increases balance.
    /// </summary>
    AccountsReceivable = 1,

    /// <summary>
    /// Revenue account - income from ride services.
    /// Credit increases revenue.
    /// </summary>
    ServiceRevenue = 2,

    /// <summary>
    /// Asset account - cash or bank deposits from payments.
    /// Debit increases cash balance.
    /// </summary>
    Cash = 3
}
