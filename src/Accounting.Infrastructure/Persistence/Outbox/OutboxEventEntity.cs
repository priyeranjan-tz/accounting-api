// T145: Outbox pattern for reliable integration event delivery
// Ensures transactional consistency between database writes and event publishing

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accounting.Infrastructure.Persistence.Outbox;

/// <summary>
/// Represents an unpublished integration event stored in the transactional outbox.
/// Events are inserted in the same transaction as domain changes, then published asynchronously.
/// </summary>
[Table("outbox_events")]
public sealed class OutboxEventEntity
{
    /// <summary>
    /// Unique identifier for the outbox event.
    /// Used for idempotency tracking in consuming systems.
    /// </summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Event type identifier (e.g., "LedgerEntryCreated", "InvoiceGenerated").
    /// Used by background processor to route events to correct handlers.
    /// </summary>
    [Required]
    [MaxLength(200)]
    [Column("event_type")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Serialized event payload as JSON.
    /// Stored as JSONB in PostgreSQL for efficient querying and indexing.
    /// </summary>
    [Required]
    [Column("payload", TypeName = "jsonb")]
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Tenant ID for multi-tenant isolation.
    /// Allows filtering events by tenant for targeted processing.
    /// </summary>
    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Timestamp when the event was created and inserted into the outbox.
    /// Used for ordering events chronologically during processing.
    /// </summary>
    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the event was successfully processed and published.
    /// NULL indicates the event is still pending processing.
    /// </summary>
    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Number of processing attempts for this event.
    /// Used for exponential backoff and dead-letter queue decisions.
    /// </summary>
    [Column("retry_count")]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Error message from the last failed processing attempt.
    /// Helps diagnose and troubleshoot event delivery failures.
    /// </summary>
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Checks if the event has been successfully processed.
    /// </summary>
    public bool IsProcessed => ProcessedAt.HasValue;

    /// <summary>
    /// Checks if the event has exceeded the maximum retry limit.
    /// </summary>
    public bool IsPoisonMessage => RetryCount > 5;
}
