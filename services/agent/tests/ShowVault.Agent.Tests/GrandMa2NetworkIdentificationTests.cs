using System.Net;
using System.Net.Sockets;
using System.Text;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class GrandMa2NetworkIdentificationTests
{
    [Fact]
    public async Task IdentifiesOnlyDocumentedGrandMa2BannerWithoutSendingData()
    {
        var listener = new TcpListener(IPAddress.Loopback, 30000);
        listener.Start();
        try
        {
            var server = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync();
                await using var stream = client.GetStream();
                var banner = Encoding.ASCII.GetBytes(
                    "MA2\r\nLogged in as User 'guest'\r\n[Channel]>Please login !\r\n[Channel]>");
                await stream.WriteAsync(banner);
                var received = new byte[1];
                return await stream.ReadAsync(received);
            });

            var result = await new GrandMa2TelnetBannerProbe().IdentifyAsync(
                IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

            Assert.Equal("grandMA2", result);
            Assert.Equal(0, await server);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task RejectsGenericTelnetBanner()
    {
        var listener = new TcpListener(IPAddress.Loopback, 30000);
        listener.Start();
        try
        {
            var server = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync();
                await using var stream = client.GetStream();
                await stream.WriteAsync(Encoding.ASCII.GetBytes("Welcome to a Telnet server\r\n"));
            });

            var result = await new GrandMa2TelnetBannerProbe().IdentifyAsync(
                IPAddress.Loopback, TimeSpan.FromMilliseconds(500), CancellationToken.None);

            Assert.Null(result);
            await server;
        }
        finally
        {
            listener.Stop();
        }
    }
}
