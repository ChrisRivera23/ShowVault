using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace ShowVault.Agent.Plugins;

public sealed record NetworkTarget(string Host, int Port)
{
    public static bool IsValid(string value)
    {
        try
        {
            Parse(value);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or FormatException)
        {
            return false;
        }
    }

    public static NetworkTarget Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!Uri.TryCreate($"tcp://{value}", UriKind.Absolute, out var uri) ||
            uri.HostNameType == UriHostNameType.Unknown ||
            uri.Port is < 1 or > 65_535 ||
            uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new FormatException($"Network target must be a host and port: {value}");
        }

        return new NetworkTarget(uri.IdnHost.ToLowerInvariant(), uri.Port);
    }

    public override string ToString() => Host.Contains(':', StringComparison.Ordinal)
        ? $"[{Host}]:{Port}"
        : $"{Host}:{Port}";
}

public enum NetworkProbeStatus
{
    Reachable,
    Refused,
    TimedOut,
    Unreachable
}

public sealed record NetworkDeviceProbe(
    string Target,
    NetworkProbeStatus Status);

public sealed record NetworkDeviceDiscoveryResult(
    string PluginId,
    string PluginVersion,
    DateTimeOffset CompletedAt,
    IReadOnlyList<NetworkDeviceProbe> Devices);

public interface INetworkEndpointConnector
{
    Task<NetworkProbeStatus> ProbeAsync(
        NetworkTarget target,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

public sealed class TcpNetworkEndpointConnector : INetworkEndpointConnector
{
    public async Task<NetworkProbeStatus> ProbeAsync(
        NetworkTarget target,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(target.Host, target.Port, timeoutSource.Token);
            return NetworkProbeStatus.Reachable;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return NetworkProbeStatus.TimedOut;
        }
        catch (SocketException exception) when (
            exception.SocketErrorCode == SocketError.ConnectionRefused)
        {
            return NetworkProbeStatus.Refused;
        }
        catch (SocketException)
        {
            return NetworkProbeStatus.Unreachable;
        }
    }
}

public sealed class NetworkDeviceDiscoveryPlugin(
    IOptions<AgentOptions> options,
    INetworkEndpointConnector connector,
    TimeProvider timeProvider)
{
    public const string PluginId = "showvault.network-device";
    private const int MaximumTargetCount = 128;
    private const int MaximumConcurrency = 8;

    public AgentPluginManifest Manifest { get; } = new(
        PluginId,
        "ShowVault Network Device Discovery",
        "0.1.0",
        new HashSet<AgentPluginCapability> { AgentPluginCapability.NetworkDiscovery },
        new HashSet<AgentPluginPermission> { AgentPluginPermission.ConnectNetworkEndpoints });

    public async Task<NetworkDeviceDiscoveryResult> DiscoverAsync(
        IReadOnlyList<string> requestedTargets,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestedTargets);
        if (requestedTargets.Count is < 1 or > MaximumTargetCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedTargets),
                $"Target count must be between 1 and {MaximumTargetCount}.");
        }

        if (timeoutMilliseconds is < 100 or > 5_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeoutMilliseconds),
                "Probe timeout must be between 100 and 5000 milliseconds.");
        }

        var allowedTargets = options.Value.NetworkDiscoveryTargets
            .Select(NetworkTarget.Parse)
            .ToHashSet();
        var targets = requestedTargets.Select(NetworkTarget.Parse).Distinct().ToArray();
        if (targets.Length != requestedTargets.Count)
        {
            throw new ArgumentException("Network discovery targets must be unique.", nameof(requestedTargets));
        }

        var unauthorized = targets.FirstOrDefault(target => !allowedTargets.Contains(target));
        if (unauthorized is not null)
        {
            throw new UnauthorizedAccessException(
                $"Network target is not allowed by the local Agent configuration: {unauthorized}");
        }

        using var concurrency = new SemaphoreSlim(MaximumConcurrency);
        var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
        var probes = await Task.WhenAll(targets.Select(async target =>
        {
            await concurrency.WaitAsync(cancellationToken);
            try
            {
                var status = await connector.ProbeAsync(target, timeout, cancellationToken);
                return new NetworkDeviceProbe(target.ToString(), status);
            }
            finally
            {
                concurrency.Release();
            }
        }));

        return new NetworkDeviceDiscoveryResult(
            Manifest.Id,
            Manifest.Version,
            timeProvider.GetUtcNow(),
            probes);
    }
}
