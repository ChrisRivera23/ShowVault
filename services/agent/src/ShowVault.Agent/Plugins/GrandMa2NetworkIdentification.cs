using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ShowVault.Agent.Plugins;

public interface IGrandMa2ProtocolProbe
{
    Task<string?> IdentifyAsync(IPAddress address, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class GrandMa2TelnetBannerProbe : IGrandMa2ProtocolProbe
{
    private const int Port = 30000;
    private const int MaximumResponseBytes = 4_096;

    public async Task<string?> IdentifyAsync(
        IPAddress address, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            using var client = new TcpClient(address.AddressFamily);
            await client.ConnectAsync(address, Port, timeoutSource.Token);
            await using var stream = client.GetStream();
            var buffer = new byte[MaximumResponseBytes];
            var received = 0;
            while (received < buffer.Length)
            {
                var count = await stream.ReadAsync(buffer.AsMemory(received), timeoutSource.Token);
                if (count == 0) break;
                received += count;
                var banner = Encoding.ASCII.GetString(buffer, 0, received);
                if (banner.Contains("Logged in as User 'guest'", StringComparison.Ordinal) &&
                    banner.Contains("[Channel]>Please login !", StringComparison.Ordinal))
                    return "grandMA2";
            }
            return null;
        }
        catch (SocketException) { return null; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (IOException) { return null; }
    }
}

public sealed record GrandMa2Identification(IPAddress Address, string ProductFamily);
public sealed record GrandMa2IdentificationResult(
    Guid ProposalId, Guid DiscoveryCommandId, int AttemptedHostCount,
    IReadOnlyList<GrandMa2Identification> Identifications, DateTimeOffset CompletedAt);

public sealed class GrandMa2NetworkIdentification(IGrandMa2ProtocolProbe probe, TimeProvider timeProvider)
{
    public async Task<GrandMa2IdentificationResult> IdentifyAsync(
        Guid proposalId, Guid discoveryCommandId, IReadOnlyList<string> hosts,
        int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        if (proposalId == Guid.Empty || discoveryCommandId == Guid.Empty || hosts.Count > 32 ||
            timeoutMilliseconds is < 100 or > 500)
            throw new ArgumentException("grandMA2 identification authorization is invalid.");
        var identifications = new List<GrandMa2Identification>();
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
