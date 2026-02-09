using Accounting.Domain.Interfaces;
using Accounting.Infrastructure.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Infrastructure.Services;

/// <summary>
/// Implementation of invoice number generator service
/// Generates unique invoice numbers in format: INV-YYYYMM-NNNNNN
/// </summary>
public class InvoiceNumberGenerator : IInvoiceNumberGenerator
{
    private readonly AccountingDbContext _dbContext;

    public InvoiceNumberGenerator(AccountingDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<string> GenerateAsync(DateTime billingPeriodStart, Guid tenantId, CancellationToken cancellationToken)
    {
        var prefix = $"INV-{billingPeriodStart:yyyyMM}";

        // Find the next sequence number for this month
        var existingInvoices = await _dbContext.Invoices
            .Where(i => i.TenantId == tenantId)
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .Select(i => i.InvoiceNumber)
            .ToListAsync(cancellationToken);

        var maxSequence = existingInvoices
            .Select(num =>
            {
                var parts = num.Split('-');
                return parts.Length == 3 && int.TryParse(parts[2], out var seq) ? seq : 0;
            })
            .DefaultIfEmpty(0)
            .Max();

        var nextSequence = maxSequence + 1;
        return $"{prefix}-{nextSequence:D6}";
    }
}
