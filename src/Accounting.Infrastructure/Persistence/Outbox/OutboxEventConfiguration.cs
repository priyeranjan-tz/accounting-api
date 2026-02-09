// T145: Entity Framework Core configuration for Outbox pattern
// Configures database mapping, indexes, and constraints for outbox_events table

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accounting.Infrastructure.Persistence.Outbox;

/// <summary>
/// Entity Framework Core configuration for OutboxEventEntity.
/// Defines table structure, indexes, and constraints for the transactional outbox pattern.
/// </summary>
public sealed class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEventEntity>
{
    public void Configure(EntityTypeBuilder<OutboxEventEntity> builder)
    {
        // Table configuration
        builder.ToTable("outbox_events");
        builder.HasKey(e => e.Id);

        // Primary key
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever(); // Manually assigned GUIDs

        // Event type (required, indexed)
        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(200)
            .IsRequired();

        // Payload as JSONB (PostgreSQL-specific)
        builder.Property(e => e.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        // Tenant ID (required for multi-tenant isolation)
        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        // Timestamps
        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ProcessedAt)
            .HasColumnName("processed_at");

        // Retry tracking
        builder.Property(e => e.RetryCount)
            .HasColumnName("retry_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message");

        // Performance Indexes
        // ============================================================================

        // Partial index for unprocessed events (background processor query)
        // Uses PostgreSQL's partial index: WHERE processed_at IS NULL
        builder.HasIndex(e => e.ProcessedAt)
            .HasDatabaseName("ix_outbox_events_unprocessed")
            .HasFilter("processed_at IS NULL"); // Only index unprocessed events

        // Composite index for tenant-based event queries
        builder.HasIndex(e => new { e.TenantId, e.CreatedAt })
            .HasDatabaseName("ix_outbox_events_tenant_created");

        // Index for chronological processing order
        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("ix_outbox_events_created_at");

        // Index for event type filtering (route events to specific handlers)
        builder.HasIndex(e => e.EventType)
            .HasDatabaseName("ix_outbox_events_event_type");

        // Ignore computed properties (not persisted to database)
        builder.Ignore(e => e.IsProcessed);
        builder.Ignore(e => e.IsPoisonMessage);
    }
}
