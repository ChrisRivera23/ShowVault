using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Platform.Organizations;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class TenantIsolationTests(TenantApiFactory factory)
    : IClassFixture<TenantApiFactory>
{
    [Fact]
    public async Task Organization_and_venue_access_is_scoped_by_membership()
    {
        using var ownerClient = CreateClient("auth0|owner");
        using var outsiderClient = CreateClient("auth0|outsider");

        var createOrganizationResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/organizations",
            new CreateOrganizationRequest("Example Venue Group", "example-venues"));
        Assert.Equal(HttpStatusCode.Created, createOrganizationResponse.StatusCode);

        var createdOrganization = await createOrganizationResponse.Content
            .ReadFromJsonAsync<ApiResponse<OrganizationSummary>>();
        Assert.NotNull(createdOrganization);
        var organizationId = createdOrganization.Payload.Id;

        var createVenueResponse = await ownerClient.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/venues",
            new CreateVenueRequest("Main Room", "America/New_York"));
        Assert.Equal(HttpStatusCode.Created, createVenueResponse.StatusCode);

        var ownerOrganizationsResponse = await ownerClient.GetAsync("/api/v1/organizations");
        Assert.True(
            ownerOrganizationsResponse.IsSuccessStatusCode,
            await ownerOrganizationsResponse.Content.ReadAsStringAsync());
        var ownerOrganizations = await ownerOrganizationsResponse.Content.ReadFromJsonAsync<
            ApiResponse<IReadOnlyList<OrganizationSummary>>>();
        Assert.NotNull(ownerOrganizations);
        Assert.Single(ownerOrganizations.Payload);

        var outsiderOrganizations = await outsiderClient.GetFromJsonAsync<
            ApiResponse<IReadOnlyList<OrganizationSummary>>>("/api/v1/organizations");
        Assert.NotNull(outsiderOrganizations);
        Assert.Empty(outsiderOrganizations.Payload);

        var forbiddenVenues = await outsiderClient.GetAsync(
            $"/api/v1/organizations/{organizationId}/venues");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenVenues.StatusCode);

        await AddMembershipAsync(organizationId, "auth0|outsider", OrganizationRole.Viewer);

        var visibleVenues = await outsiderClient.GetFromJsonAsync<
            ApiResponse<IReadOnlyList<VenueSummary>>>(
            $"/api/v1/organizations/{organizationId}/venues");
        Assert.NotNull(visibleVenues);
        Assert.Single(visibleVenues.Payload);

        var forbiddenCreate = await outsiderClient.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/venues",
            new CreateVenueRequest("Second Room", "America/New_York"));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);
    }

    [Fact]
    public async Task Personal_beta_identity_cannot_create_an_organization()
    {
        var slug = $"personal-beta-{Guid.NewGuid():N}";
        using var client = CreateClient(
            "auth0|personal-beta",
            ShowVault.Api.Security.PersonalBetaAuthenticationHandler.SchemeName);

        var response = await client.PostAsJsonAsync(
            "/api/v1/organizations",
            new CreateOrganizationRequest("Personal beta denied", slug));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(database.Organizations.Any(value => value.Slug == slug));
    }

    private HttpClient CreateClient(string subject, string? authenticationType = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        if (authenticationType is not null)
            client.DefaultRequestHeaders.Add("X-Test-Authentication-Type", authenticationType);
        return client;
    }

    private async Task AddMembershipAsync(
        Guid organizationId,
        string subject,
        OrganizationRole role)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        database.Memberships.Add(Membership.Create(
            organizationId, subject, role, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
    }
}
