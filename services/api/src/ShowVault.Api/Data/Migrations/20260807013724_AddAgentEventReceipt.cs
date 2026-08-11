using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentEventReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "received_agent_events",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProtocolVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_received_agent_events", x => x.EventId);
                    table.ForeignKey(
                        name: "FK_received_agent_events_venue_agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "venue_agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_received_agent_events_AgentId_ReceivedAt",
                table: "received_agent_events",
                columns: new[] { "AgentId", "ReceivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "received_agent_events");
        }
    }
}
