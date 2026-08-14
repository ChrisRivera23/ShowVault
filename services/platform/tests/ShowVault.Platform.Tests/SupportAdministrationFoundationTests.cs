using ShowVault.Platform.Support;
using Xunit;

namespace ShowVault.Platform.Tests;

public sealed class SupportAdministrationFoundationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-13T12:00:00Z");

    [Fact]
    public void Staff_assignment_is_issuer_bound_revisioned_and_revocation_is_terminal()
    {
        var assignment = SupportStaffAssignment.Create(
            " https://identity.showvault.test/ ", " staff|reader ", Now);

        Assert.Equal("https://identity.showvault.test/", assignment.IdentityIssuer);
        Assert.Equal("staff|reader", assignment.IdentitySubject);
        Assert.Equal(SupportStaffRole.SupportReader, assignment.Role);
        Assert.Equal(SupportStaffAssignmentState.Active, assignment.State);
        Assert.Equal(1, assignment.Revision);

        assignment.Suspend(1, Now.AddMinutes(1));
        assignment.Restore(2, Now.AddMinutes(2));
        assignment.Revoke(3, Now.AddMinutes(3));

        Assert.Equal(SupportStaffAssignmentState.Revoked, assignment.State);
        Assert.Equal(4, assignment.Revision);
        Assert.Throws<InvalidOperationException>(() =>
            assignment.Restore(4, Now.AddMinutes(4)));
    }

    [Theory]
    [InlineData("http://identity.showvault.test/")]
    [InlineData("https://user@identity.showvault.test/")]
    [InlineData("https://identity.showvault.test/?tenant=fixture")]
    [InlineData("https://identity.showvault.test/#fragment")]
    public void Staff_assignment_rejects_unsafe_issuers(string issuer) =>
        Assert.Throws<ArgumentException>(() =>
            SupportStaffAssignment.Create(issuer, "staff|reader", Now));

    [Fact]
    public void Staff_assignment_rejects_invalid_identity_revision_and_time()
    {
        Assert.Throws<ArgumentException>(() => SupportStaffAssignment.Create(
            $"https://identity.showvault.test/{new string('i', 230)}", "staff|reader", Now));
        Assert.Throws<ArgumentException>(() => SupportStaffAssignment.Create(
            "https://identity.showvault.test/", new string('s', 256), Now));
        Assert.Throws<ArgumentException>(() => SupportStaffAssignment.Create(
            "https://identity.showvault.test/", "staff\nreader", Now));

        var assignment = SupportStaffAssignment.Create(
            "https://identity.showvault.test/", "staff|reader", Now);
        Assert.Throws<InvalidOperationException>(() =>
            assignment.Suspend(2, Now.AddMinutes(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            assignment.Suspend(1, Now.AddTicks(-1)));
    }

    [Fact]
    public void Organization_grant_is_revisioned_and_revocation_is_terminal()
    {
        var grant = SupportOrganizationGrant.Create(
            Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.Equal(SupportOrganizationGrantState.Active, grant.State);
        Assert.Equal(1, grant.Revision);
        grant.Revoke(1, Now.AddMinutes(1));
        Assert.Equal(SupportOrganizationGrantState.Revoked, grant.State);
        Assert.Equal(2, grant.Revision);
        Assert.Throws<InvalidOperationException>(() =>
            grant.Revoke(2, Now.AddMinutes(2)));
    }

    [Fact]
    public void Organization_grant_rejects_empty_ids_stale_revision_and_reverse_time()
    {
        Assert.Throws<ArgumentException>(() => SupportOrganizationGrant.Create(
            Guid.Empty, Guid.NewGuid(), Now));
        Assert.Throws<ArgumentException>(() => SupportOrganizationGrant.Create(
            Guid.NewGuid(), Guid.Empty, Now));
        var grant = SupportOrganizationGrant.Create(Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.Throws<InvalidOperationException>(() =>
            grant.Revoke(2, Now.AddMinutes(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            grant.Revoke(1, Now.AddTicks(-1)));
    }

    [Fact]
    public void Support_audit_is_bounded_issuer_bound_and_allows_no_target()
    {
        var audit = SupportAuditEvent.Create(null,
            "https://identity.showvault.test/", "staff|reader",
            " support_overview_read ", " denied ", " support_target_unavailable ",
            " correlation-fixture ", " support-v1 ", Now);

        Assert.Null(audit.OrganizationId);
        Assert.Equal("https://identity.showvault.test/", audit.ActorIssuer);
        Assert.Equal("staff|reader", audit.ActorSubject);
        Assert.Equal("support_overview_read", audit.Action);
        Assert.Equal("denied", audit.Outcome);
        Assert.Equal("support_target_unavailable", audit.ReasonCode);
        Assert.Equal("correlation-fixture", audit.CorrelationId);
        Assert.Equal("support-v1", audit.PolicyVersion);
    }

    [Fact]
    public void Support_audit_rejects_empty_target_and_out_of_bounds_fields()
    {
        Assert.Throws<ArgumentException>(() => SupportAuditEvent.Create(Guid.Empty,
            "https://identity.showvault.test/", "staff|reader", "read", "allowed",
            "authorized", "correlation-fixture", "support-v1", Now));
        Assert.Throws<ArgumentException>(() => SupportAuditEvent.Create(null,
            "https://identity.showvault.test/", "staff|reader", new string('a', 81),
            "allowed", "authorized", "correlation-fixture", "support-v1", Now));
    }
}
