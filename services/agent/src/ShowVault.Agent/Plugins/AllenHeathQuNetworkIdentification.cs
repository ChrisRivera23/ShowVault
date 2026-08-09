using System.Net;
using System.Net.Sockets;

namespace ShowVault.Agent.Plugins;

public interface IAllenHeathQuProtocolProbe
{
    Task<string?> IdentifyAsync(IPAddress address, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class AllenHeathQuProtocolProbe(int port = 51_325) : IAllenHeathQuProtocolProbe
{
    private const int MaximumResponseBytes = 64;
    private static readonly byte[] SystemStateRequest =
        [0xF0, 0x00, 0x00, 0x1A, 0x50, 0x11, 0x01, 0x00, 0x7F, 0x10, 0x00, 0xF7];
    private static readonly IReadOnlyDictionary<byte, string> ProductFamilies =
        new Dictionary<byte, string>
        {
            [0x01] = "Allen & Heath Qu-16",
            [0x02] = "Allen & Heath Qu-24",
            [0x03] = "Allen & Heath Qu-32",
            [0x04] = "Allen & Heath Qu-Pac",
            [0x05] = "Allen & Heath Qu-SB"
        };

    public async Task<string?> IdentifyAsync(
        IPAddress address, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            using var client = new TcpClient(address.AddressFamily);
            await client.ConnectAsync(address, port, timeoutSource.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync(SystemStateRequest, timeoutSource.Token);

            var response = new byte[MaximumResponseBytes];
            var received = 0;
            while (received < response.Length)
            {
                var count = await stream.ReadAsync(response.AsMemory(received), timeoutSource.Token);
                if (count == 0) break;
                received += count;
                var product = ParseSystemStateReply(response.AsSpan(0, received));
                if (product is not null) return product;
            }
            return null;
        }
        catch (SocketException) { return null; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (IOException) { return null; }
    }

    private static string? ParseSystemStateReply(ReadOnlySpan<byte> response)
    {
        Span<byte> midi = stackalloc byte[MaximumResponseBytes];
        var length = 0;
        foreach (var value in response)
        {
            if (value != 0xFE) midi[length++] = value;
        }

        ReadOnlySpan<byte> prefix = [0xF0, 0x00, 0x00, 0x1A, 0x50, 0x11, 0x01, 0x00];
        for (var offset = 0; offset + 14 <= length; offset++)
        {
            var reply = midi.Slice(offset, 14);
            if (!reply[..8].SequenceEqual(prefix) || reply[8] > 0x0F || reply[9] != 0x11 ||
                reply[11] > 0x7F || reply[12] > 0x7F || reply[13] != 0xF7)
                continue;
            if (ProductFamilies.TryGetValue(reply[10], out var product)) return product;
        }
        return null;
    }
}

public sealed record AllenHeathQuIdentification(IPAddress Address, string ProductFamily);
public sealed record AllenHeathQuIdentificationResult(
    Guid ProposalId, Guid DiscoveryCommandId, int AttemptedHostCount,
    IReadOnlyList<AllenHeathQuIdentification> Identifications, DateTimeOffset CompletedAt);

public sealed class AllenHeathQuNetworkIdentification(
    IAllenHeathQuProtocolProbe probe, TimeProvider timeProvider)
{
    public async Task<AllenHeathQuIdentificationResult> IdentifyAsync(
        Guid proposalId, Guid discoveryCommandId, IReadOnlyList<string> hosts,
        int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        if (proposalId == Guid.Empty || discoveryCommandId == Guid.Empty || hosts.Count > 32 ||
            timeoutMilliseconds is < 100 or > 500)
            throw new ArgumentException("Allen & Heath Qu identification authorization is invalid.");
        var identifications = new List<AllenHeathQuIdentification>();
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
