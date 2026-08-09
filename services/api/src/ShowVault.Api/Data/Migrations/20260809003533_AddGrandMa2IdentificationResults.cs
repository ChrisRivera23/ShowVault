using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGrandMa2IdentificationResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GrandMa2IdentificationAttemptedHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GrandMa2IdentificationCommandId",
                table: "subnet_proposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrandMa2IdentificationMessage",
                table: "subnet_proposals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrandMa2IdentificationStatus",
                table: "subnet_proposals",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GrandMa2IdentifiedAt",
                table: "subnet_proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GrandMa2IdentifiedHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrandMa2IdentifiedProductFamilies",
                table: "subnet_proposals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_subnet_proposals_GrandMa2IdentificationCommandId",
                table: "subnet_proposals",
                column: "GrandMa2IdentificationCommandId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_subnet_proposals_GrandMa2IdentificationCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "GrandMa2IdentificationAttemptedHostCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "GrandMa2IdentificationCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "GrandMa2IdentificationMessage",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "GrandMa2IdentificationStatus",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "GrandMa2IdentifiedAt",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "GrandMa2IdentifiedHostCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "GrandMa2IdentifiedProductFamilies",
                table: "subnet_proposals");
        }
    }
}
