using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubnetProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subnet_proposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Network = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    PrefixLength = table.Column<int>(type: "integer", nullable: false),
                    InterfaceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DecidedBySubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subnet_proposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subnet_proposals_venue_agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "venue_agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subnet_proposals_AgentId_DetectedAt",
                table: "subnet_proposals",
                columns: new[] { "AgentId", "DetectedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subnet_proposals");
        }
    }
}
