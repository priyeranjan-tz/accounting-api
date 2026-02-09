using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceFrequencyColumnManual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Manually add invoice_frequency column
            // This is needed because the model snapshot was updated but the database wasn't
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'accounts'
                          AND column_name = 'invoice_frequency'
                    ) THEN
                        ALTER TABLE accounts
                        ADD COLUMN invoice_frequency integer NOT NULL DEFAULT 3;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "invoice_frequency",
                table: "accounts");
        }
    }
}
