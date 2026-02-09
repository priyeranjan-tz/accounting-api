using Accounting.Application.Interfaces;
using Accounting.Application.Common;

namespace Accounting.Application.Queries;

/// <summary>
/// Query to get the current balance for an account.
/// Balance = Sum(Debits to AR) - Sum(Credits to AR)
/// Positive balance = amount owed, Negative balance = credit (overpayment)
/// </summary>
public record GetAccountBalanceQuery : IQuery<Result<GetAccountBalanceResult>>
{
    /// <summary>
    /// Account to query balance for.
    /// </summary>
    public required Guid AccountId { get; init; }
}

/// <summary>
/// Result of account balance query.
/// </summary>
public record GetAccountBalanceResult
{
    /// <summary>
    /// Account identifier.
    /// </summary>
    public required Guid AccountId { get; init; }

    /// <summary>
    /// Account display name.
    /// </summary>
    public required string AccountName { get; init; }

    /// <summary>
    /// Current balance (positive = amount owed, negative = credit balance).
    /// </summary>
    public required decimal Balance { get; init; }

    /// <summary>
    /// Currency code (always "USD" per requirements).
    /// </summary>
    public required string Currency { get; init; }

    /// <summary>
    /// UTC timestamp when balance was calculated.
    /// </summary>
    public required DateTime AsOf { get; init; }
}
