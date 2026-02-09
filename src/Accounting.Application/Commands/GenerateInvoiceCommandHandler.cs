using Accounting.Application.Common;
using Accounting.Domain.Aggregates;
using Accounting.Domain.Interfaces;
using Accounting.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Accounting.Application.Commands;

/// <summary>
/// Handler for generating invoices from unbilled ride charges
/// </summary>
public class GenerateInvoiceCommandHandler
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ILedgerQueryService _ledgerQueryService;
    private readonly IInvoiceNumberGenerator _invoiceNumberGenerator;
    private readonly ILogger<GenerateInvoiceCommandHandler> _logger;
    private readonly Guid _tenantId;
    private readonly string _currentUser;

    private static readonly Meter Meter = new("Accounting.API", "1.0.0");
    private static readonly Histogram<double> InvoiceGenerationDuration = Meter.CreateHistogram<double>(
        "invoice_generation_duration_ms",
        "ms",
        "Duration of invoice generation operations in milliseconds");

    public GenerateInvoiceCommandHandler(
        IInvoiceRepository invoiceRepository,
        IAccountRepository accountRepository,
        ILedgerQueryService ledgerQueryService,
        IInvoiceNumberGenerator invoiceNumberGenerator,
        ILogger<GenerateInvoiceCommandHandler> logger,
        Guid tenantId,
        string currentUser)
    {
        _invoiceRepository = invoiceRepository;
        _accountRepository = accountRepository;
        _ledgerQueryService = ledgerQueryService;
        _invoiceNumberGenerator = invoiceNumberGenerator;
        _logger = logger;
        _tenantId = tenantId;
        _currentUser = currentUser;
    }

    public async Task<Result<GenerateInvoiceResponse>> HandleAsync(
        GenerateInvoiceCommand command,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation(
                "Generating invoice - AccountId: {AccountId}, BillingPeriod: {Start} to {End}, TenantId: {TenantId}",
                command.AccountId, command.BillingPeriodStart, command.BillingPeriodEnd, _tenantId);

            // Validate account exists and is active
            var account = await _accountRepository.GetByIdAsync(command.AccountId, _tenantId, cancellationToken);
            if (account == null)
            {
                return Result.Failure<GenerateInvoiceResponse>(
                    Error.NotFound("ACCOUNT_NOT_FOUND", $"Account with ID '{command.AccountId}' not found"));
            }

            if (!account.CanReceiveTransactions())
            {
                return Result.Failure<GenerateInvoiceResponse>(
                    Error.Validation("ACCOUNT_INACTIVE", "Cannot generate invoice for inactive account"));
            }

            // Get already invoiced ride IDs to exclude them
            var invoicedRideIds = await _invoiceRepository.GetInvoicedRideIdsAsync(
                command.AccountId,
                _tenantId,
                command.BillingPeriodStart,
                command.BillingPeriodEnd,
                cancellationToken);

            // Get unbilled ride charges from ledger
            var unbilledCharges = await _ledgerQueryService.GetUnbilledRideChargesAsync(
                command.AccountId,
                _tenantId,
                command.BillingPeriodStart,
                command.BillingPeriodEnd,
                invoicedRideIds,
                cancellationToken);

            if (unbilledCharges.Count == 0)
            {
                return Result.Failure<GenerateInvoiceResponse>(
                    Error.Validation("NO_UNBILLED_CHARGES", "No unbilled ride charges found for the specified billing period"));
            }

            // Generate invoice number
            var invoiceNumber = await _invoiceNumberGenerator.GenerateAsync(command.BillingPeriodStart, _tenantId, cancellationToken);

            // Create invoice aggregate
            var issueDate = command.IssueDate ?? DateTime.UtcNow;
            var paymentTermsDays = command.PaymentTermsDays ?? 30;
            var dueDate = issueDate.AddDays(paymentTermsDays);

            var invoice = Invoice.Create(
                Guid.NewGuid(),
                invoiceNumber,
                command.AccountId,
                _tenantId,
                command.BillingPeriodStart,
                command.BillingPeriodEnd,
                issueDate,
                dueDate,
                _currentUser);

            // Add line items
            foreach (var charge in unbilledCharges)
            {
                invoice.AddLineItem(
                    charge.RideId,
                    charge.RideDate,
                    charge.Description,
                    new Money(charge.Amount));
            }

            // Persist invoice
            await _invoiceRepository.CreateAsync(invoice, cancellationToken);

            _logger.LogInformation(
                "Invoice generated successfully - InvoiceId: {InvoiceId}, InvoiceNumber: {InvoiceNumber}, AccountId: {AccountId}, TotalAmount: {TotalAmount}, LineItems: {LineItemCount}, TenantId: {TenantId}",
                invoice.Id, invoice.InvoiceNumber, invoice.AccountId, invoice.TotalAmount.Amount, invoice.LineItems.Count, _tenantId);

            return Result.Success(new GenerateInvoiceResponse(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.AccountId,
                invoice.BillingPeriodStart,
                invoice.BillingPeriodEnd,
                invoice.IssueDate,
                invoice.DueDate,
                invoice.TotalAmount.Amount,
                invoice.Currency,
                invoice.LineItems.Count,
                invoice.CreatedAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating invoice - AccountId: {AccountId}, TenantId: {TenantId}", command.AccountId, _tenantId);
            return Result.Failure<GenerateInvoiceResponse>(
                Error.Failure("INVOICE_GENERATION_FAILED", $"Failed to generate invoice: {ex.Message}"));
        }
        finally
        {
            stopwatch.Stop();
            InvoiceGenerationDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("account_id", command.AccountId.ToString()),
                new KeyValuePair<string, object?>("tenant_id", _tenantId.ToString()));
        }
    }
}
