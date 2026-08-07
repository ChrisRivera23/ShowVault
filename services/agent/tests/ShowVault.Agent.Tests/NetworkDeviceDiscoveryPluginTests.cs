using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class NetworkDeviceDiscoveryPluginTests
{
    [Fact]
    public async Task Tcp_connector_reports_reachable_for_listening_endpoint()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var accept = listener.AcceptTcpClientAsync();

            var status = await new TcpNetworkEndpointConnector().ProbeAsync(
                new NetworkTarget(IPAddress.Loopback.ToString(), port),
                TimeSpan.FromSeconds(1),
                CancellationToken.None);

            using var accepted = await accept;
            Assert.Equal(NetworkProbeStatus.Reachable, status);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Discovery_probes_only_configured_targets_and_preserves_order()
    {
        var now = DateTimeOffset.UtcNow;
        var connector = new RecordingConnector();
        var plugin = CreatePlugin(
            ["console.test:443", "192.0.2.10:80"],
            connector,
            new FixedTimeProvider(now));

        var result = await plugin.DiscoverAsync(
            ["console.test:443", "192.0.2.10:80"],
            750,
            CancellationToken.None);

        Assert.Equal(now, result.CompletedAt);
        Assert.Equal(["console.test:443", "192.0.2.10:80"],
            result.Devices.Select(device => device.Target));
        Assert.All(result.Devices,
            device => Assert.Equal(NetworkProbeStatus.Reachable, device.Status));
        Assert.All(connector.Timeouts, timeout => Assert.Equal(TimeSpan.FromMilliseconds(750), timeout));
        Assert.Contains(
            AgentPluginPermission.ConnectNetworkEndpoints,
            plugin.Manifest.Permissions);
    }

    [Fact]
    public async Task Discovery_rejects_target_not_in_local_allowlist()
    {
        var connector = new RecordingConnector();
        var plugin = CreatePlugin(["console.test:443"], connector, TimeProvider.System);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            plugin.DiscoverAsync(["other.test:443"], 500, CancellationToken.None));

        Assert.Empty(connector.Targets);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(5001)]
    public async Task Discovery_rejects_timeout_outside_hard_bounds(int timeoutMilliseconds)
    {
        var plugin = CreatePlugin(
            ["console.test:443"],
            new RecordingConnector(),
            TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            plugin.DiscoverAsync(
                ["console.test:443"],
                timeoutMilliseconds,
                CancellationToken.None));
    }

    [Theory]
    [InlineData("missing-port")]
    [InlineData("https://console.test:443")]
    [InlineData("console.test:0")]
    public void Target_parser_rejects_invalid_values(string value)
    {
        Assert.ThrowsAny<Exception>(() => NetworkTarget.Parse(value));
    }

    private static NetworkDeviceDiscoveryPlugin CreatePlugin(
        IReadOnlyList<string> allowedTargets,
        INetworkEndpointConnector connector,
        TimeProvider timeProvider) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                NetworkDiscoveryTargets = allowedTargets
            }),
            connector,
            timeProvider);

    private sealed class RecordingConnector : INetworkEndpointConnector
    {
        public List<NetworkTarget> Targets { get; } = [];
        public List<TimeSpan> Timeouts { get; } = [];

        public Task<NetworkProbeStatus> ProbeAsync(
            NetworkTarget target,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Targets.Add(target);
            Timeouts.Add(timeout);
            return Task.FromResult(NetworkProbeStatus.Reachable);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
