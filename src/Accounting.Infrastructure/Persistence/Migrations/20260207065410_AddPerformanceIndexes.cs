using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // T142: Performance optimization - Add composite indexes for frequent query patterns
            
            // Ledger queries: Balance calculations, statements, and time-based filtering
            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_tenant_id_account_id_created_at",
                table: "ledger_entries",
                columns: new[] { "tenant_id", "account_id", "created_at" },
                descending: new[] { false, false, true });  // created_at DESC for latest-first queries

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_tenant_id_created_at",
                table: "ledger_entries",
                columns: new[] { "tenant_id", "created_at" },
                descending: new[] { false, true });

            // Account queries: Active account filtering (invoice_frequency index commented out due to migration order issue)
            // migrationBuilder.CreateIndex(
            //     name: "IX_accounts_tenant_id_status_invoice_frequency",
            //     table: "accounts",
            //     columns: new[] { "tenant_id", "status", "invoice_frequency" });

            // Invoice queries: Retrieve invoices by account and date
            migrationBuilder.CreateIndex(
                name: "IX_invoices_tenant_id_account_id_created_at",
                table: "invoices",
                columns: new[] { "tenant_id", "account_id", "created_at" },
                descending: new[] { false, false, true });

            // Invoice queries: Find latest invoice for billing period calculation
            migrationBuilder.CreateIndex(
                name: "IX_invoices_account_id_billing_period_end",
                table: "invoices",
                columns: new[] { "account_id", "billing_period_end" },
                descending: new[] { false, true });

            // Invoice line item queries: Traceability from ride to invoice
            migrationBuilder.CreateIndex(
                name: "IX_invoice_line_items_ride_id",
                table: "invoice_line_items",
                column: "ride_id");

            // Invoice line item queries: Retrieve all line items for invoice
            // Note: FK index already exists (IX_invoice_line_items_invoice_id)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes in reverse order
            migrationBuilder.DropIndex(
                name: "IX_invoice_line_items_ride_id",
                table: "invoice_line_items");

            migrationBuilder.DropIndex(
                name: "IX_invoices_account_id_billing_period_end",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_tenant_id_account_id_created_at",
                table: "invoices");

            // migrationBuilder.DropIndex(
            //     name: "IX_accounts_tenant_id_status_invoice_frequency",
            //     table: "accounts");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_tenant_id_created_at",
                table: "ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_tenant_id_account_id_created_at",
                table: "ledger_entries");
        }
    }
}
