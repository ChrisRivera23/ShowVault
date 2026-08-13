using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ManifestTotalBytes",
                table: "hosted_sync_sessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "commercial_audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorSubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RequestedBytes = table.Column<long>(type: "bigint", nullable: true),
                    ReservedBytes = table.Column<long>(type: "bigint", nullable: false),
                    CommittedBytes = table.Column<long>(type: "bigint", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_commercial_audit_events_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "commercial_licenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseTypeCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_licenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_commercial_licenses_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hosted_sync_reservations",
                columns: table => new
                {
                    HostedSyncSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalBytes = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hosted_sync_reservations", x => x.HostedSyncSessionId);
                    table.CheckConstraint("CK_hosted_sync_reservations_logical_bytes", "\"LogicalBytes\" >= 0");
                    table.ForeignKey(
                        name: "FK_hosted_sync_reservations_hosted_sync_sessions_HostedSyncSes~",
                        column: x => x.HostedSyncSessionId,
                        principalTable: "hosted_sync_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hosted_sync_reservations_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organization_storage_usage",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommittedBytes = table.Column<long>(type: "bigint", nullable: false),
                    ReservedBytes = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_storage_usage", x => x.OrganizationId);
                    table.CheckConstraint("CK_organization_storage_usage_committed_bytes", "\"CommittedBytes\" >= 0");
                    table.CheckConstraint("CK_organization_storage_usage_reserved_bytes", "\"ReservedBytes\" >= 0");
                    table.ForeignKey(
                        name: "FK_organization_storage_usage_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrentPeriodEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GraceEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_subscriptions_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_hosted_sync_sessions_manifest_total_bytes",
                table: "hosted_sync_sessions",
                sql: "\"ManifestTotalBytes\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_commercial_audit_events_OrganizationId_OccurredAt",
                table: "commercial_audit_events",
                columns: new[] { "OrganizationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_commercial_licenses_OrganizationId",
                table: "commercial_licenses",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hosted_sync_reservations_OrganizationId",
                table: "hosted_sync_reservations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_service_subscriptions_OrganizationId",
                table: "service_subscriptions",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.Sql("""
                UPDATE hosted_sync_sessions
                SET "ManifestTotalBytes" = COALESCE(
                    ("ManifestJson"::jsonb ->> 'totalBytes')::bigint, 0);

                INSERT INTO organization_storage_usage
                    ("OrganizationId", "CommittedBytes", "ReservedBytes", "Revision")
                SELECT "OrganizationId",
                    SUM(CASE WHEN "Status" = 'completed' THEN "ManifestTotalBytes" ELSE 0 END),
                    SUM(CASE WHEN "Status" = 'completed' THEN 0 ELSE "ManifestTotalBytes" END),
                    0
                FROM hosted_sync_sessions
                GROUP BY "OrganizationId";

                INSERT INTO hosted_sync_reservations
                    ("HostedSyncSessionId", "OrganizationId", "LogicalBytes", "State",
                     "ReservedAt", "CommittedAt", "Revision")
                SELECT "Id", "OrganizationId", "ManifestTotalBytes",
                    CASE WHEN "Status" = 'completed' THEN 'Committed' ELSE 'Reserved' END,
                    "CreatedAt",
                    CASE WHEN "Status" = 'completed' THEN "UpdatedAt" ELSE NULL END,
                    0
                FROM hosted_sync_sessions;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commercial_audit_events");

            migrationBuilder.DropTable(
                name: "commercial_licenses");

            migrationBuilder.DropTable(
                name: "hosted_sync_reservations");

            migrationBuilder.DropTable(
                name: "organization_storage_usage");

            migrationBuilder.DropTable(
                name: "service_subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_hosted_sync_sessions_manifest_total_bytes",
                table: "hosted_sync_sessions");

            migrationBuilder.DropColumn(
                name: "ManifestTotalBytes",
                table: "hosted_sync_sessions");
        }
    }
}
