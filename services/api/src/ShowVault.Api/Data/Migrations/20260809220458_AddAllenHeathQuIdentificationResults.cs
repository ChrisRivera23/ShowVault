using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAllenHeathQuIdentificationResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AllenHeathQuIdentificationAttemptedHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AllenHeathQuIdentificationCommandId",
                table: "subnet_proposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllenHeathQuIdentificationMessage",
                table: "subnet_proposals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllenHeathQuIdentificationStatus",
                table: "subnet_proposals",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AllenHeathQuIdentifiedAt",
                table: "subnet_proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AllenHeathQuIdentifiedHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllenHeathQuIdentifiedProductFamilies",
                table: "subnet_proposals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_subnet_proposals_AllenHeathQuIdentificationCommandId",
                table: "subnet_proposals",
                column: "AllenHeathQuIdentificationCommandId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_subnet_proposals_AllenHeathQuIdentificationCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "AllenHeathQuIdentificationAttemptedHostCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "AllenHeathQuIdentificationCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "AllenHeathQuIdentificationMessage",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "AllenHeathQuIdentificationStatus",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "AllenHeathQuIdentifiedAt",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "AllenHeathQuIdentifiedHostCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "AllenHeathQuIdentifiedProductFamilies",
                table: "subnet_proposals");
        }
    }
}
