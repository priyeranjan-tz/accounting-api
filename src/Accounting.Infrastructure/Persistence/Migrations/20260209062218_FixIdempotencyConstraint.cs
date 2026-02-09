using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixIdempotencyConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ledger_entries_idempotency",
                table: "ledger_entries");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_idempotency",
                table: "ledger_entries",
                columns: new[] { "account_id", "source_reference_id", "ledger_account" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ledger_entries_idempotency",
                table: "ledger_entries");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_idempotency",
                table: "ledger_entries",
                columns: new[] { "account_id", "source_reference_id" },
                unique: true);
        }
    }
}
