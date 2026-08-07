using System.Security.Cryptography;
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
