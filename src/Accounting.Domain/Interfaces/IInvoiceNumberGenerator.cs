namespace Accounting.Domain.Interfaces;

/// <summary>
/// Service interface for generating invoice numbers
/// </summary>
public interface IInvoiceNumberGenerator
{
    /// <summary>
    /// Generates a unique invoice number for a billing period
    /// Format: INV-YYYYMM-NNNNNN
    /// </summary>
    Task<string> GenerateAsync(DateTime billingPeriodStart, Guid tenantId, CancellationToken cancellationToken);
}
