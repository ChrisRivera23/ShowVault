using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.Api.Security;
using ShowVault.AgentContracts;
using ShowVault.Platform.Agents;
using ShowVault.Platform.Organizations;

namespace ShowVault.Api.Endpoints;

public static class AgentCommunicationEndpoints
{
    public static IEndpointRouteBuilder MapAgentCommunicationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/agent-events", ReceiveEventAsync)
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = AgentAuthenticationHandler.SchemeName
            });
        endpoints.MapGet("/api/v1/agent-commands", PollCommandsAsync)
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = AgentAuthenticationHandler.SchemeName
            });
        endpoints.MapPost(
                "/api/v1/agent-commands/{commandId:guid}/acknowledge",
                AcknowledgeCommandAsync)
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = AgentAuthenticationHandler.SchemeName
            });
        endpoints.MapPost(
                "/api/v1/organizations/{organizationId:guid}/venues/{venueId:guid}/agents/{agentId:guid}/commands",
                IssueCommandAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ReceiveEventAsync(
        AgentEventEnvelope envelope,
        ClaimsPrincipal user,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue("agent_id"), out var authenticatedAgentId) ||
            envelope.AgentId != authenticatedAgentId)
        {
            return Results.Forbid();
        }

        if (envelope.ProtocolVersion != AgentProtocol.Version || envelope.EventId == Guid.Empty)
        {
            return Results.BadRequest();
        }

        if (await database.ReceivedAgentEvents.AnyAsync(
            agentEvent => agentEvent.EventId == envelope.EventId,
            cancellationToken))
        {
            return Results.Accepted();
        }

        database.ReceivedAgentEvents.Add(ReceivedAgentEvent.FromEnvelope(
            envelope,
            timeProvider.GetUtcNow()));
        AddRecoveryCandidates(database, envelope);
        AddSubnetProposals(database, envelope);
        await UpdateRecoveryCandidateValidationAsync(
            database,
            envelope,
            cancellationToken);
        await UpdateSubnetDiscoveryAsync(database, envelope, cancellationToken);
        await UpdateMaLightingIdentificationAsync(database, envelope, cancellationToken);
        await UpdateYamahaDmeIdentificationAsync(database, envelope, cancellationToken);
        await UpdateGrandMa2IdentificationAsync(database, envelope, cancellationToken);
        await UpdateBlackmagicVideohubIdentificationAsync(database, envelope, cancellationToken);
        await UpdateNewTekTriCasterIdentificationAsync(database, envelope, cancellationToken);
        await UpdateBirdDogIdentificationAsync(database, envelope, cancellationToken);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            if (!await database.ReceivedAgentEvents.AnyAsync(
                agentEvent => agentEvent.EventId == envelope.EventId,
                cancellationToken))
            {
                throw;
            }
        }

        return Results.Accepted();
    }

    private static async Task UpdateRecoveryCandidateValidationAsync(
        PlatformDbContext database,
        AgentEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (envelope.Type is not (AgentEventType.JobCompleted or AgentEventType.JobFailed))
        {
            return;
        }

        var command = await database.IssuedAgentCommands.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.CommandId == envelope.EventId &&
                candidate.AgentId == envelope.AgentId &&
                candidate.Type == AgentCommandType.ValidateRecoveryCandidate,
            cancellationToken);
        if (command is null)
        {
            return;
        }

        ValidateRecoveryCandidatePayload? commandPayload;
        try
        {
            commandPayload = JsonSerializer.Deserialize<ValidateRecoveryCandidatePayload>(command.Payload);
        }
        catch (JsonException)
        {
            return;
        }

        if (commandPayload is null)
        {
            return;
        }

        var candidate = await database.RecoveryCandidates.SingleOrDefaultAsync(
            item => item.Id == commandPayload.CandidateId &&
                item.AgentId == envelope.AgentId &&
                item.ValidationCommandId == envelope.EventId &&
                item.ValidationStatus == RecoveryCandidateValidationStatus.Pending,
            cancellationToken);
        if (candidate is null)
        {
            return;
        }

        try
        {
            using var outcome = JsonDocument.Parse(envelope.Payload);
            if (envelope.Type == AgentEventType.JobCompleted &&
                outcome.RootElement.TryGetProperty("fileCount", out var countElement) &&
                countElement.TryGetInt32(out var fileCount) && fileCount >= 0 &&
                outcome.RootElement.TryGetProperty("truncated", out var truncatedElement) &&
                truncatedElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                candidate.CompleteValidation(
                    fileCount,
                    truncatedElement.GetBoolean(),
                    envelope.OccurredAt);
                return;
            }

            var message = outcome.RootElement.TryGetProperty("error", out var errorElement) &&
                errorElement.ValueKind == JsonValueKind.String
                ? errorElement.GetString()
                : "Agent returned invalid validation evidence.";
            candidate.FailValidation(
                string.IsNullOrWhiteSpace(message) ? "Candidate validation failed." : message[..Math.Min(message.Length, 500)],
                envelope.OccurredAt);
        }
        catch (JsonException)
        {
            candidate.FailValidation("Agent returned invalid validation evidence.", envelope.OccurredAt);
        }
    }

    private static async Task UpdateSubnetDiscoveryAsync(
        PlatformDbContext database,
        AgentEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var command = await database.IssuedAgentCommands.SingleOrDefaultAsync(item =>
            item.CommandId == envelope.EventId && item.AgentId == envelope.AgentId &&
            item.Type == AgentCommandType.DiscoverApprovedSubnet, cancellationToken);
        if (command is null) return;
        DiscoverApprovedSubnetPayload? request;
        try { request = JsonSerializer.Deserialize<DiscoverApprovedSubnetPayload>(command.Payload); }
        catch (JsonException) { return; }
        if (request is null) return;
        var proposal = await database.SubnetProposals.SingleOrDefaultAsync(item =>
            item.Id == request.ProposalId && item.AgentId == envelope.AgentId &&
            item.DiscoveryCommandId == envelope.EventId && item.DiscoveryStatus == SubnetDiscoveryStatus.Pending,
            cancellationToken);
        if (proposal is null) return;
        try
        {
            using var outcome = JsonDocument.Parse(envelope.Payload);
            if (envelope.Type == AgentEventType.JobCompleted &&
                outcome.RootElement.TryGetProperty("attemptedHostCount", out var attemptedValue) &&
                attemptedValue.TryGetInt32(out var attempted) &&
                outcome.RootElement.TryGetProperty("respondingHostCount", out var respondingValue) &&
                respondingValue.TryGetInt32(out var responding) &&
                outcome.RootElement.TryGetProperty("passiveCandidateCount", out var passiveValue) &&
                passiveValue.TryGetInt32(out var passive) &&
                outcome.RootElement.TryGetProperty("fallbackTargetCount", out var fallbackValue) &&
                fallbackValue.TryGetInt32(out var fallback) &&
                attempted is >= 0 and <= 32 && responding >= 0 && responding <= attempted &&
                passive >= 0 && fallback >= 0 && passive + fallback == attempted)
            {
                proposal.CompleteDiscovery(attempted, responding, passive, fallback, envelope.OccurredAt);
                return;
            }
            var message = outcome.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String
                ? error.GetString() : "Agent returned invalid subnet discovery evidence.";
            proposal.FailDiscovery(string.IsNullOrWhiteSpace(message) ? "Subnet discovery failed." : message[..Math.Min(500, message.Length)], envelope.OccurredAt);
        }
        catch (JsonException)
        {
            proposal.FailDiscovery("Agent returned invalid subnet discovery evidence.", envelope.OccurredAt);
        }
    }

    private static async Task UpdateMaLightingIdentificationAsync(
        PlatformDbContext database,
        AgentEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var command = await database.IssuedAgentCommands.SingleOrDefaultAsync(item =>
            item.CommandId == envelope.EventId && item.AgentId == envelope.AgentId &&
            item.Type == AgentCommandType.IdentifyMaLighting, cancellationToken);
        if (command is null) return;
        IdentifyMaLightingPayload? request;
        try { request = JsonSerializer.Deserialize<IdentifyMaLightingPayload>(command.Payload); }
        catch (JsonException) { return; }
        if (request is null) return;
        var proposal = await database.SubnetProposals.SingleOrDefaultAsync(item =>
            item.Id == request.ProposalId && item.AgentId == envelope.AgentId &&
            item.DiscoveryCommandId == request.DiscoveryCommandId &&
            item.IdentificationCommandId == envelope.EventId &&
            item.IdentificationStatus == ProductIdentificationStatus.Pending,
            cancellationToken);
        if (proposal is null) return;
        try
        {
            using var outcome = JsonDocument.Parse(envelope.Payload);
            if (envelope.Type == AgentEventType.JobCompleted &&
                outcome.RootElement.TryGetProperty("attemptedHostCount", out var attemptedValue) &&
                attemptedValue.TryGetInt32(out var attempted) &&
                outcome.RootElement.TryGetProperty("identifiedHostCount", out var identifiedValue) &&
                identifiedValue.TryGetInt32(out var identified) &&
                outcome.RootElement.TryGetProperty("productFamilies", out var familiesValue) &&
                familiesValue.ValueKind == JsonValueKind.Array)
            {
                var families = familiesValue.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                var familyEvidenceIsConsistent = identified == 0 ? families.Length == 0 : families.Length > 0;
                var boundedFamilies = families.Length == 0 ? "none" : string.Join(",", families);
                if (familyEvidenceIsConsistent && boundedFamilies.Length <= 200)
                {
                    proposal.CompleteIdentification(attempted, identified, boundedFamilies, envelope.OccurredAt);
                    return;
                }
            }
            var message = outcome.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String ? error.GetString() :
                "Agent returned invalid MA Lighting identification evidence.";
            proposal.FailIdentification(string.IsNullOrWhiteSpace(message) ?
                "MA Lighting identification failed." : message[..Math.Min(500, message.Length)], envelope.OccurredAt);
        }
        catch (JsonException)
        {
            proposal.FailIdentification("Agent returned invalid MA Lighting identification evidence.", envelope.OccurredAt);
        }
    }

    private static async Task UpdateGrandMa2IdentificationAsync(
        PlatformDbContext database,
        AgentEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var command = await database.IssuedAgentCommands.SingleOrDefaultAsync(item =>
            item.CommandId == envelope.EventId && item.AgentId == envelope.AgentId &&
            item.Type == AgentCommandType.IdentifyGrandMa2, cancellationToken);
        if (command is null) return;
        IdentifyGrandMa2Payload? request;
        try { request = JsonSerializer.Deserialize<IdentifyGrandMa2Payload>(command.Payload); }
        catch (JsonException) { return; }
        if (request is null) return;
        var proposal = await database.SubnetProposals.SingleOrDefaultAsync(item =>
            item.Id == request.ProposalId && item.AgentId == envelope.AgentId &&
            item.DiscoveryCommandId == request.DiscoveryCommandId &&
            item.GrandMa2IdentificationCommandId == envelope.EventId &&
            item.GrandMa2IdentificationStatus == ProductIdentificationStatus.Pending,
            cancellationToken);
        if (proposal is null) return;
        try
        {
            using var outcome = JsonDocument.Parse(envelope.Payload);
            if (envelope.Type == AgentEventType.JobCompleted &&
                outcome.RootElement.TryGetProperty("attemptedHostCount", out var attemptedValue) &&
                attemptedValue.TryGetInt32(out var attempted) &&
                outcome.RootElement.TryGetProperty("identifiedHostCount", out var identifiedValue) &&
                identifiedValue.TryGetInt32(out var identified) &&
                outcome.RootElement.TryGetProperty("productFamilies", out var familiesValue) &&
                familiesValue.ValueKind == JsonValueKind.Array)
            {
                var families = familiesValue.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                var consistent = identified == 0 ? families.Length == 0 :
                    families.Length == 1 && families[0] == "grandMA2";
                var evidence = families.Length == 0 ? "none" : string.Join(",", families);
                if (consistent && evidence.Length <= 200)
                {
                    proposal.CompleteGrandMa2Identification(attempted, identified, evidence, envelope.OccurredAt);
                    return;
                }
            }
            var message = outcome.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String ? error.GetString() :
                "Agent returned invalid grandMA2 identification evidence.";
            proposal.FailGrandMa2Identification(string.IsNullOrWhiteSpace(message) ?
                "grandMA2 identification failed." : message[..Math.Min(500, message.Length)], envelope.OccurredAt);
        }
        catch (JsonException)
        {
            proposal.FailGrandMa2Identification(
                "Agent returned invalid grandMA2 identification evidence.", envelope.OccurredAt);
        }
    }

    private static async Task UpdateBlackmagicVideohubIdentificationAsync(
        PlatformDbContext database,
        AgentEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var command = await database.IssuedAgentCommands.SingleOrDefaultAsync(item =>
            item.CommandId == envelope.EventId && item.AgentId == envelope.AgentId &&
            item.Type == AgentCommandType.IdentifyBlackmagicVideohub, cancellationToken);
        if (command is null) return;
        IdentifyBlackmagicVideohubPayload? request;
        try { request = JsonSerializer.Deserialize<IdentifyBlackmagicVideohubPayload>(command.Payload); }
        catch (JsonException) { return; }
        if (request is null) return;
        var proposal = await database.SubnetProposals.SingleOrDefaultAsync(item =>
            item.Id == request.ProposalId && item.AgentId == envelope.AgentId &&
            item.DiscoveryCommandId == request.DiscoveryCommandId &&
            item.BlackmagicVideohubIdentificationCommandId == envelope.EventId &&
            item.BlackmagicVideohubIdentificationStatus == ProductIdentificationStatus.Pending,
            cancellationToken);
        if (proposal is null) return;
        try
        {
            using var outcome = JsonDocument.Parse(envelope.Payload);
            if (envelope.Type == AgentEventType.JobCompleted &&
                outcome.RootElement.TryGetProperty("attemptedHostCount", out var attemptedValue) &&
                attemptedValue.TryGetInt32(out var attempted) &&
                outcome.RootElement.TryGetProperty("identifiedHostCount", out var identifiedValue) &&
                identifiedValue.TryGetInt32(out var identified) &&
                outcome.RootElement.TryGetProperty("productFamilies", out var familiesValue) &&
                familiesValue.ValueKind == JsonValueKind.Array)
            {
                var families = familiesValue.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                var consistent = identified == 0 ? families.Length == 0 :
                    families.Length == 1 && families[0] == "Blackmagic Smart Videohub 16x16";
                var evidence = families.Length == 0 ? "none" : string.Join(",", families);
                if (attempted is >= 1 and <= 32 && identified >= 0 && identified <= attempted &&
                    consistent && evidence.Length <= 200)
                {
                    proposal.CompleteBlackmagicVideohubIdentification(
                        attempted, identified, evidence, envelope.OccurredAt);
                    return;
                }
            }
            var message = outcome.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String ? error.GetString() :
                "Agent returned invalid Blackmagic Videohub identification evidence.";
            proposal.FailBlackmagicVideohubIdentification(string.IsNullOrWhiteSpace(message) ?
                "Blackmagic Videohub identification failed." :
                message[..Math.Min(500, message.Length)], envelope.OccurredAt);
        }
        catch (JsonException)
        {
            proposal.FailBlackmagicVideohubIdentification(
                "Agent returned invalid Blackmagic Videohub identification evidence.", envelope.OccurredAt);
        }
    }

    private static async Task UpdateNewTekTriCasterIdentificationAsync(
        PlatformDbContext database,
        AgentEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var command = await database.IssuedAgentCommands.SingleOrDefaultAsync(item =>
            item.CommandId == envelope.EventId && item.AgentId == envelope.AgentId &&
            item.Type == AgentCommandType.IdentifyNewTekTriCaster, cancellationToken);
        if (command is null) return;
        IdentifyNewTekTriCasterPayload? request;
        try { request = JsonSerializer.Deserialize<IdentifyNewTekTriCasterPayload>(command.Payload); }
        catch (JsonException) { return; }
        if (request is null) return;
        var proposal = await database.SubnetProposals.SingleOrDefaultAsync(item =>
            item.Id == request.ProposalId && item.AgentId == envelope.AgentId &&
            item.DiscoveryCommandId == request.DiscoveryCommandId &&
            item.NewTekTriCasterIdentificationCommandId == envelope.EventId &&
            item.NewTekTriCasterIdentificationStatus == ProductIdentificationStatus.Pending,
            cancellationToken);
        if (proposal is null) return;
        try
        {
            using var outcome = JsonDocument.Parse(envelope.Payload);
            if (envelope.Type == AgentEventType.JobCompleted &&
                outcome.RootElement.TryGetProperty("attemptedHostCount", out var attemptedValue) &&
                attemptedValue.TryGetInt32(out var attempted) &&
                outcome.RootElement.TryGetProperty("identifiedHostCount", out var identifiedValue) &&
                identifiedValue.TryGetInt32(out var identified) &&
                outcome.RootElement.TryGetProperty("productFamilies", out var familiesValue) &&
                familiesValue.ValueKind == JsonValueKind.Array)
            {
                var families = familiesValue.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                var consistent = identified == 0 ? families.Length == 0 :
                    families.Length == 1 && families[0] == "NewTek TriCaster TC1";
                var evidence = families.Length == 0 ? "none" : string.Join(",", families);
                if (attempted is >= 1 and <= 32 && identified >= 0 && identified <= attempted &&
                    consistent && evidence.Length <= 200)
                {
                    proposal.CompleteNewTekTriCasterIdentification(
                        attempted, identified, evidence, envelope.OccurredAt);
                    return;
                }
            }
            var message = outcome.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String ? error.GetString() :
                "Agent returned invalid NewTek TriCaster identification evidence.";
            proposal.FailNewTekTriCasterIdentification(string.IsNullOrWhiteSpace(message) ?
                "NewTek TriCaster identification failed." :
                message[..Math.Min(500, message.Length)], envelope.OccurredAt);
        }
        catch (JsonException)
        {
            proposal.FailNewTekTriCasterIdentification(
                "Agent returned invalid NewTek TriCaster identification evidence.", envelope.OccurredAt);
        }
    }

    private static async Task UpdateBirdDogIdentificationAsync(
        PlatformDbContext database,
        AgentEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var command = await database.IssuedAgentCommands.SingleOrDefaultAsync(item =>
            item.CommandId == envelope.EventId && item.AgentId == envelope.AgentId &&
            item.Type == AgentCommandType.IdentifyBirdDog, cancellationToken);
        if (command is null) return;
        IdentifyBirdDogPayload? request;
        try { request = JsonSerializer.Deserialize<IdentifyBirdDogPayload>(command.Payload); }
        catch (JsonException) { return; }
        if (request is null) return;
        var proposal = await database.SubnetProposals.SingleOrDefaultAsync(item =>
            item.Id == request.ProposalId && item.AgentId == envelope.AgentId &&
            item.DiscoveryCommandId == request.DiscoveryCommandId &&
            item.BirdDogIdentificationCommandId == envelope.EventId &&
            item.BirdDogIdentificationStatus == ProductIdentificationStatus.Pending,
            cancellationToken);
        if (proposal is null) return;
        try
        {
            using var outcome = JsonDocument.Parse(envelope.Payload);
            if (envelope.Type == AgentEventType.JobCompleted &&
                outcome.RootElement.TryGetProperty("attemptedHostCount", out var attemptedValue) &&
                attemptedValue.TryGetInt32(out var attempted) &&
                outcome.RootElement.TryGetProperty("identifiedHostCount", out var identifiedValue) &&
                identifiedValue.TryGetInt32(out var identified) &&
                outcome.RootElement.TryGetProperty("productFamilies", out var familiesValue) &&
                familiesValue.ValueKind == JsonValueKind.Array)
            {
                var families = familiesValue.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                var consistent = identified == 0 ? families.Length == 0 :
                    families.Length == 1 && families[0] == "BirdDog P200 (A4/A5)";
                var evidence = families.Length == 0 ? "none" : string.Join(",", families);
                if (attempted is >= 1 and <= 32 && identified >= 0 && identified <= attempted &&
                    consistent && evidence.Length <= 200)
                {
                    proposal.CompleteBirdDogIdentification(
                        attempted, identified, evidence, envelope.OccurredAt);
                    return;
                }
            }
            var message = outcome.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String ? error.GetString() :
                "Agent returned invalid BirdDog identification evidence.";
            proposal.FailBirdDogIdentification(string.IsNullOrWhiteSpace(message) ?
                "BirdDog identification failed." :
                message[..Math.Min(500, message.Length)], envelope.OccurredAt);
        }
        catch (JsonException)
        {
            proposal.FailBirdDogIdentification(
                "Agent returned invalid BirdDog identification evidence.", envelope.OccurredAt);
        }
    }

    private static async Task UpdateYamahaDmeIdentificationAsync(
        PlatformDbContext database,
        AgentEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var command = await database.IssuedAgentCommands.SingleOrDefaultAsync(item =>
            item.CommandId == envelope.EventId && item.AgentId == envelope.AgentId &&
            item.Type == AgentCommandType.IdentifyYamahaDme, cancellationToken);
        if (command is null) return;
        IdentifyYamahaDmePayload? request;
        try { request = JsonSerializer.Deserialize<IdentifyYamahaDmePayload>(command.Payload); }
        catch (JsonException) { return; }
        if (request is null) return;
        var proposal = await database.SubnetProposals.SingleOrDefaultAsync(item =>
            item.Id == request.ProposalId && item.AgentId == envelope.AgentId &&
            item.DiscoveryCommandId == request.DiscoveryCommandId &&
            item.YamahaIdentificationCommandId == envelope.EventId &&
            item.YamahaIdentificationStatus == ProductIdentificationStatus.Pending,
            cancellationToken);
        if (proposal is null) return;
        try
        {
            using var outcome = JsonDocument.Parse(envelope.Payload);
            if (envelope.Type == AgentEventType.JobCompleted &&
                outcome.RootElement.TryGetProperty("attemptedHostCount", out var attemptedValue) &&
                attemptedValue.TryGetInt32(out var attempted) &&
                outcome.RootElement.TryGetProperty("identifiedHostCount", out var identifiedValue) &&
                identifiedValue.TryGetInt32(out var identified) &&
                outcome.RootElement.TryGetProperty("productFamilies", out var familiesValue) &&
                familiesValue.ValueKind == JsonValueKind.Array)
            {
                var families = familiesValue.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                var consistent = identified == 0 ? families.Length == 0 :
                    families.Length == 1 && families[0] == "Yamaha DME7";
                var evidence = families.Length == 0 ? "none" : string.Join(",", families);
                if (consistent && evidence.Length <= 200)
                {
                    proposal.CompleteYamahaIdentification(attempted, identified, evidence, envelope.OccurredAt);
                    return;
                }
            }
            var message = outcome.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String ? error.GetString() :
                "Agent returned invalid Yamaha DME7 identification evidence.";
            proposal.FailYamahaIdentification(string.IsNullOrWhiteSpace(message) ?
                "Yamaha DME7 identification failed." : message[..Math.Min(500, message.Length)], envelope.OccurredAt);
        }
        catch (JsonException)
        {
            proposal.FailYamahaIdentification("Agent returned invalid Yamaha DME7 identification evidence.", envelope.OccurredAt);
        }
    }

    private static void AddRecoveryCandidates(
        PlatformDbContext database,
        AgentEventEnvelope envelope)
    {
        if (envelope.Type != AgentEventType.JobCompleted)
        {
            return;
        }

        JsonDocument payload;
        try
        {
            payload = JsonDocument.Parse(envelope.Payload);
        }
        catch (JsonException)
        {
            return;
        }

        using (payload)
        {
            if (!payload.RootElement.TryGetProperty("recoveryCandidates", out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var item in candidates.EnumerateArray().Take(128))
            {
                if (!item.TryGetProperty("candidateId", out var idElement) ||
                    !idElement.TryGetGuid(out var candidateId) || candidateId == Guid.Empty)
                {
                    continue;
                }

                var pluginId = ReadBoundedString(item, "pluginId", 200);
                var productName = ReadBoundedString(item, "productName", 200);
                var candidateType = ReadBoundedString(item, "candidateType", 80);
                var evidence = ReadBoundedString(item, "evidence", 500);
                if (pluginId is null || productName is null || candidateType is null || evidence is null)
                {
                    continue;
                }

                database.RecoveryCandidates.Add(RecoveryCandidate.Detected(
                    candidateId,
                    envelope.AgentId,
                    pluginId,
                    productName,
                    candidateType,
                    evidence,
                    envelope.OccurredAt));
            }
        }
    }

    private static string? ReadBoundedString(JsonElement item, string name, int maximumLength)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) || text.Length > maximumLength ? null : text;
    }

    private static void AddSubnetProposals(PlatformDbContext database, AgentEventEnvelope envelope)
    {
        if (envelope.Type != AgentEventType.JobCompleted) return;
        try
        {
            using var payload = JsonDocument.Parse(envelope.Payload);
            if (!payload.RootElement.TryGetProperty("subnetProposals", out var proposals) ||
                proposals.ValueKind != JsonValueKind.Array) return;
            foreach (var item in proposals.EnumerateArray().Take(8))
            {
                if (!item.TryGetProperty("proposalId", out var idValue) || !idValue.TryGetGuid(out var id) ||
                    id == Guid.Empty || !item.TryGetProperty("prefixLength", out var prefixValue) ||
                    !prefixValue.TryGetInt32(out var prefix) || prefix is < 16 or > 30) continue;
                var network = ReadBoundedString(item, "network", 15);
                var type = ReadBoundedString(item, "interfaceType", 40);
                var evidence = ReadBoundedString(item, "evidence", 500);
                if (network is null || type is null || evidence is null) continue;
                try
                {
                    database.SubnetProposals.Add(SubnetProposal.Detected(
                        id, envelope.AgentId, network, prefix, type, evidence, envelope.OccurredAt));
                }
                catch (ArgumentException) { }
            }
        }
        catch (JsonException) { }
    }

    private static async Task<IResult> IssueCommandAsync(
        Guid organizationId,
        Guid venueId,
        Guid agentId,
        IssueAgentCommandRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub");
        var authorized = !string.IsNullOrWhiteSpace(subject) &&
            await database.VenueAgents
                .Where(agent => agent.Id == agentId &&
                    agent.VenueId == venueId &&
                    agent.RevokedAt == null)
                .Join(
                    database.Venues.Where(venue =>
                        venue.Id == venueId && venue.OrganizationId == organizationId),
                    agent => agent.VenueId,
                    venue => venue.Id,
                    (_, venue) => venue.OrganizationId)
                .Join(
                    database.Memberships.Where(membership =>
                        membership.IdentitySubject == subject &&
                        (membership.Role == OrganizationRole.Manager ||
                         membership.Role == OrganizationRole.Administrator ||
                         membership.Role == OrganizationRole.Owner)),
                    candidateOrganizationId => candidateOrganizationId,
                    membership => membership.OrganizationId,
                    (_, _) => true)
                .AnyAsync(cancellationToken);
        if (!authorized)
        {
            return Results.Forbid();
        }

        if (request.ValidForSeconds is < 1 or > 3600 || request.Payload.Length > 262_144)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ValidForSeconds)] =
                    ["Command validity must be between 1 and 3600 seconds and payloads at most 256 KiB."]
            });
        }

        try
        {
            using var _ = JsonDocument.Parse(request.Payload);
        }
        catch (JsonException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Payload)] = ["Command payload must be valid JSON."]
            });
        }

        var envelope = AgentCommandEnvelope.Create(
            agentId,
            request.Type,
            context.TraceIdentifier,
            request.Payload,
            timeProvider.GetUtcNow(),
            TimeSpan.FromSeconds(request.ValidForSeconds));
        database.IssuedAgentCommands.Add(IssuedAgentCommand.FromEnvelope(envelope));
        await database.SaveChangesAsync(cancellationToken);
        return Results.Accepted(
            $"/api/v1/agent-commands/{envelope.CommandId}",
            ApiResponse<AgentCommandEnvelope>.Success(envelope, context.TraceIdentifier));
    }

    private static async Task<IResult> PollCommandsAsync(
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue("agent_id"), out var agentId))
        {
            return Results.Unauthorized();
        }

        var now = timeProvider.GetUtcNow();
        var pendingCommands = await database.IssuedAgentCommands
            .Where(command => command.AgentId == agentId &&
                command.Status == IssuedAgentCommandStatus.Pending)
            .Select(command => new AgentCommandEnvelope(
                command.CommandId,
                command.AgentId,
                command.Type,
                command.ProtocolVersion,
                command.IssuedAt,
                command.ExpiresAt,
                command.CorrelationId,
                command.Payload))
            .ToListAsync(cancellationToken);
        var commands = pendingCommands
            .Where(command => command.ExpiresAt > now)
            .OrderBy(command => command.IssuedAt)
            .Take(25)
            .ToList();
        return Results.Ok(ApiResponse<IReadOnlyList<AgentCommandEnvelope>>.Success(
            commands,
            context.TraceIdentifier));
    }

    private static async Task<IResult> AcknowledgeCommandAsync(
        Guid commandId,
        ClaimsPrincipal user,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue("agent_id"), out var agentId))
        {
            return Results.Unauthorized();
        }

        var command = await database.IssuedAgentCommands.SingleOrDefaultAsync(
            candidate => candidate.CommandId == commandId && candidate.AgentId == agentId,
            cancellationToken);
        if (command is null)
        {
            return Results.NotFound();
        }

        command.Acknowledge(timeProvider.GetUtcNow());
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            var acknowledged = await database.IssuedAgentCommands
                .AsNoTracking()
                .AnyAsync(
                    candidate => candidate.CommandId == commandId &&
                        candidate.AgentId == agentId &&
                        candidate.Status == IssuedAgentCommandStatus.Acknowledged,
                    cancellationToken);
            if (!acknowledged)
            {
                return Results.Conflict();
            }
        }

        return Results.NoContent();
    }
}
