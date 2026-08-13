using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billing_account_bindings",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProviderCustomerId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProviderSubscriptionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    InitialInvoiceId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    OfferingCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ProviderModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProviderRevision = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_account_bindings", x => x.OrganizationId);
                    table.ForeignKey(
                        name: "FK_billing_account_bindings_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "billing_attentions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_attentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_billing_attentions_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "billing_event_receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProviderEventId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderObjectId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProviderCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApiVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PayloadSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OutcomeCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_event_receipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "billing_purchase_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OfferingCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActiveSlot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ProviderSessionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_purchase_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_billing_purchase_attempts_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_billing_account_bindings_Provider_Environment_ProviderCusto~",
                table: "billing_account_bindings",
                columns: new[] { "Provider", "Environment", "ProviderCustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_account_bindings_Provider_Environment_ProviderSubsc~",
                table: "billing_account_bindings",
                columns: new[] { "Provider", "Environment", "ProviderSubscriptionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_attentions_OrganizationId_ResolvedAt",
                table: "billing_attentions",
                columns: new[] { "OrganizationId", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_billing_event_receipts_Provider_Environment_ProviderEventId",
                table: "billing_event_receipts",
                columns: new[] { "Provider", "Environment", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_event_receipts_State_ReceivedAt",
                table: "billing_event_receipts",
                columns: new[] { "State", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_billing_purchase_attempts_OrganizationId_ActiveSlot",
                table: "billing_purchase_attempts",
                columns: new[] { "OrganizationId", "ActiveSlot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_purchase_attempts_Provider_Environment_ProviderSess~",
                table: "billing_purchase_attempts",
                columns: new[] { "Provider", "Environment", "ProviderSessionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_account_bindings");

            migrationBuilder.DropTable(
                name: "billing_attentions");

            migrationBuilder.DropTable(
                name: "billing_event_receipts");

            migrationBuilder.DropTable(
                name: "billing_purchase_attempts");
        }
    }
}
