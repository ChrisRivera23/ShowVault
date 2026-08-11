using ShowVault.Platform.Venues;
using Xunit;

namespace ShowVault.Platform.Tests;

public sealed class VenueTests
{
    [Fact]
    public void Create_requires_an_organization_and_valid_time_zone()
    {
        var organizationId = Guid.NewGuid();
        var venue = Venue.Create(organizationId, "Main Room", "America/New_York");

        Assert.Equal(organizationId, venue.OrganizationId);
        Assert.Equal("Main Room", venue.Name);
        Assert.Equal("America/New_York", venue.TimeZoneId);
    }

    [Fact]
    public void Create_rejects_an_unknown_time_zone()
    {
        Assert.Throws<ArgumentException>(() => Venue.Create(
            Guid.NewGuid(),
            "Main Room",
            "Not/A-TimeZone"));
    }
}
