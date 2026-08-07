using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Communication;
using ShowVault.Agent.Identity;
using ShowVault.Agent.Queue;
using ShowVault.AgentContracts;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class AgentCommandPollerTests : IAsyncLifetime
{
    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(),
        "showvault-command-poller-tests",
        Guid.NewGuid().ToString("N"));

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Valid_command_is_durable_before_acknowledgement()
    {
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var identity = new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential");
        var command = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.StartDiscovery,
            "correlation",
            "{}",
            now,
            TimeSpan.FromMinutes(5));
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        var handler = new PollHandler(command, async () =>
        {
            var persisted = await CreateStore().GetPendingCommandsAsync(CancellationToken.None);
            Assert.Contains(command, persisted);
        });
        var poller = CreatePoller(store, handler, now);

        await poller.PollOnceAsync(identity, CancellationToken.None);

        Assert.Equal(1, handler.AcknowledgementCount);
    }

    [Theory]
    [InlineData("wrong-agent")]
    [InlineData("wrong-protocol")]
    [InlineData("expired")]
    public async Task Invalid_command_is_not_persisted_or_acknowledged(string invalidCase)
    {
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var identity = new StoredAgentIdentity(agentId, Guid.NewGuid(), "credential");
        var command = AgentCommandEnvelope.Create(
            agentId,
            AgentCommandType.StartDiscovery,
            "correlation",
            "{}",
            now,
            TimeSpan.FromMinutes(5));
        command = invalidCase switch
        {
            "wrong-agent" => command with { AgentId = Guid.NewGuid() },
            "wrong-protocol" => command with { ProtocolVersion = "999" },
            "expired" => command with { ExpiresAt = now },
            _ => command
        };
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);
        var handler = new PollHandler(command, () => Task.CompletedTask);

        await CreatePoller(store, handler, now).PollOnceAsync(identity, CancellationToken.None);

        Assert.Empty(await store.GetPendingCommandsAsync(CancellationToken.None));
        Assert.Equal(0, handler.AcknowledgementCount);
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    private AgentCommandPoller CreatePoller(
        AgentQueueStore store,
        HttpMessageHandler handler,
        DateTimeOffset now)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://control.test") };
        return new AgentCommandPoller(
            store,
            new AgentCommandClient(client),
            new FixedTimeProvider(now),
            NullLogger<AgentCommandPoller>.Instance);
    }

    private AgentQueueStore CreateStore() => new(Options.Create(new AgentOptions
    {
        ControlPlaneUri = new Uri("https://control.test"),
        Name = "Test Agent",
        DataDirectory = _dataDirectory
    }));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PollHandler(
        AgentCommandEnvelope command,
        Func<Task> onAcknowledge) : HttpMessageHandler
    {
        public int AcknowledgementCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal("ShowVault-Agent", request.Headers.Authorization?.Scheme);
            if (request.Method == HttpMethod.Get)
            {
                var json = JsonSerializer.Serialize(new { payload = new[] { command } });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }

            AcknowledgementCount++;
            await onAcknowledge();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
