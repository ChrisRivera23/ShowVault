using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddYamahaDmeIdentificationResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "YamahaIdentificationAttemptedHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "YamahaIdentificationCommandId",
                table: "subnet_proposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YamahaIdentificationMessage",
                table: "subnet_proposals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YamahaIdentificationStatus",
                table: "subnet_proposals",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "YamahaIdentifiedAt",
                table: "subnet_proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YamahaIdentifiedHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YamahaIdentifiedProductFamilies",
                table: "subnet_proposals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_subnet_proposals_YamahaIdentificationCommandId",
                table: "subnet_proposals",
                column: "YamahaIdentificationCommandId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_subnet_proposals_YamahaIdentificationCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "YamahaIdentificationAttemptedHostCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "YamahaIdentificationCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "YamahaIdentificationMessage",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "YamahaIdentificationStatus",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "YamahaIdentifiedAt",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "YamahaIdentifiedHostCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "YamahaIdentifiedProductFamilies",
                table: "subnet_proposals");
        }
    }
}
