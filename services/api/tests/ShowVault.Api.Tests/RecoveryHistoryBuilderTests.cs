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
                command.AgentId,
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

    [Fact]
    public void Cross_agent_and_out_of_order_links_are_ignored()
    {
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var otherAgentId = Guid.NewGuid();
        var discoveryId = Guid.NewGuid();
        var discovery = Command(
            discoveryId,
            agentId,
            AgentCommandType.StartDiscovery,
            now,
            "{}");
        var otherAgentBackup = Command(
            Guid.NewGuid(),
            otherAgentId,
            AgentCommandType.CreateBackup,
            now.AddMinutes(1),
            JsonSerializer.Serialize(new { discoveryCommandId = discoveryId }));
        var earlyBackup = Command(
            Guid.NewGuid(),
            agentId,
            AgentCommandType.CreateBackup,
            now.AddMinutes(-1),
            JsonSerializer.Serialize(new { discoveryCommandId = discoveryId }));

        var run = Assert.Single(RecoveryHistoryBuilder.Build(
            new Dictionary<Guid, string>
            {
                [agentId] = "Agent",
                [otherAgentId] = "Other Agent"
            },
            [discovery, otherAgentBackup, earlyBackup],
            new Dictionary<Guid, RecoveryHistoryOutcome>()));

        Assert.Equal("not_started", run.Stages[1].Status);
    }

    [Fact]
    public void Restore_requires_the_selected_verification_and_valid_order()
    {
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var discoveryId = Guid.NewGuid();
        var backupId = Guid.NewGuid();
        var verificationId = Guid.NewGuid();
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
                Guid.NewGuid(),
                agentId,
                AgentCommandType.StartRestore,
                now.AddMinutes(3),
                JsonSerializer.Serialize(new
                {
                    backupCommandId = backupId,
                    verificationCommandId = Guid.NewGuid()
                })),
            Command(
                Guid.NewGuid(),
                agentId,
                AgentCommandType.StartRestore,
                now.AddMinutes(1),
                JsonSerializer.Serialize(new
                {
                    backupCommandId = backupId,
                    verificationCommandId = verificationId
                }))
        };

        var run = Assert.Single(RecoveryHistoryBuilder.Build(
            new Dictionary<Guid, string> { [agentId] = "Agent" },
            commands,
            new Dictionary<Guid, RecoveryHistoryOutcome>()));

        Assert.Equal("not_started", run.Stages[3].Status);
    }

    [Fact]
    public void Outcomes_require_the_same_agent_and_valid_time()
    {
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var discoveryId = Guid.NewGuid();
        var discovery = Command(
            discoveryId,
            agentId,
            AgentCommandType.StartDiscovery,
            now,
            "{}");

        foreach (var outcome in new[]
        {
            new RecoveryHistoryOutcome(
                Guid.NewGuid(),
                AgentEventType.JobCompleted,
                now.AddSeconds(1)),
            new RecoveryHistoryOutcome(
                agentId,
                AgentEventType.JobCompleted,
                now.AddSeconds(-1))
        })
        {
            var run = Assert.Single(RecoveryHistoryBuilder.Build(
                new Dictionary<Guid, string> { [agentId] = "Agent" },
                [discovery],
                new Dictionary<Guid, RecoveryHistoryOutcome> { [discoveryId] = outcome }));

            Assert.Equal("in_progress", run.Stages[0].Status);
        }
    }

    [Fact]
    public void Expired_commands_have_a_truthful_terminal_status()
    {
        var agentId = Guid.NewGuid();
        var expired = new RecoveryHistoryCommand(
            Guid.NewGuid(),
            agentId,
            AgentCommandType.StartDiscovery,
            DateTimeOffset.UtcNow,
            IssuedAgentCommandStatus.Expired,
            null,
            "{}");

        var run = Assert.Single(RecoveryHistoryBuilder.Build(
            new Dictionary<Guid, string> { [agentId] = "Agent" },
            [expired],
            new Dictionary<Guid, RecoveryHistoryOutcome>()));

        Assert.Equal("expired", run.Status);
        Assert.Equal("expired", run.Stages[0].Status);
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
