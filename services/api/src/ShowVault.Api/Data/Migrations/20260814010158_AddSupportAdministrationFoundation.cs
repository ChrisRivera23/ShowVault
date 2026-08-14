using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportAdministrationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "support_audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorIssuer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ActorSubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_support_audit_events_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "support_staff_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentityIssuer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IdentitySubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_staff_assignments", x => x.Id);
                    table.CheckConstraint("CK_support_staff_assignments_role", "\"Role\" = 'SupportReader'");
                    table.CheckConstraint("CK_support_staff_assignments_state", "\"State\" IN ('Active', 'Suspended', 'Revoked')");
                });

            migrationBuilder.CreateTable(
                name: "support_organization_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_organization_grants", x => x.Id);
                    table.CheckConstraint("CK_support_organization_grants_state", "\"State\" IN ('Active', 'Revoked')");
                    table.ForeignKey(
                        name: "FK_support_organization_grants_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_support_organization_grants_support_staff_assignments_Staff~",
                        column: x => x.StaffAssignmentId,
                        principalTable: "support_staff_assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_support_audit_events_OccurredAt",
                table: "support_audit_events",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_support_audit_events_OrganizationId_OccurredAt",
                table: "support_audit_events",
                columns: new[] { "OrganizationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_support_organization_grants_OrganizationId",
                table: "support_organization_grants",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_support_organization_grants_StaffAssignmentId_OrganizationId",
                table: "support_organization_grants",
                columns: new[] { "StaffAssignmentId", "OrganizationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_support_staff_assignments_IdentityIssuer_IdentitySubject",
                table: "support_staff_assignments",
                columns: new[] { "IdentityIssuer", "IdentitySubject" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "support_audit_events");

            migrationBuilder.DropTable(
                name: "support_organization_grants");

            migrationBuilder.DropTable(
                name: "support_staff_assignments");
        }
    }
}
