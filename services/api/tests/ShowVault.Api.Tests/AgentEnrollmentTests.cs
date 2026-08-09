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

        var candidateId = Guid.NewGuid();
        var candidateEvent = AgentEventEnvelope.Create(
            enrolledAgent.Payload.AgentId,
            AgentEventType.JobCompleted,
            "inventory-correlation",
            System.Text.Json.JsonSerializer.Serialize(new
            {
                subnetProposals = new[]
                {
                    new
                    {
                        proposalId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        network = "192.168.10.0",
                        prefixLength = 24,
                        interfaceType = "Ethernet",
                        evidence = "Active Ethernet interface; no hosts were contacted"
                    },
                    new
                    {
                        proposalId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                        network = "10.0.0.0",
                        prefixLength = 16,
                        interfaceType = "Ethernet",
                        evidence = "Invalid overbroad private proposal"
                    }
                },
                recoveryCandidates = new[]
                {
                    new
                    {
                        candidateId,
                        pluginId = "showvault.resolume",
                        productName = "Resolume Arena",
                        candidateType = "UserDataRoot",
                        evidence = "Standard Resolume user-data location"
                    }
                }
            }),
            DateTimeOffset.UtcNow);
        Assert.Equal(
            HttpStatusCode.Accepted,
            (await agentClient.PostAsJsonAsync("/api/v1/agent-events", candidateEvent)).StatusCode);
        var proposalId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var proposalPath = $"/api/v1/organizations/{organizationId}/venues/{venueId}/subnet-proposals";
        Assert.Equal(HttpStatusCode.Forbidden, (await outsiderClient.GetAsync(proposalPath)).StatusCode);
        var proposals = await ownerClient.GetFromJsonAsync<ApiResponse<IReadOnlyList<SubnetProposalSummary>>>(proposalPath);
        Assert.Equal("192.168.10.0", Assert.Single(proposals!.Payload).Network);
        Assert.Equal(HttpStatusCode.NoContent, (await ownerClient.PutAsJsonAsync(
            $"{proposalPath}/{proposalId}/decision", new DecideSubnetProposalRequest(true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ownerClient.PostAsJsonAsync(
            $"{proposalPath}/{proposalId}/discover", new DiscoverSubnetRequest(33, 500))).StatusCode);
        var subnetDiscoveryResponse = await ownerClient.PostAsJsonAsync(
            $"{proposalPath}/{proposalId}/discover", new DiscoverSubnetRequest(32, 500));
        Assert.Equal(HttpStatusCode.Accepted, subnetDiscoveryResponse.StatusCode);
        var subnetCommand = (await subnetDiscoveryResponse.Content.ReadFromJsonAsync<
            ApiResponse<AgentCommandEnvelope>>())!.Payload;
        Assert.Equal(AgentCommandType.DiscoverApprovedSubnet, subnetCommand.Type);
        Assert.DoesNotContain("192.168", subnetCommand.Payload, StringComparison.Ordinal);
        var subnetOutcome = new AgentEventEnvelope(
            subnetCommand.CommandId,
            enrolledAgent.Payload.AgentId,
            AgentEventType.JobCompleted,
            AgentProtocol.Version,
            DateTimeOffset.UtcNow,
            subnetCommand.CorrelationId,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                proposalId,
                attemptedHostCount = 32,
                respondingHostCount = 3,
                passiveCandidateCount = 1,
                fallbackTargetCount = 31
            }));
        Assert.Equal(HttpStatusCode.Accepted,
            (await agentClient.PostAsJsonAsync("/api/v1/agent-events", subnetOutcome)).StatusCode);
        proposals = await ownerClient.GetFromJsonAsync<ApiResponse<IReadOnlyList<SubnetProposalSummary>>>(proposalPath);
        var discoveredProposal = Assert.Single(proposals!.Payload);
        Assert.Equal("completed", discoveredProposal.DiscoveryStatus);
        Assert.Equal(32, discoveredProposal.AttemptedHostCount);
        Assert.Equal(3, discoveredProposal.RespondingHostCount);
        Assert.Equal(1, discoveredProposal.PassiveCandidateCount);
        Assert.Equal(31, discoveredProposal.FallbackTargetCount);
        var identifyPath = $"{proposalPath}/{proposalId}/identify-ma-lighting";
        Assert.Equal(HttpStatusCode.Forbidden, (await outsiderClient.PostAsJsonAsync(
            identifyPath, new IdentifyMaLightingRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ownerClient.PostAsJsonAsync(
            identifyPath, new IdentifyMaLightingRequest(501))).StatusCode);
        var identificationResponse = await ownerClient.PostAsJsonAsync(
            identifyPath, new IdentifyMaLightingRequest(500));
        Assert.Equal(HttpStatusCode.Accepted, identificationResponse.StatusCode);
        var identificationCommand = (await identificationResponse.Content.ReadFromJsonAsync<
            ApiResponse<AgentCommandEnvelope>>())!.Payload;
        Assert.Equal(AgentCommandType.IdentifyMaLighting, identificationCommand.Type);
        Assert.Contains(subnetCommand.CommandId.ToString(), identificationCommand.Payload,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("192.168", identificationCommand.Payload, StringComparison.Ordinal);
        var identificationOutcome = new AgentEventEnvelope(
            identificationCommand.CommandId,
            enrolledAgent.Payload.AgentId,
            AgentEventType.JobCompleted,
            AgentProtocol.Version,
            DateTimeOffset.UtcNow,
            identificationCommand.CorrelationId,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                proposalId,
                discoveryCommandId = subnetCommand.CommandId,
                attemptedHostCount = 3,
                identifiedHostCount = 1,
                productFamilies = new[] { "grandMA3" }
            }));
        Assert.Equal(HttpStatusCode.Accepted,
            (await agentClient.PostAsJsonAsync("/api/v1/agent-events", identificationOutcome)).StatusCode);
        proposals = await ownerClient.GetFromJsonAsync<ApiResponse<IReadOnlyList<SubnetProposalSummary>>>(proposalPath);
        var identifiedProposal = Assert.Single(proposals!.Payload);
        Assert.Equal("completed", identifiedProposal.IdentificationStatus);
        Assert.Equal(3, identifiedProposal.IdentificationAttemptedHostCount);
        Assert.Equal(1, identifiedProposal.IdentifiedHostCount);
        Assert.Equal("grandMA3", identifiedProposal.IdentifiedProductFamilies);
        var yamahaPath = $"{proposalPath}/{proposalId}/identify-yamaha-dme";
        Assert.Equal(HttpStatusCode.Forbidden, (await outsiderClient.PostAsJsonAsync(
            yamahaPath, new IdentifyYamahaDmeRequest())).StatusCode);
        var yamahaResponse = await ownerClient.PostAsJsonAsync(
            yamahaPath, new IdentifyYamahaDmeRequest(500));
        Assert.Equal(HttpStatusCode.Accepted, yamahaResponse.StatusCode);
        var yamahaCommand = (await yamahaResponse.Content.ReadFromJsonAsync<
            ApiResponse<AgentCommandEnvelope>>())!.Payload;
        Assert.Equal(AgentCommandType.IdentifyYamahaDme, yamahaCommand.Type);
        Assert.Contains(subnetCommand.CommandId.ToString(), yamahaCommand.Payload,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("192.168", yamahaCommand.Payload, StringComparison.Ordinal);
        var yamahaOutcome = new AgentEventEnvelope(
            yamahaCommand.CommandId,
            enrolledAgent.Payload.AgentId,
            AgentEventType.JobCompleted,
            AgentProtocol.Version,
            DateTimeOffset.UtcNow,
            yamahaCommand.CorrelationId,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                proposalId,
                discoveryCommandId = subnetCommand.CommandId,
                attemptedHostCount = 3,
                identifiedHostCount = 1,
                productFamilies = new[] { "Yamaha DME7" }
            }));
        Assert.Equal(HttpStatusCode.Accepted,
            (await agentClient.PostAsJsonAsync("/api/v1/agent-events", yamahaOutcome)).StatusCode);
        proposals = await ownerClient.GetFromJsonAsync<ApiResponse<IReadOnlyList<SubnetProposalSummary>>>(proposalPath);
        var yamahaProposal = Assert.Single(proposals!.Payload);
        Assert.Equal("completed", yamahaProposal.YamahaIdentificationStatus);
        Assert.Equal(3, yamahaProposal.YamahaIdentificationAttemptedHostCount);
        Assert.Equal(1, yamahaProposal.YamahaIdentifiedHostCount);
        Assert.Equal("Yamaha DME7", yamahaProposal.YamahaIdentifiedProductFamilies);
        Assert.Equal("grandMA3", yamahaProposal.IdentifiedProductFamilies);
        var grandMa2Path = $"{proposalPath}/{proposalId}/identify-grandma2";
        Assert.Equal(HttpStatusCode.Forbidden, (await outsiderClient.PostAsJsonAsync(
            grandMa2Path, new IdentifyGrandMa2Request())).StatusCode);
        var grandMa2Response = await ownerClient.PostAsJsonAsync(
            grandMa2Path, new IdentifyGrandMa2Request(500));
        Assert.Equal(HttpStatusCode.Accepted, grandMa2Response.StatusCode);
        var grandMa2Command = (await grandMa2Response.Content.ReadFromJsonAsync<
            ApiResponse<AgentCommandEnvelope>>())!.Payload;
        Assert.Equal(AgentCommandType.IdentifyGrandMa2, grandMa2Command.Type);
        var grandMa2Outcome = new AgentEventEnvelope(
            grandMa2Command.CommandId, enrolledAgent.Payload.AgentId, AgentEventType.JobCompleted,
            AgentProtocol.Version, DateTimeOffset.UtcNow, grandMa2Command.CorrelationId,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                proposalId,
                discoveryCommandId = subnetCommand.CommandId,
                attemptedHostCount = 3,
                identifiedHostCount = 1,
                productFamilies = new[] { "grandMA2" }
            }));
        Assert.Equal(HttpStatusCode.Accepted,
            (await agentClient.PostAsJsonAsync("/api/v1/agent-events", grandMa2Outcome)).StatusCode);
        proposals = await ownerClient.GetFromJsonAsync<ApiResponse<IReadOnlyList<SubnetProposalSummary>>>(proposalPath);
        var grandMa2Proposal = Assert.Single(proposals!.Payload);
        Assert.Equal("completed", grandMa2Proposal.GrandMa2IdentificationStatus);
        Assert.Equal(1, grandMa2Proposal.GrandMa2IdentifiedHostCount);
        Assert.Equal("grandMA2", grandMa2Proposal.GrandMa2IdentifiedProductFamilies);
        Assert.Equal("grandMA3", grandMa2Proposal.IdentifiedProductFamilies);
        Assert.Equal("Yamaha DME7", grandMa2Proposal.YamahaIdentifiedProductFamilies);
        var projectorPath = $"{proposalPath}/{proposalId}/identify-projectors";
        Assert.Equal(HttpStatusCode.Forbidden, (await outsiderClient.PostAsJsonAsync(
            projectorPath, new IdentifyProjectorsRequest())).StatusCode);
        var projectorResponse = await ownerClient.PostAsJsonAsync(
            projectorPath, new IdentifyProjectorsRequest(500));
        Assert.Equal(HttpStatusCode.Accepted, projectorResponse.StatusCode);
        var projectorCommand = (await projectorResponse.Content.ReadFromJsonAsync<
            ApiResponse<AgentCommandEnvelope>>())!.Payload;
        Assert.Equal(AgentCommandType.IdentifyProjectors, projectorCommand.Type);
        Assert.Contains(subnetCommand.CommandId.ToString(), projectorCommand.Payload,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("192.168", projectorCommand.Payload, StringComparison.Ordinal);
        var candidatePath =
            $"/api/v1/organizations/{organizationId}/venues/{venueId}/recovery-candidates";
        Assert.Equal(HttpStatusCode.Forbidden, (await outsiderClient.GetAsync(candidatePath)).StatusCode);
        var candidates = await ownerClient.GetFromJsonAsync<
            ApiResponse<IReadOnlyList<RecoveryCandidateSummary>>>(candidatePath);
        Assert.NotNull(candidates);
        var candidate = Assert.Single(candidates.Payload);
        Assert.Equal(candidateId, candidate.Id);
        Assert.Equal("pending", candidate.Decision);
        Assert.DoesNotContain("/", candidate.Evidence, StringComparison.Ordinal);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await ownerClient.PutAsJsonAsync(
                $"{candidatePath}/{candidateId}/decision",
                new DecideRecoveryCandidateRequest(true))).StatusCode);
        var approvedCandidates = await ownerClient.GetFromJsonAsync<
            ApiResponse<IReadOnlyList<RecoveryCandidateSummary>>>(candidatePath);
        Assert.Equal("approved", Assert.Single(approvedCandidates!.Payload).Decision);
        var validationResponse = await ownerClient.PostAsJsonAsync(
            $"{candidatePath}/{candidateId}/validate",
            new ValidateRecoveryCandidateRequest(500));
        Assert.Equal(HttpStatusCode.Accepted, validationResponse.StatusCode);
        Guid validationCommandId;
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var decisionCommand = await database.IssuedAgentCommands.SingleAsync(command =>
                command.AgentId == enrolledAgent.Payload.AgentId &&
                command.Type == AgentCommandType.ApplyRecoveryCandidateDecision);
            var decisionPayload = System.Text.Json.JsonSerializer.Deserialize<
                ApplyRecoveryCandidateDecisionPayload>(decisionCommand.Payload);
            Assert.Equal(candidateId, decisionPayload!.CandidateId);
            Assert.True(decisionPayload.Approved);
            Assert.DoesNotContain("path", decisionCommand.Payload, StringComparison.OrdinalIgnoreCase);
            var validationCommand = await database.IssuedAgentCommands.SingleAsync(command =>
                command.AgentId == enrolledAgent.Payload.AgentId &&
                command.Type == AgentCommandType.ValidateRecoveryCandidate);
            var validationPayload = System.Text.Json.JsonSerializer.Deserialize<
                ValidateRecoveryCandidatePayload>(validationCommand.Payload);
            Assert.Equal(candidateId, validationPayload!.CandidateId);
            Assert.Equal(500, validationPayload.MaxFiles);
            Assert.DoesNotContain("path", validationCommand.Payload, StringComparison.OrdinalIgnoreCase);
            validationCommandId = validationCommand.CommandId;
        }
        var validationOutcome = new AgentEventEnvelope(
            validationCommandId,
            enrolledAgent.Payload.AgentId,
            AgentEventType.JobCompleted,
            AgentProtocol.Version,
            DateTimeOffset.UtcNow,
            "validation-outcome",
            "{\"candidateId\":\"" + candidateId +
                "\",\"pluginId\":\"showvault.resolume\",\"fileCount\":12,\"truncated\":false}");
        Assert.Equal(
            HttpStatusCode.Accepted,
            (await agentClient.PostAsJsonAsync("/api/v1/agent-events", validationOutcome)).StatusCode);
        var validatedCandidates = await ownerClient.GetFromJsonAsync<
            ApiResponse<IReadOnlyList<RecoveryCandidateSummary>>>(candidatePath);
        var validatedCandidate = Assert.Single(validatedCandidates!.Payload);
        Assert.Equal("passed", validatedCandidate.ValidationStatus);
        Assert.Equal(12, validatedCandidate.ValidationFileCount);
        Assert.False(validatedCandidate.ValidationTruncated);
        var backupResponse = await ownerClient.PostAsync(
            $"{candidatePath}/{candidateId}/backup",
            null);
        Assert.Equal(HttpStatusCode.Accepted, backupResponse.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var candidateBackupCommand = await database.IssuedAgentCommands.SingleAsync(command =>
                command.AgentId == enrolledAgent.Payload.AgentId &&
                command.Type == AgentCommandType.CreateBackup);
            Assert.Contains(validationCommandId.ToString(), candidateBackupCommand.Payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("path", candidateBackupCommand.Payload, StringComparison.OrdinalIgnoreCase);
        }
        var retryValidationResponse = await ownerClient.PostAsJsonAsync(
            $"{candidatePath}/{candidateId}/validate",
            new ValidateRecoveryCandidateRequest(500));
        var retryValidation = await retryValidationResponse.Content.ReadFromJsonAsync<
            ApiResponse<AgentCommandEnvelope>>();
        Assert.NotNull(retryValidation);
        var failedOutcome = new AgentEventEnvelope(
            retryValidation.Payload.CommandId,
            enrolledAgent.Payload.AgentId,
            AgentEventType.JobFailed,
            AgentProtocol.Version,
            DateTimeOffset.UtcNow,
            "validation-failed",
            "{\"error\":\"Recognized recovery content was not found.\"}");
        Assert.Equal(
            HttpStatusCode.Accepted,
            (await agentClient.PostAsJsonAsync("/api/v1/agent-events", failedOutcome)).StatusCode);
        var failedCandidates = await ownerClient.GetFromJsonAsync<
            ApiResponse<IReadOnlyList<RecoveryCandidateSummary>>>(candidatePath);
        var failedCandidate = Assert.Single(failedCandidates!.Payload);
        Assert.Equal("failed", failedCandidate.ValidationStatus);
        Assert.Equal("Recognized recovery content was not found.", failedCandidate.ValidationMessage);

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
