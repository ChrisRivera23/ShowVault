using System.Text.Json;
using ShowVault.Agent.Identity;
using ShowVault.Agent.Plugins;
using ShowVault.Agent.Queue;
using ShowVault.Agent.Recovery;
using ShowVault.AgentContracts;

namespace ShowVault.Agent.Execution;

public sealed class AgentCommandExecutor(
    AgentQueueStore queueStore,
    DiscoveryPluginRegistry pluginRegistry,
    RecoveryPackageWriter packageWriter,
    TimeProvider timeProvider,
    ILogger<AgentCommandExecutor> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ExecutePendingOnceAsync(
        StoredAgentIdentity identity,
        CancellationToken cancellationToken)
    {
        var pending = await queueStore.GetCommandsAsync(
            LocalAgentCommandStatus.Pending,
            cancellationToken);
        foreach (var command in pending)
        {
            if (command.AgentId != identity.AgentId)
            {
                continue;
            }

            await queueStore.TryTransitionCommandAsync(
                command.CommandId,
                LocalAgentCommandStatus.Pending,
                LocalAgentCommandStatus.Running,
                timeProvider.GetUtcNow(),
                cancellationToken);
        }

        var running = await queueStore.GetCommandsAsync(
            LocalAgentCommandStatus.Running,
            cancellationToken);
        foreach (var command in running.OrderBy(command => command.IssuedAt))
        {
            if (command.AgentId == identity.AgentId)
            {
                await ExecuteRunningAsync(identity, command, cancellationToken);
            }
        }
    }

    private async Task ExecuteRunningAsync(
        StoredAgentIdentity identity,
        AgentCommandEnvelope command,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (command.Type)
            {
                case AgentCommandType.StartDiscovery:
                    await ExecuteDiscoveryAsync(identity, command, cancellationToken);
                    break;
                case AgentCommandType.CreateBackup:
                    await ExecuteCreateBackupAsync(identity, command, cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Command type is not executable yet: {command.Type}");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Agent command {CommandId} failed", command.CommandId);
            await RecordOutcomeAsync(
                identity,
                command,
                AgentEventType.JobFailed,
                LocalAgentCommandStatus.Failed,
                JsonSerializer.Serialize(new { error = exception.Message }, JsonOptions),
                cancellationToken);
        }
    }

    private async Task ExecuteDiscoveryAsync(
        StoredAgentIdentity identity,
        AgentCommandEnvelope command,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<StartDiscoveryPayload>(command.Payload, JsonOptions)
            ?? throw new InvalidOperationException("StartDiscovery payload is required.");
        var plugin = pluginRegistry.GetRequired(payload.PluginId);
        var result = await plugin.DiscoverAsync(
            new DiscoveryRequest(payload.RootPath, payload.MaxFiles),
            cancellationToken);
        var resultJson = JsonSerializer.Serialize(result, JsonOptions);
        await queueStore.StoreDiscoveryResultAsync(
            command.CommandId,
            resultJson,
            result.CompletedAt,
            cancellationToken);
        await RecordOutcomeAsync(
            identity,
            command,
            AgentEventType.JobCompleted,
            LocalAgentCommandStatus.Completed,
            JsonSerializer.Serialize(
                new
                {
                    result.PluginId,
                    result.RootPath,
                    fileCount = result.Files.Count,
                    result.Truncated
                },
                JsonOptions),
            cancellationToken);
    }

    private async Task ExecuteCreateBackupAsync(
        StoredAgentIdentity identity,
        AgentCommandEnvelope command,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<CreateBackupPayload>(command.Payload, JsonOptions)
            ?? throw new InvalidOperationException("CreateBackup payload is required.");
        var discoveryJson = await queueStore.GetDiscoveryResultJsonAsync(
            payload.DiscoveryCommandId,
            cancellationToken)
            ?? throw new InvalidOperationException("The referenced discovery result was not found.");
        var discovery = JsonSerializer.Deserialize<DiscoveryResult>(discoveryJson, JsonOptions)
            ?? throw new InvalidOperationException("The referenced discovery result is invalid.");
        var package = await packageWriter.CreateAsync(
            identity.AgentId,
            payload.DiscoveryCommandId,
            discovery,
            command.IssuedAt,
            cancellationToken);
        var manifestJson = JsonSerializer.Serialize(package.Manifest, JsonOptions);
        await queueStore.StoreRecoveryPackageAsync(
            command.CommandId,
            package.PackageId,
            package.PackagePath,
            manifestJson,
            command.IssuedAt,
            cancellationToken);
        await RecordOutcomeAsync(
            identity,
            command,
            AgentEventType.JobCompleted,
            LocalAgentCommandStatus.Completed,
            JsonSerializer.Serialize(
                new
                {
                    package.PackageId,
                    fileCount = package.Manifest.Files.Count,
                    formatVersion = package.Manifest.FormatVersion
                },
                JsonOptions),
            cancellationToken);
    }

    private async Task RecordOutcomeAsync(
        StoredAgentIdentity identity,
        AgentCommandEnvelope command,
        AgentEventType eventType,
        LocalAgentCommandStatus finalStatus,
        string payload,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await queueStore.EnqueueEventAsync(
            new AgentEventEnvelope(
                command.CommandId,
                identity.AgentId,
                eventType,
                AgentProtocol.Version,
                now,
                command.CorrelationId,
                payload),
            cancellationToken);
        await queueStore.TryTransitionCommandAsync(
            command.CommandId,
            LocalAgentCommandStatus.Running,
            finalStatus,
            now,
            cancellationToken);
    }

    private sealed record StartDiscoveryPayload(
        string PluginId,
        string RootPath,
        int MaxFiles = 1_000);

    private sealed record CreateBackupPayload(Guid DiscoveryCommandId);
}
