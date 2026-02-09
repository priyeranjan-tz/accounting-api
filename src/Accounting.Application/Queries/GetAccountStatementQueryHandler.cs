using Accounting.Application.Common;
using Accounting.Domain.Interfaces;
using Accounting.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Accounting.Application.Queries;

/// <summary>
/// Handler for retrieving account statements with opening balance, transactions, and closing balance
/// </summary>
public class GetAccountStatementQueryHandler
{
    private readonly IAccountRepository _accountRepository;
    private readonly ILedgerRepository _ledgerRepository;
    private readonly ILogger<GetAccountStatementQueryHandler> _logger;
    private readonly Guid _tenantId;

    public GetAccountStatementQueryHandler(
        IAccountRepository accountRepository,
        ILedgerRepository ledgerRepository,
        ILogger<GetAccountStatementQueryHandler> logger,
        Guid tenantId)
    {
        _accountRepository = accountRepository;
        _ledgerRepository = ledgerRepository;
        _logger = logger;
        _tenantId = tenantId;
    }

    public async Task<Result<GetAccountStatementResponse>> HandleAsync(
        GetAccountStatementQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Retrieving account statement - AccountId: {AccountId}, Period: {StartDate} to {EndDate}, TenantId: {TenantId}",
            query.AccountId, query.StartDate, query.EndDate, _tenantId);

        // Validate account exists
        var account = await _accountRepository.GetByIdAsync(query.AccountId, _tenantId, cancellationToken);
        if (account == null)
        {
            _logger.LogWarning(
                "Account not found - AccountId: {AccountId}, TenantId: {TenantId}",
                query.AccountId, _tenantId);
            return Result.Failure<GetAccountStatementResponse>(
                Error.NotFound("ACCOUNT_NOT_FOUND", $"Account with ID '{query.AccountId}' not found"));
        }

        // Calculate opening balance (balance before period start)
        var accountId = new AccountId(query.AccountId);
        var openingBalance = await _ledgerRepository.GetAccountBalanceAsync(
            accountId,
            query.StartDate.AddSeconds(-1), // Just before period start
            cancellationToken);

        _logger.LogDebug(
            "Opening balance calculated - AccountId: {AccountId}, Balance: {Balance}, AsOf: {AsOf}",
            query.AccountId, openingBalance.Amount, query.StartDate.AddSeconds(-1));

        // Get all transactions in the period
        var allTransactions = await _ledgerRepository.GetByAccountIdAsync(
            query.AccountId,
            _tenantId,
            cancellationToken);

        var periodTransactions = allTransactions
            .Where(t => t.CreatedAt >= query.StartDate && t.CreatedAt <= query.EndDate)
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .ToList();

        var totalCount = periodTransactions.Count;

        // Apply pagination (T137: Use AsNoTracking already applied in repository)
        var pagedTransactions = periodTransactions
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(entry => new StatementTransactionDto(
                entry.Id,
                entry.CreatedAt,  // Use CreatedAt instead of TransactionDate
                entry.LedgerAccount.ToString(),
                entry.DebitAmount.Amount,
                entry.CreditAmount.Amount,
                entry.Description ?? string.Empty,
                entry.SourceType.ToString(),
                Guid.TryParse(entry.SourceReferenceId, out var sourceGuid) ? sourceGuid : null))
            .ToList();

        // Calculate closing balance (balance at period end)
        var closingBalance = await _ledgerRepository.GetAccountBalanceAsync(
            accountId,
            query.EndDate,
            cancellationToken);

        _logger.LogInformation(
            "Account statement retrieved - AccountId: {AccountId}, OpeningBalance: {OpeningBalance}, ClosingBalance: {ClosingBalance}, TransactionCount: {Count}, Page: {Page}/{TotalPages}",
            query.AccountId, openingBalance.Amount, closingBalance.Amount, totalCount, query.Page, (int)Math.Ceiling(totalCount / (decimal)query.PageSize));

        return Result.Success(new GetAccountStatementResponse(
            query.AccountId,
            account.Name,
            query.StartDate,
            query.EndDate,
            openingBalance.Amount,
            closingBalance.Amount,
            pagedTransactions,
            totalCount,
            query.Page,
            query.PageSize));
    }
}
