using Accounting.Application.Common;
using Accounting.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Accounting.Application.Queries;

/// <summary>
/// Handler for retrieving an invoice by invoice number
/// </summary>
public class GetInvoiceQueryHandler
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ILogger<GetInvoiceQueryHandler> _logger;
    private readonly Guid _tenantId;

    public GetInvoiceQueryHandler(
        IInvoiceRepository invoiceRepository,
        ILogger<GetInvoiceQueryHandler> logger,
        Guid tenantId)
    {
        _invoiceRepository = invoiceRepository;
        _logger = logger;
        _tenantId = tenantId;
    }

    public async Task<Result<GetInvoiceResponse>> HandleAsync(
        GetInvoiceQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Retrieving invoice - InvoiceNumber: {InvoiceNumber}, TenantId: {TenantId}",
            query.InvoiceNumber, _tenantId);

        var invoice = await _invoiceRepository.GetByInvoiceNumberAsync(query.InvoiceNumber, _tenantId, cancellationToken);
        if (invoice == null)
        {
            _logger.LogWarning(
                "Invoice not found - InvoiceNumber: {InvoiceNumber}, TenantId: {TenantId}",
                query.InvoiceNumber, _tenantId);
            return Result.Failure<GetInvoiceResponse>(
                Error.NotFound("INVOICE_NOT_FOUND", $"Invoice with number '{query.InvoiceNumber}' not found"));
        }

        var lineItems = invoice.LineItems.Select(li => new InvoiceLineItemDto(
            li.Id,
            li.RideId,
            li.RideDate,
            li.Description,
            li.Amount.Amount)).ToList();

        var response = new GetInvoiceResponse(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.AccountId,
            invoice.BillingPeriodStart,
            invoice.BillingPeriodEnd,
            invoice.IssueDate,
            invoice.DueDate,
            invoice.TotalAmount.Amount,
            invoice.Currency,
            invoice.CreatedAt,
            invoice.CreatedBy,
            lineItems);

        _logger.LogInformation(
            "Invoice retrieved successfully - InvoiceId: {InvoiceId}, InvoiceNumber: {InvoiceNumber}, LineItems: {LineItemCount}, TenantId: {TenantId}",
            invoice.Id, invoice.InvoiceNumber, invoice.LineItems.Count, _tenantId);

        return Result.Success(response);
    }
}
