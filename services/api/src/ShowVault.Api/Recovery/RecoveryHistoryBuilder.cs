using System.Text.Json;
using ShowVault.AgentContracts;
using ShowVault.Api.Contracts;
using ShowVault.Platform.Agents;

namespace ShowVault.Api.Recovery;

public static class RecoveryHistoryBuilder
{
    private static readonly AgentCommandType[] StageTypes =
    [
        AgentCommandType.StartDiscovery,
        AgentCommandType.CreateBackup,
        AgentCommandType.VerifyBackup,
        AgentCommandType.StartRestore
    ];

    public static IReadOnlyList<RecoveryRunSummary> Build(
        IReadOnlyDictionary<Guid, string> agentNames,
        IReadOnlyList<RecoveryHistoryCommand> commands,
        IReadOnlyDictionary<Guid, RecoveryHistoryOutcome> outcomes)
    {
        var relevantCommands = commands
            .Where(command => StageTypes.Contains(command.Type))
            .ToList();
        var backupsByDiscovery = relevantCommands
            .Where(command => command.Type == AgentCommandType.CreateBackup)
            .Select(command => (command, parentId: ReadGuid(command.Payload, "discoveryCommandId")))
            .Where(candidate => candidate.parentId.HasValue)
            .GroupBy(candidate => candidate.parentId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(candidate => candidate.command).ToList());
        var verificationsByBackup = relevantCommands
            .Where(command => command.Type == AgentCommandType.VerifyBackup)
            .Select(command => (command, parentId: ReadGuid(command.Payload, "backupCommandId")))
            .Where(candidate => candidate.parentId.HasValue)
            .GroupBy(candidate => candidate.parentId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(candidate => candidate.command).ToList());
        var restoresByBackup = relevantCommands
            .Where(command => command.Type == AgentCommandType.StartRestore)
            .Select(command => (command, parentId: ReadGuid(command.Payload, "backupCommandId")))
            .Where(candidate => candidate.parentId.HasValue)
            .GroupBy(candidate => candidate.parentId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(candidate => candidate.command).ToList());

        return relevantCommands
            .Where(command => command.Type == AgentCommandType.StartDiscovery)
            .OrderByDescending(command => command.IssuedAt)
            .Select(discovery =>
            {
                var backup = Latest(backupsByDiscovery, discovery.CommandId);
                var verification = backup is null
                    ? null
                    : Latest(verificationsByBackup, backup.CommandId);
                var restore = backup is null
                    ? null
                    : Latest(restoresByBackup, backup.CommandId);
                var stages = new[]
                {
                    BuildStage("scan", discovery, outcomes),
                    BuildStage("backup", backup, outcomes),
                    BuildStage("verify", verification, outcomes),
                    BuildStage("restore", restore, outcomes)
                };
                return new RecoveryRunSummary(
                    discovery.CommandId,
                    discovery.AgentId,
                    agentNames.GetValueOrDefault(discovery.AgentId, "Unknown Agent"),
                    discovery.IssuedAt,
                    GetRunStatus(stages),
                    stages);
            })
            .ToList();
    }

    private static RecoveryHistoryCommand? Latest(
        IReadOnlyDictionary<Guid, List<RecoveryHistoryCommand>> candidates,
        Guid parentId) =>
        candidates.TryGetValue(parentId, out var commands)
            ? commands.OrderByDescending(command => command.IssuedAt).First()
            : null;

    private static RecoveryStageSummary BuildStage(
        string stage,
        RecoveryHistoryCommand? command,
        IReadOnlyDictionary<Guid, RecoveryHistoryOutcome> outcomes)
    {
        if (command is null)
        {
            return new RecoveryStageSummary(stage, "not_started", null, null);
        }

        if (outcomes.TryGetValue(command.CommandId, out var outcome))
        {
            return new RecoveryStageSummary(
                stage,
                outcome.Type == AgentEventType.JobCompleted ? "completed" : "failed",
                command.CommandId,
                outcome.OccurredAt);
        }

        return new RecoveryStageSummary(
            stage,
            command.Status == IssuedAgentCommandStatus.Acknowledged ? "in_progress" : "pending",
            command.CommandId,
            command.AcknowledgedAt ?? command.IssuedAt);
    }

    private static string GetRunStatus(IReadOnlyList<RecoveryStageSummary> stages)
    {
        if (stages.Any(stage => stage.Status == "failed"))
        {
            return "failed";
        }

        if (stages.Last().Status == "completed")
        {
            return "completed";
        }

        return stages.Any(stage => stage.Status is "completed" or "in_progress")
            ? "in_progress"
            : "pending";
    }

    private static Guid? ReadGuid(string payload, string propertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var property = document.RootElement.EnumerateObject().FirstOrDefault(
                candidate => string.Equals(
                    candidate.Name,
                    propertyName,
                    StringComparison.OrdinalIgnoreCase));
            return property.Value.ValueKind == JsonValueKind.String &&
                Guid.TryParse(property.Value.GetString(), out var value)
                ? value
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record RecoveryHistoryCommand(
    Guid CommandId,
    Guid AgentId,
    AgentCommandType Type,
    DateTimeOffset IssuedAt,
    IssuedAgentCommandStatus Status,
    DateTimeOffset? AcknowledgedAt,
    string Payload);

public sealed record RecoveryHistoryOutcome(
    AgentEventType Type,
    DateTimeOffset OccurredAt);
