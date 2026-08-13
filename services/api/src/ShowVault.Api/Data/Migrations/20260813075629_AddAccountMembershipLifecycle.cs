using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountMembershipLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_memberships_organizations_OrganizationId",
                table: "memberships");

            migrationBuilder.AddColumn<string>(
                name: "DisplayLabel",
                table: "memberships",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "memberships",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "memberships",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "memberships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE memberships SET \"State\" = 'Active', \"UpdatedAt\" = \"CreatedAt\", \"Revision\" = 1;");

            migrationBuilder.AlterColumn<long>(
                name: "Revision",
                table: "memberships",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "State",
                table: "memberships",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "memberships",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "account_audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorSubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TargetEntityType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_audit_events_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organization_invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TokenDigest = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    TokenKeyId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedBySubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AcceptedMembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcceptedBySubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TerminalAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_invitations", x => x.Id);
                    table.CheckConstraint("CK_organization_invitations_role", "\"Role\" IN ('Viewer', 'Technician', 'Manager', 'Administrator')");
                    table.CheckConstraint("CK_organization_invitations_state", "\"State\" IN ('Pending', 'Accepted', 'Revoked', 'Expired')");
                    table.ForeignKey(
                        name: "FK_organization_invitations_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_memberships_state",
                table: "memberships",
                sql: "\"State\" IN ('Active', 'Suspended', 'Revoked')");

            migrationBuilder.CreateIndex(
                name: "IX_account_audit_events_OrganizationId_Action_OccurredAt",
                table: "account_audit_events",
                columns: new[] { "OrganizationId", "Action", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_account_audit_events_OrganizationId_TargetEntityType_Target~",
                table: "account_audit_events",
                columns: new[] { "OrganizationId", "TargetEntityType", "TargetEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_invitations_OrganizationId_State_ExpiresAt",
                table: "organization_invitations",
                columns: new[] { "OrganizationId", "State", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_invitations_TokenDigest",
                table: "organization_invitations",
                column: "TokenDigest",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_memberships_organizations_OrganizationId",
                table: "memberships",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_memberships_organizations_OrganizationId",
                table: "memberships");

            migrationBuilder.DropTable(
                name: "account_audit_events");

            migrationBuilder.DropTable(
                name: "organization_invitations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_memberships_state",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "DisplayLabel",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "State",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "memberships");

            migrationBuilder.AddForeignKey(
                name: "FK_memberships_organizations_OrganizationId",
                table: "memberships",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
