using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ShowVault.Api.Contracts;
using ShowVault.AgentContracts;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class AgentEnrollmentTests(TenantApiFactory factory)
    : IClassFixture<TenantApiFactory>
{
    [Fact]
    public async Task Enrollment_is_single_use_and_agent_credentials_are_revocable()
    {
        using var ownerClient = CreateHumanClient("auth0|agent-owner");
        using var outsiderClient = CreateHumanClient("auth0|agent-outsider");
        var organizationId = await CreateOrganizationAsync(ownerClient);
        var venueId = await CreateVenueAsync(ownerClient, organizationId);

        var forbiddenEnrollment = await outsiderClient.PostAsync(
            $"/api/v1/organizations/{organizationId}/venues/{venueId}/agent-enrollments",
            null);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenEnrollment.StatusCode);

        var enrollmentResponse = await ownerClient.PostAsync(
            $"/api/v1/organizations/{organizationId}/venues/{venueId}/agent-enrollments",
            null);
        Assert.Equal(HttpStatusCode.Created, enrollmentResponse.StatusCode);
        Assert.Equal("no-store", enrollmentResponse.Headers.CacheControl?.ToString());
        var enrollment = await enrollmentResponse.Content.ReadFromJsonAsync<
            ApiResponse<CreateAgentEnrollmentResponse>>();
        Assert.NotNull(enrollment);
        Assert.StartsWith("sve_", enrollment.Payload.EnrollmentCode);

        var enrollResponse = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/agents/enroll",
            new EnrollAgentRequest(enrollment.Payload.EnrollmentCode, "Main Control Agent"));
        Assert.Equal(HttpStatusCode.OK, enrollResponse.StatusCode);
        Assert.Equal("no-store", enrollResponse.Headers.CacheControl?.ToString());
        var enrolledAgent = await enrollResponse.Content.ReadFromJsonAsync<
            ApiResponse<EnrollAgentResponse>>();
        Assert.NotNull(enrolledAgent);
        Assert.Equal(venueId, enrolledAgent.Payload.VenueId);

        var reusedEnrollment = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/agents/enroll",
            new EnrollAgentRequest(enrollment.Payload.EnrollmentCode, "Replay Agent"));
        Assert.Equal(HttpStatusCode.Unauthorized, reusedEnrollment.StatusCode);

        using var agentClient = CreateAgentClient(enrolledAgent.Payload.Credential);
        var identity = await agentClient.GetFromJsonAsync<ApiResponse<AgentIdentityResponse>>(
            "/api/v1/agent-identity");
        Assert.NotNull(identity);
        Assert.Equal(enrolledAgent.Payload.AgentId, identity.Payload.AgentId);
        Assert.Equal(venueId, identity.Payload.VenueId);

        using var invalidAgentClient = CreateAgentClient(
            $"{enrolledAgent.Payload.AgentId}.sva_invalid");
        var invalidIdentity = await invalidAgentClient.GetAsync("/api/v1/agent-identity");
        Assert.Equal(HttpStatusCode.Unauthorized, invalidIdentity.StatusCode);

        var rotationResponse = await agentClient.PostAsync(
            "/api/v1/agents/rotate-credential",
            null);
        Assert.Equal(HttpStatusCode.OK, rotationResponse.StatusCode);
        Assert.Equal("no-store", rotationResponse.Headers.CacheControl?.ToString());
        var rotation = await rotationResponse.Content.ReadFromJsonAsync<
            ApiResponse<RotateAgentCredentialResponse>>();
        Assert.NotNull(rotation);

        var replacedIdentity = await agentClient.GetAsync("/api/v1/agent-identity");
        Assert.Equal(HttpStatusCode.Unauthorized, replacedIdentity.StatusCode);

        using var rotatedAgentClient = CreateAgentClient(rotation.Payload.Credential);
        var rotatedIdentity = await rotatedAgentClient.GetAsync("/api/v1/agent-identity");
        Assert.Equal(HttpStatusCode.OK, rotatedIdentity.StatusCode);

        var revokeResponse = await ownerClient.DeleteAsync(
            $"/api/v1/organizations/{organizationId}/venues/{venueId}/agents/{enrolledAgent.Payload.AgentId}");
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        var revokedIdentity = await rotatedAgentClient.GetAsync("/api/v1/agent-identity");
        Assert.Equal(HttpStatusCode.Unauthorized, revokedIdentity.StatusCode);
    }

    private HttpClient CreateHumanClient(string subject)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        return client;
    }

    private HttpClient CreateAgentClient(string credential)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("ShowVault-Agent", credential);
        return client;
    }

    private static async Task<Guid> CreateOrganizationAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/organizations",
            new CreateOrganizationRequest("Agent Test Group", $"agent-test-{Guid.NewGuid():N}"));
        response.EnsureSuccessStatusCode();
        var organization = await response.Content.ReadFromJsonAsync<
            ApiResponse<OrganizationSummary>>();
        Assert.NotNull(organization);
        return organization.Payload.Id;
    }

    private static async Task<Guid> CreateVenueAsync(HttpClient client, Guid organizationId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/venues",
            new CreateVenueRequest("Main Room", "America/New_York"));
        response.EnsureSuccessStatusCode();
        var venue = await response.Content.ReadFromJsonAsync<ApiResponse<VenueSummary>>();
        Assert.NotNull(venue);
        return venue.Payload.Id;
    }
}
