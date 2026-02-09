using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Accounting.API.Middleware;

/// <summary>
/// Authentication middleware for JWT validation.
/// TODO: Integrate with Keycloak for production JWT validation.
/// This is a placeholder implementation for development.
/// </summary>
public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationMiddleware> _logger;
    private readonly bool _isDevelopment;

    public AuthenticationMiddleware(
        RequestDelegate next,
        ILogger<AuthenticationMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _isDevelopment = environment.IsDevelopment();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for health check endpoints
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        // For development: use a default tenant if no auth header present
        if (_isDevelopment)
        {
            if (!context.Request.Headers.ContainsKey("Authorization"))
            {
                // Create a default development identity with tenant
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, "dev-user"),
                    new Claim(ClaimTypes.Name, "Development User"),
                    new Claim(TenantIsolationMiddleware.TenantIdClaimType, "11111111-1111-1111-1111-111111111111")
                };

                var identity = new ClaimsIdentity(claims, "Development");
                context.User = new ClaimsPrincipal(identity);

                _logger.LogDebug("Development mode: Using default tenant for unauthenticated request");

                await _next(context);
                return;
            }
        }

        // Extract JWT from Authorization header
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication Required",
                Type = "https://httpstatuses.com/401",
                Detail = "A valid Bearer token is required to access this resource.",
                Instance = context.Request.Path
            };

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problemDetails);
            return;
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();

        try
        {
            // TODO: Replace with Keycloak JWT validation
            // For now, just parse the JWT without validation (DEVELOPMENT ONLY)
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            var claims = jwtToken.Claims.ToList();
            var identity = new ClaimsIdentity(claims, "JWT");
            context.User = new ClaimsPrincipal(identity);

            _logger.LogDebug("JWT authenticated successfully. Subject: {Subject}", jwtToken.Subject);

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT validation failed");

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid Token",
                Type = "https://httpstatuses.com/401",
                Detail = "The provided token is invalid or expired.",
                Instance = context.Request.Path
            };

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }
}

/// <summary>
/// Extension methods for authentication middleware.
/// </summary>
public static class AuthenticationMiddlewareExtensions
{
    /// <summary>
    /// Adds JWT authentication middleware to the pipeline.
    /// </summary>
    public static IApplicationBuilder UseJwtAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuthenticationMiddleware>();
    }
}
