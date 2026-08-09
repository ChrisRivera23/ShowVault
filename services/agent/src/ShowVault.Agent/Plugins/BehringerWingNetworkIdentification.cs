using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ShowVault.Agent.Plugins;

public interface IBehringerWingProtocolProbe
{
    Task<string?> IdentifyAsync(IPAddress address, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class BehringerWingProtocolProbe(int port = 2_222) : IBehringerWingProtocolProbe
{
    private const int MaximumResponseBytes = 256;
    private static readonly byte[] InformationRequest = "WING?"u8.ToArray();

    public async Task<string?> IdentifyAsync(
        IPAddress address, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            using var client = new UdpClient(address.AddressFamily);
            client.Connect(address, port);
            await client.SendAsync(InformationRequest, timeoutSource.Token);
            var response = new byte[MaximumResponseBytes + 1];
            var received = await client.Client.ReceiveAsync(
                response, SocketFlags.None, timeoutSource.Token);
            return ParseInformationReply(response.AsSpan(0, received));
        }
        catch (SocketException) { return null; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
    }

    private static string? ParseInformationReply(ReadOnlySpan<byte> response)
    {
        if (response.IsEmpty || response.Length > MaximumResponseBytes) return null;
        foreach (var value in response)
        {
            if (value is < 0x20 or > 0x7E) return null;
        }

        var fields = Encoding.ASCII.GetString(response).Split(',', StringSplitOptions.None);
        if (fields.Length != 6 || fields[0] != "WING" || fields[3] != "ngc-full" ||
            !IPAddress.TryParse(fields[1], out var returnedAddress) ||
            returnedAddress.AddressFamily != AddressFamily.InterNetwork ||
            string.IsNullOrEmpty(fields[2]) || string.IsNullOrEmpty(fields[4]) ||
            string.IsNullOrEmpty(fields[5]))
            return null;

        return "Behringer WING";
    }
}

public sealed record BehringerWingIdentification(IPAddress Address, string ProductFamily);
public sealed record BehringerWingIdentificationResult(
    Guid ProposalId, Guid DiscoveryCommandId, int AttemptedHostCount,
    IReadOnlyList<BehringerWingIdentification> Identifications, DateTimeOffset CompletedAt);

public sealed class BehringerWingNetworkIdentification(
    IBehringerWingProtocolProbe probe, TimeProvider timeProvider)
{
    public async Task<BehringerWingIdentificationResult> IdentifyAsync(
        Guid proposalId, Guid discoveryCommandId, IReadOnlyList<string> hosts,
        int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        if (proposalId == Guid.Empty || discoveryCommandId == Guid.Empty || hosts.Count > 32 ||
            timeoutMilliseconds is < 100 or > 500)
            throw new ArgumentException("Behringer WING identification authorization is invalid.");
        var identifications = new List<BehringerWingIdentification>();
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
