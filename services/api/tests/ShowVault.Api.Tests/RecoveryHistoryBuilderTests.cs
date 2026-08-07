using System.Text.Json;
using ShowVault.AgentContracts;
using ShowVault.Api.Recovery;
using ShowVault.Platform.Agents;
using Xunit;

namespace ShowVault.Api.Tests;

public sealed class RecoveryHistoryBuilderTests
{
    [Fact]
    public void Commands_and_outcomes_form_a_complete_recovery_run()
    {
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var discoveryId = Guid.NewGuid();
        var backupId = Guid.NewGuid();
        var verificationId = Guid.NewGuid();
        var restoreId = Guid.NewGuid();
        var commands = new[]
        {
            Command(discoveryId, agentId, AgentCommandType.StartDiscovery, now, "{}"),
            Command(
                backupId,
                agentId,
                AgentCommandType.CreateBackup,
                now.AddMinutes(1),
                JsonSerializer.Serialize(new { discoveryCommandId = discoveryId })),
            Command(
                verificationId,
                agentId,
                AgentCommandType.VerifyBackup,
                now.AddMinutes(2),
                JsonSerializer.Serialize(new { backupCommandId = backupId })),
            Command(
                restoreId,
                agentId,
                AgentCommandType.StartRestore,
                now.AddMinutes(3),
                JsonSerializer.Serialize(new
                {
                    backupCommandId = backupId,
                    verificationCommandId = verificationId
                }))
        };
        var outcomes = commands.ToDictionary(
            command => command.CommandId,
            command => new RecoveryHistoryOutcome(
                AgentEventType.JobCompleted,
                command.IssuedAt.AddSeconds(30)));

        var run = Assert.Single(RecoveryHistoryBuilder.Build(
            new Dictionary<Guid, string> { [agentId] = "Main Agent" },
            commands,
            outcomes));

        Assert.Equal("completed", run.Status);
        Assert.Equal("Main Agent", run.AgentName);
        Assert.Collection(
            run.Stages,
            stage => AssertStage(stage, "scan", "completed"),
            stage => AssertStage(stage, "backup", "completed"),
            stage => AssertStage(stage, "verify", "completed"),
            stage => AssertStage(stage, "restore", "completed"));
    }

    [Fact]
    public void Malformed_links_are_ignored_without_losing_discovery_history()
    {
        var agentId = Guid.NewGuid();
        var discovery = Command(
            Guid.NewGuid(),
            agentId,
            AgentCommandType.StartDiscovery,
            DateTimeOffset.UtcNow,
            "{}");
        var malformedBackup = Command(
            Guid.NewGuid(),
            agentId,
            AgentCommandType.CreateBackup,
            DateTimeOffset.UtcNow,
            "not-json");
        var wrongShapeBackup = Command(
            Guid.NewGuid(),
            agentId,
            AgentCommandType.CreateBackup,
            DateTimeOffset.UtcNow,
            "[]");

        var run = Assert.Single(RecoveryHistoryBuilder.Build(
            new Dictionary<Guid, string> { [agentId] = "Agent" },
            [discovery, malformedBackup, wrongShapeBackup],
            new Dictionary<Guid, RecoveryHistoryOutcome>()));

        Assert.Equal("not_started", run.Stages[1].Status);
    }

    private static RecoveryHistoryCommand Command(
        Guid commandId,
        Guid agentId,
        AgentCommandType type,
        DateTimeOffset issuedAt,
        string payload) =>
        new(
            commandId,
            agentId,
            type,
            issuedAt,
            IssuedAgentCommandStatus.Acknowledged,
            issuedAt.AddSeconds(1),
            payload);

    private static void AssertStage(
        ShowVault.Api.Contracts.RecoveryStageSummary stage,
        string expectedStage,
        string expectedStatus)
    {
        Assert.Equal(expectedStage, stage.Stage);
        Assert.Equal(expectedStatus, stage.Status);
    }
}
