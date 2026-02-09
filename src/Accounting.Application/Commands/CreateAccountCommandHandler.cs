using Accounting.Application.Common;
using Accounting.Domain.Aggregates;
using Accounting.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Accounting.Application.Commands;

/// <summary>
/// Handler for creating new customer accounts
/// </summary>
public class CreateAccountCommandHandler
{
    private readonly IAccountRepository _accountRepository;
    private readonly ILogger<CreateAccountCommandHandler> _logger;
    private readonly Guid _tenantId;
    private readonly string _currentUser;

    public CreateAccountCommandHandler(
        IAccountRepository accountRepository,
        ILogger<CreateAccountCommandHandler> logger,
        Guid tenantId,
        string currentUser)
    {
        _accountRepository = accountRepository;
        _logger = logger;
        _tenantId = tenantId;
        _currentUser = currentUser;
    }

    public async Task<Result<CreateAccountResponse>> HandleAsync(
        CreateAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating account - Name: {Name}, Type: {Type}, Status: {Status}, TenantId: {TenantId}",
            command.Name, command.Type, command.Status, _tenantId);

        // Check if account with this name already exists
        var exists = await _accountRepository.ExistsByNameAsync(command.Name, _tenantId, cancellationToken);
        if (exists)
        {
            _logger.LogWarning(
                "Account creation failed - duplicate name: {Name}, TenantId: {TenantId}",
                command.Name, _tenantId);
            return Result.Failure<CreateAccountResponse>(
                Error.Conflict("ACCOUNT_ALREADY_EXISTS", $"An account with name '{command.Name}' already exists for this tenant"));
        }

        try
        {
            // Create account aggregate
            var account = Account.Create(
                Guid.NewGuid(),
                command.Name,
                command.Type,
                _tenantId,
                _currentUser,
                command.InvoiceFrequency);

            // Apply status if not active
            if (command.Status == Domain.Enums.AccountStatus.Inactive)
            {
                account.Deactivate(_currentUser);
            }

            // Persist to repository
            var createdAccount = await _accountRepository.CreateAsync(account, cancellationToken);

            _logger.LogInformation(
                "Account created successfully - AccountId: {AccountId}, Name: {Name}, TenantId: {TenantId}",
                createdAccount.Id, createdAccount.Name, _tenantId);

            return Result.Success(new CreateAccountResponse(
                createdAccount.Id,
                createdAccount.Name,
                createdAccount.Type,
                createdAccount.Status,
                createdAccount.Currency,
                createdAccount.CreatedAt));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex,
                "Account creation failed - validation error: {Message}",
                ex.Message);
            return Result.Failure<CreateAccountResponse>(
                Error.Validation("INVALID_ACCOUNT_DATA", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Account creation failed - unexpected error for Name: {Name}, TenantId: {TenantId}",
                command.Name, _tenantId);
            return Result.Failure<CreateAccountResponse>(
                Error.Failure("ACCOUNT_CREATION_FAILED", "An unexpected error occurred while creating the account"));
        }
    }
}
