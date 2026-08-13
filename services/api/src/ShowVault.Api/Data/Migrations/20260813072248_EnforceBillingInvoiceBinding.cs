using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceBillingInvoiceBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_billing_account_bindings_Provider_Environment_InitialInvoic~",
                table: "billing_account_bindings",
                columns: new[] { "Provider", "Environment", "InitialInvoiceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_billing_account_bindings_Provider_Environment_InitialInvoic~",
                table: "billing_account_bindings");
        }
    }
}
