using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_LedgerEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ledger_account = table.Column<int>(type: "integer", nullable: false, comment: "1=AccountsReceivable, 2=ServiceRevenue, 3=Cash"),
                    debit_amount = table.Column<decimal>(type: "numeric(19,4)", nullable: false, defaultValue: 0.0000m),
                    credit_amount = table.Column<decimal>(type: "numeric(19,4)", nullable: false, defaultValue: 0.0000m),
                    source_type = table.Column<int>(type: "integer", nullable: false, comment: "1=RideCharge, 2=Payment"),
                    source_reference_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_ledger_entries", x => x.id);
                    table.CheckConstraint("ck_ledger_entries_single_sided", "(debit_amount > 0 AND credit_amount = 0) OR \r\n                  (debit_amount = 0 AND credit_amount > 0)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_account_id",
                table: "ledger_entries",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_created_at",
                table: "ledger_entries",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_idempotency",
                table: "ledger_entries",
                columns: new[] { "account_id", "source_reference_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_tenant_id",
                table: "ledger_entries",
                column: "tenant_id");

            // T049: Create PostgreSQL trigger to prevent ledger entry updates (enforce immutability)
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION prevent_ledger_update()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'Ledger entries are immutable and cannot be updated. Entry ID: %', OLD.id;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER prevent_ledger_update_trigger
                    BEFORE UPDATE ON ledger_entries
                    FOR EACH ROW
                    EXECUTE FUNCTION prevent_ledger_update();
            ");

            // T050: Create PostgreSQL trigger to prevent ledger entry deletes (enforce immutability)
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION prevent_ledger_delete()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'Ledger entries cannot be deleted (append-only ledger). Entry ID: %', OLD.id;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER prevent_ledger_delete_trigger
                    BEFORE DELETE ON ledger_entries
                    FOR EACH ROW
                    EXECUTE FUNCTION prevent_ledger_delete();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop triggers first
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS prevent_ledger_update_trigger ON ledger_entries;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS prevent_ledger_delete_trigger ON ledger_entries;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS prevent_ledger_update();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS prevent_ledger_delete();");

            // Drop table
            migrationBuilder.DropTable(
                name: "ledger_entries");
        }
    }
}
