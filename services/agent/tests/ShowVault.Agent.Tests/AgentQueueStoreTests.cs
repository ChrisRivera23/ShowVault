using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using ShowVault.Agent.Communication;
using ShowVault.Agent.Identity;
using ShowVault.Agent.Queue;
using ShowVault.AgentContracts;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class AgentQueueStoreTests : IAsyncLifetime
{
    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(),
        "showvault-agent-tests",
        Guid.NewGuid().ToString("N"));

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Events_survive_restart_and_are_removed_only_after_delivery()
    {
        var now = DateTimeOffset.UtcNow;
        var envelope = AgentEventEnvelope.Create(
            Guid.NewGuid(),
            AgentEventType.AgentConnected,
            "correlation-1",
            "{}",
            now);
        var firstStore = CreateStore();
        await firstStore.InitializeAsync(CancellationToken.None);
        await firstStore.EnqueueEventAsync(envelope, CancellationToken.None);

        var restartedStore = CreateStore();
        await restartedStore.InitializeAsync(CancellationToken.None);
        var pending = await restartedStore.GetPendingEventsAsync(
            now.AddSeconds(1),
            10,
            CancellationToken.None);

        Assert.Single(pending);
        Assert.Equal(envelope, pending[0].Envelope);

        await restartedStore.MarkEventDeliveredAsync(
            envelope.EventId,
            now.AddSeconds(2),
            CancellationToken.None);
        Assert.Empty(await restartedStore.GetPendingEventsAsync(
            now.AddMinutes(1),
            10,
            CancellationToken.None));
    }

    [Fact]
    public async Task Commands_are_deduplicated_and_survive_restart()
    {
        var command = AgentCommandEnvelope.Create(
            Guid.NewGuid(),
            AgentCommandType.StartDiscovery,
            "correlation-2",
            "{}",
            TimeSpan.FromMinutes(5));
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueCommandAsync(command, DateTimeOffset.UtcNow, CancellationToken.None);
        await store.EnqueueCommandAsync(command, DateTimeOffset.UtcNow, CancellationToken.None);

        var pending = await CreateStore().GetPendingCommandsAsync(CancellationToken.None);

        Assert.Single(pending);
        Assert.Equal(command, pending[0]);
    }

    [Fact]
    public async Task Failed_delivery_remains_durable_until_retry_succeeds()
    {
        var now = DateTimeOffset.UtcNow;
        var timeProvider = new ManualTimeProvider(now);
        var envelope = AgentEventEnvelope.Create(
            Guid.NewGuid(),
            AgentEventType.AgentConnected,
            "correlation-3",
            "{}",
            now);
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueEventAsync(envelope, CancellationToken.None);
        var handler = new SequencedHandler();
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://control.showvault.test")
        };
        var dispatcher = new AgentEventDispatcher(
            store,
            new AgentEventClient(client),
            timeProvider,
            NullLogger<AgentEventDispatcher>.Instance);
        var identity = new StoredAgentIdentity(envelope.AgentId, Guid.NewGuid(), "credential");

        await dispatcher.DispatchPendingOnceAsync(identity, CancellationToken.None);
        Assert.Empty(await store.GetPendingEventsAsync(now, 10, CancellationToken.None));

        timeProvider.Advance(TimeSpan.FromSeconds(2));
        await dispatcher.DispatchPendingOnceAsync(identity, CancellationToken.None);

        Assert.Equal(2, handler.RequestCount);
        Assert.Empty(await store.GetPendingEventsAsync(
            now.AddMinutes(1),
            10,
            CancellationToken.None));
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    private AgentQueueStore CreateStore() => new(Options.Create(new AgentOptions
    {
        ControlPlaneUri = new Uri("https://control.showvault.test"),
        Name = "Test Agent",
        DataDirectory = _dataDirectory
    }));

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }

    private sealed class SequencedHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(
                RequestCount == 1
                    ? System.Net.HttpStatusCode.ServiceUnavailable
                    : System.Net.HttpStatusCode.Accepted));
        }
    }
}
