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
        var resultJson = await store.GetDiscoveryResultJsonAsync(
            command.CommandId,
            CancellationToken.None);
        Assert.Contains("console.show", resultJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CollectSystemInventory_persists_inventory_and_completes_durably()
    {
        var now = DateTimeOffset.UtcNow;
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

        await CreateExecutor(store, now).ExecutePendingOnceAsync(
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
        var outcome = Assert.Single(await store.GetPendingEventsAsync(
            now.AddMinutes(1),
            10,
            CancellationToken.None)).Envelope;
        Assert.Equal(AgentEventType.JobCompleted, outcome.Type);
        Assert.Contains("volumeCount", outcome.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoverNetworkDevices_persists_allowlisted_probe_results()
    {
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var command = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.DiscoverNetworkDevices,
            "network-correlation",
            JsonSerializer.Serialize(new
            {
                targets = new[] { "console.test:443" },
                timeoutMilliseconds = 500
            }),
            now,
            TimeSpan.FromMinutes(5));
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueCommandAsync(command, now, CancellationToken.None);

        await CreateExecutor(store, now).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"),
            CancellationToken.None);

        Assert.Single(await store.GetCommandsAsync(
            LocalAgentCommandStatus.Completed,
            CancellationToken.None));
        var resultJson = await store.GetDiscoveryResultJsonAsync(
            command.CommandId,
            CancellationToken.None);
        Assert.Contains("console.test:443", resultJson, StringComparison.Ordinal);
        using var result = JsonDocument.Parse(resultJson!);
        Assert.Equal(
            (int)NetworkProbeStatus.Reachable,
            result.RootElement.GetProperty("devices")[0].GetProperty("status").GetInt32());
        var outcome = Assert.Single(await store.GetPendingEventsAsync(
            now.AddMinutes(1),
            10,
            CancellationToken.None)).Envelope;
        Assert.Contains("reachableCount", outcome.Payload, StringComparison.Ordinal);
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

    [Fact]
    public async Task Resolume_discovery_flows_into_immutable_recovery_package()
    {
        var bundle = Path.Combine(_testRoot, "resolume-bundle");
        Directory.CreateDirectory(bundle);
        await File.WriteAllTextAsync(Path.Combine(bundle, "Venue.avc"), "composition");
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var discovery = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.StartDiscovery,
            "resolume-discovery",
            JsonSerializer.Serialize(new
            {
                pluginId = ResolumeDiscoveryPlugin.PluginId,
                rootPath = bundle
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

        var package = await store.GetRecoveryPackageAsync(
            backup.CommandId,
            CancellationToken.None);
        Assert.NotNull(package);
        Assert.Contains(ResolumeDiscoveryPlugin.PluginId, package.ManifestJson, StringComparison.Ordinal);
        Assert.Equal(
            "composition",
            await File.ReadAllTextAsync(Path.Combine(
                package.PackagePath,
                RecoveryPackageFormat.ContentDirectoryName,
                "Venue.avc")));
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
        var resolumePlugin = new ResolumeDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                ResolumeDiscoveryRoots = [_testRoot]
            }),
            timeProvider);
        var grandMa2Plugin = new GrandMa2ShowDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                GrandMa2ExportRoots = [Path.Combine(_testRoot, "gma2")]
            }),
            timeProvider);
        var grandMa3Plugin = new GrandMa3ShowDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                GrandMa3ExportRoots = [Path.Combine(_testRoot, "grandMA3")]
            }),
            timeProvider);
        var yamahaDm7Plugin = new YamahaDm7DiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaDm7ExportRoots = [Path.Combine(_testRoot, "yamaha-dm7")]
            }),
            timeProvider);
        var yamahaRivagePlugin = new YamahaRivageDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaRivageExportRoots = [Path.Combine(_testRoot, "yamaha-rivage")]
            }),
            timeProvider);
        var yamahaClQlPlugin = new YamahaClQlDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaClQlExportRoots = [Path.Combine(_testRoot, "yamaha-cl-ql")]
            }),
            timeProvider);
        var yamahaTfPlugin = new YamahaTfDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaTfExportRoots = [Path.Combine(_testRoot, "yamaha-tf")]
            }),
            timeProvider);
        var yamahaDm3Plugin = new YamahaDm3DiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaDm3ExportRoots = [Path.Combine(_testRoot, "yamaha-dm3")]
            }),
            timeProvider);
        var yamahaDme7Plugin = new YamahaDme7DiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaDme7ProjectRoots = [Path.Combine(_testRoot, "yamaha-dme7")]
            }),
            timeProvider);
        var yamahaMtxMrxPlugin = new YamahaMtxMrxDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaMtxMrxProjectRoots = [Path.Combine(_testRoot, "yamaha-mtx-mrx")]
            }),
            timeProvider);
        var yamahaPcDdiPlugin = new YamahaPcDdiDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaPcDdiProjectRoots = [Path.Combine(_testRoot, "yamaha-pc-d-di")]
            }),
            timeProvider);
        var yamahaControlPlugin = new YamahaProVisionaireControlDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaProVisionaireControlProjectRoots =
                    [Path.Combine(_testRoot, "yamaha-provisionaire-control")]
            }),
            timeProvider);
        var yamahaDme5Dme3Plugin = new YamahaDme5Dme3DiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaDme5Dme3ProjectRoots = [Path.Combine(_testRoot, "yamaha-dme5-dme3")]
            }),
            timeProvider);
        var qsysDesignerPlugin = new QsysDesignerDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                QsysDesignerProjectRoots = [Path.Combine(_testRoot, "qsys-designer")]
            }),
            timeProvider);
        var etcEosPlugin = new EtcEosShowDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                EtcEosShowArchiveRoots = [Path.Combine(_testRoot, "etc-eos")]
            }),
            timeProvider);
        var danteControllerPlugin = new DanteControllerDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                DanteControllerPresetRoots = [Path.Combine(_testRoot, "dante-controller")]
            }),
            timeProvider);
        var allenHeathSqPlugin = new AllenHeathSqShowDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                AllenHeathSqShowRoots =
                    [Path.Combine(_testRoot, "AHSQ", "SHOWS", "SHOW0000")]
            }),
            timeProvider);
        var crestronSimplPlugin = new CrestronSimplDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                CrestronSimplProjectRoots = [Path.Combine(_testRoot, "crestron-simpl")]
            }),
            timeProvider);
        var shureDesignerPlugin = new ShureDesignerDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                ShureDesignerRoomRoots = [Path.Combine(_testRoot, "shure-designer")]
            }),
            timeProvider);
        var blackmagicAtemPlugin = new BlackmagicAtemDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                BlackmagicAtemStateRoots = [Path.Combine(_testRoot, "blackmagic-atem")]
            }),
            timeProvider);
        var digicoSdQuantumPlugin = new DigicoSdQuantumDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                DigicoSdQuantumSessionRoots = [Path.Combine(_testRoot, "digico-sd-quantum")]
            }),
            timeProvider);
        var sslLivePlugin = new SslLiveDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                SslLiveShowRoots = [Path.Combine(_testRoot, "ssl-live")]
            }),
            timeProvider);
        var lawoMc2Plugin = new LawoMc2DiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                LawoMc2ProductionRoots = [Path.Combine(_testRoot, "lawo-mc2")]
            }),
            timeProvider);
        var calrecApolloArtemisPlugin = new CalrecApolloArtemisDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                CalrecApolloArtemisShowRoots =
                    [Path.Combine(_testRoot, "calrec-apollo-artemis")]
            }),
            timeProvider);
        var studerVistaPlugin = new StuderVistaDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                StuderVistaTitleBackupRoots =
                    [Path.Combine(_testRoot, "BCK_D950_BACKUP_test")]
            }),
            timeProvider);
        var midasProPlugin = new MidasProDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                MidasProShowRoots = [Path.Combine(_testRoot, "midas-pro")]
            }),
            timeProvider);
        var behringerWingPlugin = new BehringerWingDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                BehringerWingShowRoots = [Path.Combine(_testRoot, "behringer-wing")]
            }),
            timeProvider);
        var soundcraftViPlugin = new SoundcraftViDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                SoundcraftViShowRoots = [Path.Combine(_testRoot, "soundcraft-vi")]
            }),
            timeProvider);
        var tascamModelMtrPlugin = new TascamModelMtrDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                TascamModelMtrSongRoots =
                    [Path.Combine(_testRoot, "MTR", "tascam-song")]
            }),
            timeProvider);
        var rolandM5000Plugin = new RolandM5000DiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                RolandM5000ProjectRoots = [Path.Combine(_testRoot, "roland-m5000")]
            }),
            timeProvider);
        var preSonusSeriesIiiPlugin = new PreSonusStudioLiveSeriesIiiDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                PreSonusStudioLiveSeriesIiiBackupRoots =
                    [Path.Combine(_testRoot, "presonus-series-iii")]
            }),
            timeProvider);
        var biampTesiraPlugin = new BiampTesiraDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                BiampTesiraConfigurationRoots = [Path.Combine(_testRoot, "biamp-tesira")]
            }),
            timeProvider);
        var symetrixComposerPlugin = new SymetrixComposerDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                SymetrixComposerSiteRoots = [Path.Combine(_testRoot, "symetrix-composer")]
            }),
            timeProvider);
        var boseControlSpacePlugin = new BoseControlSpaceDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                BoseControlSpaceProjectRoots = [Path.Combine(_testRoot, "bose-controlspace")]
            }),
            timeProvider);
        var peaveyNwarePlugin = new PeaveyNwareDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                PeaveyNwareProjectRoots = [Path.Combine(_testRoot, "peavey-nware")]
            }),
            timeProvider);
        var ashlyProteaNePlugin = new AshlyProteaNeDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                AshlyProteaNeProjectRoots = [Path.Combine(_testRoot, "ashly-protea-ne")]
            }),
            timeProvider);
        var powersoftArmoniaPlusPlugin = new PowersoftArmoniaPlusDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"), Name = "Test Agent",
                PowersoftArmoniaPlusProjectRoots = [Path.Combine(_testRoot, "powersoft-armoniaplus")]
            }), timeProvider);
        var crownAudioArchitectPlugin = new CrownAudioArchitectDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"), Name = "Test Agent",
                CrownAudioArchitectVenueRoots = [Path.Combine(_testRoot, "crown-audio-architect")]
            }), timeProvider);
        var verifier = new RecoveryPackageVerifier();
        return new AgentCommandExecutor(
            store,
            new DiscoveryPluginRegistry(
                [
                    plugin,
                    resolumePlugin,
                    grandMa2Plugin,
                    grandMa3Plugin,
                    yamahaDm7Plugin,
                    yamahaRivagePlugin,
                    yamahaClQlPlugin,
                    yamahaTfPlugin,
                    yamahaDm3Plugin,
                    yamahaDme7Plugin,
                    yamahaMtxMrxPlugin,
                    yamahaPcDdiPlugin,
                    yamahaControlPlugin,
                    yamahaDme5Dme3Plugin,
                    qsysDesignerPlugin,
                    etcEosPlugin,
                    danteControllerPlugin,
                    allenHeathSqPlugin,
                    crestronSimplPlugin,
                    shureDesignerPlugin,
                    blackmagicAtemPlugin,
                    digicoSdQuantumPlugin,
                    sslLivePlugin,
                    lawoMc2Plugin,
                    calrecApolloArtemisPlugin,
                    studerVistaPlugin,
                    midasProPlugin,
                    behringerWingPlugin,
                    soundcraftViPlugin,
                    tascamModelMtrPlugin,
                    rolandM5000Plugin,
                    preSonusSeriesIiiPlugin,
                    biampTesiraPlugin,
                    symetrixComposerPlugin,
                    boseControlSpacePlugin,
                    peaveyNwarePlugin,
                    ashlyProteaNePlugin,
                    powersoftArmoniaPlusPlugin,
                    crownAudioArchitectPlugin
                ]),
            new SystemInventoryPlugin(timeProvider),
            new NetworkDeviceDiscoveryPlugin(
                Options.Create(new AgentOptions
                {
                    ControlPlaneUri = new Uri("https://control.test"),
                    Name = "Test Agent",
                    NetworkDiscoveryTargets = ["console.test:443"]
                }),
                new ReachableNetworkConnector(),
                timeProvider),
            new RecoveryPackageWriter(CreateOptions()),
            verifier,
            new RecoveryPackageRestorer(CreateOptions(), verifier, store),
            timeProvider,
            NullLogger<AgentCommandExecutor>.Instance);
    }

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

    private sealed class ReachableNetworkConnector : INetworkEndpointConnector
    {
        public Task<NetworkProbeStatus> ProbeAsync(
            NetworkTarget target,
            TimeSpan timeout,
            CancellationToken cancellationToken) =>
            Task.FromResult(NetworkProbeStatus.Reachable);
    }
}
