using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDesktopCatalogScans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "desktop_catalog_scans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_desktop_catalog_scans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_desktop_catalog_scans_venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_desktop_catalog_scans_VenueId_CompletedAt",
                table: "desktop_catalog_scans",
                columns: new[] { "VenueId", "CompletedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_desktop_catalog_scan_candidates_desktop_catalog_scans_ScanId",
                table: "desktop_catalog_scan_candidates",
                column: "ScanId",
                principalTable: "desktop_catalog_scans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_desktop_catalog_scan_candidates_desktop_catalog_scans_ScanId",
                table: "desktop_catalog_scan_candidates");

            migrationBuilder.DropTable(
                name: "desktop_catalog_scans");
        }
    }
}
