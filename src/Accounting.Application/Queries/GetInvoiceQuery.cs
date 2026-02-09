namespace Accounting.Application.Queries;

/// <summary>
/// Query to get an invoice by invoice number
/// </summary>
public record GetInvoiceQuery(string InvoiceNumber);

/// <summary>
/// Response with full invoice details including line items
/// </summary>
public record GetInvoiceResponse(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid AccountId,
    DateTime BillingPeriodStart,
    DateTime BillingPeriodEnd,
    DateTime IssueDate,
    DateTime DueDate,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedAt,
    string CreatedBy,
    List<InvoiceLineItemDto> LineItems);

/// <summary>
/// Invoice line item details
/// </summary>
public record InvoiceLineItemDto(
    Guid LineItemId,
    Guid RideId,
    DateTime RideDate,
    string Description,
    decimal Amount);
