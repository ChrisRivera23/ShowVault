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
        Assert.Contains("recoveryCandidates", outcome.Payload, StringComparison.Ordinal);
        Assert.Contains("subnetProposals", outcome.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("rootPath", outcome.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Candidate_decision_resolves_only_local_candidate_and_persists_exact_scope()
    {
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var localPath = Path.Combine(_testRoot, "Resolume Arena");
        Directory.CreateDirectory(localPath);
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.StoreRecoveryCandidatesAsync(
        [
            new LocalRecoveryCandidate(
                candidateId,
                ResolumeDiscoveryPlugin.PluginId,
                "Resolume Arena",
                "UserDataRoot",
                localPath,
                "Standard Resolume user-data location",
                true)
        ], now, CancellationToken.None);
        var approve = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.ApplyRecoveryCandidateDecision,
            "approve-candidate",
            JsonSerializer.Serialize(new ApplyRecoveryCandidateDecisionPayload(candidateId, true)),
            now,
            TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(approve, now, CancellationToken.None);
        var executor = CreateExecutor(store, now);
        var identity = new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential");

        await executor.ExecutePendingOnceAsync(identity, CancellationToken.None);

        var approved = Assert.Single(await store.GetApprovedRecoveryScopesAsync(
            CancellationToken.None));
        Assert.Equal(candidateId, approved.CandidateId);
        Assert.Equal(localPath, approved.LocalPath);
        var outcome = Assert.Single(await store.GetPendingEventsAsync(
            now.AddMinutes(1), 10, CancellationToken.None)).Envelope;
        Assert.DoesNotContain(localPath, outcome.Payload, StringComparison.Ordinal);

        var reject = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.ApplyRecoveryCandidateDecision,
            "reject-candidate",
            JsonSerializer.Serialize(new ApplyRecoveryCandidateDecisionPayload(candidateId, false)),
            now.AddSeconds(1),
            TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(reject, now.AddSeconds(1), CancellationToken.None);
        await executor.ExecutePendingOnceAsync(identity, CancellationToken.None);

        Assert.Empty(await store.GetApprovedRecoveryScopesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Candidate_approval_fails_when_id_is_not_in_local_inventory()
    {
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var command = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.ApplyRecoveryCandidateDecision,
            "unknown-candidate",
            JsonSerializer.Serialize(new ApplyRecoveryCandidateDecisionPayload(Guid.NewGuid(), true)),
            now,
            TimeSpan.FromMinutes(5));
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueCommandAsync(command, now, CancellationToken.None);

        await CreateExecutor(store, now).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"),
            CancellationToken.None);

        Assert.Empty(await store.GetApprovedRecoveryScopesAsync(CancellationToken.None));
        Assert.Single(await store.GetCommandsAsync(
            LocalAgentCommandStatus.Failed,
            CancellationToken.None));
    }

    [Fact]
    public async Task Subnet_decision_resolves_only_an_Agent_local_proposal()
    {
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.StoreSubnetProposalsAsync([
            new LocalSubnetProposal(proposalId, "192.168.10.0", 24, "Ethernet",
                "Active Ethernet interface; no hosts were contacted", true)
        ], now, CancellationToken.None);
        var command = AgentCommandEnvelope.Create(agentId, AgentCommandType.ApplySubnetProposalDecision,
            "subnet-decision", JsonSerializer.Serialize(new ApplySubnetProposalDecisionPayload(proposalId, true)),
            now, TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(command, now, CancellationToken.None);

        await CreateExecutor(store, now).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"), CancellationToken.None);

        Assert.Single(await store.GetCommandsAsync(LocalAgentCommandStatus.Completed, CancellationToken.None));
        var outcome = Assert.Single(await store.GetPendingEventsAsync(now.AddMinutes(1), 10,
            CancellationToken.None)).Envelope;
        Assert.Contains(proposalId.ToString(), outcome.Payload, StringComparison.OrdinalIgnoreCase);

        var discoveryCommand = AgentCommandEnvelope.Create(agentId, AgentCommandType.DiscoverApprovedSubnet,
            "subnet-discovery", JsonSerializer.Serialize(new DiscoverApprovedSubnetPayload(proposalId, 4, 250)),
            now.AddSeconds(1), TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(discoveryCommand, now.AddSeconds(1), CancellationToken.None);
        await CreateExecutor(store, now.AddSeconds(1)).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"), CancellationToken.None);

        Assert.Equal(2, (await store.GetCommandsAsync(LocalAgentCommandStatus.Completed, CancellationToken.None)).Count);
        var discoveryJson = await store.GetDiscoveryResultJsonAsync(discoveryCommand.CommandId, CancellationToken.None);
        Assert.Contains("\"attemptedHostCount\":4", discoveryJson, StringComparison.Ordinal);
        Assert.DoesNotContain("192.168.10.1", discoveryJson, StringComparison.Ordinal);
        Assert.Equal(["192.168.10.1", "192.168.10.2", "192.168.10.3", "192.168.10.4"],
            await store.GetReachableSubnetHostsAsync(discoveryCommand.CommandId, CancellationToken.None));
        var discoveryOutcome = (await store.GetPendingEventsAsync(now.AddMinutes(1), 10,
            CancellationToken.None)).Single(item => item.Envelope.EventId == discoveryCommand.CommandId).Envelope;
        Assert.DoesNotContain("192.168.10.1", discoveryOutcome.Payload, StringComparison.Ordinal);

        var identifyCommand = AgentCommandEnvelope.Create(agentId, AgentCommandType.IdentifyMaLighting,
            "ma-identification", JsonSerializer.Serialize(new IdentifyMaLightingPayload(
                proposalId, discoveryCommand.CommandId, 250)), now.AddSeconds(2), TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(identifyCommand, now.AddSeconds(2), CancellationToken.None);
        await CreateExecutor(store, now.AddSeconds(2)).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"), CancellationToken.None);

        var identificationJson = await store.GetDiscoveryResultJsonAsync(
            identifyCommand.CommandId, CancellationToken.None);
        Assert.Contains("\"identifiedHostCount\":4", identificationJson, StringComparison.Ordinal);
        Assert.Contains("grandMA3", identificationJson, StringComparison.Ordinal);
        Assert.DoesNotContain("192.168.10.1", identificationJson, StringComparison.Ordinal);
        var identificationOutcome = (await store.GetPendingEventsAsync(now.AddMinutes(1), 10,
            CancellationToken.None)).Single(item => item.Envelope.EventId == identifyCommand.CommandId).Envelope;
        Assert.DoesNotContain("192.168.10.1", identificationOutcome.Payload, StringComparison.Ordinal);

        var yamahaCommand = AgentCommandEnvelope.Create(agentId, AgentCommandType.IdentifyYamahaDme,
            "yamaha-identification", JsonSerializer.Serialize(new IdentifyYamahaDmePayload(
                proposalId, discoveryCommand.CommandId, 250)), now.AddSeconds(3), TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(yamahaCommand, now.AddSeconds(3), CancellationToken.None);
        await CreateExecutor(store, now.AddSeconds(3)).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"), CancellationToken.None);
        var yamahaJson = await store.GetDiscoveryResultJsonAsync(yamahaCommand.CommandId, CancellationToken.None);
        Assert.Contains("\"identifiedHostCount\":4", yamahaJson, StringComparison.Ordinal);
        Assert.Contains("Yamaha DME7", yamahaJson, StringComparison.Ordinal);
        Assert.DoesNotContain("192.168.10.1", yamahaJson, StringComparison.Ordinal);

        var grandMa2Command = AgentCommandEnvelope.Create(agentId, AgentCommandType.IdentifyGrandMa2,
            "grandma2-identification", JsonSerializer.Serialize(new IdentifyGrandMa2Payload(
                proposalId, discoveryCommand.CommandId, 250)), now.AddSeconds(4), TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(grandMa2Command, now.AddSeconds(4), CancellationToken.None);
        await CreateExecutor(store, now.AddSeconds(4)).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"), CancellationToken.None);
        var grandMa2Json = await store.GetDiscoveryResultJsonAsync(grandMa2Command.CommandId, CancellationToken.None);
        Assert.Contains("\"identifiedHostCount\":4", grandMa2Json, StringComparison.Ordinal);
        Assert.Contains("grandMA2", grandMa2Json, StringComparison.Ordinal);
        Assert.DoesNotContain("192.168.10.1", grandMa2Json, StringComparison.Ordinal);

        var projectorCommand = AgentCommandEnvelope.Create(agentId, AgentCommandType.IdentifyProjectors,
            "projector-identification", JsonSerializer.Serialize(new IdentifyProjectorsPayload(
                proposalId, discoveryCommand.CommandId, 250)), now.AddSeconds(5), TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(projectorCommand, now.AddSeconds(5), CancellationToken.None);
        await CreateExecutor(store, now.AddSeconds(5)).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"), CancellationToken.None);
        var projectorJson = await store.GetDiscoveryResultJsonAsync(
            projectorCommand.CommandId, CancellationToken.None);
        Assert.Contains("\"identifiedHostCount\":4", projectorJson, StringComparison.Ordinal);
        Assert.Contains("Christie LX41", projectorJson, StringComparison.Ordinal);
        Assert.DoesNotContain("192.168.10.1", projectorJson, StringComparison.Ordinal);

        var blackmagicCommand = AgentCommandEnvelope.Create(
            agentId, AgentCommandType.IdentifyBlackmagicVideohub,
            "blackmagic-videohub-identification",
            JsonSerializer.Serialize(new IdentifyBlackmagicVideohubPayload(
                proposalId, discoveryCommand.CommandId, 250)),
            now.AddSeconds(6), TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(blackmagicCommand, now.AddSeconds(6), CancellationToken.None);
        await CreateExecutor(store, now.AddSeconds(6)).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"), CancellationToken.None);
        var blackmagicJson = await store.GetDiscoveryResultJsonAsync(
            blackmagicCommand.CommandId, CancellationToken.None);
        Assert.Contains("\"identifiedHostCount\":4", blackmagicJson, StringComparison.Ordinal);
        Assert.Contains("Blackmagic Smart Videohub 16x16", blackmagicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("192.168.10.1", blackmagicJson, StringComparison.Ordinal);

        var newTekCommand = AgentCommandEnvelope.Create(
            agentId, AgentCommandType.IdentifyNewTekTriCaster,
            "newtek-tricaster-identification",
            JsonSerializer.Serialize(new IdentifyNewTekTriCasterPayload(
                proposalId, discoveryCommand.CommandId, 250)),
            now.AddSeconds(7), TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(newTekCommand, now.AddSeconds(7), CancellationToken.None);
        await CreateExecutor(store, now.AddSeconds(7)).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"), CancellationToken.None);
        var newTekJson = await store.GetDiscoveryResultJsonAsync(
            newTekCommand.CommandId, CancellationToken.None);
        Assert.Contains("\"identifiedHostCount\":4", newTekJson, StringComparison.Ordinal);
        Assert.Contains("NewTek TriCaster TC1", newTekJson, StringComparison.Ordinal);
        Assert.DoesNotContain("192.168.10.1", newTekJson, StringComparison.Ordinal);

        var birdDogCommand = AgentCommandEnvelope.Create(
            agentId, AgentCommandType.IdentifyBirdDog,
            "birddog-identification",
            JsonSerializer.Serialize(new IdentifyBirdDogPayload(
                proposalId, discoveryCommand.CommandId, 250)),
            now.AddSeconds(8), TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(birdDogCommand, now.AddSeconds(8), CancellationToken.None);
        await CreateExecutor(store, now.AddSeconds(8)).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"), CancellationToken.None);
        var birdDogJson = await store.GetDiscoveryResultJsonAsync(
            birdDogCommand.CommandId, CancellationToken.None);
        Assert.Contains("\"identifiedHostCount\":4", birdDogJson, StringComparison.Ordinal);
        Assert.Contains("BirdDog P200 (A4/A5)", birdDogJson, StringComparison.Ordinal);
        Assert.DoesNotContain("192.168.10.1", birdDogJson, StringComparison.Ordinal);

        var panasonicCommand = AgentCommandEnvelope.Create(
            agentId, AgentCommandType.IdentifyPanasonicCamera,
            "panasonic-camera-identification",
            JsonSerializer.Serialize(new IdentifyPanasonicCameraPayload(
                proposalId, discoveryCommand.CommandId, 250)),
            now.AddSeconds(9), TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(panasonicCommand, now.AddSeconds(9), CancellationToken.None);
        await CreateExecutor(store, now.AddSeconds(9)).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"), CancellationToken.None);
        var panasonicJson = await store.GetDiscoveryResultJsonAsync(
            panasonicCommand.CommandId, CancellationToken.None);
        Assert.Contains("\"identifiedHostCount\":4", panasonicJson, StringComparison.Ordinal);
        Assert.Contains("Panasonic AW-UE100", panasonicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("192.168.10.1", panasonicJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("macos", true)]
    [InlineData("macos", false)]
    [InlineData("windows", true)]
    [InlineData("windows", false)]
    public async Task Direct_link_fixture_runs_through_queue_and_emits_only_path_free_diagnostics(
        string platform,
        bool populatedCache)
    {
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var peer = System.Net.IPAddress.Parse("169.254.220.9");
        var populatedArpOutput = platform == "windows"
            ? """
              Interface: 169.254.73.42 --- 0x6
                Internet Address      Physical Address      Type
                169.254.220.9         aa-bb-cc-dd-ee-ff     dynamic
              """
            : "? (169.254.220.9) at aa:bb:cc:dd:ee:ff on en7 ifscope [ethernet]";
        var arpOutput = populatedCache ? populatedArpOutput : platform == "windows"
            ? "Interface: 169.254.73.42 --- 0x6\r\n  Internet Address Physical Address Type"
            : "? (169.254.220.9) at (incomplete) on en7 ifscope [ethernet]";
        var interfaceProvider = new FixtureInterfaceProvider();
        var arpReader = new FixtureArpTableReader(arpOutput);
        var neighborProvider = new ArpLinkLocalNeighborProvider(
            interfaceProvider, arpReader, new ImmediateObservationDelay());
        var approvedDiscovery = new ApprovedSubnetDiscovery(
            new SelectiveReachabilityProbe(peer), new FixedTimeProvider(now), neighborProvider);
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.StoreSubnetProposalsAsync([
            new LocalSubnetProposal(proposalId, "169.254.0.0", 16, "GigabitEthernet",
                "One physical direct link; no hosts were contacted", true)
        ], now, CancellationToken.None);

        var decision = AgentCommandEnvelope.Create(agentId, AgentCommandType.ApplySubnetProposalDecision,
            $"{platform}-{populatedCache}-decision", JsonSerializer.Serialize(
                new ApplySubnetProposalDecisionPayload(proposalId, true)),
            now, TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(decision, now, CancellationToken.None);
        await CreateExecutor(store, now, approvedSubnetDiscovery: approvedDiscovery).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"), CancellationToken.None);

        var discovery = AgentCommandEnvelope.Create(agentId, AgentCommandType.DiscoverApprovedSubnet,
            $"{platform}-{populatedCache}-discovery", JsonSerializer.Serialize(
                new DiscoverApprovedSubnetPayload(proposalId, 4, 250)),
            now.AddSeconds(1), TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(discovery, now.AddSeconds(1), CancellationToken.None);
        await CreateExecutor(store, now.AddSeconds(1), approvedSubnetDiscovery: approvedDiscovery)
            .ExecutePendingOnceAsync(
                new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"), CancellationToken.None);

        var result = await store.GetDiscoveryResultJsonAsync(discovery.CommandId, CancellationToken.None);
        Assert.Contains("\"attemptedHostCount\":4", result, StringComparison.Ordinal);
        var passiveCount = populatedCache ? 1 : 0;
        var respondingCount = populatedCache ? 1 : 0;
        Assert.Contains($"\"respondingHostCount\":{respondingCount}", result, StringComparison.Ordinal);
        Assert.Contains($"\"passiveCandidateCount\":{passiveCount}", result, StringComparison.Ordinal);
        Assert.Contains($"\"fallbackTargetCount\":{4 - passiveCount}", result, StringComparison.Ordinal);
        Assert.DoesNotContain(peer.ToString(), result, StringComparison.Ordinal);
        Assert.DoesNotContain("aa:bb", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("aa-bb", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("en7", result, StringComparison.Ordinal);
        string[] expectedHosts = populatedCache ? [peer.ToString()] : [];
        Assert.Equal(expectedHosts,
            await store.GetReachableSubnetHostsAsync(discovery.CommandId, CancellationToken.None));
        var outcome = (await store.GetPendingEventsAsync(now.AddMinutes(1), 10, CancellationToken.None))
            .Single(item => item.Envelope.EventId == discovery.CommandId).Envelope;
        Assert.DoesNotContain(peer.ToString(), outcome.Payload, StringComparison.Ordinal);
        Assert.Contains($"\"passiveCandidateCount\":{passiveCount}", outcome.Payload, StringComparison.Ordinal);
        Assert.Equal(2, arpReader.ReadCount);

        if (populatedCache)
        {
            var grandMa2 = AgentCommandEnvelope.Create(agentId, AgentCommandType.IdentifyGrandMa2,
                $"{platform}-grandma2", JsonSerializer.Serialize(new IdentifyGrandMa2Payload(
                    proposalId, discovery.CommandId, 250)), now.AddSeconds(2), TimeSpan.FromMinutes(5));
            await store.EnqueueCommandAsync(grandMa2, now.AddSeconds(2), CancellationToken.None);
            await CreateExecutor(store, now.AddSeconds(2), approvedSubnetDiscovery: approvedDiscovery)
                .ExecutePendingOnceAsync(
                    new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"), CancellationToken.None);
            var grandMa2Result = await store.GetDiscoveryResultJsonAsync(
                grandMa2.CommandId, CancellationToken.None);
            Assert.Contains("\"identifiedHostCount\":1", grandMa2Result, StringComparison.Ordinal);
            Assert.Contains("grandMA2", grandMa2Result, StringComparison.Ordinal);
            Assert.DoesNotContain(peer.ToString(), grandMa2Result, StringComparison.Ordinal);

            var yamaha = AgentCommandEnvelope.Create(agentId, AgentCommandType.IdentifyYamahaDme,
                $"{platform}-yamaha", JsonSerializer.Serialize(new IdentifyYamahaDmePayload(
                    proposalId, discovery.CommandId, 250)), now.AddSeconds(3), TimeSpan.FromMinutes(5));
            await store.EnqueueCommandAsync(yamaha, now.AddSeconds(3), CancellationToken.None);
            await CreateExecutor(store, now.AddSeconds(3), approvedSubnetDiscovery: approvedDiscovery)
                .ExecutePendingOnceAsync(
                    new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"), CancellationToken.None);
            var yamahaResult = await store.GetDiscoveryResultJsonAsync(
                yamaha.CommandId, CancellationToken.None);
            Assert.Contains("\"identifiedHostCount\":1", yamahaResult, StringComparison.Ordinal);
            Assert.Contains("Yamaha DME7", yamahaResult, StringComparison.Ordinal);
            Assert.DoesNotContain(peer.ToString(), yamahaResult, StringComparison.Ordinal);
            Assert.Equal(2, arpReader.ReadCount);
        }
    }

    [Fact]
    public async Task Approved_candidate_validation_resolves_local_path_and_emits_path_free_result()
    {
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var userData = Path.Combine(_testRoot, "Resolume Arena");
        Directory.CreateDirectory(Path.Combine(userData, "Compositions"));
        await File.WriteAllTextAsync(
            Path.Combine(userData, "Compositions", "Venue.avc"),
            "composition");
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.StoreRecoveryCandidatesAsync(
        [
            new LocalRecoveryCandidate(
                candidateId,
                ResolumeDiscoveryPlugin.PluginId,
                "Resolume Arena",
                "UserDataRoot",
                userData,
                "Standard Resolume user-data location",
                true)
        ], now, CancellationToken.None);
        Assert.True(await store.ApplyRecoveryCandidateDecisionAsync(
            candidateId, true, now, CancellationToken.None));
        var command = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.ValidateRecoveryCandidate,
            "validate-candidate",
            JsonSerializer.Serialize(new ValidateRecoveryCandidatePayload(candidateId, 100)),
            now,
            TimeSpan.FromMinutes(5));
        await store.EnqueueCommandAsync(command, now, CancellationToken.None);

        await CreateExecutor(store, now, configureResolumeRoots: false).ExecutePendingOnceAsync(
            new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential"),
            CancellationToken.None);

        var result = await store.GetDiscoveryResultJsonAsync(command.CommandId, CancellationToken.None);
        Assert.Contains("Venue.avc", result, StringComparison.Ordinal);
        var outcome = Assert.Single(await store.GetPendingEventsAsync(
            now.AddMinutes(1), 10, CancellationToken.None)).Envelope;
        Assert.Equal(AgentEventType.JobCompleted, outcome.Type);
        Assert.Contains(candidateId.ToString(), outcome.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(userData, outcome.Payload, StringComparison.Ordinal);
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

    private AgentCommandExecutor CreateExecutor(
        AgentQueueStore store,
        DateTimeOffset now,
        bool configureResolumeRoots = true,
        ApprovedSubnetDiscovery? approvedSubnetDiscovery = null)
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
                ResolumeDiscoveryRoots = configureResolumeRoots ? [_testRoot] : []
            }),
            timeProvider,
            store);
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
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                PowersoftArmoniaPlusProjectRoots = [Path.Combine(_testRoot, "powersoft-armoniaplus")]
            }), timeProvider);
        var crownAudioArchitectPlugin = new CrownAudioArchitectDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                CrownAudioArchitectVenueRoots = [Path.Combine(_testRoot, "crown-audio-architect")]
            }), timeProvider);
        var labGruppenLakePlugin = new LabGruppenLakeDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                LabGruppenLakeSystemRoots = [Path.Combine(_testRoot, "lab-gruppen-lake")]
            }), timeProvider);
        var dynacordSonicuePlugin = new DynacordSonicueDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                DynacordSonicueProjectRoots = [Path.Combine(_testRoot, "dynacord-sonicue")]
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
                    crownAudioArchitectPlugin,
                    labGruppenLakePlugin,
                    dynacordSonicuePlugin
                ]),
            new SystemInventoryPlugin(
                timeProvider,
                new LocalRecoveryCandidateDiscovery(new EmptyStandardLocationProvider()),
                new LocalSubnetProposalDiscovery(new EmptyInterfaceProvider())),
            new NetworkDeviceDiscoveryPlugin(
                Options.Create(new AgentOptions
                {
                    ControlPlaneUri = new Uri("https://control.test"),
                    Name = "Test Agent",
                    NetworkDiscoveryTargets = ["console.test:443"]
                }),
                new ReachableNetworkConnector(),
                timeProvider),
            approvedSubnetDiscovery ?? new ApprovedSubnetDiscovery(new ReachableSubnetProbe(), timeProvider),
            new MaLightingNetworkIdentification(new GrandMa3Probe(), timeProvider),
            new YamahaDmeNetworkIdentification(new YamahaDmeProbe(), timeProvider),
            new GrandMa2NetworkIdentification(new GrandMa2Probe(), timeProvider),
            new PjLinkNetworkIdentification(new PjLinkProbe(), timeProvider),
            new BlackmagicVideohubNetworkIdentification(new BlackmagicVideohubProbe(), timeProvider),
            new NewTekTriCasterNetworkIdentification(new NewTekTriCasterProbe(), timeProvider),
            new BirdDogNetworkIdentification(new BirdDogProbe(), timeProvider),
            new PanasonicCameraNetworkIdentification(new PanasonicCameraProbe(), timeProvider),
            new RecoveryPackageWriter(CreateOptions()),
            verifier,
            new RecoveryPackageRestorer(CreateOptions(), verifier, store),
            timeProvider,
            NullLogger<AgentCommandExecutor>.Instance);

    }

    private sealed class EmptyStandardLocationProvider : IHostStandardLocationProvider
    {
        public IReadOnlyList<StandardLocationCandidate> GetCandidates() => [];
    }

    private sealed class EmptyInterfaceProvider : ILocalInterfaceProvider
    {
        public IReadOnlyList<LocalInterfaceAddress> GetAddresses() => [];
    }

    private sealed class ReachableSubnetProbe : ISubnetReachabilityProbe
    {
        public Task<bool> IsReachableAsync(
            System.Net.IPAddress address,
            TimeSpan timeout,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class SelectiveReachabilityProbe(System.Net.IPAddress reachable)
        : ISubnetReachabilityProbe
    {
        public Task<bool> IsReachableAsync(
            System.Net.IPAddress address,
            TimeSpan timeout,
            CancellationToken cancellationToken) => Task.FromResult(address.Equals(reachable));
    }

    private sealed class FixtureInterfaceProvider : ILocalInterfaceProvider
    {
        public IReadOnlyList<LocalInterfaceAddress> GetAddresses() =>
        [
            new("en7", "USB Ethernet", System.Net.NetworkInformation.NetworkInterfaceType.GigabitEthernet,
                System.Net.NetworkInformation.OperationalStatus.Up,
                System.Net.IPAddress.Parse("169.254.73.42"),
                System.Net.IPAddress.Parse("255.255.0.0"))
        ];
    }

    private sealed class FixtureArpTableReader(string output) : IArpTableReader
    {
        public int ReadCount { get; private set; }
        public Task<string> ReadAsync(CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(output);
        }
    }

    private sealed class ImmediateObservationDelay : IPassiveNeighborObservationDelay
    {
        public Task WaitAsync(CancellationToken cancellationToken) =>
            Task.Delay(TimeSpan.Zero, cancellationToken);
    }

    private sealed class GrandMa3Probe : IMaLightingProtocolProbe
    {
        public Task<string?> IdentifyAsync(
            System.Net.IPAddress address,
            TimeSpan timeout,
            CancellationToken cancellationToken) => Task.FromResult<string?>("grandMA3");
    }

    private sealed class YamahaDmeProbe : IYamahaDmeProtocolProbe
    {
        public Task<string?> IdentifyAsync(
            System.Net.IPAddress address,
            TimeSpan timeout,
            CancellationToken cancellationToken) => Task.FromResult<string?>("Yamaha DME7");
    }

    private sealed class GrandMa2Probe : IGrandMa2ProtocolProbe
    {
        public Task<string?> IdentifyAsync(
            System.Net.IPAddress address, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("grandMA2");
    }

    private sealed class PjLinkProbe : IProjectorProtocolProbe
    {
        public Task<string?> IdentifyAsync(
            System.Net.IPAddress address, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("Christie LX41");
    }

    private sealed class BlackmagicVideohubProbe : IBlackmagicVideohubProtocolProbe
    {
        public Task<string?> IdentifyAsync(
            System.Net.IPAddress address, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("Blackmagic Smart Videohub 16x16");
    }

    private sealed class NewTekTriCasterProbe : INewTekTriCasterProtocolProbe
    {
        public Task<string?> IdentifyAsync(
            System.Net.IPAddress address, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("NewTek TriCaster TC1");
    }

    private sealed class BirdDogProbe : IBirdDogProtocolProbe
    {
        public Task<string?> IdentifyAsync(
            System.Net.IPAddress address, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("BirdDog P200 (A4/A5)");
    }

    private sealed class PanasonicCameraProbe : IPanasonicCameraProtocolProbe
    {
        public Task<string?> IdentifyAsync(
            System.Net.IPAddress address, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("Panasonic AW-UE100");
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
