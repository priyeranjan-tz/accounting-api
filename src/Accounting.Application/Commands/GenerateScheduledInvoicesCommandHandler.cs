using Accounting.Application.Common;
using Accounting.Domain.Enums;
using Accounting.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Accounting.Application.Commands;

/// <summary>
/// Handler for generating invoices for accounts based on their invoice frequency schedule
/// </summary>
public class GenerateScheduledInvoicesCommandHandler
{
    private readonly IAccountRepository _accountRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ILedgerQueryService _ledgerQueryService;
    private readonly IInvoiceNumberGenerator _invoiceNumberGenerator;
    private readonly ILogger<GenerateScheduledInvoicesCommandHandler> _logger;
    private readonly Guid _tenantId;

    public GenerateScheduledInvoicesCommandHandler(
        IAccountRepository accountRepository,
        IInvoiceRepository invoiceRepository,
        ILedgerQueryService ledgerQueryService,
        IInvoiceNumberGenerator invoiceNumberGenerator,
        ILogger<GenerateScheduledInvoicesCommandHandler> logger,
        Guid tenantId)
    {
        _accountRepository = accountRepository;
        _invoiceRepository = invoiceRepository;
        _ledgerQueryService = ledgerQueryService;
        _invoiceNumberGenerator = invoiceNumberGenerator;
        _logger = logger;
        _tenantId = tenantId;
    }

    public async Task<Result<GenerateScheduledInvoicesResponse>> HandleAsync(
        GenerateScheduledInvoicesCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting scheduled invoice generation - Frequency: {Frequency}, GenerationDate: {GenerationDate}, TenantId: {TenantId}",
            command.Frequency, command.GenerationDate, _tenantId);

        int accountsProcessed = 0;
        int invoicesGenerated = 0;
        int failedAccounts = 0;

        try
        {
            // Get all active accounts with the specified invoice frequency
            var accounts = await _accountRepository.GetByInvoiceFrequencyAsync(
                command.Frequency, 
                _tenantId, 
                cancellationToken);

            _logger.LogInformation(
                "Found {Count} accounts with frequency {Frequency} for tenant {TenantId}",
                accounts.Count(), command.Frequency, _tenantId);

            foreach (var account in accounts)
            {
                accountsProcessed++;

                try
                {
                    // Calculate billing period based on frequency
                    var (periodStart, periodEnd) = CalculateBillingPeriod(
                        command.Frequency, 
                        command.GenerationDate);

                    // Get last invoice for this account to determine next billing period
                    var lastInvoice = await _invoiceRepository.GetLatestByAccountIdAsync(
                        account.Id, 
                        _tenantId, 
                        cancellationToken);

                    // Adjust period start if last invoice exists
                    if (lastInvoice != null && lastInvoice.BillingPeriodEnd >= periodStart)
                    {
                        periodStart = lastInvoice.BillingPeriodEnd.AddSeconds(1);
                    }

                    // Check if there are any unbilled charges in this period
                    var unbilledCharges = await _ledgerQueryService.GetUnbilledRideChargesAsync(
                        account.Id,
                        _tenantId,
                        periodStart,
                        periodEnd,
                        new List<Guid>(), // No exclusions for scheduled generation
                        cancellationToken);

                    if (!unbilledCharges.Any())
                    {
                        _logger.LogDebug(
                            "No unbilled charges found for account {AccountId} in period {Start} to {End}",
                            account.Id, periodStart, periodEnd);
                        continue;
                    }

                    // Generate invoice using the existing command handler
                    var loggerFactory = new Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory();
                    var generateInvoiceHandler = new GenerateInvoiceCommandHandler(
                        _invoiceRepository,
                        _accountRepository,
                        _ledgerQueryService,
                        _invoiceNumberGenerator,
                        loggerFactory.CreateLogger<GenerateInvoiceCommandHandler>(),
                        _tenantId,
                        "system-scheduler");

                    var generateResult = await generateInvoiceHandler.HandleAsync(
                        new GenerateInvoiceCommand(account.Id, periodStart, periodEnd),
                        cancellationToken);

                    if (generateResult.IsSuccess)
                    {
                        invoicesGenerated++;
                        _logger.LogInformation(
                            "Invoice generated successfully - AccountId: {AccountId}, InvoiceNumber: {InvoiceNumber}, Period: {Start} to {End}",
                            account.Id, generateResult.Value.InvoiceNumber, periodStart, periodEnd);
                    }
                    else
                    {
                        failedAccounts++;
                        _logger.LogWarning(
                            "Failed to generate invoice for account {AccountId}: {Error}",
                            account.Id, generateResult.Error.Message);
                    }
                }
                catch (Exception ex)
                {
                    failedAccounts++;
                    _logger.LogError(ex,
                        "Error processing account {AccountId} for scheduled invoice generation",
                        account.Id);
                }
            }

            _logger.LogInformation(
                "Scheduled invoice generation completed - Processed: {Processed}, Generated: {Generated}, Failed: {Failed}",
                accountsProcessed, invoicesGenerated, failedAccounts);

            return Result.Success(new GenerateScheduledInvoicesResponse(
                accountsProcessed,
                invoicesGenerated,
                failedAccounts,
                command.GenerationDate));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Scheduled invoice generation failed unexpectedly - Frequency: {Frequency}",
                command.Frequency);
            return Result.Failure<GenerateScheduledInvoicesResponse>(
                Error.Failure(
                    "SCHEDULED_INVOICE_GENERATION_FAILED",
                    "An unexpected error occurred during scheduled invoice generation"));
        }
    }

    /// <summary>
    /// Calculates the billing period based on invoice frequency and generation date
    /// </summary>
    private (DateTime PeriodStart, DateTime PeriodEnd) CalculateBillingPeriod(
        InvoiceFrequency frequency,
        DateTime generationDate)
    {
        return frequency switch
        {
            InvoiceFrequency.Daily => (
                generationDate.Date.AddDays(-1),
                generationDate.Date.AddSeconds(-1)),

            InvoiceFrequency.Weekly => (
                generationDate.Date.AddDays(-7),
                generationDate.Date.AddSeconds(-1)),

            InvoiceFrequency.Monthly => (
                new DateTime(generationDate.Year, generationDate.Month, 1).AddMonths(-1),
                new DateTime(generationDate.Year, generationDate.Month, 1).AddSeconds(-1)),

            _ => throw new ArgumentException($"Unsupported invoice frequency: {frequency}")
        };
    }
}
