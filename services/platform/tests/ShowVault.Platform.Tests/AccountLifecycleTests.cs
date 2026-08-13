using ShowVault.Platform.Organizations;
using Xunit;

namespace ShowVault.Platform.Tests;

public sealed class AccountLifecycleTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-13T12:00:00Z");

    [Fact]
    public void Membership_lifecycle_is_revisioned_and_revocation_is_terminal()
    {
        var membership = Membership.Create(Guid.NewGuid(), "auth0|member",
            OrganizationRole.Technician, Now, "  Lighting desk  ");

        Assert.Equal("Lighting desk", membership.DisplayLabel);
        Assert.Equal(MembershipState.Active, membership.State);
        Assert.Equal(1, membership.Revision);

        membership.ChangeRole(OrganizationRole.Manager, 1, Now.AddMinutes(1));
        membership.Suspend(2, Now.AddMinutes(2));
        membership.ChangeRole(OrganizationRole.Viewer, 3, Now.AddMinutes(3));
        membership.Restore(4, Now.AddMinutes(4));
        membership.Revoke(5, Now.AddMinutes(5));

        Assert.Equal(MembershipState.Revoked, membership.State);
        Assert.Equal(OrganizationRole.Viewer, membership.Role);
        Assert.Equal(6, membership.Revision);
        Assert.Throws<InvalidOperationException>(() =>
            membership.Restore(6, Now.AddMinutes(6)));
    }

    [Fact]
    public void Membership_denies_owner_mutation_stale_revision_and_reverse_time()
    {
        var owner = Membership.Create(Guid.NewGuid(), "auth0|owner",
            OrganizationRole.Owner, Now);
        Assert.Throws<InvalidOperationException>(() =>
            owner.Suspend(1, Now.AddMinutes(1)));

        var member = Membership.Create(Guid.NewGuid(), "auth0|member",
            OrganizationRole.Viewer, Now);
        Assert.Throws<InvalidOperationException>(() =>
            member.ChangeRole(OrganizationRole.Owner, 1, Now.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() =>
            member.Suspend(2, Now.AddMinutes(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            member.Suspend(1, Now.AddTicks(-1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(81)]
    public void Membership_rejects_labels_outside_boundary(int length)
    {
        var label = length == 0 ? " " : new string('x', length);
        Assert.Throws<ArgumentException>(() => Membership.Create(
            Guid.NewGuid(), "auth0|member", OrganizationRole.Viewer, Now, label));
    }

    [Fact]
    public void Invitation_accepts_once_and_records_only_bounded_identity_linkage()
    {
        var invitation = Invitation();
        var membershipId = Guid.NewGuid();

        invitation.Accept(membershipId, "auth0|accepted", 1, Now.AddHours(1));

        Assert.Equal(OrganizationInvitationState.Accepted, invitation.State);
        Assert.Equal(membershipId, invitation.AcceptedMembershipId);
        Assert.Equal("auth0|accepted", invitation.AcceptedBySubject);
        Assert.Equal(2, invitation.Revision);
        Assert.Throws<InvalidOperationException>(() =>
            invitation.Accept(Guid.NewGuid(), "auth0|other", 2, Now.AddHours(2)));
    }

    [Fact]
    public void Invitation_expires_at_exact_boundary_and_terminal_state_does_not_change()
    {
        var invitation = Invitation();
        invitation.ObserveExpiry(Now.AddDays(7));
        Assert.Equal(OrganizationInvitationState.Expired, invitation.State);
        Assert.Equal(2, invitation.Revision);
        invitation.ObserveExpiry(Now.AddDays(8));
        Assert.Equal(2, invitation.Revision);
        Assert.Throws<InvalidOperationException>(() =>
            invitation.Revoke(2, Now.AddDays(8)));
    }

    [Fact]
    public void Invitation_rejects_owner_and_non_32_byte_digest()
    {
        Assert.Throws<ArgumentException>(() => OrganizationInvitation.Create(
            Guid.NewGuid(), "Owner", OrganizationRole.Owner, new byte[32], "active",
            "auth0|owner", Now, Now.AddDays(7)));
        Assert.Throws<ArgumentException>(() => OrganizationInvitation.Create(
            Guid.NewGuid(), "Viewer", OrganizationRole.Viewer, new byte[31], "active",
            "auth0|owner", Now, Now.AddDays(7)));
    }

    private static OrganizationInvitation Invitation() => OrganizationInvitation.Create(
        Guid.NewGuid(), "Guest operator", OrganizationRole.Technician,
        Enumerable.Range(0, 32).Select(value => (byte)value).ToArray(),
        "fixture-active", "auth0|owner", Now, Now.AddDays(7));
}
