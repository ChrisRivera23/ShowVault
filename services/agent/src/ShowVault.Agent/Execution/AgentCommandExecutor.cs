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
    SystemInventoryPlugin systemInventoryPlugin,
    NetworkDeviceDiscoveryPlugin networkDeviceDiscoveryPlugin,
    ApprovedSubnetDiscovery approvedSubnetDiscovery,
    MaLightingNetworkIdentification maLightingIdentification,
    YamahaDmeNetworkIdentification yamahaDmeIdentification,
    GrandMa2NetworkIdentification grandMa2Identification,
    RecoveryPackageWriter packageWriter,
    RecoveryPackageVerifier packageVerifier,
    RecoveryPackageRestorer packageRestorer,
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
                case AgentCommandType.CollectSystemInventory:
                    await ExecuteSystemInventoryAsync(identity, command, cancellationToken);
                    break;
                case AgentCommandType.DiscoverNetworkDevices:
                    await ExecuteNetworkDiscoveryAsync(identity, command, cancellationToken);
                    break;
                case AgentCommandType.DiscoverApprovedSubnet:
                    await ExecuteApprovedSubnetDiscoveryAsync(identity, command, cancellationToken);
                    break;
                case AgentCommandType.IdentifyMaLighting:
                    await ExecuteMaLightingIdentificationAsync(identity, command, cancellationToken);
                    break;
                case AgentCommandType.IdentifyYamahaDme:
                    await ExecuteYamahaDmeIdentificationAsync(identity, command, cancellationToken);
                    break;
                case AgentCommandType.IdentifyGrandMa2:
                    await ExecuteGrandMa2IdentificationAsync(identity, command, cancellationToken);
                    break;
                case AgentCommandType.ApplyRecoveryCandidateDecision:
                    await ExecuteRecoveryCandidateDecisionAsync(identity, command, cancellationToken);
                    break;
                case AgentCommandType.ApplySubnetProposalDecision:
                    await ExecuteSubnetProposalDecisionAsync(identity, command, cancellationToken);
                    break;
                case AgentCommandType.ValidateRecoveryCandidate:
                    await ExecuteRecoveryCandidateValidationAsync(identity, command, cancellationToken);
                    break;
                case AgentCommandType.CreateBackup:
                    await ExecuteCreateBackupAsync(identity, command, cancellationToken);
                    break;
                case AgentCommandType.VerifyBackup:
                    await ExecuteVerifyBackupAsync(identity, command, cancellationToken);
                    break;
                case AgentCommandType.StartRestore:
                    await ExecuteRestoreAsync(identity, command, cancellationToken);
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
                    commandId = command.CommandId,
                    result.PluginId,
                    result.RootPath,
                    fileCount = result.Files.Count,
                    result.Truncated
                },
                JsonOptions),
            cancellationToken);
    }

    private async Task ExecuteRecoveryCandidateDecisionAsync(
        StoredAgentIdentity identity,
        AgentCommandEnvelope command,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ApplyRecoveryCandidateDecisionPayload>(
            command.Payload,
            JsonOptions)
            ?? throw new InvalidOperationException("Recovery candidate decision payload is required.");
        if (payload.CandidateId == Guid.Empty)
        {
            throw new InvalidOperationException("Recovery candidate ID must not be empty.");
        }

        var applied = await queueStore.ApplyRecoveryCandidateDecisionAsync(
            payload.CandidateId,
            payload.Approved,
            command.IssuedAt,
            cancellationToken);
        if (!applied)
        {
            throw new InvalidOperationException(
                "The recovery candidate is not present in this Agent's local inventory.");
        }

        await RecordOutcomeAsync(
            identity,
            command,
            AgentEventType.JobCompleted,
            LocalAgentCommandStatus.Completed,
            JsonSerializer.Serialize(new
            {
                payload.CandidateId,
                payload.Approved
            }, JsonOptions),
            cancellationToken);
    }

    private async Task ExecuteRecoveryCandidateValidationAsync(
        StoredAgentIdentity identity,
        AgentCommandEnvelope command,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ValidateRecoveryCandidatePayload>(
            command.Payload,
            JsonOptions)
            ?? throw new InvalidOperationException("Recovery candidate validation payload is required.");
        if (payload.CandidateId == Guid.Empty || payload.MaxFiles is < 1 or > 100_000)
        {
            throw new InvalidOperationException("Recovery candidate validation payload is invalid.");
        }

        var scope = await queueStore.GetApprovedRecoveryScopeAsync(
            payload.CandidateId,
            cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "The recovery candidate does not have an approved local scope.");
        if (!string.Equals(scope.CandidateType, "UserDataRoot", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "This recovery candidate type is not eligible for product validation.");
        }

        var plugin = pluginRegistry.GetRequired(scope.PluginId);
        var result = await plugin.DiscoverAsync(
            new DiscoveryRequest(scope.LocalPath, payload.MaxFiles),
            cancellationToken);
        await queueStore.StoreDiscoveryResultAsync(
            command.CommandId,
            JsonSerializer.Serialize(result, JsonOptions),
            result.CompletedAt,
            cancellationToken);
        await RecordOutcomeAsync(
            identity,
            command,
            AgentEventType.JobCompleted,
            LocalAgentCommandStatus.Completed,
            JsonSerializer.Serialize(new
            {
                payload.CandidateId,
                result.PluginId,
                fileCount = result.Files.Count,
                result.Truncated
            }, JsonOptions),
            cancellationToken);
    }

    private async Task ExecuteSubnetProposalDecisionAsync(
        StoredAgentIdentity identity,
        AgentCommandEnvelope command,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ApplySubnetProposalDecisionPayload>(command.Payload, JsonOptions)
            ?? throw new InvalidOperationException("Subnet proposal decision payload is required.");
        if (payload.ProposalId == Guid.Empty || !await queueStore.ApplySubnetProposalDecisionAsync(
                payload.ProposalId, payload.Approved, command.IssuedAt, cancellationToken))
        {
            throw new InvalidOperationException("The subnet proposal is not present in this Agent's local inventory.");
        }

        await RecordOutcomeAsync(
            identity,
            command,
            AgentEventType.JobCompleted,
            LocalAgentCommandStatus.Completed,
            JsonSerializer.Serialize(payload, JsonOptions),
            cancellationToken);
    }

    private async Task ExecuteSystemInventoryAsync(
        StoredAgentIdentity identity,
        AgentCommandEnvelope command,
        CancellationToken cancellationToken)
    {
        var result = await systemInventoryPlugin.CollectAsync(cancellationToken);
        await queueStore.StoreDiscoveryResultAsync(
            command.CommandId,
            JsonSerializer.Serialize(result, JsonOptions),
            result.CollectedAt,
            cancellationToken);
        await queueStore.StoreRecoveryCandidatesAsync(
            result.RecoveryCandidates,
            result.CollectedAt,
            cancellationToken);
        await queueStore.StoreSubnetProposalsAsync(
            result.SubnetProposals,
            result.CollectedAt,
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
                    result.OperatingSystem,
                    result.OsArchitecture,
                    result.LogicalProcessorCount,
                    volumeCount = result.Volumes.Count,
                    recoveryCandidateCount = result.RecoveryCandidates.Count,
                    subnetProposalCount = result.SubnetProposals.Count,
                    subnetProposals = result.SubnetProposals,
                    recoveryCandidates = result.RecoveryCandidates.Select(candidate => new
                    {
                        candidate.CandidateId,
                        candidate.PluginId,
                        candidate.ProductName,
                        candidate.CandidateType,
                        candidate.Evidence
                    })
                },
                JsonOptions),
            cancellationToken);
    }

    private async Task ExecuteNetworkDiscoveryAsync(
        StoredAgentIdentity identity,
        AgentCommandEnvelope command,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<DiscoverNetworkDevicesPayload>(
            command.Payload,
            JsonOptions)
            ?? throw new InvalidOperationException("DiscoverNetworkDevices payload is required.");
        var result = await networkDeviceDiscoveryPlugin.DiscoverAsync(
            payload.Targets,
            payload.TimeoutMilliseconds,
            cancellationToken);
        await queueStore.StoreDiscoveryResultAsync(
            command.CommandId,
            JsonSerializer.Serialize(result, JsonOptions),
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
                    targetCount = result.Devices.Count,
                    reachableCount = result.Devices.Count(
                        device => device.Status == NetworkProbeStatus.Reachable)
                },
                JsonOptions),
            cancellationToken);
    }

    private async Task ExecuteApprovedSubnetDiscoveryAsync(
        StoredAgentIdentity identity,
        AgentCommandEnvelope command,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<DiscoverApprovedSubnetPayload>(command.Payload, JsonOptions)
            ?? throw new InvalidOperationException("Approved subnet discovery payload is required.");
        var subnet = await queueStore.GetApprovedSubnetAsync(payload.ProposalId, cancellationToken)
            ?? throw new InvalidOperationException("The subnet is not approved on this Agent.");
        var result = await approvedSubnetDiscovery.DiscoverAsync(
            subnet, payload.MaxHosts, payload.TimeoutMilliseconds, cancellationToken);
        await queueStore.StoreReachableSubnetHostsAsync(command.CommandId, result.ProposalId,
            result.RespondingAddresses, result.CompletedAt, cancellationToken);
        var pathFreeResult = new
        {
            result.ProposalId,
            result.AttemptedHostCount,
            result.RespondingHostCount,
            result.PassiveCandidateCount,
            result.FallbackTargetCount,
            result.CompletedAt
        };
        await queueStore.StoreDiscoveryResultAsync(command.CommandId,
            JsonSerializer.Serialize(pathFreeResult, JsonOptions), result.CompletedAt, cancellationToken);
        await RecordOutcomeAsync(identity, command, AgentEventType.JobCompleted,
            LocalAgentCommandStatus.Completed, JsonSerializer.Serialize(pathFreeResult, JsonOptions), cancellationToken);
    }

    private async Task ExecuteMaLightingIdentificationAsync(
        StoredAgentIdentity identity,
        AgentCommandEnvelope command,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<IdentifyMaLightingPayload>(command.Payload, JsonOptions)
            ?? throw new InvalidOperationException("MA Lighting identification payload is required.");
        if (!await queueStore.IsReachableHostAuthorizationAsync(
                payload.ProposalId, payload.DiscoveryCommandId, cancellationToken))
            throw new InvalidOperationException("The responding-host authorization is not present on this Agent.");
        var hosts = await queueStore.GetReachableSubnetHostsAsync(payload.DiscoveryCommandId, cancellationToken);
        var result = await maLightingIdentification.IdentifyAsync(payload.ProposalId,
            payload.DiscoveryCommandId, hosts, payload.TimeoutMilliseconds, cancellationToken);
        await queueStore.StoreMaLightingIdentificationsAsync(command.CommandId, result, cancellationToken);
        var pathFreeResult = new
        {
            result.ProposalId,
            result.DiscoveryCommandId,
            result.AttemptedHostCount,
            identifiedHostCount = result.Identifications.Count,
            productFamilies = result.Identifications.Select(item => item.ProductFamily).Distinct().Order().ToArray(),
            result.CompletedAt
        };
        await queueStore.StoreDiscoveryResultAsync(command.CommandId,
            JsonSerializer.Serialize(pathFreeResult, JsonOptions), result.CompletedAt, cancellationToken);
        await RecordOutcomeAsync(identity, command, AgentEventType.JobCompleted,
            LocalAgentCommandStatus.Completed, JsonSerializer.Serialize(pathFreeResult, JsonOptions), cancellationToken);
    }

    private async Task ExecuteYamahaDmeIdentificationAsync(
        StoredAgentIdentity identity,
        AgentCommandEnvelope command,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<IdentifyYamahaDmePayload>(command.Payload, JsonOptions)
            ?? throw new InvalidOperationException("Yamaha DME identification payload is required.");
        if (!await queueStore.IsReachableHostAuthorizationAsync(
                payload.ProposalId, payload.DiscoveryCommandId, cancellationToken))
            throw new InvalidOperationException("The responding-host authorization is not present on this Agent.");
        var hosts = await queueStore.GetReachableSubnetHostsAsync(payload.DiscoveryCommandId, cancellationToken);
        var result = await yamahaDmeIdentification.IdentifyAsync(payload.ProposalId,
            payload.DiscoveryCommandId, hosts, payload.TimeoutMilliseconds, cancellationToken);
        await queueStore.StoreYamahaDmeIdentificationsAsync(command.CommandId, result, cancellationToken);
        var pathFreeResult = new
        {
            result.ProposalId,
            result.DiscoveryCommandId,
            result.AttemptedHostCount,
            identifiedHostCount = result.Identifications.Count,
            productFamilies = result.Identifications.Select(item => item.ProductFamily).Distinct().Order().ToArray(),
            result.CompletedAt
        };
        await queueStore.StoreDiscoveryResultAsync(command.CommandId,
            JsonSerializer.Serialize(pathFreeResult, JsonOptions), result.CompletedAt, cancellationToken);
        await RecordOutcomeAsync(identity, command, AgentEventType.JobCompleted,
            LocalAgentCommandStatus.Completed, JsonSerializer.Serialize(pathFreeResult, JsonOptions), cancellationToken);
    }

    private async Task ExecuteGrandMa2IdentificationAsync(
        StoredAgentIdentity identity,
        AgentCommandEnvelope command,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<IdentifyGrandMa2Payload>(command.Payload, JsonOptions)
            ?? throw new InvalidOperationException("grandMA2 identification payload is required.");
        if (!await queueStore.IsReachableHostAuthorizationAsync(
                payload.ProposalId, payload.DiscoveryCommandId, cancellationToken))
            throw new InvalidOperationException("The responding-host authorization is not present on this Agent.");
        var hosts = await queueStore.GetReachableSubnetHostsAsync(payload.DiscoveryCommandId, cancellationToken);
        var result = await grandMa2Identification.IdentifyAsync(payload.ProposalId,
            payload.DiscoveryCommandId, hosts, payload.TimeoutMilliseconds, cancellationToken);
        await queueStore.StoreGrandMa2IdentificationsAsync(command.CommandId, result, cancellationToken);
        var pathFreeResult = new
        {
            result.ProposalId,
            result.DiscoveryCommandId,
            result.AttemptedHostCount,
            identifiedHostCount = result.Identifications.Count,
            productFamilies = result.Identifications.Select(item => item.ProductFamily).Distinct().Order().ToArray(),
            result.CompletedAt
        };
        await queueStore.StoreDiscoveryResultAsync(command.CommandId,
            JsonSerializer.Serialize(pathFreeResult, JsonOptions), result.CompletedAt, cancellationToken);
        await RecordOutcomeAsync(identity, command, AgentEventType.JobCompleted,
            LocalAgentCommandStatus.Completed, JsonSerializer.Serialize(pathFreeResult, JsonOptions), cancellationToken);
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

    private async Task ExecuteRestoreAsync(
        StoredAgentIdentity identity,
        AgentCommandEnvelope command,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<StartRestorePayload>(command.Payload, JsonOptions)
            ?? throw new InvalidOperationException("StartRestore payload is required.");
        var package = await queueStore.GetRecoveryPackageAsync(
            payload.BackupCommandId,
            cancellationToken)
            ?? throw new InvalidOperationException("The referenced recovery package was not found.");
        var verification = await queueStore.GetPackageVerificationAsync(
            payload.VerificationCommandId,
            cancellationToken)
            ?? throw new InvalidOperationException("The referenced package verification was not found.");
        ValidatePassingVerification(payload.VerificationCommandId, package.PackageId, verification);

        var storedRestoration = await queueStore.GetRecoveryRestorationAsync(
            command.CommandId,
            cancellationToken);
        RecoveryRestorationResult result;
        string resultJson;
        string evidenceSha256;
        if (storedRestoration is null)
        {
            result = await packageRestorer.RestoreAsync(
                command.CommandId,
                identity.AgentId,
                package,
                payload.VerificationCommandId,
                payload.TargetPath,
                command.IssuedAt,
                cancellationToken);
            resultJson = RecoveryPackageRestorer.Serialize(result);
            evidenceSha256 = HashEvidence(resultJson);
            await queueStore.StoreRecoveryRestorationAsync(
                command.CommandId,
                package.PackageId,
                result.TargetPath,
                resultJson,
                evidenceSha256,
                command.IssuedAt,
                cancellationToken);
        }
        else
        {
            if (storedRestoration.PackageId != package.PackageId ||
                HashEvidence(storedRestoration.ResultJson) != storedRestoration.EvidenceSha256)
            {
                throw new InvalidOperationException("Stored restoration evidence is invalid.");
            }

            resultJson = storedRestoration.ResultJson;
            evidenceSha256 = storedRestoration.EvidenceSha256;
            result = JsonSerializer.Deserialize<RecoveryRestorationResult>(resultJson, JsonOptions)
                ?? throw new InvalidOperationException("Stored restoration evidence is invalid.");
            if (result.RestorationId != command.CommandId ||
                result.PackageId != package.PackageId ||
                result.VerificationId != payload.VerificationCommandId)
            {
                throw new InvalidOperationException("Stored restoration evidence identity is invalid.");
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
                    result.RestorationId,
                    result.PackageId,
                    result.VerificationId,
                    result.TargetPath,
                    result.Passed,
                    result.FileCount,
                    evidenceSha256
                },
                JsonOptions),
            cancellationToken);
    }

    private static void ValidatePassingVerification(
        Guid verificationCommandId,
        string packageId,
        StoredPackageVerification verification)
    {
        if (verification.PackageId != packageId ||
            HashEvidence(verification.ResultJson) != verification.EvidenceSha256)
        {
            throw new InvalidOperationException("Package verification evidence is invalid.");
        }

        var result = JsonSerializer.Deserialize<RecoveryPackageVerificationResult>(
            verification.ResultJson,
            JsonOptions)
            ?? throw new InvalidOperationException("Package verification evidence is invalid.");
        if (!result.Passed || result.VerificationId != verificationCommandId ||
            result.PackageId != packageId)
        {
            throw new InvalidOperationException("A passing verification is required before restore.");
        }
    }

    private static string HashEvidence(string resultJson) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(resultJson)));

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

    private sealed record DiscoverNetworkDevicesPayload(
        IReadOnlyList<string> Targets,
        int TimeoutMilliseconds = 1_000);

    private sealed record VerifyBackupPayload(Guid BackupCommandId);

    private sealed record StartRestorePayload(
        Guid BackupCommandId,
        Guid VerificationCommandId,
        string TargetPath);
}
