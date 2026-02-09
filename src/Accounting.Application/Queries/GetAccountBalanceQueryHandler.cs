using Accounting.Application.Common;
using Accounting.Domain.Interfaces;
using Accounting.Domain.ValueObjects;

namespace Accounting.Application.Queries;

/// <summary>
/// Handler for querying account balance.
/// Calculates balance using double-entry accounting:
/// Balance = Sum(Debits to AR) - Sum(Credits to AR)
/// 
/// Positive balance = amount owed by customer
/// Negative balance = credit balance (overpayment)
/// </summary>
public class GetAccountBalanceQueryHandler
{
    private readonly ILedgerRepository _ledgerRepository;
    private readonly Guid _tenantId; // Will be injected from HTTP context via middleware

    public GetAccountBalanceQueryHandler(
        ILedgerRepository ledgerRepository,
        Guid tenantId)
    {
        _ledgerRepository = ledgerRepository;
        _tenantId = tenantId;
    }

    public async Task<Result<GetAccountBalanceResult>> HandleAsync(
        GetAccountBalanceQuery query,
        CancellationToken cancellationToken = default)
    {
        var accountId = new AccountId(query.AccountId);

        // Calculate balance from ledger entries
        var balance = await _ledgerRepository.GetAccountBalanceAsync(
            accountId,
            cancellationToken);

        // Build result
        var result = new GetAccountBalanceResult
        {
            AccountId = query.AccountId,
            AccountName = "Account", // Note: Account name lookup deferred (query optimization)
            Balance = balance.Amount,
            Currency = "USD",
            AsOf = DateTime.UtcNow
        };

        return Result<GetAccountBalanceResult>.Success(result);
    }
}
