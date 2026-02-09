namespace Accounting.Infrastructure.Persistence.Entities;

/// <summary>
/// Persistence entity for invoices
/// </summary>
public class InvoiceEntity
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime BillingPeriodStart { get; set; }
    public DateTime BillingPeriodEnd { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;

    // Navigation property
    public List<InvoiceLineItemEntity> LineItems { get; set; } = new();
}

/// <summary>
/// Persistence entity for invoice line items
/// </summary>
public class InvoiceLineItemEntity
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid RideId { get; set; }
    public DateTime RideDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    // Navigation property
    public InvoiceEntity Invoice { get; set; } = null!;
}
