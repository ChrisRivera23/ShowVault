using System.Net;
using System.Net.Sockets;

namespace ShowVault.Agent.Plugins;

public sealed class NecProjectorProbe : IProjectorProtocolProbe
{
    private const int Port = 7_142;
    private const int ResponseLength = 22;
    private static readonly byte[] BaseModelTypeRequest = [0x00, 0xBF, 0x00, 0x00, 0x01, 0x00, 0xC0];

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
            await stream.WriteAsync(BaseModelTypeRequest, timeoutSource.Token);
            var response = await ReadResponseAsync(stream, timeoutSource.Token);
            if (response is null || response[0] != 0x20 || response[1] != 0xBF ||
                response[4] != 0x10 || response[5] != 0x00 ||
                response[^1] != response[..^1].Aggregate(0, (sum, value) => sum + value) % 256)
                return null;

            return (response[6], response[7], response[17], response[18]) switch
            {
                (0xFF, 0x30, 0x00, 0x10) => "NEC NP-PH3501QL",
                (0xFF, 0x30, 0x01, 0x10) => "NEC NP-PH2601QL",
                (0xFF, 0x35, 0x00, 0x10) => "NEC NP-PX2000UL",
                (0xFF, 0x35, 0x00, 0x11) => "NEC NP-PX2201UL",
                _ => null
            };
        }
        catch (SocketException) { return null; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (IOException) { return null; }
    }

    private static async Task<byte[]?> ReadResponseAsync(
        Stream stream, CancellationToken cancellationToken)
    {
        var response = new byte[ResponseLength];
        var offset = 0;
        while (offset < response.Length)
        {
            var count = await stream.ReadAsync(response.AsMemory(offset), cancellationToken);
            if (count == 0) return null;
            offset += count;
        }
        return response;
    }
}
