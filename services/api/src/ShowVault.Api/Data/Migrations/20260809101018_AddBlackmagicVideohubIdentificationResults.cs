using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBlackmagicVideohubIdentificationResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BlackmagicVideohubIdentificationAttemptedHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BlackmagicVideohubIdentificationCommandId",
                table: "subnet_proposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlackmagicVideohubIdentificationMessage",
                table: "subnet_proposals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlackmagicVideohubIdentificationStatus",
                table: "subnet_proposals",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BlackmagicVideohubIdentifiedAt",
                table: "subnet_proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BlackmagicVideohubIdentifiedHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlackmagicVideohubIdentifiedProductFamilies",
                table: "subnet_proposals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_subnet_proposals_BlackmagicVideohubIdentificationCommandId",
                table: "subnet_proposals",
                column: "BlackmagicVideohubIdentificationCommandId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_subnet_proposals_BlackmagicVideohubIdentificationCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "BlackmagicVideohubIdentificationAttemptedHostCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "BlackmagicVideohubIdentificationCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "BlackmagicVideohubIdentificationMessage",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "BlackmagicVideohubIdentificationStatus",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "BlackmagicVideohubIdentifiedAt",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "BlackmagicVideohubIdentifiedHostCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "BlackmagicVideohubIdentifiedProductFamilies",
                table: "subnet_proposals");
        }
    }
}
