using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBehringerWingIdentificationResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BehringerWingIdentificationAttemptedHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BehringerWingIdentificationCommandId",
                table: "subnet_proposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BehringerWingIdentificationMessage",
                table: "subnet_proposals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BehringerWingIdentificationStatus",
                table: "subnet_proposals",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BehringerWingIdentifiedAt",
                table: "subnet_proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BehringerWingIdentifiedHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BehringerWingIdentifiedProductFamilies",
                table: "subnet_proposals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_subnet_proposals_BehringerWingIdentificationCommandId",
                table: "subnet_proposals",
                column: "BehringerWingIdentificationCommandId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_subnet_proposals_BehringerWingIdentificationCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "BehringerWingIdentificationAttemptedHostCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "BehringerWingIdentificationCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "BehringerWingIdentificationMessage",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "BehringerWingIdentificationStatus",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "BehringerWingIdentifiedAt",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "BehringerWingIdentifiedHostCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "BehringerWingIdentifiedProductFamilies",
                table: "subnet_proposals");
        }
    }
}
