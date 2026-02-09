using Accounting.Application.Commands;
using Accounting.Application.Queries;
using Accounting.Domain.Interfaces;
using Accounting.API.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Accounting.API.Endpoints;

/// <summary>
/// API endpoints for invoice management (User Story 3).
/// Provides operations for generating and retrieving invoices.
/// </summary>
public static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/invoices")
            .WithTags("Invoices");

        // T107: POST /invoices - Generate invoice
        group.MapPost("", GenerateInvoice)
            .WithName("GenerateInvoice")
            .WithSummary("Generate a new invoice for an account and billing period");

        // T108: GET /invoices - List invoices
        group.MapGet("", ListInvoices)
            .WithName("ListInvoices")
            .WithSummary("List all invoices with optional filtering and pagination");

        // T109: GET /invoices/{invoiceNumber} - Get invoice by number
        group.MapGet("/{invoiceNumber}", GetInvoice)
            .WithName("GetInvoice")
            .WithSummary("Get invoice details by invoice number");

        // T122-T123: POST /invoices/scheduled - Generate scheduled invoices (US4)
        group.MapPost("/scheduled", GenerateScheduledInvoices)
            .WithName("GenerateScheduledInvoices")
            .WithSummary("Generate invoices for accounts based on invoice frequency");
    }

    private static async Task<IResult> GenerateInvoice(
        [FromBody] GenerateInvoiceCommand command,
        [FromServices] IInvoiceRepository invoiceRepository,
        [FromServices] IAccountRepository accountRepository,
        [FromServices] ILedgerQueryService ledgerQueryService,
        [FromServices] IInvoiceNumberGenerator invoiceNumberGenerator,
        [FromServices] ILogger<GenerateInvoiceCommandHandler> logger,
        HttpContext httpContext)
    {
        var tenantId = httpContext.GetTenantId() ?? Guid.Empty;
        var currentUser = httpContext.User?.Identity?.Name ?? "system";
        var handler = new GenerateInvoiceCommandHandler(
            invoiceRepository,
            accountRepository,
            ledgerQueryService,
            invoiceNumberGenerator,
            logger,
            tenantId,
            currentUser);

        var result = await handler.HandleAsync(command, httpContext.RequestAborted);

        if (result.IsFailure)
        {
            return result.Error!.Type switch
            {
                Application.Common.ErrorType.Validation => Results.Problem(
                    title: "Validation Error",
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status400BadRequest,
                    type: "https://tools.ietf.org/html/rfc9457#section-3.1",
                    extensions: new Dictionary<string, object?>
                    {
                        ["traceId"] = httpContext.TraceIdentifier,
                        ["errors"] = new Dictionary<string, string[]>
                        {
                            [result.Error.Code] = new[] { result.Error.Message }
                        }
                    }),
                Application.Common.ErrorType.NotFound => Results.Problem(
                    title: "Not Found",
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status404NotFound,
                    type: "https://tools.ietf.org/html/rfc9457#section-3.1",
                    extensions: new Dictionary<string, object?>
                    {
                        ["traceId"] = httpContext.TraceIdentifier
                    }),
                _ => Results.Problem(
                    title: "Internal Server Error",
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status500InternalServerError,
                    type: "https://tools.ietf.org/html/rfc9457#section-3.1",
                    extensions: new Dictionary<string, object?>
                    {
                        ["traceId"] = httpContext.TraceIdentifier
                    })
            };
        }

        return Results.Created($"/invoices/{result.Value!.InvoiceNumber}", result.Value);
    }

    private static async Task<IResult> ListInvoices(
        HttpContext httpContext,
        [FromServices] IInvoiceRepository repository,
        [FromServices] ILogger<ListInvoicesQueryHandler> logger)
    {
        // Manually extract query parameters
        Guid? accountId = null;
        if (httpContext.Request.Query.TryGetValue("AccountId", out var accountIdValue))
        {
            if (Guid.TryParse(accountIdValue, out var parsed))
                accountId = parsed;
        }
        
        int page = 1;
        if (httpContext.Request.Query.TryGetValue("Page", out var pageValue))
        {
            if (int.TryParse(pageValue, out var parsed))
                page = parsed;
        }
        
        int pageSize = 20;
        if (httpContext.Request.Query.TryGetValue("PageSize", out var pageSizeValue))
        {
            if (int.TryParse(pageSizeValue, out var parsed))
                pageSize = parsed;
        }

        var tenantId = httpContext.GetTenantId() ?? Guid.Empty;
        var query = new ListInvoicesQuery(accountId, page, pageSize);

        var handler = new ListInvoicesQueryHandler(repository, logger, tenantId);
        var result = await handler.HandleAsync(query, httpContext.RequestAborted);

        if (result.IsFailure)
        {
            return Results.Problem(
                title: "Internal Server Error",
                detail: result.Error!.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                type: "https://tools.ietf.org/html/rfc9457#section-3.1",
                extensions: new Dictionary<string, object?>
                {
                    ["traceId"] = httpContext.TraceIdentifier
                });
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> GetInvoice(
        [FromRoute] string invoiceNumber,
        [FromServices] IInvoiceRepository repository,
        [FromServices] ILogger<GetInvoiceQueryHandler> logger,
        HttpContext httpContext)
    {
        var tenantId = httpContext.GetTenantId() ?? Guid.Empty;
        var query = new GetInvoiceQuery(invoiceNumber);
        var handler = new GetInvoiceQueryHandler(repository, logger, tenantId);
        var result = await handler.HandleAsync(query, httpContext.RequestAborted);

        if (result.IsFailure)
        {
            return result.Error!.Type switch
            {
                Application.Common.ErrorType.NotFound => Results.Problem(
                    title: "Not Found",
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status404NotFound,
                    type: "https://tools.ietf.org/html/rfc9457#section-3.1",
                    extensions: new Dictionary<string, object?>
                    {
                        ["traceId"] = httpContext.TraceIdentifier
                    }),
                _ => Results.Problem(
                    title: "Internal Server Error",
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status500InternalServerError,
                    type: "https://tools.ietf.org/html/rfc9457#section-3.1",
                    extensions: new Dictionary<string, object?>
                    {
                        ["traceId"] = httpContext.TraceIdentifier
                    })
            };
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> GenerateScheduledInvoices(
        [FromBody] GenerateScheduledInvoicesRequest request,
        [FromServices] IAccountRepository accountRepository,
        [FromServices] IInvoiceRepository invoiceRepository,
        [FromServices] ILedgerQueryService ledgerQueryService,
        [FromServices] IInvoiceNumberGenerator invoiceNumberGenerator,
        [FromServices] ILogger<GenerateScheduledInvoicesCommandHandler> logger,
        HttpContext httpContext)
    {
        var tenantId = httpContext.GetTenantId() ?? Guid.Empty;
        var command = new GenerateScheduledInvoicesCommand(
            (Domain.Enums.InvoiceFrequency)request.Frequency,
            request.GenerationDate ?? DateTime.UtcNow);

        var handler = new GenerateScheduledInvoicesCommandHandler(
            accountRepository,
            invoiceRepository,
            ledgerQueryService,
            invoiceNumberGenerator,
            logger,
            tenantId);

        var result = await handler.HandleAsync(command, httpContext.RequestAborted);

        if (result.IsFailure)
        {
            return Results.Problem(
                title: "Scheduled Invoice Generation Error",
                detail: result.Error!.Message,
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://tools.ietf.org/html/rfc9457#section-3.1",
                extensions: new Dictionary<string, object?>
                {
                    ["traceId"] = httpContext.TraceIdentifier
                });
        }

        return Results.Ok(result.Value);
    }

    /// <summary>
    /// Request body for scheduled invoice generation
    /// </summary>
    public record GenerateScheduledInvoicesRequest(
        int Frequency,
        DateTime? GenerationDate);
}
