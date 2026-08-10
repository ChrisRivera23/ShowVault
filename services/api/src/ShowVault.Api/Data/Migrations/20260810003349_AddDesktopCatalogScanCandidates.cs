using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDesktopCatalogScanCandidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "desktop_catalog_scan_candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PluginId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CandidateType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_desktop_catalog_scan_candidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_desktop_catalog_scan_candidates_venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_desktop_catalog_scan_candidates_ScanId_CandidateKey",
                table: "desktop_catalog_scan_candidates",
                columns: new[] { "ScanId", "CandidateKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_desktop_catalog_scan_candidates_VenueId_DetectedAt",
                table: "desktop_catalog_scan_candidates",
                columns: new[] { "VenueId", "DetectedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "desktop_catalog_scan_candidates");
        }
    }
}
