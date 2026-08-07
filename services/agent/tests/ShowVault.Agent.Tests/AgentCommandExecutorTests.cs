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
        ILogger<AgentCommandExecutor>? logger = null)
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
        var verifier = new RecoveryPackageVerifier(CreateOptions());
        return new AgentCommandExecutor(
            store,
            new DiscoveryPluginRegistry([plugin]),
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
