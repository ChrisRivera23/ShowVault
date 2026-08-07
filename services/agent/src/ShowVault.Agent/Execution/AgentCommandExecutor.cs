using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
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
    RecoveryPackageVerifier packageVerifier,
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

            var now = timeProvider.GetUtcNow();
            if (command.ExpiresAt <= now)
            {
                await RecordOutcomeAsync(
                    identity,
                    command,
                    AgentEventType.JobFailed,
                    LocalAgentCommandStatus.Pending,
                    LocalAgentCommandStatus.Expired,
                    JsonSerializer.Serialize(new { error = "command-expired" }, JsonOptions),
                    cancellationToken);
                continue;
            }

            await queueStore.TryStartCommandAsync(
                command.CommandId,
                command.ExpiresAt,
                now,
                cancellationToken);
        }

        var running = await queueStore.GetCommandsAsync(
            LocalAgentCommandStatus.Running,
            cancellationToken);
        foreach (var command in running.OrderBy(command => command.IssuedAt))
        {
            if (command.AgentId == identity.AgentId)
            {
                if (command.ExpiresAt <= timeProvider.GetUtcNow())
                {
                    await RecordOutcomeAsync(
                        identity,
                        command,
                        AgentEventType.JobFailed,
                        LocalAgentCommandStatus.Running,
                        LocalAgentCommandStatus.Expired,
                        JsonSerializer.Serialize(new { error = "command-expired" }, JsonOptions),
                        cancellationToken);
                }
                else
                {
                    await ExecuteRunningAsync(identity, command, cancellationToken);
                }
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
                case AgentCommandType.VerifyBackup:
                    await ExecuteVerifyBackupAsync(identity, command, cancellationToken);
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
            var failureCategory = ClassifyFailure(exception);
            logger.LogError(
                "Agent command {CommandId} failed with category {FailureCategory}",
                command.CommandId,
                failureCategory);
            await RecordOutcomeAsync(
                identity,
                command,
                AgentEventType.JobFailed,
                LocalAgentCommandStatus.Running,
                LocalAgentCommandStatus.Failed,
                JsonSerializer.Serialize(new { error = failureCategory }, JsonOptions),
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
            LocalAgentCommandStatus.Running,
            LocalAgentCommandStatus.Completed,
            JsonSerializer.Serialize(
                new
                {
                    result.PluginId,
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
            LocalAgentCommandStatus.Running,
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

    private async Task ExecuteVerifyBackupAsync(
        StoredAgentIdentity identity,
        AgentCommandEnvelope command,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<VerifyBackupPayload>(command.Payload, JsonOptions)
            ?? throw new InvalidOperationException("VerifyBackup payload is required.");
        var package = await queueStore.GetRecoveryPackageAsync(
            payload.BackupCommandId,
            cancellationToken)
            ?? throw new InvalidOperationException("The referenced recovery package was not found.");
        var storedVerification = await queueStore.GetPackageVerificationAsync(
            command.CommandId,
            cancellationToken);
        RecoveryPackageVerificationResult result;
        string resultJson;
        string evidenceSha256;
        if (storedVerification is null)
        {
            result = await packageVerifier.VerifyAsync(
                command.CommandId,
                identity.AgentId,
                package.PackageId,
                package.PackagePath,
                command.IssuedAt,
                cancellationToken);
            resultJson = RecoveryPackageVerifier.Serialize(result);
            evidenceSha256 = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(resultJson)));
            await queueStore.StorePackageVerificationAsync(
                command.CommandId,
                package.PackageId,
                resultJson,
                evidenceSha256,
                command.IssuedAt,
                cancellationToken);
        }
        else
        {
            if (storedVerification.PackageId != package.PackageId)
            {
                throw new InvalidOperationException(
                    "Stored verification evidence references a different package.");
            }

            resultJson = storedVerification.ResultJson;
            evidenceSha256 = storedVerification.EvidenceSha256;
            var actualEvidenceSha256 = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(resultJson)));
            if (!string.Equals(
                actualEvidenceSha256,
                evidenceSha256,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Stored verification evidence digest is invalid.");
            }

            result = JsonSerializer.Deserialize<RecoveryPackageVerificationResult>(
                resultJson,
                JsonOptions)
                ?? throw new InvalidOperationException("Stored verification evidence is invalid.");
            if (result.VerificationId != command.CommandId || result.PackageId != package.PackageId)
            {
                throw new InvalidOperationException("Stored verification evidence identity is invalid.");
            }
        }

        await RecordOutcomeAsync(
            identity,
            command,
            AgentEventType.JobCompleted,
            LocalAgentCommandStatus.Completed,
            JsonSerializer.Serialize(
                new
                {
                    result.VerificationId,
                    result.PackageId,
                    result.Passed,
                    levels = result.Levels.Select(level => new { level.Level, level.Passed }),
                    evidenceSha256
                },
                JsonOptions),
            cancellationToken);
    }

    private async Task RecordOutcomeAsync(
        StoredAgentIdentity identity,
        AgentCommandEnvelope command,
        AgentEventType eventType,
        LocalAgentCommandStatus expectedStatus,
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
            expectedStatus,
            finalStatus,
            now,
            cancellationToken);
    }

    private static string ClassifyFailure(Exception exception) =>
        exception switch
        {
            UnauthorizedAccessException => "discovery-not-authorized",
            DirectoryNotFoundException => "discovery-root-unavailable",
            FileNotFoundException or IOException => "discovery-content-unavailable",
            JsonException or ArgumentException => "invalid-command-payload",
            InvalidOperationException => "command-not-executable",
            _ => "command-execution-failed"
        };

    private sealed record StartDiscoveryPayload(
        string PluginId,
        string RootPath,
        int MaxFiles = 1_000);

    private sealed record CreateBackupPayload(Guid DiscoveryCommandId);

    private sealed record VerifyBackupPayload(Guid BackupCommandId);
}
