using Accounting.Domain.ValueObjects;

namespace Accounting.Domain.Aggregates;

/// <summary>
/// Invoice aggregate root representing a billing document for ride charges
/// </summary>
public sealed class Invoice
{
    private readonly List<InvoiceLineItem> _lineItems = new();

    public Guid Id { get; private set; }
    public string InvoiceNumber { get; private set; } = string.Empty;
    public Guid AccountId { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTime BillingPeriodStart { get; private set; }
    public DateTime BillingPeriodEnd { get; private set; }
    public DateTime IssueDate { get; private set; }
    public DateTime DueDate { get; private set; }
    public Money TotalAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public IReadOnlyList<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    private Invoice() { } // Required for EF Core

    private Invoice(
        Guid id,
        string invoiceNumber,
        Guid accountId,
        Guid tenantId,
        DateTime billingPeriodStart,
        DateTime billingPeriodEnd,
        DateTime issueDate,
        DateTime dueDate,
        string createdBy)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("Invoice number cannot be empty", nameof(invoiceNumber));

        if (billingPeriodEnd <= billingPeriodStart)
            throw new ArgumentException("Billing period end must be after billing period start", nameof(billingPeriodEnd));

        if (dueDate < issueDate)
            throw new ArgumentException("Due date cannot be before issue date", nameof(dueDate));

        Id = id;
        InvoiceNumber = invoiceNumber;
        AccountId = accountId;
        TenantId = tenantId;
        BillingPeriodStart = billingPeriodStart;
        BillingPeriodEnd = billingPeriodEnd;
        IssueDate = issueDate;
        DueDate = dueDate;
        Currency = "USD"; // Fixed to USD per requirements
        TotalAmount = new Money(0);
        CreatedAt = DateTime.UtcNow;
        CreatedBy = createdBy ?? throw new ArgumentNullException(nameof(createdBy));
    }

    /// <summary>
    /// Creates a new invoice for an account and billing period
    /// </summary>
    public static Invoice Create(
        Guid id,
        string invoiceNumber,
        Guid accountId,
        Guid tenantId,
        DateTime billingPeriodStart,
        DateTime billingPeriodEnd,
        DateTime issueDate,
        DateTime dueDate,
        string createdBy)
    {
        return new Invoice(
            id,
            invoiceNumber,
            accountId,
            tenantId,
            billingPeriodStart,
            billingPeriodEnd,
            issueDate,
            dueDate,
            createdBy);
    }

    /// <summary>
    /// Adds a line item to the invoice and recalculates total
    /// </summary>
    public void AddLineItem(
        Guid rideId,
        DateTime rideDate,
        string description,
        Money amount)
    {
        if (amount.Amount < 0)
            throw new ArgumentException("Line item amount cannot be negative", nameof(amount));

        var lineItem = new InvoiceLineItem(
            Guid.NewGuid(),
            Id,
            rideId,
            rideDate,
            description,
            amount);

        _lineItems.Add(lineItem);
        RecalculateTotal();
    }

    /// <summary>
    /// Recalculates the invoice total from all line items
    /// </summary>
    private void RecalculateTotal()
    {
        var total = _lineItems.Sum(li => li.Amount.Amount);
        TotalAmount = new Money(total);
    }
}

/// <summary>
/// Invoice line item entity representing a single ride charge on an invoice
/// </summary>
public sealed class InvoiceLineItem
{
    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid RideId { get; private set; }
    public DateTime RideDate { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Money Amount { get; private set; }

    private InvoiceLineItem() { } // Required for EF Core

    internal InvoiceLineItem(
        Guid id,
        Guid invoiceId,
        Guid rideId,
        DateTime rideDate,
        string description,
        Money amount)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Line item description cannot be empty", nameof(description));

        Id = id;
        InvoiceId = invoiceId;
        RideId = rideId;
        RideDate = rideDate;
        Description = description;
        Amount = amount;
    }
}
