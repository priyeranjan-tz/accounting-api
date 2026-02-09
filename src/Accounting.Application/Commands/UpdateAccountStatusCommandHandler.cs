using Accounting.Application.Common;
using Accounting.Domain.Enums;
using Accounting.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Accounting.Application.Commands;

/// <summary>
/// Handler for updating account status (activate/deactivate)
/// </summary>
public class UpdateAccountStatusCommandHandler
{
    private readonly IAccountRepository _accountRepository;
    private readonly ILogger<UpdateAccountStatusCommandHandler> _logger;
    private readonly Guid _tenantId;
    private readonly string _currentUser;

    public UpdateAccountStatusCommandHandler(
        IAccountRepository accountRepository,
        ILogger<UpdateAccountStatusCommandHandler> logger,
        Guid tenantId,
        string currentUser)
    {
        _accountRepository = accountRepository;
        _logger = logger;
        _tenantId = tenantId;
        _currentUser = currentUser;
    }

    public async Task<Result<UpdateAccountStatusResponse>> HandleAsync(
        UpdateAccountStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating account status - AccountId: {AccountId}, NewStatus: {Status}, TenantId: {TenantId}",
            command.AccountId, command.Status, _tenantId);

        // Retrieve account with tenant filtering
        var account = await _accountRepository.GetByIdAsync(command.AccountId, _tenantId, cancellationToken);
        if (account == null)
        {
            _logger.LogWarning(
                "Account not found - AccountId: {AccountId}, TenantId: {TenantId}",
                command.AccountId, _tenantId);
            return Result.Failure<UpdateAccountStatusResponse>(
                Error.NotFound("ACCOUNT_NOT_FOUND", $"Account with ID '{command.AccountId}' not found"));
        }

        try
        {
            // Apply status change
            if (command.Status == AccountStatus.Active)
            {
                account.Activate(_currentUser);
            }
            else
            {
                account.Deactivate(_currentUser);
            }

            // Persist changes
            await _accountRepository.UpdateAsync(account, cancellationToken);

            _logger.LogInformation(
                "Account status updated successfully - AccountId: {AccountId}, NewStatus: {Status}, TenantId: {TenantId}",
                account.Id, account.Status, _tenantId);

            return Result.Success(new UpdateAccountStatusResponse(
                account.Id,
                account.Name,
                account.Type,
                account.Status,
                account.ModifiedAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Account status update failed - AccountId: {AccountId}, TenantId: {TenantId}",
                command.AccountId, _tenantId);
            return Result.Failure<UpdateAccountStatusResponse>(
                Error.Failure("STATUS_UPDATE_FAILED", "An unexpected error occurred while updating account status"));
        }
    }
}
