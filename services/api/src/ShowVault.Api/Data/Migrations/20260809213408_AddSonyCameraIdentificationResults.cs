using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSonyCameraIdentificationResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SonyCameraIdentificationAttemptedHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SonyCameraIdentificationCommandId",
                table: "subnet_proposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SonyCameraIdentificationMessage",
                table: "subnet_proposals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SonyCameraIdentificationStatus",
                table: "subnet_proposals",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SonyCameraIdentifiedAt",
                table: "subnet_proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SonyCameraIdentifiedHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SonyCameraIdentifiedProductFamilies",
                table: "subnet_proposals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_subnet_proposals_SonyCameraIdentificationCommandId",
                table: "subnet_proposals",
                column: "SonyCameraIdentificationCommandId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_subnet_proposals_SonyCameraIdentificationCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "SonyCameraIdentificationAttemptedHostCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "SonyCameraIdentificationCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "SonyCameraIdentificationMessage",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "SonyCameraIdentificationStatus",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "SonyCameraIdentifiedAt",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "SonyCameraIdentifiedHostCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "SonyCameraIdentifiedProductFamilies",
                table: "subnet_proposals");
        }
    }
}
