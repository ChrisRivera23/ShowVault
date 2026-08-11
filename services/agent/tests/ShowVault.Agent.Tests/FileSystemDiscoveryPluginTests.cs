using System.Security.Cryptography;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class FileSystemDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-discovery-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Discovery_returns_relative_paths_metadata_and_content_hashes()
    {
        Directory.CreateDirectory(Path.Combine(_root, "shows"));
        var contents = Encoding.UTF8.GetBytes("show configuration");
        await File.WriteAllBytesAsync(Path.Combine(_root, "shows", "main.show"), contents);
        var now = DateTimeOffset.UtcNow;
        var plugin = CreatePlugin(new FixedTimeProvider(now));

        var result = await plugin.DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        var file = Assert.Single(result.Files);
        Assert.Equal(Path.Combine("shows", "main.show"), file.RelativePath);
        Assert.Equal(contents.Length, file.Size);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(contents)), file.Sha256);
        Assert.Equal(FileSystemDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Equal(now, result.CompletedAt);
        Assert.False(result.Truncated);
    }

    [Fact]
    public async Task Discovery_is_bounded_by_requested_file_limit()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "one.txt"), "1");
        await File.WriteAllTextAsync(Path.Combine(_root, "two.txt"), "2");
        var plugin = CreatePlugin(TimeProvider.System);

        var result = await plugin.DiscoverAsync(
            new DiscoveryRequest(_root, MaxFiles: 1),
            CancellationToken.None);

        Assert.Single(result.Files);
        Assert.True(result.Truncated);
    }

    [Fact]
    public async Task Discovery_rejects_paths_outside_locally_allowed_roots()
    {
        Directory.CreateDirectory(_root);
        var outsideRoot = Path.Combine(
            Path.GetTempPath(),
            "showvault-outside-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideRoot);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreatePlugin(TimeProvider.System).DiscoverAsync(
                    new DiscoveryRequest(outsideRoot),
                    CancellationToken.None));
        }
        finally
        {
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Discovery_rejects_requested_root_symlink_that_escapes_allowed_root()
    {
        Directory.CreateDirectory(_root);
        var outsideRoot = Path.Combine(
            Path.GetTempPath(),
            "showvault-linked-root-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllTextAsync(Path.Combine(outsideRoot, "outside.txt"), "outside");
        var linkedRoot = Path.Combine(_root, "linked-root");
        Directory.CreateSymbolicLink(linkedRoot, outsideRoot);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreatePlugin(TimeProvider.System).DiscoverAsync(
                    new DiscoveryRequest(linkedRoot),
                    CancellationToken.None));
        }
        finally
        {
            Directory.Delete(linkedRoot);
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Discovery_rejects_linked_content_instead_of_hashing_its_target()
    {
        Directory.CreateDirectory(_root);
        var outsideFile = Path.Combine(
            Path.GetTempPath(),
            $"showvault-linked-file-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(outsideFile, "outside");
        var linkedFile = Path.Combine(_root, "linked.txt");
        File.CreateSymbolicLink(linkedFile, outsideFile);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreatePlugin(TimeProvider.System).DiscoverAsync(
                    new DiscoveryRequest(_root),
                    CancellationToken.None));
        }
        finally
        {
            File.Delete(linkedFile);
            File.Delete(outsideFile);
        }
    }

    [Fact]
    public async Task Discovery_rejects_non_regular_unix_socket_entries()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var shortRoot = Path.Combine("/tmp", $"sv-{Guid.NewGuid():N}");
        Directory.CreateDirectory(shortRoot);
        try
        {
            var socketPath = Path.Combine(shortRoot, "local.sock");
            using var socket = new Socket(
                AddressFamily.Unix,
                SocketType.Stream,
                ProtocolType.Unspecified);
            socket.Bind(new UnixDomainSocketEndPoint(socketPath));
            var plugin = new FileSystemDiscoveryPlugin(
                Options.Create(new AgentOptions
                {
                    ControlPlaneUri = new Uri("https://control.test"),
                    Name = "Test Agent",
                    DiscoveryRoots = [shortRoot]
                }),
                TimeProvider.System);

            await Assert.ThrowsAnyAsync<IOException>(() =>
                plugin.DiscoverAsync(
                    new DiscoveryRequest(shortRoot),
                    CancellationToken.None));
        }
        finally
        {
            Directory.Delete(shortRoot, recursive: true);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private FileSystemDiscoveryPlugin CreatePlugin(TimeProvider timeProvider) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                DiscoveryRoots = [_root]
            }),
            timeProvider);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
