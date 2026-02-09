using Accounting.Application.Common;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Accounting.Domain.Interfaces;
using Accounting.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Accounting.Application.Commands;

/// <summary>
/// Handler for recording ride charges using double-entry accounting.
/// Creates two ledger entries: Debit AccountsReceivable, Credit ServiceRevenue.
/// 
/// Double-Entry Logic:
/// DR Accounts Receivable $X (increases amount owed by customer)
/// CR Service Revenue      $X (recognizes earned revenue)
/// </summary>
public class RecordRideChargeCommandHandler
{
    private readonly ILedgerRepository _ledgerRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ILedgerQueryService _ledgerQueryService;
    private readonly IInvoiceNumberGenerator _invoiceNumberGenerator;
    private readonly ILogger<RecordRideChargeCommandHandler> _logger;
    private readonly Guid _tenantId; // Will be injected from HTTP context via middleware
    private readonly string _currentUser;

    public RecordRideChargeCommandHandler(
        ILedgerRepository ledgerRepository,
        IAccountRepository accountRepository,
        IInvoiceRepository invoiceRepository,
        ILedgerQueryService ledgerQueryService,
        IInvoiceNumberGenerator invoiceNumberGenerator,
        ILogger<RecordRideChargeCommandHandler> logger,
        Guid tenantId,
        string currentUser = "system")
    {
        _ledgerRepository = ledgerRepository;
        _accountRepository = accountRepository;
        _invoiceRepository = invoiceRepository;
        _ledgerQueryService = ledgerQueryService;
        _invoiceNumberGenerator = invoiceNumberGenerator;
        _logger = logger;
        _tenantId = tenantId;
        _currentUser = currentUser;
    }

    public async Task<Result<RecordRideChargeResult>> HandleAsync(
        RecordRideChargeCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Recording ride charge - RideId: {RideId}, AccountId: {AccountId}, FareAmount: {FareAmount}, TenantId: {TenantId}",
            command.RideId, command.AccountId, command.FareAmount, _tenantId);

        // Validate command
        if (command.FareAmount <= 0)
        {
            _logger.LogWarning(
                "Invalid fare amount - RideId: {RideId}, FareAmount: {FareAmount}",
                command.RideId, command.FareAmount);
            return Result.Failure<RecordRideChargeResult>(
                Error.Validation("FareAmount", "Fare amount must be greater than zero"));
        }

        // Validate RideId looks like a unique identifier
        if (command.RideId.Length < 10 || command.RideId.Contains(" "))
        {
            _logger.LogWarning(
                "Invalid RideId format - RideId: {RideId}. Must be a unique identifier, not a description.",
                command.RideId);
            return Result.Failure<RecordRideChargeResult>(
                Error.Validation("RideId", 
                    $"RideId '{command.RideId}' appears to be a description. " +
                    "RideId must be a unique identifier (e.g., UUID or sequential ID), not descriptive text. " +
                    "Use the Description field for human-readable text like 'home' or 'office trip'."));
        }

        // T084: Validate account exists and is active
        var account = await _accountRepository.GetByIdAsync(command.AccountId, _tenantId, cancellationToken);
        if (account == null)
        {
            _logger.LogWarning(
                "Account not found - AccountId: {AccountId}, TenantId: {TenantId}",
                command.AccountId, _tenantId);
            return Result.Failure<RecordRideChargeResult>(
                Error.NotFound("ACCOUNT_NOT_FOUND", $"Account with ID '{command.AccountId}' not found"));
        }

        if (!account.CanReceiveTransactions())
        {
            _logger.LogWarning(
                "Cannot post transaction to inactive account - AccountId: {AccountId}, Status: {Status}, TenantId: {TenantId}",
                command.AccountId, account.Status, _tenantId);
            return Result.Failure<RecordRideChargeResult>(
                Error.Validation("INACTIVE_ACCOUNT", "Cannot post transactions to inactive accounts"));
        }

        var accountId = new AccountId(command.AccountId);
        var rideId = new RideId(command.RideId);
        var amount = new Money(command.FareAmount);

        // CRITICAL: Idempotency check - prevent duplicate charges for same ride to same account
        if (await _ledgerRepository.RideAlreadyChargedAsync(accountId, rideId, cancellationToken))
        {
            _logger.LogWarning(
                "Duplicate ride charge detected - RideId: {RideId}, AccountId: {AccountId}, TenantId: {TenantId}",
                command.RideId, command.AccountId, _tenantId);
            return Result.Failure<RecordRideChargeResult>(
                Error.Conflict(
                    "DuplicateRideCharge",
                    $"Ride '{command.RideId}' has already been charged to account '{command.AccountId}'"));
        }

        // Create double-entry ledger entries
        var debitEntry = LedgerEntry.Debit(
            accountId: accountId,
            ledgerAccount: LedgerAccount.AccountsReceivable,
            amount: amount,
            sourceType: TransactionType.RideCharge,
            sourceReferenceId: command.RideId,
            tenantId: _tenantId,
            createdBy: _currentUser,
            description: command.Description ?? $"Ride charge for {command.RideId}");

        var creditEntry = LedgerEntry.Credit(
            accountId: accountId,
            ledgerAccount: LedgerAccount.ServiceRevenue,
            amount: amount,
            sourceType: TransactionType.RideCharge,
            sourceReferenceId: command.RideId,
            tenantId: _tenantId,
            createdBy: "system",
            description: command.Description ?? $"Revenue from ride {command.RideId}");

        // Append to ledger
        Guid transactionId;
        try
        {
            transactionId = await _ledgerRepository.AppendEntriesAsync(
                new[] { debitEntry, creditEntry },
                cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Duplicate transaction") || 
                                                     ex.Message.Contains("already been posted"))
        {
            // Caught either the app-level check or database constraint violation
            var innerMessage = ex.InnerException?.Message ?? "No inner exception";
            _logger.LogWarning(ex,
                "Duplicate ride charge detected - RideId: {RideId}, AccountId: {AccountId}, TenantId: {TenantId}, InnerException: {InnerException}",
                command.RideId, command.AccountId, _tenantId, innerMessage);
            return Result.Failure<RecordRideChargeResult>(
                Error.Conflict(
                    "DUPLICATE_RIDE_CHARGE",
                    $"Ride ID '{command.RideId}' has already been charged to this account. " +
                    $"Each ride must have a unique RideId. If this is a new ride, generate a new UUID."));
        }
        catch (Exception ex)
        {
            // Catch any other unexpected errors during ledger append
            var innerMessage = ex.InnerException?.Message ?? "No inner exception";
            var innerType = ex.InnerException?.GetType().Name ?? "None";
            _logger.LogError(ex,
                "Failed to append ledger entries - RideId: {RideId}, AccountId: {AccountId}, Error: {Error}, InnerException: {InnerException} ({InnerType})",
                command.RideId, command.AccountId, ex.Message, innerMessage, innerType);
            throw;
        }

        _logger.LogInformation(
            "Ledger entries created - TransactionId: {TransactionId}, RideId: {RideId}, AccountId: {AccountId}, Amount: {Amount}",
            transactionId, command.RideId, command.AccountId, command.FareAmount);

        // T128: Check if account uses per-ride invoicing and generate invoice immediately
        if (account.InvoiceFrequency == InvoiceFrequency.PerRide)
        {
            _logger.LogInformation(
                "Account uses per-ride invoicing - generating invoice immediately - AccountId: {AccountId}, RideId: {RideId}",
                command.AccountId, command.RideId);

            var loggerFactory = new Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory();
            var generateInvoiceHandler = new GenerateInvoiceCommandHandler(
                _invoiceRepository,
                _accountRepository,
                _ledgerQueryService,
                _invoiceNumberGenerator,
                loggerFactory.CreateLogger<GenerateInvoiceCommandHandler>(),
                _tenantId,
                _currentUser);

            var rideDate = DateTime.UtcNow;
            var generateResult = await generateInvoiceHandler.HandleAsync(
                new GenerateInvoiceCommand(
                    command.AccountId,
                    rideDate.Date, // Start of day
                    rideDate), // Current time
                cancellationToken);

            if (generateResult.IsSuccess)
            {
                _logger.LogInformation(
                    "Per-ride invoice generated - InvoiceNumber: {InvoiceNumber}, RideId: {RideId}, AccountId: {AccountId}",
                    generateResult.Value.InvoiceNumber, command.RideId, command.AccountId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to generate per-ride invoice - RideId: {RideId}, AccountId: {AccountId}, Error: {Error}",
                    command.RideId, command.AccountId, generateResult.Error.Message);
            }
        }

        // Calculate new balance
        var newBalance = await _ledgerRepository.GetAccountBalanceAsync(
            accountId,
            cancellationToken);

        // Build result
        var result = new RecordRideChargeResult
        {
            TransactionId = transactionId,
            AccountId = command.AccountId,
            Entries = new List<LedgerEntryDto>
            {
                new LedgerEntryDto
                {
                    Id = debitEntry.Id,
                    LedgerAccount = debitEntry.LedgerAccount.ToString(),
                    DebitAmount = debitEntry.DebitAmount.Amount,
                    CreditAmount = debitEntry.CreditAmount.Amount
                },
                new LedgerEntryDto
                {
                    Id = creditEntry.Id,
                    LedgerAccount = creditEntry.LedgerAccount.ToString(),
                    DebitAmount = creditEntry.DebitAmount.Amount,
                    CreditAmount = creditEntry.CreditAmount.Amount
                }
            },
            NewBalance = newBalance.Amount,
            CreatedAt = DateTime.UtcNow
        };

        _logger.LogInformation(
            "Ride charge recorded successfully - TransactionId: {TransactionId}, RideId: {RideId}, AccountId: {AccountId}, NewBalance: {NewBalance}, TenantId: {TenantId}",
            transactionId, command.RideId, command.AccountId, newBalance.Amount, _tenantId);

        return Result<RecordRideChargeResult>.Success(result);
    }
}
