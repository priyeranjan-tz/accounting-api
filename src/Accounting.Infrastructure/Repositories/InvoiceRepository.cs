using Accounting.Domain.Aggregates;
using Accounting.Domain.Interfaces;
using Accounting.Infrastructure.Persistence.DbContext;
using Accounting.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for invoice operations using Entity Framework Core
/// </summary>
public class InvoiceRepository : IInvoiceRepository
{
    private readonly AccountingDbContext _dbContext;

    public InvoiceRepository(AccountingDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<Invoice> CreateAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        if (invoice == null)
            throw new ArgumentNullException(nameof(invoice));

        var entity = InvoiceMapper.MapToPersistence(invoice);
        await _dbContext.Invoices.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return invoice;
    }

    public async Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("Invoice number cannot be empty", nameof(invoiceNumber));

        var entity = await _dbContext.Invoices
            .Include(i => i.LineItems)
            .Where(i => i.TenantId == tenantId)
            .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber, cancellationToken);

        return entity == null ? null : InvoiceMapper.MapToDomain(entity);
    }

    public async Task<(List<Invoice> Invoices, int TotalCount)> ListByAccountAsync(
        Guid accountId,
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Invoices
            .Include(i => i.LineItems)
            .Where(i => i.TenantId == tenantId)
            .Where(i => i.AccountId == accountId);

        var totalCount = await query.CountAsync(cancellationToken);

        var entities = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var invoices = entities.Select(InvoiceMapper.MapToDomain).ToList();

        return (invoices, totalCount);
    }

    public async Task<(List<Invoice> Invoices, int TotalCount)> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Invoices
            .Include(i => i.LineItems)
            .Where(i => i.TenantId == tenantId);

        var totalCount = await query.CountAsync(cancellationToken);

        var entities = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var invoices = entities.Select(InvoiceMapper.MapToDomain).ToList();

        return (invoices, totalCount);
    }

    public async Task<bool> ExistsByInvoiceNumberAsync(string invoiceNumber, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("Invoice number cannot be empty", nameof(invoiceNumber));

        return await _dbContext.Invoices
            .Where(i => i.TenantId == tenantId)
            .AnyAsync(i => i.InvoiceNumber == invoiceNumber, cancellationToken);
    }

    public async Task<List<Guid>> GetInvoicedRideIdsAsync(
        Guid accountId,
        Guid tenantId,
        DateTime billingPeriodStart,
        DateTime billingPeriodEnd,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.InvoiceLineItems
            .Where(li => li.Invoice.TenantId == tenantId)
            .Where(li => li.Invoice.AccountId == accountId)
            .Where(li => li.RideDate >= billingPeriodStart && li.RideDate < billingPeriodEnd)
            .Select(li => li.RideId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<Invoice?> GetLatestByAccountIdAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Invoices
            .Include(i => i.LineItems)
            .Where(i => i.TenantId == tenantId)
            .Where(i => i.AccountId == accountId)
            .OrderByDescending(i => i.BillingPeriodEnd)
            .FirstOrDefaultAsync(cancellationToken);

        return entity == null ? null : InvoiceMapper.MapToDomain(entity);
    }
}
