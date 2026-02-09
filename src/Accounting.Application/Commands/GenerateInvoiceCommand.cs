namespace Accounting.Application.Commands;

/// <summary>
/// Command to generate an invoice for an account and billing period
/// </summary>
public record GenerateInvoiceCommand(
    Guid AccountId,
    DateTime BillingPeriodStart,
    DateTime BillingPeriodEnd,
    DateTime? IssueDate = null,
    int? PaymentTermsDays = null);

/// <summary>
/// Response after generating an invoice
/// </summary>
public record GenerateInvoiceResponse(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid AccountId,
    DateTime BillingPeriodStart,
    DateTime BillingPeriodEnd,
    DateTime IssueDate,
    DateTime DueDate,
    decimal TotalAmount,
    string Currency,
    int LineItemCount,
    DateTime CreatedAt);
