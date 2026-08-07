using System.Text.Json;
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
        var resultJson = await store.GetDiscoveryResultJsonAsync(
            command.CommandId,
            CancellationToken.None);
        Assert.Contains("console.show", resultJson, StringComparison.Ordinal);
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
        await CreateExecutor(restartedStore, now).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"),
            CancellationToken.None);

        Assert.Single(await restartedStore.GetCommandsAsync(
            LocalAgentCommandStatus.Failed,
            CancellationToken.None));
        var events = await restartedStore.GetPendingEventsAsync(
            now.AddMinutes(1),
            10,
            CancellationToken.None);
        Assert.Equal(AgentEventType.JobFailed, Assert.Single(events).Envelope.Type);
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
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    private AgentCommandExecutor CreateExecutor(AgentQueueStore store, DateTimeOffset now)
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
        return new AgentCommandExecutor(
            store,
            new DiscoveryPluginRegistry([plugin]),
            new RecoveryPackageWriter(CreateOptions()),
            timeProvider,
            NullLogger<AgentCommandExecutor>.Instance);
    }

    private IOptions<AgentOptions> CreateOptions() => Options.Create(new AgentOptions
    {
        ControlPlaneUri = new Uri("https://control.test"),
        Name = "Test Agent",
        DataDirectory = Path.Combine(_testRoot, "data"),
        PackageDirectory = Path.Combine(_testRoot, "packages"),
        DiscoveryRoots = [_testRoot]
    });

    private AgentQueueStore CreateStore() => new(CreateOptions());

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
