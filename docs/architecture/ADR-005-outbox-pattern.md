# ADR-005: Outbox Pattern for Integration Events

**Status**: Accepted  
**Date**: 2026-02-07  
**Deciders**: Engineering Team, Architecture Team  
**Technical Story**: Accounting Ledger System - Phase 8 (T145-T146)

## Context

The accounting system needs to publish integration events to external systems (e.g., Billing Service, Notification Service, Analytics). We must ensure **at-least-once delivery** while maintaining transactional consistency with database writes.

## Decision

Implement the **Transactional Outbox Pattern** with Quartz.NET background processor.

### Implementation Details

**1. Outbox Table** (stores unpublished events):
```sql
CREATE TABLE outbox_events (
    id UUID PRIMARY KEY,
    event_type VARCHAR(200) NOT NULL,
    payload JSONB NOT NULL,
    tenant_id UUID NOT NULL,
    created_at TIMESTAMP NOT NULL,
    processed_at TIMESTAMP NULL,
    retry_count INT DEFAULT 0,
    error_message TEXT NULL
);

CREATE INDEX ix_outbox_events_processed_at ON outbox_events(processed_at) WHERE processed_at IS NULL;
```

**2. Publish Event in Same Transaction as Ledger Write**:
```csharp
public async Task<Guid> AppendEntriesAsync(IEnumerable<LedgerEntry> entries, ...)
{
    using var transaction = await _dbContext.Database.BeginTransactionAsync();

    // 1. Insert ledger entries
    await _dbContext.LedgerEntries.AddRangeAsync(persistenceEntries);
    await _dbContext.SaveChangesAsync();

    // 2. Insert outbox event (same transaction)
    var outboxEvent = new OutboxEventEntity
    {
        Id = Guid.NewGuid(),
        EventType = "LedgerEntryCreated",
        Payload = JsonSerializer.Serialize(new LedgerEntryCreatedEvent
        {
            TransactionId = transactionId,
            AccountId = accountId,
            Amount = amount,
            TenantId = tenantId
        }),
        TenantId = tenantId,
        CreatedAt = DateTime.UtcNow
    };
    await _dbContext.OutboxEvents.AddAsync(outboxEvent);
    await _dbContext.SaveChangesAsync();

    await transaction.CommitAsync(); // Both or neither
    return transactionId;
}
```

**3. Background Processor** (Quartz.NET job runs every 30 seconds):
```csharp
public class OutboxProcessorJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var unprocessedEvents = await _dbContext.OutboxEvents
            .Where(e => e.ProcessedAt == null)
            .OrderBy(e => e.CreatedAt)
            .Take(100)
            .ToListAsync();

        foreach (var evt in unprocessedEvents)
        {
            try
            {
                // Publish to message broker (RabbitMQ, Azure Service Bus, etc.)
                await _messageBus.PublishAsync(evt.EventType, evt.Payload);

                // Mark as processed
                evt.ProcessedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                evt.RetryCount++;
                evt.ErrorMessage = ex.Message;
                await _dbContext.SaveChangesAsync();

                if (evt.RetryCount > 5)
                {
                    _logger.LogError("Outbox event failed after 5 retries: {EventId}", evt.Id);
                    // Move to dead-letter queue
                }
            }
        }
    }
}
```

**4. Integration Event Types**:
```csharp
public record LedgerEntryCreatedEvent(
    Guid TransactionId,
    Guid AccountId,
    decimal Amount,
    Guid TenantId,
    DateTime Timestamp
);

public record InvoiceGeneratedEvent(
    string InvoiceNumber,
    Guid AccountId,
    decimal TotalAmount,
    Guid TenantId,
    DateTime GeneratedAt
);
```

## Consequences

### Positive

- **Consistency**: Database write + event publish in same transaction (ACID guarantee)
- **Reliability**: Events never lost (persisted to database before publish)
- **At-Least-Once Delivery**: Retries ensure eventual delivery
- **Auditable**: Outbox table provides complete event history
- **Decoupling**: Consuming services don't block ledger writes

### Negative

- **Latency**: Events delivered asynchronously (30s delay by default)
- **Duplicate Events**: Consumers must handle idempotency (event ID check)
- **Storage Overhead**: Outbox table grows over time (requires cleanup)
- **Operational Complexity**: Background processor adds failure mode

### Mitigation

- **Faster Processing**: Reduce Quartz.NET interval to 5 seconds for critical events
- **Idempotency**: Include unique `EventId` in payload for deduplication
- **Cleanup Job**: Purge processed events >7 days old (separate Quartz.NET job)
- **Monitoring**: Alert on outbox queue depth >  1000 events

## Alternatives Considered

### Alternative 1: Synchronous Message Bus Publish

**Description**: Publish event to message bus immediately after database write

**Example**:
```csharp
await _dbContext.SaveChangesAsync();  // 1. Persist ledger entry
await _messageBus.PublishAsync(...);  // 2. Publish event
```

**Rejected Because**:
- **Failure Scenario**: If message bus is down, ledger entry saved but event not published
- **Inconsistency**: Database committed but event lost (violates consistency guarantee)
- **Network Failures**: Message broker outage blocks ledger writes

### Alternative 2: Event Sourcing (Event Store)

**Description**: Store domain events as source of truth, project ledger entries from events

**Rejected Because**:
- **Complexity**: Requires event replays, projections, versioning
- **Query Performance**: Rebuilding state from events is slow
- **Over-Engineering**: Ledger entries are already append-only (event-like)
- **Team Expertise**: Event sourcing requires specialized knowledge

### Alternative 3: Change Data Capture (CDC)

**Description**: Use PostgreSQL logical replication to stream changes to Kafka

**Rejected Because**:
- **Infrastructure Overhead**: Requires Kafka, Debezium, or similar
- **CDC Limitations**: Cannot filter by business logic (publishes all DB changes)
- **Deployment Complexity**: Additional services to manage
- **Cost**: Kafka cluster for small event volume is overkill

## Related Decisions

- **ADR-003**: Immutable ledger (events are also immutable once published)
- **ADR-004**: Multi-tenant isolation (outbox events must include `TenantId`)

## Implementation Checklist

- [ ] Create `outbox_events` table with indexes (T145)
- [ ] Implement `OutboxEventEntity` and repository methods
- [ ] Modify `LedgerRepository.AppendEntriesAsync()` to insert outbox event
- [ ] Create `OutboxProcessorJob` with Quartz.NET (T146)
- [ ] Configure Quartz.NET job (runs every 30 seconds)
- [ ] Implement retry logic with exponential backoff
- [ ] Add cleanup job (purge processed events >7 days old)
- [ ] Integration tests validate event delivery
- [ ] Monitor outbox queue depth in production

## References

- Chris Richardson: [Transactional Outbox Pattern](https://microservices.io/patterns/data/transactional-outbox.html)
- Microsoft: [Outbox Pattern Documentation](https://learn.microsoft.com/en-us/azure/architecture/best-practices/transactional-outbox-cosmos)
- Udi Dahan: [Reliable Messaging](https://udidahan.com/2009/04/20/soa-patterns-the-saga/)
- Martin Fowler: [Event-Driven Architecture](https://martinfowler.com/articles/201701-event-driven.html)

## Future Enhancements

- **Message Broker Integration**: Connect to RabbitMQ, Azure Service Bus, or AWS SQS
- **Dead-Letter Queue**: Move permanently failed events to DLQ for investigation
- **Event Versioning**: Support schema evolution for integration events
- **Metrics**: Track outbox queue depth, publish latency, retry rates
