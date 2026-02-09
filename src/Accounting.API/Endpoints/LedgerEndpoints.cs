using Accounting.Application.Commands;
using Accounting.Application.Queries;
using Accounting.Domain.Interfaces;
using Accounting.API.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Accounting.API.Endpoints;

/// <summary>
/// API endpoints for ledger operations (User Story 1).
/// Implements double-entry accounting for ride charges and payments.
/// </summary>
public static class LedgerEndpoints
{
    public static void MapLedgerEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/ledger")
            .WithTags("Ledger");

        // T051: POST /ledger/charges - Record ride charge
        group.MapPost("/charges", RecordRideCharge)
            .WithName("RecordRideCharge")
            .WithSummary("Record a ride charge to an account");

        // T052: POST /ledger/payments - Record payment
        group.MapPost("/payments", RecordPayment)
            .WithName("RecordPayment")
            .WithSummary("Record a payment received from a customer");

        // T053: GET /accounts/{accountId}/balance - Get account balance
        routes.MapGet("/accounts/{accountId:guid}/balance", GetAccountBalance)
            .WithTags("Accounts")
            .WithName("GetAccountBalance")
            .WithSummary("Get current balance for an account");

        // T135: GET /accounts/{accountId}/statements - Get account statement
        routes.MapGet("/accounts/{accountId:guid}/statements", GetAccountStatement)
            .WithTags("Accounts")
            .WithName("GetAccountStatement")
            .WithSummary("Get account statement for a date range with transaction details");
    }

    private static async Task<IResult> RecordRideCharge(
        [FromBody] RecordRideChargeCommand command,
        [FromServices] ILedgerRepository repository,
        [FromServices] IAccountRepository accountRepository,
        [FromServices] IInvoiceRepository invoiceRepository,
        [FromServices] ILedgerQueryService ledgerQueryService,
        [FromServices] IInvoiceNumberGenerator invoiceNumberGenerator,
        [FromServices] ILogger<RecordRideChargeCommandHandler> logger,
        HttpContext httpContext)
    {
        var tenantId = httpContext.GetTenantId() ?? Guid.Empty;
        var currentUser = httpContext.User?.Identity?.Name ?? "system";
        var handler = new RecordRideChargeCommandHandler(
            repository, 
            accountRepository,
            invoiceRepository,
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
                Application.Common.ErrorType.Conflict => Results.Problem(
                    title: "Conflict",
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status409Conflict,
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

        return Results.Created($"/ledger/transactions/{result.Value!.TransactionId}", result.Value);
    }

    private static async Task<IResult> RecordPayment(
        [FromBody] RecordPaymentCommand command,
        [FromServices] ILedgerRepository repository,
        [FromServices] ILogger<RecordPaymentCommandHandler> logger,
        HttpContext httpContext)
    {
        var tenantId = httpContext.GetTenantId() ?? Guid.Empty;
        var handler = new RecordPaymentCommandHandler(repository, logger, tenantId);
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

        return Results.Created($"/ledger/transactions/{result.Value!.TransactionId}", result.Value);
    }

    private static async Task<IResult> GetAccountBalance(
        [FromRoute] Guid accountId,
        [FromServices] ILedgerRepository repository,
        HttpContext httpContext)
    {
        var tenantId = httpContext.GetTenantId() ?? Guid.Empty;
        var handler = new GetAccountBalanceQueryHandler(repository, tenantId);
        var query = new GetAccountBalanceQuery { AccountId = accountId };
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

    private static async Task<IResult> GetAccountStatement(
        Guid accountId,
        [FromServices] IAccountRepository accountRepository,
        [FromServices] ILedgerRepository ledgerRepository,
        [FromServices] ILogger<GetAccountStatementQueryHandler> logger,
        HttpContext httpContext)
    {
        // Manually extract query parameters
        // Default: last 30 days
        DateTime startDate = DateTime.UtcNow.AddDays(-30);
        if (httpContext.Request.Query.TryGetValue("startDate", out var startDateValue))
        {
            if (DateTime.TryParse(startDateValue, out var parsed))
                startDate = parsed;
        }
        
        DateTime endDate = DateTime.UtcNow;
        if (httpContext.Request.Query.TryGetValue("endDate", out var endDateValue))
        {
            if (DateTime.TryParse(endDateValue, out var parsed))
                endDate = parsed;
        }
        
        int page = 1;
        if (httpContext.Request.Query.TryGetValue("page", out var pageValue))
        {
            if (int.TryParse(pageValue, out var parsed))
                page = parsed;
        }
        
        int pageSize = 50;
        if (httpContext.Request.Query.TryGetValue("pageSize", out var pageSizeValue))
        {
            if (int.TryParse(pageSizeValue, out var parsed))
                pageSize = parsed;
        }
        
        var tenantId = httpContext.GetTenantId() ?? Guid.Empty;
        
        var handler = new GetAccountStatementQueryHandler(
            accountRepository,
            ledgerRepository,
            logger,
            tenantId);

        var query = new GetAccountStatementQuery(
            accountId,
            startDate,
            endDate,
            page,
            pageSize);

        var result = await handler.HandleAsync(query, httpContext.RequestAborted);

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
                        ["traceId"] = httpContext.TraceIdentifier
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

        return Results.Ok(result.Value);
    }
}
