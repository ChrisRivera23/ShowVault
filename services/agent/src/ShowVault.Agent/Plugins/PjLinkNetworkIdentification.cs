using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ShowVault.Agent.Plugins;

public interface IProjectorProtocolProbe
{
    Task<string?> IdentifyAsync(IPAddress address, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class ProjectorProtocolProbe(IReadOnlyList<IProjectorProtocolProbe> probes) : IProjectorProtocolProbe
{
    public async Task<string?> IdentifyAsync(
        IPAddress address, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(probes.Select(
            probe => probe.IdentifyAsync(address, timeout, cancellationToken)));
        return results.FirstOrDefault(result => result is not null);
    }
}

public sealed class PjLinkProjectorProbe : IProjectorProtocolProbe
{
    private const int Port = 4_352;
    private const int MaximumLineBytes = 256;

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
            if (await ReadLineAsync(stream, timeoutSource.Token) != "PJLINK 0")
                return null;

            await stream.WriteAsync(Encoding.ASCII.GetBytes("%1INF1 ?\r"), timeoutSource.Token);
            var manufacturer = await ReadLineAsync(stream, timeoutSource.Token);
            if (manufacturer is not "%1INF1=CHRISTIE" and not "%1INF1=Panasonic" and not "%1INF1=EPSON")
                return null;

            await stream.WriteAsync(Encoding.ASCII.GetBytes("%1INF2 ?\r"), timeoutSource.Token);
            return (manufacturer, await ReadLineAsync(stream, timeoutSource.Token)) switch
            {
                ("%1INF1=CHRISTIE", "%1INF2=LX41") => "Christie LX41",
                ("%1INF1=CHRISTIE", "%1INF2=LW41") => "Christie LW41",
                ("%1INF1=Panasonic", "%1INF2=DZ770") => "Panasonic PT-DZ770",
                ("%1INF1=Panasonic", "%1INF2=VW431DEA") => "Panasonic PT-VW431DEA",
                ("%1INF1=Panasonic", "%1INF2=RZ470") => "Panasonic PT-RZ470",
                ("%1INF1=Panasonic", "%1INF2=RW430") => "Panasonic PT-RW430",
                ("%1INF1=EPSON", "%1INF2=EPSON QB1000B") => "Epson QB1000B",
                ("%1INF1=EPSON", "%1INF2=EPSON QB1000W") => "Epson QB1000W",
                _ => null
            };
        }
        catch (SocketException) { return null; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (IOException) { return null; }
    }

    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaximumLineBytes];
        for (var offset = 0; offset < buffer.Length; offset++)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(offset, 1), cancellationToken);
            if (count == 0) return null;
            if (buffer[offset] == '\r') return Encoding.ASCII.GetString(buffer, 0, offset);
        }
        return null;
    }
}

public sealed record PjLinkIdentification(IPAddress Address, string ProductFamily);
public sealed record PjLinkIdentificationResult(
    Guid ProposalId, Guid DiscoveryCommandId, int AttemptedHostCount,
    IReadOnlyList<PjLinkIdentification> Identifications, DateTimeOffset CompletedAt);

public sealed class PjLinkNetworkIdentification(IProjectorProtocolProbe probe, TimeProvider timeProvider)
{
    public async Task<PjLinkIdentificationResult> IdentifyAsync(
        Guid proposalId, Guid discoveryCommandId, IReadOnlyList<string> hosts,
        int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        if (proposalId == Guid.Empty || discoveryCommandId == Guid.Empty || hosts.Count > 32 ||
            timeoutMilliseconds is < 100 or > 500)
            throw new ArgumentException("PJLink identification authorization is invalid.");
        var identifications = new List<PjLinkIdentification>();
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
