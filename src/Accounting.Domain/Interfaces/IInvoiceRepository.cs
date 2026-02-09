using Accounting.Domain.Aggregates;

namespace Accounting.Domain.Interfaces;

/// <summary>
/// Repository interface for Invoice aggregate
/// </summary>
public interface IInvoiceRepository
{
    /// <summary>
    /// Creates a new invoice with its line items
    /// </summary>
    Task<Invoice> CreateAsync(Invoice invoice, CancellationToken cancellationToken);

    /// <summary>
    /// Gets an invoice by invoice number with tenant filtering
    /// </summary>
    Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists invoices for an account with pagination
    /// </summary>
    Task<(List<Invoice> Invoices, int TotalCount)> ListByAccountAsync(
        Guid accountId,
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists all invoices for a tenant with pagination
    /// </summary>
    Task<(List<Invoice> Invoices, int TotalCount)> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks if an invoice exists for the given invoice number
    /// </summary>
    Task<bool> ExistsByInvoiceNumberAsync(string invoiceNumber, Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets ride IDs that have been invoiced for an account in a billing period
    /// </summary>
    Task<List<Guid>> GetInvoicedRideIdsAsync(
        Guid accountId,
        Guid tenantId,
        DateTime billingPeriodStart,
        DateTime billingPeriodEnd,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the latest invoice for an account
    /// </summary>
    Task<Invoice?> GetLatestByAccountIdAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken cancellationToken);
}
