using Accounting.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accounting.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for LedgerEntryEntity.
/// Implements idempotency constraint, immutability triggers, and double-entry validation.
/// </summary>
public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntryEntity>
{
    public void Configure(EntityTypeBuilder<LedgerEntryEntity> builder)
    {
        // Table name (snake_case via AccountingDbContext convention)
        builder.ToTable("ledger_entries", t =>
        {
            // CRITICAL: Check constraint - enforce single-sided entries (exactly one of debit or credit is non-zero)
            t.HasCheckConstraint(
                "ck_ledger_entries_single_sided",
                @"(debit_amount > 0 AND credit_amount = 0) OR 
                  (debit_amount = 0 AND credit_amount > 0)");
        });

        // Primary key
        builder.HasKey(e => e.Id);

        // Properties
        builder.Property(e => e.Id)
            .IsRequired()
            .ValueGeneratedNever(); // Domain generates GUIDs

        builder.Property(e => e.AccountId)
            .IsRequired();

        builder.Property(e => e.LedgerAccount)
            .IsRequired()
            .HasComment("1=AccountsReceivable, 2=ServiceRevenue, 3=Cash");

        // CRITICAL: Money precision - decimal(19,4) for cent-level accuracy
        builder.Property(e => e.DebitAmount)
            .IsRequired()
            .HasColumnType("NUMERIC(19,4)")
            .HasDefaultValue(0.0000m);

        builder.Property(e => e.CreditAmount)
            .IsRequired()
            .HasColumnType("NUMERIC(19,4)")
            .HasDefaultValue(0.0000m);

        builder.Property(e => e.SourceType)
            .IsRequired()
            .HasComment("1=RideCharge, 2=Payment");

        builder.Property(e => e.SourceReferenceId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Description)
            .HasMaxLength(500);

   

        // CRITICAL: Idempotency constraint - prevent duplicate charges for same ride
        // Unique index on (account_id, source_reference_id, ledger_account) allows debit + credit entries
        // for same transaction while preventing true duplicates
        builder.HasIndex(e => new { e.AccountId, e.SourceReferenceId, e.LedgerAccount })
            .IsUnique()
            .HasDatabaseName("ix_ledger_entries_idempotency");

        // Index on tenant_id for multi-tenant query filtering
        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_ledger_entries_tenant_id");

        // Index on account_id for balance calculation queries
        builder.HasIndex(e => e.AccountId)
            .HasDatabaseName("ix_ledger_entries_account_id");

        // Index on created_at for chronological queries
        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("ix_ledger_entries_created_at");

        // Note: Database triggers for immutability are defined in migration 20260206135129_InitialCreate_LedgerEntries:
        // - prevent_ledger_update: Prevents UPDATE statements on ledger_entries table
        // - prevent_ledger_delete: Prevents DELETE statements on ledger_entries table
    }
}
