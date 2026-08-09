using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShowVault.Api.Contracts;
using ShowVault.Api.Data;
using ShowVault.AgentContracts;
using ShowVault.Platform.Agents;
using ShowVault.Platform.Organizations;

namespace ShowVault.Api.Endpoints;

public static class RecoveryWorkflowEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapRecoveryWorkflowEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var agents = endpoints.MapGroup(
                "/api/v1/organizations/{organizationId:guid}/venues/{venueId:guid}/agents")
            .RequireAuthorization();
        agents.MapGet("/", ListAgentsAsync);
        agents.MapPost("/{agentId:guid}/inventory", CollectCatalogApplicationsAsync);
        agents.MapPost("/{agentId:guid}/recovery/discover", StartDiscoveryAsync);
        agents.MapPost("/{agentId:guid}/recovery/backup", CreateBackupAsync);
        agents.MapPost("/{agentId:guid}/recovery/verify", VerifyBackupAsync);
        agents.MapPost("/{agentId:guid}/recovery/restore", StartRestoreAsync);
        return endpoints;
    }

    private static Task<IResult> CollectCatalogApplicationsAsync(
        Guid organizationId,
        Guid venueId,
        Guid agentId,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        IssueAsync(
            organizationId, venueId, agentId, AgentCommandType.CollectCatalogApplications,
            new Dictionary<string, object>(), null,
            user, context, database, timeProvider, cancellationToken);

    private static async Task<IResult> ListAgentsAsync(
        Guid organizationId,
        Guid venueId,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        CancellationToken cancellationToken)
    {
        if (!await HasVenueMembershipAsync(
                organizationId, venueId, user, database, false, cancellationToken))
        {
            return Results.Forbid();
        }

        var result = await database.VenueAgents
            .Where(agent => agent.VenueId == venueId && agent.RevokedAt == null)
            .OrderBy(agent => agent.Name)
            .Select(agent => new VenueAgentSummary(agent.Id, agent.Name, agent.CreatedAt))
            .ToListAsync(cancellationToken);
        return Results.Ok(ApiResponse<IReadOnlyList<VenueAgentSummary>>.Success(
            result,
            context.TraceIdentifier));
    }

    private static Task<IResult> StartDiscoveryAsync(
        Guid organizationId,
        Guid venueId,
        Guid agentId,
        StartRecoveryDiscoveryRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PluginId) || request.PluginId.Length > 200 ||
            string.IsNullOrWhiteSpace(request.RootPath) || request.RootPath.Length > 4_096 ||
            request.MaxFiles is < 1 or > 100_000)
        {
            return Task.FromResult<IResult>(Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request)] = ["Plugin ID, exact root, and a maximum of 1 to 100,000 files are required."]
            }));
        }

        return IssueAsync(
            organizationId, venueId, agentId, AgentCommandType.StartDiscovery,
            new { pluginId = request.PluginId.Trim(), rootPath = request.RootPath, request.MaxFiles },
            null, user, context, database, timeProvider, cancellationToken);
    }

    private static Task<IResult> CreateBackupAsync(
        Guid organizationId,
        Guid venueId,
        Guid agentId,
        CreateRecoveryBackupRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        IssueAsync(
            organizationId, venueId, agentId, AgentCommandType.CreateBackup,
            request, (request.DiscoveryCommandId, AgentCommandType.StartDiscovery),
            user, context, database, timeProvider, cancellationToken);

    private static Task<IResult> VerifyBackupAsync(
        Guid organizationId,
        Guid venueId,
        Guid agentId,
        VerifyRecoveryBackupRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        IssueAsync(
            organizationId, venueId, agentId, AgentCommandType.VerifyBackup,
            request, (request.BackupCommandId, AgentCommandType.CreateBackup),
            user, context, database, timeProvider, cancellationToken);

    private static async Task<IResult> StartRestoreAsync(
        Guid organizationId,
        Guid venueId,
        Guid agentId,
        StartRecoveryRestoreRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetPath) || request.TargetPath.Length > 4_096)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.TargetPath)] = ["A restore target is required."]
            });
        }

        if (!await HasVenueMembershipAsync(
                organizationId, venueId, user, database, true, cancellationToken) ||
            !await database.VenueAgents.AnyAsync(
                agent => agent.Id == agentId && agent.VenueId == venueId && agent.RevokedAt == null,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var dependencies = await database.IssuedAgentCommands
            .Where(command => command.AgentId == agentId &&
                (command.CommandId == request.BackupCommandId ||
                 command.CommandId == request.VerificationCommandId))
            .Select(command => new { command.CommandId, command.Type, command.Payload })
            .ToListAsync(cancellationToken);
        var backupIsValid = dependencies.Any(command =>
            command.CommandId == request.BackupCommandId &&
            command.Type == AgentCommandType.CreateBackup);
        var verification = dependencies.SingleOrDefault(command =>
            command.CommandId == request.VerificationCommandId &&
            command.Type == AgentCommandType.VerifyBackup);
        VerifyRecoveryBackupRequest? verificationPayload = null;
        if (verification is not null)
        {
            try
            {
                verificationPayload = JsonSerializer.Deserialize<VerifyRecoveryBackupRequest>(
                    verification.Payload,
                    JsonOptions);
            }
            catch (JsonException)
            {
                // Treat malformed historical commands as invalid dependencies.
            }
        }

        if (!backupIsValid || verificationPayload?.BackupCommandId != request.BackupCommandId)
        {
            return InvalidDependency();
        }

        return await IssueAsync(
            organizationId, venueId, agentId, AgentCommandType.StartRestore,
            request, null, user, context, database, timeProvider, cancellationToken);
    }

    private static async Task<IResult> IssueAsync(
        Guid organizationId,
        Guid venueId,
        Guid agentId,
        AgentCommandType commandType,
        object payload,
        (Guid Id, AgentCommandType Type)? dependency,
        ClaimsPrincipal user,
        HttpContext context,
        PlatformDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!await HasVenueMembershipAsync(
                organizationId, venueId, user, database, true, cancellationToken) ||
            !await database.VenueAgents.AnyAsync(
                agent => agent.Id == agentId && agent.VenueId == venueId && agent.RevokedAt == null,
                cancellationToken))
        {
            return Results.Forbid();
        }

        if (dependency is { } required && !await database.IssuedAgentCommands.AnyAsync(
                command => command.CommandId == required.Id &&
                    command.AgentId == agentId && command.Type == required.Type,
                cancellationToken))
        {
            return InvalidDependency();
        }

        var envelope = AgentCommandEnvelope.Create(
            agentId,
            commandType,
            context.TraceIdentifier,
            JsonSerializer.Serialize(payload, JsonOptions),
            timeProvider.GetUtcNow(),
            TimeSpan.FromMinutes(15));
        database.IssuedAgentCommands.Add(IssuedAgentCommand.FromEnvelope(envelope));
        await database.SaveChangesAsync(cancellationToken);
        return Results.Accepted(
            $"/api/v1/agent-commands/{envelope.CommandId}",
            ApiResponse<AgentCommandEnvelope>.Success(envelope, context.TraceIdentifier));
    }

    private static Task<bool> HasVenueMembershipAsync(
        Guid organizationId,
        Guid venueId,
        ClaimsPrincipal user,
        PlatformDbContext database,
        bool requireManager,
        CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject)) return Task.FromResult(false);

        return database.Venues
            .Where(venue => venue.Id == venueId && venue.OrganizationId == organizationId)
            .Join(
                database.Memberships.Where(membership =>
                    membership.OrganizationId == organizationId &&
                    membership.IdentitySubject == subject &&
                    (!requireManager || membership.Role == OrganizationRole.Manager ||
                     membership.Role == OrganizationRole.Administrator ||
                     membership.Role == OrganizationRole.Owner)),
                venue => venue.OrganizationId,
                membership => membership.OrganizationId,
                (_, _) => true)
            .AnyAsync(cancellationToken);
    }

    private static IResult InvalidDependency() => Results.ValidationProblem(
        new Dictionary<string, string[]>
        {
            ["dependency"] = ["The referenced prior-stage command does not belong to this Agent or has the wrong type."]
        });
}
