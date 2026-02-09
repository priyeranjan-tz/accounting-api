using Accounting.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accounting.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for InvoiceEntity
/// </summary>
public class InvoiceConfiguration : IEntityTypeConfiguration<InvoiceEntity>
{
    public void Configure(EntityTypeBuilder<InvoiceEntity> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.InvoiceNumber)
            .HasColumnName("invoice_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.BillingPeriodStart)
            .HasColumnName("billing_period_start")
            .IsRequired();

        builder.Property(e => e.BillingPeriodEnd)
            .HasColumnName("billing_period_end")
            .IsRequired();

        builder.Property(e => e.IssueDate)
            .HasColumnName("issue_date")
            .IsRequired();

        builder.Property(e => e.DueDate)
            .HasColumnName("due_date")
            .IsRequired();

        builder.Property(e => e.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("NUMERIC(19,4)")
            .IsRequired();

        builder.Property(e => e.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100)
            .IsRequired();

        // Relationships
        builder.HasMany(e => e.LineItems)
            .WithOne(li => li.Invoice)
            .HasForeignKey(li => li.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_invoices_tenant_id");

        builder.HasIndex(e => new { e.TenantId, e.InvoiceNumber })
            .IsUnique()
            .HasDatabaseName("ix_invoices_tenant_id_invoice_number");

        builder.HasIndex(e => e.AccountId)
            .HasDatabaseName("ix_invoices_account_id");
    }
}

/// <summary>
/// EF Core configuration for InvoiceLineItemEntity
/// </summary>
public class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItemEntity>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItemEntity> builder)
    {
        builder.ToTable("invoice_line_items");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.InvoiceId)
            .HasColumnName("invoice_id")
            .IsRequired();

        builder.Property(e => e.RideId)
            .HasColumnName("ride_id")
            .IsRequired();

        builder.Property(e => e.RideDate)
            .HasColumnName("ride_date")
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.Amount)
            .HasColumnName("amount")
            .HasColumnType("NUMERIC(19,4)")
            .IsRequired();

        // Indexes
        builder.HasIndex(e => e.InvoiceId)
            .HasDatabaseName("ix_invoice_line_items_invoice_id");

        builder.HasIndex(e => e.RideId)
            .HasDatabaseName("ix_invoice_line_items_ride_id");
    }
}
