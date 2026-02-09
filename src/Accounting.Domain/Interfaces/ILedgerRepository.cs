using Accounting.Domain.Entities;
using Accounting.Domain.ValueObjects;

namespace Accounting.Domain.Interfaces;

/// <summary>
/// Repository for ledger entry operations.
/// Provides access to the append-only ledger with double-entry accounting enforcement.
/// </summary>
public interface ILedgerRepository
{
    /// <summary>
    /// Appends a pair of ledger entries (debit and credit) to the ledger.
    /// CRITICAL: Enforces double-entry accounting - must provide exactly 2 entries.
    /// </summary>
    /// <param name="entries">Pair of ledger entries (one debit, one credit)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transaction ID representing this double-entry transaction</returns>
    /// <exception cref="InvalidOperationException">If entries don't satisfy double-entry rules</exception>
    /// <exception cref="DbException">If idempotency constraint is violated (duplicate ride/payment)</exception>
    Task<Guid> AppendEntriesAsync(
        IEnumerable<LedgerEntry> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the current balance for an account.
    /// Balance = Sum(Debits to AR) - Sum(Credits to AR)
    /// </summary>
    /// <param name="accountId">Account to calculate balance for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current balance (positive = owed, negative = credit)</returns>
    Task<Money> GetAccountBalanceAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the balance for an account as of a specific date/time
    /// </summary>
    /// <param name="accountId">Account to calculate balance for</param>
    /// <param name="asOfDate">Calculate balance as of this date/time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Balance as of the specified date</returns>
    Task<Money> GetAccountBalanceAsync(
        AccountId accountId,
        DateTime asOfDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all ledger entries for an account
    /// </summary>
    /// <param name="accountId">Account ID to retrieve entries for</param>
    /// <param name="tenantId">Tenant ID for isolation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of ledger entries for the account</returns>
    Task<List<LedgerEntry>> GetByAccountIdAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a ride has already been charged to an account (idempotency check).
    /// </summary>
    /// <param name="accountId">Account ID to check</param>
    /// <param name="rideId">External ride identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if ride already charged to this account, false otherwise</returns>
    Task<bool> RideAlreadyChargedAsync(
        AccountId accountId,
        RideId rideId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a payment has already been recorded (for idempotency enforcement).
    /// </summary>
    /// <param name="paymentReferenceId">Payment reference to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if payment has been recorded, false otherwise</returns>
    Task<bool> PaymentAlreadyRecordedAsync(
        string paymentReferenceId,
        CancellationToken cancellationToken = default);
}
