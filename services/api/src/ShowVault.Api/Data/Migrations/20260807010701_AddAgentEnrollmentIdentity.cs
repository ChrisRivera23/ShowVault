using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentEnrollmentIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_enrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecretHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    CreatedBySubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_enrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_enrollments_venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "venue_agents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CredentialHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venue_agents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_venue_agents_venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_enrollments_SecretHash",
                table: "agent_enrollments",
                column: "SecretHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_enrollments_VenueId",
                table: "agent_enrollments",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_venue_agents_VenueId",
                table: "venue_agents",
                column: "VenueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_enrollments");

            migrationBuilder.DropTable(
                name: "venue_agents");
        }
    }
}
