using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShowVault.Api.Data;
using ShowVault.Api.Support;
using ShowVault.Platform.Organizations;
using ShowVault.Platform.Support;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class SupportAdministrationPersistenceTests(TenantApiFactory factory)
    : IClassFixture<TenantApiFactory>
{
    [Fact]
    public async Task Foundation_persists_exact_authority_grant_and_minimized_audit()
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var now = DateTimeOffset.Parse("2026-08-13T12:00:00Z");
        var organization = Organization.Create("Synthetic support organization",
            $"support-{Guid.NewGuid():N}");
        var assignment = SupportStaffAssignment.Create(
            "https://identity.showvault.test/", $"staff|{Guid.NewGuid():N}", now);
        var grant = SupportOrganizationGrant.Create(assignment.Id, organization.Id, now);
        var audit = SupportAuditEvent.Create(organization.Id,
            assignment.IdentityIssuer, assignment.IdentitySubject,
            "support_overview_read", "allowed", "authorized",
            "correlation-fixture", "support-v1", now);
        database.AddRange(organization, assignment, grant, audit);

        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        var storedAssignment = await database.SupportStaffAssignments.SingleAsync(value =>
            value.Id == assignment.Id);
        var storedGrant = await database.SupportOrganizationGrants.SingleAsync(value =>
            value.Id == grant.Id);
        var storedAudit = await database.SupportAuditEvents.SingleAsync(value =>
            value.Id == audit.Id);
        Assert.Equal(SupportStaffRole.SupportReader, storedAssignment.Role);
        Assert.Equal(SupportStaffAssignmentState.Active, storedAssignment.State);
        Assert.Equal(SupportOrganizationGrantState.Active, storedGrant.State);
        Assert.Equal(organization.Id, storedAudit.OrganizationId);
        Assert.Equal("authorized", storedAudit.ReasonCode);
    }

    [Fact]
    public async Task Model_enforces_identity_and_grant_uniqueness()
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var now = DateTimeOffset.UtcNow;
        var organization = Organization.Create("Synthetic uniqueness organization",
            $"unique-{Guid.NewGuid():N}");
        var subject = $"staff|{Guid.NewGuid():N}";
        var assignment = SupportStaffAssignment.Create(
            "https://identity.showvault.test/", subject, now);
        database.AddRange(organization, assignment,
            SupportOrganizationGrant.Create(assignment.Id, organization.Id, now));
        await database.SaveChangesAsync();

        database.SupportStaffAssignments.Add(SupportStaffAssignment.Create(
            "https://identity.showvault.test/", subject, now));
        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        database.ChangeTracker.Clear();

        database.SupportOrganizationGrants.Add(SupportOrganizationGrant.Create(
            assignment.Id, organization.Id, now));
        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
    }

    [Fact]
    public async Task Support_audit_is_append_only_for_async_and_sync_saves()
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = SupportAuditEvent.Create(null,
            "https://identity.showvault.test/", $"staff|{Guid.NewGuid():N}",
            "support_overview_read", "denied", "support_target_unavailable",
            "correlation-fixture", "support-v1", DateTimeOffset.UtcNow);
        database.SupportAuditEvents.Add(audit);
        await database.SaveChangesAsync();

        database.Entry(audit).Property(value => value.ReasonCode).CurrentValue = "changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => database.SaveChangesAsync());
        Assert.Throws<InvalidOperationException>(() => database.SaveChanges());
    }

    [Fact]
    public void Model_uses_concurrency_unique_indexes_and_restrictive_relationships()
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var assignment = database.Model.FindEntityType(typeof(SupportStaffAssignment))!;
        var grant = database.Model.FindEntityType(typeof(SupportOrganizationGrant))!;
        var audit = database.Model.FindEntityType(typeof(SupportAuditEvent))!;

        Assert.True(assignment.FindProperty(nameof(SupportStaffAssignment.Revision))!.IsConcurrencyToken);
        Assert.True(grant.FindProperty(nameof(SupportOrganizationGrant.Revision))!.IsConcurrencyToken);
        Assert.Contains(assignment.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(SupportStaffAssignment.IdentityIssuer),
                 nameof(SupportStaffAssignment.IdentitySubject)]));
        Assert.Contains(grant.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(SupportOrganizationGrant.StaffAssignmentId),
                 nameof(SupportOrganizationGrant.OrganizationId)]));
        Assert.All(grant.GetForeignKeys(), foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        Assert.All(audit.GetForeignKeys(), foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    [Fact]
    public void Checked_in_support_configuration_is_disabled_and_empty()
    {
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<SupportAdminOptions>>().Value;

        Assert.False(options.Enabled);
        Assert.Null(options.Authority);
        Assert.Null(options.Audience);
    }
}
