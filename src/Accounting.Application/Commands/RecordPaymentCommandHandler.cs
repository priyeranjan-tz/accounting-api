using Accounting.Application.Common;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Accounting.Domain.Interfaces;
using Accounting.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Accounting.Application.Commands;

/// <summary>
/// Handler for recording payments using double-entry accounting.
/// Creates two ledger entries: Debit Cash, Credit AccountsReceivable.
/// Supports partial, full, and overpayments.
/// 
/// Double-Entry Logic:
/// DR Cash                  $X (increases cash received)
/// CR Accounts Receivable   $X (reduces amount owed by customer)
/// </summary>
public class RecordPaymentCommandHandler
{
    private readonly ILedgerRepository _ledgerRepository;
    private readonly ILogger<RecordPaymentCommandHandler> _logger;
    private readonly Guid _tenantId; // Will be injected from HTTP context via middleware
    private readonly string _currentUser;

    public RecordPaymentCommandHandler(
        ILedgerRepository ledgerRepository,
        ILogger<RecordPaymentCommandHandler> logger,
        Guid tenantId,
        string currentUser = "system")
    {
        _ledgerRepository = ledgerRepository;
        _logger = logger;
        _tenantId = tenantId;
        _currentUser = currentUser;
    }

    public async Task<Result<RecordPaymentResult>> HandleAsync(
        RecordPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Recording payment - PaymentReferenceId: {PaymentReferenceId}, AccountId: {AccountId}, Amount: {Amount}, PaymentMode: {PaymentMode}, TenantId: {TenantId}",
            command.PaymentReferenceId, command.AccountId, command.Amount, command.PaymentMode ?? "unknown", _tenantId);

        // Validate command
        if (command.Amount <= 0)
        {
            _logger.LogWarning(
                "Invalid payment amount - PaymentReferenceId: {PaymentReferenceId}, Amount: {Amount}",
                command.PaymentReferenceId, command.Amount);
            return Result.Failure<RecordPaymentResult>(
                Error.Validation("Amount", "Payment amount must be greater than zero"));
        }

        var accountId = new AccountId(command.AccountId);
        var amount = new Money(command.Amount);

        // CRITICAL: Idempotency check - prevent duplicate payment recording
        if (await _ledgerRepository.PaymentAlreadyRecordedAsync(command.PaymentReferenceId, cancellationToken))
        {
            _logger.LogWarning(
                "Duplicate payment detected - PaymentReferenceId: {PaymentReferenceId}, AccountId: {AccountId}, TenantId: {TenantId}",
                command.PaymentReferenceId, command.AccountId, _tenantId);
            return Result.Failure<RecordPaymentResult>(
                Error.Conflict(
                    "DuplicatePayment",
                    $"Payment '{command.PaymentReferenceId}' has already been recorded for account '{command.AccountId}'"));
        }

        // Create double-entry ledger entries
        var debitEntry = LedgerEntry.Debit(
            accountId: accountId,
            ledgerAccount: LedgerAccount.Cash,
            amount: amount,
            sourceType: TransactionType.Payment,
            sourceReferenceId: command.PaymentReferenceId,
            tenantId: _tenantId,
            createdBy: _currentUser,
            description: command.Description ?? $"Payment {command.PaymentReferenceId} via {command.PaymentMode ?? "unknown method"}");

        var creditEntry = LedgerEntry.Credit(
            accountId: accountId,
            ledgerAccount: LedgerAccount.AccountsReceivable,
            amount: amount,
            sourceType: TransactionType.Payment,
            sourceReferenceId: command.PaymentReferenceId,
            tenantId: _tenantId,
            createdBy: "system",
            description: command.Description ?? $"Payment applied: {command.PaymentReferenceId}");

        // Append to ledger
        var transactionId = await _ledgerRepository.AppendEntriesAsync(
            new[] { debitEntry, creditEntry },
            cancellationToken);

        _logger.LogInformation(
            "Ledger entries created - TransactionId: {TransactionId}, PaymentReferenceId: {PaymentReferenceId}, AccountId: {AccountId}, Amount: {Amount}",
            transactionId, command.PaymentReferenceId, command.AccountId, command.Amount);

        // Calculate new balance
        var newBalance = await _ledgerRepository.GetAccountBalanceAsync(
            accountId,
            cancellationToken);

        // Build result
        var result = new RecordPaymentResult
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
            "Payment recorded successfully - TransactionId: {TransactionId}, PaymentReferenceId: {PaymentReferenceId}, AccountId: {AccountId}, NewBalance: {NewBalance}, TenantId: {TenantId}",
            transactionId, command.PaymentReferenceId, command.AccountId, newBalance.Amount, _tenantId);

        return Result<RecordPaymentResult>.Success(result);
    }
}
