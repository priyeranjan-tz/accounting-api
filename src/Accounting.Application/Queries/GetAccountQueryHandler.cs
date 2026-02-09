using Accounting.Application.Common;
using Accounting.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Accounting.Application.Queries;

/// <summary>
/// Handler for retrieving a single account by ID
/// </summary>
public class GetAccountQueryHandler
{
    private readonly IAccountRepository _accountRepository;
    private readonly ILogger<GetAccountQueryHandler> _logger;
    private readonly Guid _tenantId;

    public GetAccountQueryHandler(
        IAccountRepository accountRepository,
        ILogger<GetAccountQueryHandler> logger,
        Guid tenantId)
    {
        _accountRepository = accountRepository;
        _logger = logger;
        _tenantId = tenantId;
    }

    public async Task<Result<GetAccountResponse>> HandleAsync(
        GetAccountQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Retrieving account - AccountId: {AccountId}, TenantId: {TenantId}",
            query.AccountId, _tenantId);

        var account = await _accountRepository.GetByIdAsync(query.AccountId, _tenantId, cancellationToken);
        if (account == null)
        {
            _logger.LogWarning(
                "Account not found - AccountId: {AccountId}, TenantId: {TenantId}",
                query.AccountId, _tenantId);
            return Result.Failure<GetAccountResponse>(
                Error.NotFound("ACCOUNT_NOT_FOUND", $"Account with ID '{query.AccountId}' not found"));
        }

        var response = new GetAccountResponse(
            account.Id,
            account.Name,
            account.Type,
            account.Status,
            account.Currency,
            account.CreatedAt,
            account.CreatedBy,
            account.ModifiedAt,
            account.ModifiedBy);

        _logger.LogInformation(
            "Account retrieved successfully - AccountId: {AccountId}, Name: {Name}, TenantId: {TenantId}",
            account.Id, account.Name, _tenantId);

        return Result.Success(response);
    }
}
