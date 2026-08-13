using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Commercial;
using ShowVault.Api.Data;
using ShowVault.Api.HostedSync;
using ShowVault.Platform.Commercial;
using ShowVault.Platform.Organizations;
using ShowVault.Platform.Venues;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class CommercialConcurrencyTests
{
    [Fact]
    public async Task Concurrent_begins_cannot_over_allocate_organization_limit()
    {
        var path = Path.Combine(Path.GetTempPath(),
            $"showvault-commercial-{Guid.NewGuid():N}.sqlite3");
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseSqlite($"Data Source={path};Default Timeout=30")
            .Options;
        try
        {
            var organization = Organization.Create("Synthetic", $"quota-{Guid.NewGuid():N}");
            var venue = Venue.Create(organization.Id, "Synthetic Venue", "UTC");
            await using (var seed = new PlatformDbContext(options))
            {
                await seed.Database.EnsureCreatedAsync();
                var now = DateTimeOffset.UtcNow;
                seed.AddRange(organization, venue,
                    Membership.Create(organization.Id, "owner", OrganizationRole.Owner),
                    new CommercialLicense
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organization.Id,
                        LicenseTypeCode = "synthetic.perpetual",
                        State = CommercialLicenseState.Active,
                        EffectiveAt = now.AddDays(-1),
                        UpdatedAt = now
                    },
                    new ServiceSubscription
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organization.Id,
                        PlanCode = SyntheticCommercialPlanPolicyCatalog.PlanCode,
                        State = ServiceSubscriptionState.Active,
                        UpdatedAt = now
                    });
                await seed.SaveChangesAsync();
            }

            const long bytes = 60L * 1024 * 1024;
            var first = ReserveAsync(options, Session(organization.Id, venue.Id, bytes), "first");
            var second = ReserveAsync(options, Session(organization.Id, venue.Id, bytes), "second");
            var decisions = await Task.WhenAll(first, second);

            Assert.Single(decisions, value =>
                value == HostedSyncReservationDecision.Created);
            Assert.Single(decisions, value =>
                value == HostedSyncReservationDecision.QuotaExceeded);
            await using var verify = new PlatformDbContext(options);
            var usage = await verify.OrganizationStorageUsages.SingleAsync();
            Assert.Equal(bytes, usage.ReservedBytes);
            Assert.Equal(0, usage.CommittedBytes);
            Assert.Single(await verify.HostedSyncReservations.ToListAsync());
            var audit = await verify.CommercialAuditEvents.FirstAsync();
            audit.Outcome = "mutated";
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                verify.SaveChangesAsync());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static async Task<HostedSyncReservationDecision> ReserveAsync(
        DbContextOptions<PlatformDbContext> options,
        HostedSyncSession session,
        string correlationId)
    {
        await using var database = new PlatformDbContext(options);
        var service = new CommercialStateService(database,
            new SyntheticCommercialPlanPolicyCatalog(), TimeProvider.System);
        return (await service.TryCreateSessionAsync(session, "owner", correlationId,
            CancellationToken.None)).Decision;
    }

    private static HostedSyncSession Session(Guid organizationId, Guid venueId, long bytes)
    {
        var id = Guid.CreateVersion7();
        return new()
        {
            Id = id,
            OrganizationId = organizationId,
            VenueId = venueId,
            RecoveryPointId = id.ToString("N").PadRight(64, 'a'),
            ManifestDigest = new string('b', 64),
            ManifestJson = "{}",
            ManifestTotalBytes = bytes,
            Status = "uploading",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
