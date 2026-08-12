using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Execution;
using ShowVault.Agent.Identity;
using ShowVault.Agent.Plugins;
using ShowVault.Agent.Queue;
using ShowVault.Agent.Recovery;
using ShowVault.AgentContracts;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class AgentCommandExecutorTests : IAsyncLifetime
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "showvault-executor-tests",
        Guid.NewGuid().ToString("N"));

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_testRoot);
        Directory.CreateDirectory(Path.Combine(_testRoot, "restores"));
        return Task.CompletedTask;
    }

    [Fact]
    public async Task StartDiscovery_completes_durably_and_enqueues_one_stable_outcome()
    {
        var discoveryRoot = Path.Combine(_testRoot, "source");
        Directory.CreateDirectory(discoveryRoot);
        await File.WriteAllTextAsync(Path.Combine(discoveryRoot, "console.show"), "settings");
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var command = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.StartDiscovery,
            "discovery-correlation",
            JsonSerializer.Serialize(new
            {
                pluginId = FileSystemDiscoveryPlugin.PluginId,
                rootPath = discoveryRoot,
                maxFiles = 10
            }),
            now,
            TimeSpan.FromMinutes(5));
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueCommandAsync(command, now, CancellationToken.None);
        var executor = CreateExecutor(store, now);
        var identity = new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential");

        await executor.ExecutePendingOnceAsync(identity, CancellationToken.None);
        await executor.ExecutePendingOnceAsync(identity, CancellationToken.None);

        Assert.Empty(await store.GetPendingCommandsAsync(CancellationToken.None));
        Assert.Single(await store.GetCommandsAsync(
            LocalAgentCommandStatus.Completed,
            CancellationToken.None));
        var events = await store.GetPendingEventsAsync(
            now.AddMinutes(1),
            10,
            CancellationToken.None);
        var outcome = Assert.Single(events).Envelope;
        Assert.Equal(command.CommandId, outcome.EventId);
        Assert.Equal(AgentEventType.JobCompleted, outcome.Type);
        Assert.Equal(command.CorrelationId, outcome.CorrelationId);
        Assert.Contains("fileCount", outcome.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(discoveryRoot, outcome.Payload, StringComparison.Ordinal);
        var resultJson = await store.GetDiscoveryResultJsonAsync(
            command.CommandId,
            CancellationToken.None);
        Assert.Contains("console.show", resultJson, StringComparison.Ordinal);
        Assert.Contains(discoveryRoot, resultJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CollectSystemInventory_persists_inventory_and_completes_durably()
    {
        var now = DateTimeOffset.UtcNow;
        const string privateMachineName = "private-synthetic-machine";
        const string privateVolumeName = "private-synthetic-volume";
        var agentId = Guid.NewGuid();
        var command = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.CollectSystemInventory,
            "inventory-correlation",
            "{}",
            now,
            TimeSpan.FromMinutes(5));
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueCommandAsync(command, now, CancellationToken.None);
        var logger = new CapturingLogger();
        var inventorySource = new TestSystemInventorySource(
            new SystemInventoryHostFacts(
                privateMachineName,
                "Synthetic OS",
                "X64",
                "X64",
                4),
            [new SystemVolume(privateVolumeName, "Fixed", 1_000, 500)]);

        var executor = CreateExecutor(store, now, logger, inventorySource);
        await executor.ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"),
            CancellationToken.None);
        await executor.ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"),
            CancellationToken.None);

        Assert.Single(await store.GetCommandsAsync(
            LocalAgentCommandStatus.Completed,
            CancellationToken.None));
        var inventoryJson = await store.GetDiscoveryResultJsonAsync(
            command.CommandId,
            CancellationToken.None);
        Assert.Contains(SystemInventoryPlugin.PluginId, inventoryJson, StringComparison.Ordinal);
        Assert.Contains("logicalProcessorCount", inventoryJson, StringComparison.Ordinal);
        Assert.Contains(privateMachineName, inventoryJson, StringComparison.Ordinal);
        Assert.Contains(privateVolumeName, inventoryJson, StringComparison.Ordinal);
        var outcome = Assert.Single(await store.GetPendingEventsAsync(
            now.AddMinutes(1),
            10,
            CancellationToken.None)).Envelope;
        Assert.Equal(AgentEventType.JobCompleted, outcome.Type);
        Assert.Contains("volumeCount", outcome.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(privateMachineName, outcome.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(privateVolumeName, outcome.Payload, StringComparison.Ordinal);
        Assert.All(logger.Messages, message =>
        {
            Assert.DoesNotContain(privateMachineName, message, StringComparison.Ordinal);
            Assert.DoesNotContain(privateVolumeName, message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task CollectSystemInventory_failure_emits_only_a_bounded_category()
    {
        var now = DateTimeOffset.UtcNow;
        const string sensitiveFailure = "private-inventory-source-detail";
        var agentId = Guid.NewGuid();
        var command = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.CollectSystemInventory,
            "inventory-failure-correlation",
            "{}",
            now,
            TimeSpan.FromMinutes(5));
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueCommandAsync(command, now, CancellationToken.None);
        var logger = new CapturingLogger();

        await CreateExecutor(
                store,
                now,
                logger,
                new ThrowingSystemInventorySource(sensitiveFailure))
            .ExecutePendingOnceAsync(
                new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"),
                CancellationToken.None);

        Assert.Single(await store.GetCommandsAsync(
            LocalAgentCommandStatus.Failed,
            CancellationToken.None));
        Assert.Null(await store.GetDiscoveryResultJsonAsync(
            command.CommandId,
            CancellationToken.None));
        var outcome = Assert.Single(await store.GetPendingEventsAsync(
            now.AddMinutes(1),
            10,
            CancellationToken.None)).Envelope;
        Assert.Equal(AgentEventType.JobFailed, outcome.Type);
        Assert.Equal(JsonSerializer.Serialize(new { error = "command-not-executable" }), outcome.Payload);
        Assert.DoesNotContain(sensitiveFailure, outcome.Payload, StringComparison.Ordinal);
        Assert.All(
            logger.Messages,
            message => Assert.DoesNotContain(sensitiveFailure, message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CollectSystemInventory_cancellation_emits_no_completion_event()
    {
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var command = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.CollectSystemInventory,
            "inventory-cancellation-correlation",
            "{}",
            now,
            TimeSpan.FromMinutes(5));
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueCommandAsync(command, now, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var executor = CreateExecutor(
            store,
            now,
            inventorySource: new CancelingSystemInventorySource(cancellation));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            executor.ExecutePendingOnceAsync(
                new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"),
                cancellation.Token));

        Assert.Single(await store.GetCommandsAsync(
            LocalAgentCommandStatus.Running,
            CancellationToken.None));
        Assert.Empty(await store.GetPendingEventsAsync(
            now.AddMinutes(1),
            10,
            CancellationToken.None));
        Assert.Null(await store.GetDiscoveryResultJsonAsync(
            command.CommandId,
            CancellationToken.None));
    }

    [Fact]
    public async Task Running_command_resumes_after_restart_and_records_failure()
    {
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var command = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.StartDiscovery,
            "failed-correlation",
            JsonSerializer.Serialize(new
            {
                pluginId = FileSystemDiscoveryPlugin.PluginId,
                rootPath = Path.Combine(_testRoot, "missing")
            }),
            now,
            TimeSpan.FromMinutes(5));
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueCommandAsync(command, now, CancellationToken.None);
        Assert.True(await store.TryTransitionCommandAsync(
            command.CommandId,
            LocalAgentCommandStatus.Pending,
            LocalAgentCommandStatus.Running,
            now,
            CancellationToken.None));

        var restartedStore = CreateStore();
        var logger = new CapturingLogger();
        await CreateExecutor(restartedStore, now, logger).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"),
            CancellationToken.None);

        Assert.Single(await restartedStore.GetCommandsAsync(
            LocalAgentCommandStatus.Failed,
            CancellationToken.None));
        var events = await restartedStore.GetPendingEventsAsync(
            now.AddMinutes(1),
            10,
            CancellationToken.None);
        var outcome = Assert.Single(events).Envelope;
        Assert.Equal(AgentEventType.JobFailed, outcome.Type);
        Assert.Equal("{\"error\":\"discovery-root-unavailable\"}", outcome.Payload);
        Assert.DoesNotContain(_testRoot, outcome.Payload, StringComparison.Ordinal);
        Assert.All(
            logger.Messages,
            message => Assert.DoesNotContain(_testRoot, message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Expired_pending_command_is_terminal_without_plugin_execution()
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var command = CreateDiscoveryCommand(agentId, issuedAt, TimeSpan.FromSeconds(1));
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueCommandAsync(command, issuedAt, CancellationToken.None);

        await CreateExecutor(store, issuedAt.AddMinutes(1)).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"),
            CancellationToken.None);
        await CreateExecutor(store, issuedAt.AddMinutes(2)).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"),
            CancellationToken.None);

        Assert.Single(await store.GetCommandsAsync(
            LocalAgentCommandStatus.Expired,
            CancellationToken.None));
        Assert.Null(await store.GetDiscoveryResultJsonAsync(command.CommandId, CancellationToken.None));
        var outcome = Assert.Single(await store.GetPendingEventsAsync(
            issuedAt.AddMinutes(2),
            10,
            CancellationToken.None)).Envelope;
        Assert.Equal(AgentEventType.JobFailed, outcome.Type);
        Assert.Equal("{\"error\":\"command-expired\"}", outcome.Payload);
    }

    [Fact]
    public async Task Expired_running_command_is_terminal_after_restart_without_resuming_plugin()
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var command = CreateDiscoveryCommand(agentId, issuedAt, TimeSpan.FromSeconds(1));
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueCommandAsync(command, issuedAt, CancellationToken.None);
        Assert.True(await store.TryStartCommandAsync(
            command.CommandId,
            command.ExpiresAt,
            issuedAt,
            CancellationToken.None));

        var restartedStore = CreateStore();
        await CreateExecutor(restartedStore, issuedAt.AddMinutes(1)).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"),
            CancellationToken.None);
        await CreateExecutor(restartedStore, issuedAt.AddMinutes(2)).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"),
            CancellationToken.None);

        Assert.Single(await restartedStore.GetCommandsAsync(
            LocalAgentCommandStatus.Expired,
            CancellationToken.None));
        Assert.Null(await restartedStore.GetDiscoveryResultJsonAsync(
            command.CommandId,
            CancellationToken.None));
        Assert.Single(await restartedStore.GetPendingEventsAsync(
            issuedAt.AddMinutes(3),
            10,
            CancellationToken.None));
    }

    [Theory]
    [InlineData("unauthorized", "discovery-not-authorized")]
    [InlineData("missing-root", "discovery-root-unavailable")]
    [InlineData("malformed", "invalid-command-payload")]
    [InlineData("unknown-plugin", "command-not-executable")]
    public async Task Failure_outcomes_and_logs_use_bounded_path_free_categories(
        string scenario,
        string expectedCategory)
    {
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var sensitivePath = scenario == "unauthorized"
            ? Path.Combine(
                Path.GetDirectoryName(_testRoot)!,
                $"sensitive-outside-{Guid.NewGuid():N}")
            : Path.Combine(_testRoot, "sensitive-local-path");
        var payload = scenario switch
        {
            "unauthorized" => JsonSerializer.Serialize(new
            {
                pluginId = FileSystemDiscoveryPlugin.PluginId,
                rootPath = sensitivePath
            }),
            "missing-root" => JsonSerializer.Serialize(new
            {
                pluginId = FileSystemDiscoveryPlugin.PluginId,
                rootPath = sensitivePath
            }),
            "malformed" => JsonSerializer.Serialize(new
            {
                pluginId = FileSystemDiscoveryPlugin.PluginId,
                rootPath = (string?)null,
                marker = sensitivePath
            }),
            "unknown-plugin" => JsonSerializer.Serialize(new
            {
                pluginId = sensitivePath,
                rootPath = _testRoot
            }),
            _ => throw new InvalidOperationException("Unknown test scenario.")
        };
        var command = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.StartDiscovery,
            $"failure-{scenario}",
            payload,
            now,
            TimeSpan.FromMinutes(5));
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueCommandAsync(command, now, CancellationToken.None);
        var logger = new CapturingLogger();

        await CreateExecutor(store, now, logger).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"),
            CancellationToken.None);

        var outcome = Assert.Single(await store.GetPendingEventsAsync(
            now.AddMinutes(1),
            10,
            CancellationToken.None)).Envelope;
        Assert.Equal(JsonSerializer.Serialize(new { error = expectedCategory }), outcome.Payload);
        Assert.DoesNotContain(sensitivePath, outcome.Payload, StringComparison.Ordinal);
        Assert.All(
            logger.Messages,
            message => Assert.DoesNotContain(sensitivePath, message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateBackup_packages_a_completed_discovery_and_records_it_durably()
    {
        var discoveryRoot = Path.Combine(_testRoot, "backup-source");
        Directory.CreateDirectory(discoveryRoot);
        await File.WriteAllTextAsync(Path.Combine(discoveryRoot, "venue.show"), "configuration");
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var discoveryCommand = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.StartDiscovery,
            "discovery",
            JsonSerializer.Serialize(new
            {
                pluginId = FileSystemDiscoveryPlugin.PluginId,
                rootPath = discoveryRoot
            }),
            now,
            TimeSpan.FromMinutes(5));
        var backupCommand = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.CreateBackup,
            "backup",
            JsonSerializer.Serialize(new { discoveryCommandId = discoveryCommand.CommandId }),
            now.AddSeconds(1),
            TimeSpan.FromMinutes(5));
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueCommandAsync(discoveryCommand, now, CancellationToken.None);
        var executor = CreateExecutor(store, now);
        var identity = new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential");
        await executor.ExecutePendingOnceAsync(identity, CancellationToken.None);
        await store.EnqueueCommandAsync(backupCommand, now, CancellationToken.None);

        await executor.ExecutePendingOnceAsync(identity, CancellationToken.None);

        var package = await store.GetRecoveryPackageAsync(
            backupCommand.CommandId,
            CancellationToken.None);
        Assert.NotNull(package);
        Assert.True(Directory.Exists(package.PackagePath));
        Assert.True(File.Exists(Path.Combine(
            package.PackagePath,
            RecoveryPackageFormat.ManifestFileName)));
        Assert.Equal(
            "configuration",
            await File.ReadAllTextAsync(Path.Combine(
                package.PackagePath,
                RecoveryPackageFormat.ContentDirectoryName,
                "venue.show")));

        var verifyCommand = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.VerifyBackup,
            "verify",
            JsonSerializer.Serialize(new { backupCommandId = backupCommand.CommandId }),
            now.AddSeconds(2),
            TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(verifyCommand, now, CancellationToken.None);
        await executor.ExecutePendingOnceAsync(identity, CancellationToken.None);
        await executor.ExecutePendingOnceAsync(identity, CancellationToken.None);

        var verification = await store.GetPackageVerificationAsync(
            verifyCommand.CommandId,
            CancellationToken.None);
        Assert.NotNull(verification);
        Assert.Equal(package.PackageId, verification.PackageId);
        Assert.Equal(64, verification.EvidenceSha256.Length);
        Assert.Contains("\"passed\":true", verification.ResultJson, StringComparison.Ordinal);
        var verificationResult = JsonSerializer.Deserialize<RecoveryPackageVerificationResult>(
            verification.ResultJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(verificationResult);
        Assert.Equal(now, verificationResult.VerifiedAt);
        Assert.Single((await store.GetCommandsAsync(
            LocalAgentCommandStatus.Completed,
            CancellationToken.None)), candidate => candidate.CommandId == verifyCommand.CommandId);

        var restoreTarget = Path.Combine(_testRoot, "restores", "restored-venue");
        var restoreCommand = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.StartRestore,
            "restore",
            JsonSerializer.Serialize(new
            {
                backupCommandId = backupCommand.CommandId,
                verificationCommandId = verifyCommand.CommandId,
                targetPath = restoreTarget
            }),
            now.AddSeconds(3),
            TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(restoreCommand, now, CancellationToken.None);
        await executor.ExecutePendingOnceAsync(identity, CancellationToken.None);
        await executor.ExecutePendingOnceAsync(identity, CancellationToken.None);

        Assert.Equal(
            "configuration",
            await File.ReadAllTextAsync(Path.Combine(restoreTarget, "venue.show")));
        var restoration = await store.GetRecoveryRestorationAsync(
            restoreCommand.CommandId,
            CancellationToken.None);
        Assert.NotNull(restoration);
        Assert.Equal(package.PackageId, restoration.PackageId);
        Assert.Equal(64, restoration.EvidenceSha256.Length);
        var restorationResult = JsonSerializer.Deserialize<RecoveryRestorationResult>(
            restoration.ResultJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(restorationResult);
        Assert.Equal(now, restorationResult.RestoredAt);
        var restoreOutcome = Assert.Single(
            await store.GetPendingEventsAsync(now.AddMinutes(1), 10, CancellationToken.None),
            candidate => candidate.Envelope.EventId == restoreCommand.CommandId);
        Assert.DoesNotContain(restoreTarget, restoreOutcome.Envelope.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("targetPath", restoreOutcome.Envelope.Payload, StringComparison.Ordinal);

        var replayTarget = Path.Combine(_testRoot, "restores", "replay-target");
        var mismatchedTarget = Path.Combine(_testRoot, "restores", "different-target");
        var replayCommand = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.StartRestore,
            "restore-replay",
            JsonSerializer.Serialize(new
            {
                backupCommandId = backupCommand.CommandId,
                verificationCommandId = verifyCommand.CommandId,
                targetPath = replayTarget
            }),
            now.AddSeconds(4),
            TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(replayCommand, now, CancellationToken.None);
        var mismatchedResultJson = RecoveryPackageRestorer.Serialize(new RecoveryRestorationResult(
            replayCommand.CommandId,
            package.PackageId,
            verifyCommand.CommandId,
            mismatchedTarget,
            now,
            1,
            true,
            ["stored evidence"]));
        await store.StoreRecoveryRestorationAsync(
            replayCommand.CommandId,
            package.PackageId,
            mismatchedTarget,
            mismatchedResultJson,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(mismatchedResultJson))),
            now,
            CancellationToken.None);

        await executor.ExecutePendingOnceAsync(identity, CancellationToken.None);

        Assert.Contains(
            await store.GetCommandsAsync(LocalAgentCommandStatus.Failed, CancellationToken.None),
            candidate => candidate.CommandId == replayCommand.CommandId);
        Assert.False(Directory.Exists(replayTarget));
        var replayOutcome = Assert.Single(
            await store.GetPendingEventsAsync(now.AddMinutes(1), 10, CancellationToken.None),
            candidate => candidate.Envelope.EventId == replayCommand.CommandId);
        Assert.DoesNotContain(replayTarget, replayOutcome.Envelope.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(mismatchedTarget, replayOutcome.Envelope.Payload, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("digest")]
    [InlineData("identity")]
    public async Task Stored_verification_evidence_replay_is_validated(string scenario)
    {
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var backupCommand = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.CreateBackup,
            "stored-backup",
            JsonSerializer.Serialize(new { discoveryCommandId = Guid.NewGuid() }),
            now.AddMinutes(-2),
            TimeSpan.FromMinutes(5));
        var verifyCommand = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.VerifyBackup,
            $"stored-verification-{scenario}",
            JsonSerializer.Serialize(new { backupCommandId = backupCommand.CommandId }),
            now.AddMinutes(-1),
            TimeSpan.FromMinutes(5));
        var packageId = new string('a', 64);
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueCommandAsync(backupCommand, now, CancellationToken.None);
        Assert.True(await store.TryTransitionCommandAsync(
            backupCommand.CommandId,
            LocalAgentCommandStatus.Pending,
            LocalAgentCommandStatus.Running,
            now,
            CancellationToken.None));
        Assert.True(await store.TryTransitionCommandAsync(
            backupCommand.CommandId,
            LocalAgentCommandStatus.Running,
            LocalAgentCommandStatus.Completed,
            now,
            CancellationToken.None));
        await store.StoreRecoveryPackageAsync(
            backupCommand.CommandId,
            packageId,
            Path.Combine(_testRoot, "unused-replay-path", packageId),
            "{}",
            now,
            CancellationToken.None);
        await store.EnqueueCommandAsync(verifyCommand, now, CancellationToken.None);
        var result = new RecoveryPackageVerificationResult(
            scenario == "identity" ? Guid.NewGuid() : verifyCommand.CommandId,
            packageId,
            now.AddSeconds(-30),
            true,
            [
                new RecoveryPackageVerificationLevel("structural", true, ["Passed."]),
                new RecoveryPackageVerificationLevel("cryptographic", true, ["Passed."])
            ]);
        var resultJson = RecoveryPackageVerifier.Serialize(result);
        var evidenceSha256 = scenario == "digest"
            ? new string('0', 64)
            : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(resultJson)));
        await store.StorePackageVerificationAsync(
            verifyCommand.CommandId,
            packageId,
            resultJson,
            evidenceSha256,
            result.VerifiedAt,
            CancellationToken.None);

        await CreateExecutor(store, now).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"),
            CancellationToken.None);

        var expectedStatus = scenario == "valid"
            ? LocalAgentCommandStatus.Completed
            : LocalAgentCommandStatus.Failed;
        Assert.Contains(
            await store.GetCommandsAsync(expectedStatus, CancellationToken.None),
            command => command.CommandId == verifyCommand.CommandId);
        var outcome = Assert.Single(await store.GetPendingEventsAsync(
            now.AddMinutes(1),
            10,
            CancellationToken.None));
        Assert.Equal(
            scenario == "valid" ? AgentEventType.JobCompleted : AgentEventType.JobFailed,
            outcome.Envelope.Type);
    }

    [Fact]
    public async Task Resolume_discovery_packages_exact_bundle_with_path_free_outcomes()
    {
        var bundle = Path.Combine(_testRoot, "resolume-bundle");
        Directory.CreateDirectory(Path.Combine(bundle, "media"));
        await File.WriteAllTextAsync(Path.Combine(bundle, "Venue.avc"), "composition");
        await File.WriteAllTextAsync(Path.Combine(bundle, "media", "intro.mov"), "media");
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var discovery = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.StartDiscovery,
            "resolume-discovery",
            JsonSerializer.Serialize(new
            {
                pluginId = ResolumeDiscoveryPlugin.PluginId,
                rootPath = bundle,
                maxFiles = ResolumeDiscoveryPlugin.MaximumFileLimit
            }),
            now,
            TimeSpan.FromMinutes(5));
        var backup = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.CreateBackup,
            "resolume-backup",
            JsonSerializer.Serialize(new { discoveryCommandId = discovery.CommandId }),
            now.AddSeconds(1),
            TimeSpan.FromMinutes(5));
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueCommandAsync(discovery, now, CancellationToken.None);
        var executor = CreateExecutor(store, now);
        var identity = new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential");

        await executor.ExecutePendingOnceAsync(identity, CancellationToken.None);
        await store.EnqueueCommandAsync(backup, now, CancellationToken.None);
        await executor.ExecutePendingOnceAsync(identity, CancellationToken.None);
        await executor.ExecutePendingOnceAsync(identity, CancellationToken.None);

        var localResult = await store.GetDiscoveryResultJsonAsync(
            discovery.CommandId,
            CancellationToken.None);
        Assert.Contains(bundle, localResult, StringComparison.Ordinal);
        Assert.Contains("Venue.avc", localResult, StringComparison.Ordinal);
        var package = await store.GetRecoveryPackageAsync(
            backup.CommandId,
            CancellationToken.None);
        Assert.NotNull(package);
        Assert.Equal(
            "composition",
            await File.ReadAllTextAsync(Path.Combine(
                package.PackagePath,
                RecoveryPackageFormat.ContentDirectoryName,
                "Venue.avc")));
        var outcomes = await store.GetPendingEventsAsync(
            now.AddMinutes(1),
            10,
            CancellationToken.None);
        Assert.Equal(2, outcomes.Count);
        Assert.All(outcomes, outcome =>
        {
            Assert.DoesNotContain(bundle, outcome.Envelope.Payload, StringComparison.Ordinal);
            Assert.DoesNotContain("Venue.avc", outcome.Envelope.Payload, StringComparison.Ordinal);
            Assert.DoesNotContain("intro.mov", outcome.Envelope.Payload, StringComparison.Ordinal);
        });
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    private AgentCommandExecutor CreateExecutor(
        AgentQueueStore store,
        DateTimeOffset now,
        ILogger<AgentCommandExecutor>? logger = null,
        ISystemInventorySource? inventorySource = null)
    {
        var timeProvider = new FixedTimeProvider(now);
        var plugin = new FileSystemDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                DiscoveryRoots = [_testRoot]
            }),
            timeProvider);
        var resolumePlugin = new ResolumeDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                ResolumeDiscoveryRoots = [Path.Combine(_testRoot, "resolume-bundle")]
            }),
            timeProvider);
        var verifier = new RecoveryPackageVerifier(CreateOptions());
        return new AgentCommandExecutor(
            store,
            new DiscoveryPluginRegistry([plugin, resolumePlugin]),
            new SystemInventoryPlugin(
                timeProvider,
                inventorySource ?? new TestSystemInventorySource(
                    new SystemInventoryHostFacts(
                        "synthetic-machine",
                        "Synthetic OS",
                        "X64",
                        "X64",
                        4),
                    [])),
            new RecoveryPackageWriter(CreateOptions()),
            verifier,
            new RecoveryPackageRestorer(CreateOptions(), verifier, store),
            timeProvider,
            logger ?? NullLogger<AgentCommandExecutor>.Instance);
    }

    private AgentCommandEnvelope CreateDiscoveryCommand(
        Guid agentId,
        DateTimeOffset issuedAt,
        TimeSpan validity) =>
        AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.StartDiscovery,
            "expiry-correlation",
            JsonSerializer.Serialize(new
            {
                pluginId = FileSystemDiscoveryPlugin.PluginId,
                rootPath = _testRoot,
                maxFiles = 10
            }),
            issuedAt,
            validity);

    private IOptions<AgentOptions> CreateOptions() => Options.Create(new AgentOptions
    {
        ControlPlaneUri = new Uri("https://control.test"),
        Name = "Test Agent",
        DataDirectory = Path.Combine(_testRoot, "data"),
        PackageDirectory = Path.Combine(_testRoot, "packages"),
        DiscoveryRoots = [_testRoot],
        RestoreRoots = [Path.Combine(_testRoot, "restores")]
    });

    private AgentQueueStore CreateStore() => new(CreateOptions());

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestSystemInventorySource(
        SystemInventoryHostFacts hostFacts,
        IReadOnlyList<SystemVolume> volumes) : ISystemInventorySource
    {
        public SystemInventoryHostFacts ReadHostFacts() => hostFacts;

        public IEnumerable<SystemVolume> EnumerateVolumes() => volumes;
    }

    private sealed class ThrowingSystemInventorySource(string sensitiveFailure)
        : ISystemInventorySource
    {
        public SystemInventoryHostFacts ReadHostFacts() =>
            throw new InvalidOperationException(sensitiveFailure);

        public IEnumerable<SystemVolume> EnumerateVolumes() => [];
    }

    private sealed class CancelingSystemInventorySource(CancellationTokenSource cancellation)
        : ISystemInventorySource
    {
        public SystemInventoryHostFacts ReadHostFacts() =>
            new("synthetic", "Synthetic OS", "X64", "X64", 4);

        public IEnumerable<SystemVolume> EnumerateVolumes()
        {
            cancellation.Cancel();
            yield return new SystemVolume("must-not-be-stored", "Fixed", 100, 50);
        }
    }

    private sealed class CapturingLogger : ILogger<AgentCommandExecutor>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
