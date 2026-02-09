using Accounting.Application.Common;
using Accounting.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Accounting.Application.Queries;

/// <summary>
/// Handler for listing accounts with filtering and pagination
/// </summary>
public class ListAccountsQueryHandler
{
    private readonly IAccountRepository _accountRepository;
    private readonly ILogger<ListAccountsQueryHandler> _logger;
    private readonly Guid _tenantId;

    public ListAccountsQueryHandler(
        IAccountRepository accountRepository,
        ILogger<ListAccountsQueryHandler> logger,
        Guid tenantId)
    {
        _accountRepository = accountRepository;
        _logger = logger;
        _tenantId = tenantId;
    }

    public async Task<Result<ListAccountsResponse>> HandleAsync(
        ListAccountsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Listing accounts - Status: {Status}, Type: {Type}, Page: {Page}, PageSize: {PageSize}, TenantId: {TenantId}",
            query.Status, query.Type, query.Page, query.PageSize, _tenantId);

        try
        {
            var (accounts, totalCount) = await _accountRepository.ListAsync(
                _tenantId,
                query.Status,
                query.Type,
                query.Page,
                query.PageSize,
                cancellationToken);

            var accountDtos = accounts.Select(a => new AccountDto(
                a.Id,
                a.Name,
                a.Type,
                a.Status,
                a.Currency,
                a.CreatedAt,
                a.ModifiedAt
            )).ToList();

            var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

            var response = new ListAccountsResponse(
                accountDtos,
                new PaginationMetadata(
                    query.Page,
                    query.PageSize,
                    totalPages,
                    totalCount));

            _logger.LogInformation(
                "Listed {Count} accounts (Page {Page}/{TotalPages}) - TenantId: {TenantId}",
                accountDtos.Count, query.Page, totalPages, _tenantId);

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to list accounts - TenantId: {TenantId}",
                _tenantId);
            return Result.Failure<ListAccountsResponse>(
                Error.Failure("ACCOUNTS_LIST_FAILED", "An unexpected error occurred while retrieving accounts"));
        }
    }
}
