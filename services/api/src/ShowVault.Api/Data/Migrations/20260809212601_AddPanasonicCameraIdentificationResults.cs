using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPanasonicCameraIdentificationResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PanasonicCameraIdentificationAttemptedHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PanasonicCameraIdentificationCommandId",
                table: "subnet_proposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PanasonicCameraIdentificationMessage",
                table: "subnet_proposals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PanasonicCameraIdentificationStatus",
                table: "subnet_proposals",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PanasonicCameraIdentifiedAt",
                table: "subnet_proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PanasonicCameraIdentifiedHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PanasonicCameraIdentifiedProductFamilies",
                table: "subnet_proposals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_subnet_proposals_PanasonicCameraIdentificationCommandId",
                table: "subnet_proposals",
                column: "PanasonicCameraIdentificationCommandId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_subnet_proposals_PanasonicCameraIdentificationCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "PanasonicCameraIdentificationAttemptedHostCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "PanasonicCameraIdentificationCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "PanasonicCameraIdentificationMessage",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "PanasonicCameraIdentificationStatus",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "PanasonicCameraIdentifiedAt",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "PanasonicCameraIdentifiedHostCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "PanasonicCameraIdentifiedProductFamilies",
                table: "subnet_proposals");
        }
    }
}
