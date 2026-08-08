using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoveryCandidateValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ValidatedAt",
                table: "recovery_candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ValidationCommandId",
                table: "recovery_candidates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValidationFileCount",
                table: "recovery_candidates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationMessage",
                table: "recovery_candidates",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationStatus",
                table: "recovery_candidates",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ValidationTruncated",
                table: "recovery_candidates",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_recovery_candidates_ValidationCommandId",
                table: "recovery_candidates",
                column: "ValidationCommandId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_recovery_candidates_ValidationCommandId",
                table: "recovery_candidates");

            migrationBuilder.DropColumn(
                name: "ValidatedAt",
                table: "recovery_candidates");

            migrationBuilder.DropColumn(
                name: "ValidationCommandId",
                table: "recovery_candidates");

            migrationBuilder.DropColumn(
                name: "ValidationFileCount",
                table: "recovery_candidates");

            migrationBuilder.DropColumn(
                name: "ValidationMessage",
                table: "recovery_candidates");

            migrationBuilder.DropColumn(
                name: "ValidationStatus",
                table: "recovery_candidates");

            migrationBuilder.DropColumn(
                name: "ValidationTruncated",
                table: "recovery_candidates");
        }
    }
}
