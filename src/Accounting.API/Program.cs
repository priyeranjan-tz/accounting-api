using Accounting.API.Extensions;
using Accounting.API.Middleware;
using Accounting.API.Endpoints;
using Accounting.Application.Commands;
using Accounting.Application.Queries;
using Accounting.Domain.Interfaces;
using Accounting.Infrastructure.Persistence.DbContext;
using Accounting.Infrastructure.Repositories;
using Accounting.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// T025: Configure Serilog for structured logging
// ============================================================================
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Accounting.API")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/accounting-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}",
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

builder.Host.UseSerilog();

// ============================================================================
// T026: Configure OpenTelemetry for tracing and metrics
// ============================================================================
var serviceName = builder.Configuration.GetValue<string>("OpenTelemetry:ServiceName") ?? "Accounting.API";
var serviceVersion = builder.Configuration.GetValue<string>("OpenTelemetry:ServiceVersion") ?? "1.0.0";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddConsoleExporter()) // For development; use OTLP exporter in production
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("Accounting.API")
        .AddConsoleExporter()); // For development; use OTLP exporter in production

// ============================================================================
// T027: Add Polly resilience policies
// ============================================================================
// Temporarily disabled to isolate endpoint issues
// builder.Services.AddResiliencePolicies(builder.Configuration);

// ============================================================================
// Database configuration
// ============================================================================
builder.Services.AddDbContext<AccountingDbContext>((serviceProvider, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("AccountingDb")
        ?? throw new InvalidOperationException("Connection string 'AccountingDb' not found.");

    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);
        npgsqlOptions.CommandTimeout(30);
    });

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// ============================================================================
// T028: Add health checks
// ============================================================================
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AccountingDbContext>(
        name: "database",
        tags: new[] { "db", "postgresql" });

// Add services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip;
        options.JsonSerializerOptions.AllowTrailingCommas = true;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddEndpointsApiExplorer();

// ============================================================================
// T138: Configure Swagger/OpenAPI with comprehensive documentation
// ============================================================================
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Accounting Ledger API",
        Version = "v1",
        Description = @"
Multi-tenant accounting ledger system implementing double-entry bookkeeping principles for ride-hailing operations.

**Features:**
- Double-entry accounting with Accounts Receivable and Service Revenue tracking
- Multi-tenant data isolation with tenant-level filtering
- ACID-compliant ledger operations with idempotency guarantees
- Automated invoice generation (per-ride, daily, weekly, monthly frequencies)
- Account statement generation with balance calculations
- PostgreSQL-backed persistence with immutable ledger entries

**Authentication:**
- API requires `X-Tenant-Id` header for all requests (multi-tenant isolation)
- Production deployments should implement proper OAuth2/OIDC authentication

**Precision:**
- All monetary values use NUMERIC(19,4) precision (4 decimal places)
- Balances calculated using double-entry accounting principles

**Idempotency:**
- Ride charges are idempotent on `rideId` (409 Conflict on duplicate)
- Invoice generation is idempotent on `invoiceNumber` (unique constraint)
",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Accounting API Support",
            Email = "support@example.com"
        }
    });

    // Add security definition for tenant header
    options.AddSecurityDefinition("TenantId", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name = "X-Tenant-Id",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Description = "Tenant identifier for multi-tenant isolation (UUID format)"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "TenantId"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Configure HTTP JSON options for minimal APIs
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// ============================================================================
// T124: Configure Quartz.NET for background job scheduling
// TODO: Add Quartz.Extensions.Hosting package reference to Accounting.API.csproj
// ============================================================================
// builder.Services.AddQuartz(quartz =>
// {
//     quartz.UseMicrosoftDependencyInjectionJobFactory();
//     Accounting.Infrastructure.BackgroundJobs.QuartzConfiguration.ConfigureJobs(quartz);
// });
// 
// builder.Services.AddQuartzHostedService(options =>
// {
//     options.WaitForJobsToComplete = true;
// });

// ============================================================================
// Register application handlers and repositories
// ============================================================================
// Repositories
builder.Services.AddScoped<ILedgerRepository, LedgerRepository>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();

// Domain services
builder.Services.AddScoped<ILedgerQueryService, LedgerQueryService>();
builder.Services.AddScoped<IInvoiceNumberGenerator, InvoiceNumberGenerator>();

// Note: Handlers are created manually in endpoints to properly resolve TenantId from HttpContext
// See LedgerEndpoints.cs for handler instantiation

builder.Services.AddHttpContextAccessor();

// ============================================================================
// T144: Add rate limiting policies
// ============================================================================
// Temporarily disabled to isolate endpoint issues
// builder.Services.AddRateLimitingPolicies();

// ============================================================================
// Build application
// ============================================================================
var app = builder.Build();

// ============================================================================
// Configure middleware pipeline
// ============================================================================

// T022: Global exception handling (must be first)
app.UseGlobalExceptionHandler();

// Serilog request logging
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
        
        var tenantId = httpContext.GetTenantId();
        if (tenantId.HasValue)
        {
            diagnosticContext.Set("TenantId", tenantId.Value);
        }
    };
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Accounting Ledger API v1");
        options.RoutePrefix = string.Empty; // Serve Swagger UI at root
        options.DocumentTitle = "Accounting Ledger API - Interactive Documentation";
        options.DefaultModelsExpandDepth(-1); // Collapse models by default
        options.DisplayRequestDuration();
    });
}
else
{
    // Production: Swagger available at /swagger but not at root
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Accounting Ledger API v1");
        options.RoutePrefix = "swagger";
    });
}

// T143: HTTPS redirect for security (disabled in development)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// T144: Rate limiting
// Temporarily disabled to isolate endpoint issues
// app.UseRateLimiter();

// T023: Authentication middleware
app.UseJwtAuthentication();

// T024: Tenant isolation middleware (requires authentication first)
app.UseTenantIsolation();

// ============================================================================
// T028: Map health check endpoints
// ============================================================================
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // Liveness check - always returns healthy if app is running
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db") // Readiness check - includes database
});

app.MapHealthChecks("/health/startup", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db") // Startup check - includes database
});

// Map controllers and endpoints
app.MapControllers();

// Map ledger endpoints (User Story 1)
app.MapLedgerEndpoints();

// Map account endpoints (User Story 2)
app.MapAccountEndpoints();

// Map invoice endpoints (User Story 3)
app.MapInvoiceEndpoints();

// ============================================================================
// Database initialization - Apply migrations on startup
// ============================================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<AccountingDbContext>();
        Log.Information("Applying database migrations...");
        dbContext.Database.Migrate();
        Log.Information("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while migrating the database");
        throw;
    }
}

// ============================================================================
// Run application
// ============================================================================
try
{
    Log.Information("Starting Accounting.API (Service: {ServiceName}, Version: {ServiceVersion})", 
        serviceName, serviceVersion);
    app.Run();
    Log.Information("Accounting.API stopped cleanly");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Accounting.API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program class accessible for integration/contract tests
public partial class Program { }
