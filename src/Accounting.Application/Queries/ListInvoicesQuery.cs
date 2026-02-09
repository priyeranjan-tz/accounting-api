namespace Accounting.Application.Queries;

/// <summary>
/// Query to list invoices with optional account filtering and pagination
/// </summary>
public record ListInvoicesQuery(
    Guid? AccountId = null,
    int Page = 1,
    int PageSize = 20);

/// <summary>
/// Response with paginated list of invoices
/// </summary>
public record ListInvoicesResponse(
    List<InvoiceSummaryDto> Invoices,
    PaginationMetadata Pagination);

/// <summary>
/// Summary DTO for invoice list
/// </summary>
public record InvoiceSummaryDto(
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
