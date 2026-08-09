using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubnetDiscoveryTargetDiagnostics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FallbackTargetCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PassiveCandidateCount",
                table: "subnet_proposals",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FallbackTargetCount",
                table: "subnet_proposals");

            migrationBuilder.DropColumn(
                name: "PassiveCandidateCount",
                table: "subnet_proposals");
        }
    }
}
