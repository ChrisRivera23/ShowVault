using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ShowVault.Agent.Plugins;

public interface IYamahaDmeProtocolProbe
{
    Task<string?> IdentifyAsync(IPAddress address, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class YamahaDmeRemoteControlProbe : IYamahaDmeProtocolProbe
{
    private const int Port = 49280;
    private const int MaximumResponseBytes = 4_096;

    public async Task<string?> IdentifyAsync(
        IPAddress address,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            using var client = new TcpClient(address.AddressFamily);
            await client.ConnectAsync(address, Port, timeoutSource.Token);
            await using var stream = client.GetStream();
            var query = Encoding.ASCII.GetBytes("devinfo productname\ndevinfo manufacturer\n");
            await stream.WriteAsync(query, timeoutSource.Token);

            var buffer = new byte[MaximumResponseBytes];
            var received = 0;
            while (received < buffer.Length)
            {
                var count = await stream.ReadAsync(buffer.AsMemory(received), timeoutSource.Token);
                if (count == 0) break;
                received += count;
                var response = Encoding.ASCII.GetString(buffer, 0, received);
                if (response.Contains("OK devinfo productname \"DME7\"", StringComparison.Ordinal) &&
                    response.Contains("OK devinfo manufacturer \"Yamaha Corporation\"", StringComparison.Ordinal))
                    return "Yamaha DME7";
            }
            return null;
        }
        catch (SocketException) { return null; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (IOException) { return null; }
    }
}

public sealed record YamahaDmeIdentification(IPAddress Address, string ProductFamily);
public sealed record YamahaDmeIdentificationResult(
    Guid ProposalId,
    Guid DiscoveryCommandId,
    int AttemptedHostCount,
    IReadOnlyList<YamahaDmeIdentification> Identifications,
    DateTimeOffset CompletedAt);

public sealed class YamahaDmeNetworkIdentification(IYamahaDmeProtocolProbe probe, TimeProvider timeProvider)
{
    public async Task<YamahaDmeIdentificationResult> IdentifyAsync(
        Guid proposalId,
        Guid discoveryCommandId,
        IReadOnlyList<string> hosts,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        if (proposalId == Guid.Empty || discoveryCommandId == Guid.Empty || hosts.Count > 32 ||
            timeoutMilliseconds is < 100 or > 500)
            throw new ArgumentException("Yamaha DME identification authorization is invalid.");
        var identifications = new List<YamahaDmeIdentification>();
        foreach (var host in hosts)
        {
            var address = IPAddress.Parse(host);
            var family = await probe.IdentifyAsync(address,
                TimeSpan.FromMilliseconds(timeoutMilliseconds), cancellationToken);
            if (family is not null) identifications.Add(new(address, family));
        }
        return new(proposalId, discoveryCommandId, hosts.Count, identifications, timeProvider.GetUtcNow());
    }
}
