using Accounting.Application.Common;
using Accounting.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Accounting.Application.Queries;

/// <summary>
/// Handler for listing invoices with optional account filtering and pagination
/// </summary>
public class ListInvoicesQueryHandler
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ILogger<ListInvoicesQueryHandler> _logger;
    private readonly Guid _tenantId;

    public ListInvoicesQueryHandler(
        IInvoiceRepository invoiceRepository,
        ILogger<ListInvoicesQueryHandler> logger,
        Guid tenantId)
    {
        _invoiceRepository = invoiceRepository;
        _logger = logger;
        _tenantId = tenantId;
    }

    public async Task<Result<ListInvoicesResponse>> HandleAsync(
        ListInvoicesQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Listing invoices - AccountId: {AccountId}, Page: {Page}, PageSize: {PageSize}, TenantId: {TenantId}",
            query.AccountId, query.Page, query.PageSize, _tenantId);

        var (invoices, totalCount) = query.AccountId.HasValue
            ? await _invoiceRepository.ListByAccountAsync(query.AccountId.Value, _tenantId, query.Page, query.PageSize, cancellationToken)
            : await _invoiceRepository.ListAsync(_tenantId, query.Page, query.PageSize, cancellationToken);

        var invoiceDtos = invoices.Select(inv => new InvoiceSummaryDto(
            inv.Id,
            inv.InvoiceNumber,
            inv.AccountId,
            inv.BillingPeriodStart,
            inv.BillingPeriodEnd,
            inv.IssueDate,
            inv.DueDate,
            inv.TotalAmount.Amount,
            inv.Currency,
            inv.LineItems.Count,
            inv.CreatedAt)).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var pagination = new PaginationMetadata(query.Page, query.PageSize, totalPages, totalCount);
        var response = new ListInvoicesResponse(invoiceDtos, pagination);

        _logger.LogInformation(
            "Listed {Count} invoices (Page {Page}/{TotalPages}) - TenantId: {TenantId}",
            invoiceDtos.Count, query.Page, totalPages, _tenantId);

        return Result.Success(response);
    }
}
