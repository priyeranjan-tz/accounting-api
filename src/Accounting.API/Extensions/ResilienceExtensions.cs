using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Microsoft.Extensions.Logging;

namespace Accounting.API.Extensions;

/// <summary>
/// Extension methods for configuring Polly resilience policies.
/// Implements retry, circuit breaker, and timeout patterns for database and HTTP operations.
/// </summary>
public static class ResilienceExtensions
{
    /// <summary>
    /// Adds resilience policies to the service collection.
    /// </summary>
    public static IServiceCollection AddResiliencePolicies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database resilience policy
        var dbMaxRetries = configuration.GetValue<int>("Resilience:Database:MaxRetryAttempts", 5);
        var dbRetryBackoff = configuration.GetValue<int>("Resilience:Database:RetryBackoffSeconds", 2);
        var dbCircuitThreshold = configuration.GetValue<double>("Resilience:Database:CircuitBreakerThreshold", 0.5);
        var dbCircuitDuration = configuration.GetValue<int>("Resilience:Database:CircuitBreakerDurationSeconds", 30);

        services.AddResiliencePipeline("database", builder =>
        {
            builder
                // Retry with exponential backoff
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = dbMaxRetries,
                    Delay = TimeSpan.FromSeconds(dbRetryBackoff),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    OnRetry = args =>
                    {
                        Console.WriteLine(
                            $"Database operation retry {args.AttemptNumber} of {dbMaxRetries}. Delay: {args.RetryDelay.TotalMilliseconds}ms");
                        return ValueTask.CompletedTask;
                    }
                })
                // Circuit breaker to prevent cascading failures
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = dbCircuitThreshold,
                    SamplingDuration = TimeSpan.FromSeconds(10),
                    MinimumThroughput = 5,
                    BreakDuration = TimeSpan.FromSeconds(dbCircuitDuration),
                    OnOpened = args =>
                    {
                        Console.WriteLine($"Database circuit breaker opened. Break duration: {dbCircuitDuration}s");
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = args =>
                    {
                        Console.WriteLine("Database circuit breaker closed");
                        return ValueTask.CompletedTask;
                    }
                })
                // Timeout to prevent hanging operations
                .AddTimeout(TimeSpan.FromSeconds(30));
        });

        // HTTP resilience policy
        var httpMaxRetries = configuration.GetValue<int>("Resilience:Http:MaxRetryAttempts", 3);
        var httpRetryBackoff = configuration.GetValue<int>("Resilience:Http:RetryBackoffSeconds", 1);
        var httpTimeout = configuration.GetValue<int>("Resilience:Http:TimeoutSeconds", 10);

        services.AddResiliencePipeline("http", builder =>
        {
            builder
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = httpMaxRetries,
                    Delay = TimeSpan.FromSeconds(httpRetryBackoff),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    OnRetry = args =>
                    {
                        Console.WriteLine(
                            $"HTTP operation retry {args.AttemptNumber} of {httpMaxRetries}. Delay: {args.RetryDelay.TotalMilliseconds}ms");
                        return ValueTask.CompletedTask;
                    }
                })
                .AddTimeout(TimeSpan.FromSeconds(httpTimeout));
        });

        return services;
    }
}
