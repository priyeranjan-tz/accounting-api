using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Accounting.API.Middleware;

/// <summary>
/// Middleware for extracting and validating JWT tenant ID from authenticated requests.
/// Ensures multi-tenant isolation by making tenant ID available to downstream services.
/// </summary>
public class TenantIsolationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantIsolationMiddleware> _logger;

    public const string TenantIdClaimType = "tenant_id";
    public const string TenantIdHeaderName = "X-Tenant-Id";
    public const string TenantIdItemKey = "TenantId";

    public TenantIsolationMiddleware(
        RequestDelegate next,
        ILogger<TenantIsolationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip tenant extraction for health check endpoints
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        // Extract tenant ID from JWT claims
        var tenantIdClaim = context.User.FindFirst(TenantIdClaimType)?.Value;

        if (string.IsNullOrWhiteSpace(tenantIdClaim))
        {
            _logger.LogWarning(
                "Missing tenant ID claim in JWT. Path: {Path}, User: {User}",
                context.Request.Path,
                context.User.Identity?.Name ?? "Anonymous");

            // Return 403 Forbidden for missing tenant
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Tenant Required",
                Type = "https://httpstatuses.com/403",
                Detail = "A valid tenant identifier is required to access this resource.",
                Instance = context.Request.Path
            };

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problemDetails);
            return;
        }

        // Parse tenant ID to Guid
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            _logger.LogWarning(
                "Invalid tenant ID format: {TenantId}. Path: {Path}",
                tenantIdClaim,
                context.Request.Path);

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Invalid Tenant",
                Type = "https://httpstatuses.com/403",
                Detail = "The tenant identifier format is invalid.",
                Instance = context.Request.Path
            };

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problemDetails);
            return;
        }

        // Store tenant ID in HttpContext.Items for downstream access
        context.Items[TenantIdItemKey] = tenantId;

        // Optionally add tenant ID to response headers for debugging
        context.Response.Headers[TenantIdHeaderName] = tenantId.ToString();

        _logger.LogDebug(
            "Tenant {TenantId} identified for request {Path}",
            tenantId,
            context.Request.Path);

        await _next(context);
    }
}

/// <summary>
/// Extension methods for tenant isolation middleware.
/// </summary>
public static class TenantIsolationMiddlewareExtensions
{
    /// <summary>
    /// Adds tenant isolation middleware to the pipeline.
    /// Must be called after authentication middleware.
    /// </summary>
    public static IApplicationBuilder UseTenantIsolation(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantIsolationMiddleware>();
    }

    /// <summary>
    /// Gets the current tenant ID from HttpContext.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The tenant ID if available; otherwise, null.</returns>
    public static Guid? GetTenantId(this HttpContext context)
    {
        if (context.Items.TryGetValue(TenantIsolationMiddleware.TenantIdItemKey, out var tenantId))
        {
            return tenantId as Guid?;
        }

        return null;
    }
}
