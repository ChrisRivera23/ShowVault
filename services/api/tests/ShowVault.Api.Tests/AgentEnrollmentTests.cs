using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
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

        var agentEvent = AgentEventEnvelope.Create(
            enrolledAgent.Payload.AgentId,
            AgentEventType.AgentConnected,
            "event-correlation",
            "{}",
            DateTimeOffset.UtcNow);
        var firstDelivery = await agentClient.PostAsJsonAsync("/api/v1/agent-events", agentEvent);
        var duplicateDelivery = await agentClient.PostAsJsonAsync("/api/v1/agent-events", agentEvent);
        Assert.Equal(HttpStatusCode.Accepted, firstDelivery.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, duplicateDelivery.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            Assert.Equal(1, await database.ReceivedAgentEvents.CountAsync(
                received => received.EventId == agentEvent.EventId));
        }

        var mismatchedEvent = agentEvent with
        {
            EventId = Guid.NewGuid(),
            AgentId = Guid.NewGuid()
        };
        var forbiddenEvent = await agentClient.PostAsJsonAsync(
            "/api/v1/agent-events",
            mismatchedEvent);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenEvent.StatusCode);

        var forbiddenCommand = await outsiderClient.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/venues/{venueId}/agents/{enrolledAgent.Payload.AgentId}/commands",
            new IssueAgentCommandRequest(AgentCommandType.StartDiscovery, "{}"));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCommand.StatusCode);

        var issueCommand = await ownerClient.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/venues/{venueId}/agents/{enrolledAgent.Payload.AgentId}/commands",
            new IssueAgentCommandRequest(AgentCommandType.StartDiscovery, "{\"scope\":\"local\"}"));
        Assert.Equal(HttpStatusCode.Accepted, issueCommand.StatusCode);
        var issuedCommand = await issueCommand.Content.ReadFromJsonAsync<
            ApiResponse<AgentCommandEnvelope>>();
        Assert.NotNull(issuedCommand);

        var poll = await agentClient.GetFromJsonAsync<
            ApiResponse<IReadOnlyList<AgentCommandEnvelope>>>("/api/v1/agent-commands");
        Assert.NotNull(poll);
        Assert.Contains(poll.Payload, command =>
            command.CommandId == issuedCommand.Payload.CommandId);

        var firstAcknowledgement = await agentClient.PostAsync(
            $"/api/v1/agent-commands/{issuedCommand.Payload.CommandId}/acknowledge",
            null);
        var duplicateAcknowledgement = await agentClient.PostAsync(
            $"/api/v1/agent-commands/{issuedCommand.Payload.CommandId}/acknowledge",
            null);
        Assert.Equal(HttpStatusCode.NoContent, firstAcknowledgement.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, duplicateAcknowledgement.StatusCode);

        var afterAcknowledgement = await agentClient.GetFromJsonAsync<
            ApiResponse<IReadOnlyList<AgentCommandEnvelope>>>("/api/v1/agent-commands");
        Assert.NotNull(afterAcknowledgement);
        Assert.DoesNotContain(afterAcknowledgement.Payload, command =>
            command.CommandId == issuedCommand.Payload.CommandId);

        var forbiddenHistory = await outsiderClient.GetAsync(
            $"/api/v1/organizations/{organizationId}/venues/{venueId}/recovery-runs");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenHistory.StatusCode);
        var recoveryHistory = await ownerClient.GetFromJsonAsync<
            ApiResponse<IReadOnlyList<RecoveryRunSummary>>>(
                $"/api/v1/organizations/{organizationId}/venues/{venueId}/recovery-runs");
        Assert.NotNull(recoveryHistory);
        var recoveryRun = Assert.Single(recoveryHistory.Payload);
        Assert.Equal(issuedCommand.Payload.CommandId, recoveryRun.DiscoveryCommandId);
        Assert.Equal("in_progress", recoveryRun.Status);

        var agents = await ownerClient.GetFromJsonAsync<
            ApiResponse<IReadOnlyList<VenueAgentSummary>>>(
                $"/api/v1/organizations/{organizationId}/venues/{venueId}/agents");
        Assert.NotNull(agents);
        Assert.Equal(enrolledAgent.Payload.AgentId, Assert.Single(agents.Payload).Id);

        var workflowBase = $"/api/v1/organizations/{organizationId}/venues/{venueId}" +
            $"/agents/{enrolledAgent.Payload.AgentId}/recovery";
        var discoveryCommand = await PostWorkflowCommandAsync(
            ownerClient,
            $"{workflowBase}/discover",
            new StartRecoveryDiscoveryRequest("showvault.filesystem", "/approved/show", 250));
        Assert.Equal(AgentCommandType.StartDiscovery, discoveryCommand.Type);

        var backupCommand = await PostWorkflowCommandAsync(
            ownerClient,
            $"{workflowBase}/backup",
            new CreateRecoveryBackupRequest(discoveryCommand.CommandId));
        Assert.Equal(AgentCommandType.CreateBackup, backupCommand.Type);

        var verifyCommand = await PostWorkflowCommandAsync(
            ownerClient,
            $"{workflowBase}/verify",
            new VerifyRecoveryBackupRequest(backupCommand.CommandId));
        Assert.Equal(AgentCommandType.VerifyBackup, verifyCommand.Type);

        var restoreCommand = await PostWorkflowCommandAsync(
            ownerClient,
            $"{workflowBase}/restore",
            new StartRecoveryRestoreRequest(
                backupCommand.CommandId,
                verifyCommand.CommandId,
                "/approved/restore"));
        Assert.Equal(AgentCommandType.StartRestore, restoreCommand.Type);

        var secondBackupCommand = await PostWorkflowCommandAsync(
            ownerClient,
            $"{workflowBase}/backup",
            new CreateRecoveryBackupRequest(discoveryCommand.CommandId));
        var mismatchedRestore = await ownerClient.PostAsJsonAsync(
            $"{workflowBase}/restore",
            new StartRecoveryRestoreRequest(
                secondBackupCommand.CommandId,
                verifyCommand.CommandId,
                "/approved/restore-two"));
        Assert.Equal(HttpStatusCode.BadRequest, mismatchedRestore.StatusCode);

        var invalidDependency = await ownerClient.PostAsJsonAsync(
            $"{workflowBase}/backup",
            new CreateRecoveryBackupRequest(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, invalidDependency.StatusCode);

        var forbiddenWorkflow = await outsiderClient.PostAsJsonAsync(
            $"{workflowBase}/discover",
            new StartRecoveryDiscoveryRequest("showvault.filesystem", "/approved/show"));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenWorkflow.StatusCode);

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

    private static async Task<AgentCommandEnvelope> PostWorkflowCommandAsync<TRequest>(
        HttpClient client,
        string path,
        TRequest request)
    {
        var response = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var command = await response.Content.ReadFromJsonAsync<ApiResponse<AgentCommandEnvelope>>();
        Assert.NotNull(command);
        return command.Payload;
    }
}
