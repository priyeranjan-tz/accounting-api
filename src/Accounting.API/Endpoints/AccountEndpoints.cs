using Accounting.Application.Commands;
using Accounting.Application.Queries;
using Accounting.Domain.Interfaces;
using Accounting.Domain.Enums;
using Accounting.API.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Accounting.API.Endpoints;

/// <summary>
/// API endpoints for account management (User Story 2).
/// Provides CRUD operations for accounts with multi-tenant isolation.
/// </summary>
public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/accounts")
            .WithTags("Accounts");

        // T079: POST /accounts - Create new account
        group.MapPost("", CreateAccount)
            .WithName("CreateAccount")
            .WithSummary("Create a new account");

        // T080: GET /accounts - List accounts with pagination
        group.MapGet("", ListAccounts)
            .WithName("ListAccounts")
            .WithSummary("List all accounts with optional filtering and pagination");

        // T081: GET /accounts/{id} - Get account by ID
        group.MapGet("/{id:guid}", GetAccount)
            .WithName("GetAccount")
            .WithSummary("Get account details by ID");

        // T082: PATCH /accounts/{id} - Update account status
        group.MapPatch("/{id:guid}", UpdateAccountStatus)
            .WithName("UpdateAccountStatus")
            .WithSummary("Update account status (activate/deactivate)");
    }

    private static async Task<IResult> CreateAccount(
        [FromBody] CreateAccountCommand command,
        [FromServices] IAccountRepository repository,
        [FromServices] ILogger<CreateAccountCommandHandler> logger,
        HttpContext httpContext)
    {
        var tenantId = httpContext.GetTenantId() ?? Guid.Empty;
        var currentUser = httpContext.User?.Identity?.Name ?? "system";
        var handler = new CreateAccountCommandHandler(repository, logger, tenantId, currentUser);
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

        return Results.Created($"/accounts/{result.Value!.Id}", result.Value);
    }

    private static async Task<IResult> ListAccounts(
        HttpContext httpContext,
        [FromServices] IAccountRepository repository,
        [FromServices] ILogger<ListAccountsQueryHandler> logger)
    {
        // Manually extract query parameters
        AccountStatus? status = null;
        if (httpContext.Request.Query.TryGetValue("Status", out var statusValue))
        {
            if (Enum.TryParse<AccountStatus>(statusValue, true, out var parsed))
                status = parsed;
        }
        
        AccountType? type = null;
        if (httpContext.Request.Query.TryGetValue("Type", out var typeValue))
        {
            if (Enum.TryParse<AccountType>(typeValue, true, out var parsed))
                type = parsed;
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
        var query = new ListAccountsQuery(status, type, page, pageSize);

        var handler = new ListAccountsQueryHandler(repository, logger, tenantId);
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

    private static async Task<IResult> GetAccount(
        [FromRoute] Guid id,
        [FromServices] IAccountRepository repository,
        [FromServices] ILogger<GetAccountQueryHandler> logger,
        HttpContext httpContext)
    {
        var tenantId = httpContext.GetTenantId() ?? Guid.Empty;
        var query = new GetAccountQuery(id);
        var handler = new GetAccountQueryHandler(repository, logger, tenantId);
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

    private static async Task<IResult> UpdateAccountStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateAccountStatusRequest request,
        [FromServices] IAccountRepository repository,
        [FromServices] ILogger<UpdateAccountStatusCommandHandler> logger,
        HttpContext httpContext)
    {
        var tenantId = httpContext.GetTenantId() ?? Guid.Empty;
        var currentUser = httpContext.User?.Identity?.Name ?? "system";
        var command = new UpdateAccountStatusCommand(id, request.Status);
        var handler = new UpdateAccountStatusCommandHandler(repository, logger, tenantId, currentUser);
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

        return Results.Ok(result.Value);
    }

    /// <summary>
    /// Request model for updating account status
    /// </summary>
    public record UpdateAccountStatusRequest(AccountStatus Status);
}
