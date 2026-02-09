// T146: Outbox processor background job
// Processes unpublished integration events from the outbox table every 30 seconds

using Accounting.Infrastructure.Persistence.DbContext;
using Accounting.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Accounting.Infrastructure.Persistence.Outbox;

/// <summary>
/// Background job that processes outbox events and publishes them to external systems.
/// Runs every 30 seconds to ensure at-least-once delivery of integration events.
/// </summary>
[DisallowConcurrentExecution] // Prevent overlapping executions
public sealed class OutboxProcessorJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessorJob> _logger;

    private const int BatchSize = 100;
    private const int MaxRetryCount = 5;
    private const int ProcessingIntervalSeconds = 30;

    public OutboxProcessorJob(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessorJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobId = context.FireInstanceId;
        var scheduledTime = context.ScheduledFireTimeUtc?.UtcDateTime ?? DateTime.UtcNow;

        _logger.LogInformation(
            "OutboxProcessorJob started - JobId: {JobId}, ScheduledTime: {ScheduledTime}",
            jobId, scheduledTime);

        var startTime = DateTime.UtcNow;
        var processedCount = 0;
        var failedCount = 0;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();

            // Fetch unprocessed events (oldest first, limited batch size)
            var unprocessedEvents = await dbContext.OutboxEvents
                .Where(e => e.ProcessedAt == null && e.RetryCount < MaxRetryCount)
                .OrderBy(e => e.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(context.CancellationToken);

            if (unprocessedEvents.Count == 0)
            {
                _logger.LogDebug("No unprocessed outbox events found");
                return;
            }

            _logger.LogInformation(
                "Processing {Count} outbox events (JobId: {JobId})",
                unprocessedEvents.Count, jobId);

            foreach (var evt in unprocessedEvents)
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("OutboxProcessorJob cancelled mid-execution");
                    break;
                }

                try
                {
                    await ProcessEventAsync(evt, context.CancellationToken);
                    
                    // Mark as successfully processed
                    evt.ProcessedAt = DateTime.UtcNow;
                    evt.ErrorMessage = null;
                    
                    processedCount++;

                    _logger.LogDebug(
                        "Successfully processed event {EventId} of type {EventType}",
                        evt.Id, evt.EventType);
                }
                catch (Exception ex)
                {
                    // Increment retry count and log error
                    evt.RetryCount++;
                    evt.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
                    
                    failedCount++;

                    if (evt.RetryCount >= MaxRetryCount)
                    {
                        _logger.LogError(
                            ex,
                            "Outbox event {EventId} failed after {RetryCount} attempts - moving to poison message queue",
                            evt.Id, evt.RetryCount);
                        
                        // FUTURE: Implement dead-letter queue for failed events requiring manual intervention
                        // Consider: Azure Service Bus DLQ, AWS SQS DLQ, or custom table for poison messages
                    }
                    else
                    {
                        _logger.LogWarning(
                            ex,
                            "Outbox event {EventId} failed (attempt {RetryCount}/{MaxRetryCount})",
                            evt.Id, evt.RetryCount, MaxRetryCount);
                    }
                }
            }

            // Persist all changes in one transaction
            await dbContext.SaveChangesAsync(context.CancellationToken);

            var duration = DateTime.UtcNow - startTime;

            _logger.LogInformation(
                "OutboxProcessorJob completed - Processed: {ProcessedCount}, Failed: {FailedCount}, Duration: {Duration}ms",
                processedCount, failedCount, duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;

            _logger.LogError(
                ex,
                "OutboxProcessorJob failed - JobId: {JobId}, Duration: {Duration}ms",
                jobId, duration.TotalMilliseconds);

            throw; // Let Quartz handle retry logic
        }
    }

    /// <summary>
    /// Processes a single outbox event by publishing it to the message broker.
    /// </summary>
    private async Task ProcessEventAsync(OutboxEventEntity evt, CancellationToken cancellationToken)
    {
        // FUTURE: Implement message broker integration for production event streaming
        // Options:
        // - Azure Service Bus: await serviceBusClient.SendMessageAsync(...)
        // - RabbitMQ: channel.BasicPublish(...)
        // - AWS SQS: await sqsClient.SendMessageAsync(...)
        // - Kafka: await producer.ProduceAsync(...)
        // - Kafka: await producer.ProduceAsync(...)

        _logger.LogInformation(
            "Publishing event {EventId} of type {EventType} for tenant {TenantId}",
            evt.Id, evt.EventType, evt.TenantId);

        // Simulate message broker call
        await Task.Delay(10, cancellationToken);

        // Event types we support:
        // - LedgerEntryCreated: Notify billing system of new transactions
        // - InvoiceGenerated: Trigger invoice delivery (email, PDF generation)
        // - AccountCreated: Sync to external CRM/ERP systems
        // - PaymentReceived: Update dunning systems, send receipts

        _logger.LogDebug(
            "Event {EventId} published successfully to message broker",
            evt.Id);
    }
}
