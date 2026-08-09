using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNewTekTriCasterIdentificationResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NewTekTriCasterIdentificationAttemptedHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NewTekTriCasterIdentificationCommandId",
                table: "subnet_proposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewTekTriCasterIdentificationMessage",
                table: "subnet_proposals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewTekTriCasterIdentificationStatus",
                table: "subnet_proposals",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NewTekTriCasterIdentifiedAt",
                table: "subnet_proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NewTekTriCasterIdentifiedHostCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewTekTriCasterIdentifiedProductFamilies",
                table: "subnet_proposals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_subnet_proposals_NewTekTriCasterIdentificationCommandId",
                table: "subnet_proposals",
                column: "NewTekTriCasterIdentificationCommandId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_subnet_proposals_NewTekTriCasterIdentificationCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "NewTekTriCasterIdentificationAttemptedHostCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "NewTekTriCasterIdentificationCommandId",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "NewTekTriCasterIdentificationMessage",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "NewTekTriCasterIdentificationStatus",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "NewTekTriCasterIdentifiedAt",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "NewTekTriCasterIdentifiedHostCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "NewTekTriCasterIdentifiedProductFamilies",
                table: "subnet_proposals");
        }
    }
}
