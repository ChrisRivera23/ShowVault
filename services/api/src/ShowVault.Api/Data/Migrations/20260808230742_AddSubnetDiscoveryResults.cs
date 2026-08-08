using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubnetDiscoveryResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptedHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DiscoveredAt",
                table: "subnet_proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DiscoveryCommandId",
                table: "subnet_proposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscoveryMessage",
                table: "subnet_proposals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscoveryStatus",
                table: "subnet_proposals",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RespondingHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_subnet_proposals_DiscoveryCommandId",
                table: "subnet_proposals",
                column: "DiscoveryCommandId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_subnet_proposals_DiscoveryCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "AttemptedHostCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "DiscoveredAt",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "DiscoveryCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "DiscoveryMessage",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "DiscoveryStatus",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "RespondingHostCount",
                table: "subnet_proposals");
        }
    }
}
