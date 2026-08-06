using ShowVault.Platform.Organizations;
using Xunit;

namespace ShowVault.Platform.Tests;

public sealed class OrganizationTests
{
    [Fact]
    public void Create_normalizes_name_and_slug()
    {
        var organization = Organization.Create("  Example Venue Group  ", "Example-Venues");

        Assert.Equal("Example Venue Group", organization.Name);
        Assert.Equal("example-venues", organization.Slug);
        Assert.NotEqual(Guid.Empty, organization.Id);
    }

    [Theory]
    [InlineData("invalid slug")]
    [InlineData("invalid--slug")]
    [InlineData("-invalid")]
    public void Create_rejects_invalid_slugs(string slug)
    {
        Assert.Throws<ArgumentException>(() => Organization.Create("Example", slug));
    }

    [Fact]
    public void Membership_uses_the_external_identity_subject()
    {
        var organizationId = Guid.NewGuid();
        var membership = Membership.Create(
            organizationId,
            "auth0|user-123",
            OrganizationRole.Owner);

        Assert.Equal(organizationId, membership.OrganizationId);
        Assert.Equal("auth0|user-123", membership.IdentitySubject);
        Assert.Equal(OrganizationRole.Owner, membership.Role);
    }
}
