using Microsoft.Data.Sqlite;
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
    public async Task Invalid_command_is_rejected_before_queue_persistence()
    {
        var command = AgentCommandEnvelope.Create(
            Guid.NewGuid(),
            AgentCommandType.StartDiscovery,
            "correlation-invalid-command",
            "{}",
            TimeSpan.FromMinutes(5)) with
        {
            Type = (AgentCommandType)999
        };
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.EnqueueCommandAsync(
                command,
                DateTimeOffset.UtcNow,
                CancellationToken.None));

        Assert.Empty(await store.GetPendingCommandsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Command_state_transitions_are_conditional_and_survive_restart()
    {
        var command = AgentCommandEnvelope.Create(
            Guid.NewGuid(),
            AgentCommandType.StartDiscovery,
            "correlation-state",
            "{}",
            TimeSpan.FromMinutes(5));
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueCommandAsync(command, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.False(await store.TryTransitionCommandAsync(
            command.CommandId,
            LocalAgentCommandStatus.Pending,
            LocalAgentCommandStatus.Completed,
            DateTimeOffset.UtcNow,
            CancellationToken.None));
        Assert.True(await store.TryTransitionCommandAsync(
            command.CommandId,
            LocalAgentCommandStatus.Pending,
            LocalAgentCommandStatus.Running,
            DateTimeOffset.UtcNow,
            CancellationToken.None));
        Assert.False(await store.TryTransitionCommandAsync(
            command.CommandId,
            LocalAgentCommandStatus.Pending,
            LocalAgentCommandStatus.Running,
            DateTimeOffset.UtcNow,
            CancellationToken.None));

        var running = await CreateStore().GetCommandsAsync(
            LocalAgentCommandStatus.Running,
            CancellationToken.None);
        Assert.Single(running);
        Assert.Equal(command, running[0]);
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

    [Fact]
    public async Task Invalid_event_is_rejected_before_queue_persistence()
    {
        var now = DateTimeOffset.UtcNow;
        var envelope = AgentEventEnvelope.Create(
            Guid.NewGuid(),
            AgentEventType.AgentConnected,
            "correlation-invalid",
            "{}",
            now) with
        {
            Payload = "not-json"
        };
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.EnqueueEventAsync(envelope, CancellationToken.None));

        Assert.Empty(await store.GetPendingEventsAsync(
            now.AddMinutes(1),
            10,
            CancellationToken.None));
    }

    [Fact]
    public async Task Permanent_delivery_failure_is_not_retried_after_restart()
    {
        var now = DateTimeOffset.UtcNow;
        var timeProvider = new ManualTimeProvider(now);
        var envelope = AgentEventEnvelope.Create(
            Guid.NewGuid(),
            AgentEventType.AgentConnected,
            "correlation-permanent",
            "{}",
            now);
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueEventAsync(envelope, CancellationToken.None);
        var handler = new ConstantHandler(System.Net.HttpStatusCode.BadRequest);
        var dispatcher = new AgentEventDispatcher(
            store,
            new AgentEventClient(new HttpClient(handler)
            {
                BaseAddress = new Uri("https://control.showvault.test")
            }),
            timeProvider,
            NullLogger<AgentEventDispatcher>.Instance);
        var identity = new StoredAgentIdentity(envelope.AgentId, Guid.NewGuid(), "credential");

        await dispatcher.DispatchPendingOnceAsync(identity, CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromHours(1));
        var restartedDispatcher = new AgentEventDispatcher(
            CreateStore(),
            new AgentEventClient(new HttpClient(handler)
            {
                BaseAddress = new Uri("https://control.showvault.test")
            }),
            timeProvider,
            NullLogger<AgentEventDispatcher>.Instance);
        await restartedDispatcher.DispatchPendingOnceAsync(identity, CancellationToken.None);

        Assert.Equal(1, handler.RequestCount);
        Assert.Empty(await CreateStore().GetPendingEventsAsync(
            now.AddDays(1),
            10,
            CancellationToken.None));
    }

    [Fact]
    public async Task Authentication_failure_preserves_event_for_credential_recovery()
    {
        var now = DateTimeOffset.UtcNow;
        var timeProvider = new ManualTimeProvider(now);
        var envelope = AgentEventEnvelope.Create(
            Guid.NewGuid(),
            AgentEventType.AgentConnected,
            "correlation-auth",
            "{}",
            now);
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        await store.EnqueueEventAsync(envelope, CancellationToken.None);
        var handler = new ConstantHandler(System.Net.HttpStatusCode.Unauthorized);
        var dispatcher = new AgentEventDispatcher(
            store,
            new AgentEventClient(new HttpClient(handler)
            {
                BaseAddress = new Uri("https://control.showvault.test")
            }),
            timeProvider,
            NullLogger<AgentEventDispatcher>.Instance);
        var identity = new StoredAgentIdentity(envelope.AgentId, Guid.NewGuid(), "credential");

        await dispatcher.DispatchPendingOnceAsync(identity, CancellationToken.None);

        Assert.Single(await CreateStore().GetPendingEventsAsync(
            now.AddMinutes(1),
            10,
            CancellationToken.None));
    }

    [Fact]
    public async Task Legacy_outbox_schema_is_upgraded_without_losing_pending_events()
    {
        Directory.CreateDirectory(_dataDirectory);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(_dataDirectory, "agent-queue.db"),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        await using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE event_outbox (
                    event_id TEXT PRIMARY KEY,
                    envelope_json TEXT NOT NULL,
                    occurred_at TEXT NOT NULL,
                    attempt_count INTEGER NOT NULL DEFAULT 0,
                    next_attempt_at TEXT NOT NULL,
                    delivered_at TEXT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        var envelope = AgentEventEnvelope.Create(
            Guid.NewGuid(),
            AgentEventType.AgentConnected,
            "correlation-upgrade",
            "{}",
            DateTimeOffset.UtcNow);
        await store.EnqueueEventAsync(envelope, CancellationToken.None);

        var pending = await store.GetPendingEventsAsync(
            DateTimeOffset.UtcNow.AddMinutes(1),
            10,
            CancellationToken.None);
        Assert.Single(pending);
        Assert.Equal(envelope, pending[0].Envelope);
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

    private sealed class ConstantHandler(System.Net.HttpStatusCode statusCode)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
