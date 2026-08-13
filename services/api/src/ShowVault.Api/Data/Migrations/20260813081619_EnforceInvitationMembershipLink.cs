using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShowVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceInvitationMembershipLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_organization_invitations_AcceptedMembershipId",
                table: "organization_invitations",
                column: "AcceptedMembershipId");

            migrationBuilder.AddForeignKey(
                name: "FK_organization_invitations_memberships_AcceptedMembershipId",
                table: "organization_invitations",
                column: "AcceptedMembershipId",
                principalTable: "memberships",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_organization_invitations_memberships_AcceptedMembershipId",
                table: "organization_invitations");

            migrationBuilder.DropIndex(
                name: "IX_organization_invitations_AcceptedMembershipId",
                table: "organization_invitations");
        }
    }
}
