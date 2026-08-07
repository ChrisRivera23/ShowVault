using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class ResolumeDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-resolume-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Discovery_hashes_portable_composition_and_collected_media()
    {
        var bundle = Path.Combine(_root, "Festival Show");
        Directory.CreateDirectory(Path.Combine(bundle, "media"));
        var composition = Encoding.UTF8.GetBytes("portable composition");
        var media = Encoding.UTF8.GetBytes("collected media");
        await File.WriteAllBytesAsync(Path.Combine(bundle, "Festival.avc"), composition);
        await File.WriteAllBytesAsync(Path.Combine(bundle, "media", "intro.mov"), media);
        var now = DateTimeOffset.UtcNow;
        var plugin = CreatePlugin(new FixedTimeProvider(now));

        var result = await plugin.DiscoverAsync(
            new DiscoveryRequest(bundle),
            CancellationToken.None);

        Assert.Equal(ResolumeDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Equal(now, result.CompletedAt);
        Assert.False(result.Truncated);
        Assert.Collection(
            result.Files.OrderBy(file => file.RelativePath),
            file =>
            {
                Assert.Equal("Festival.avc", file.RelativePath);
                Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(composition)), file.Sha256);
            },
            file =>
            {
                Assert.Equal(Path.Combine("media", "intro.mov"), file.RelativePath);
                Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(media)), file.Sha256);
            });
    }

    [Fact]
    public async Task Discovery_rejects_bundle_outside_resolume_allowlist()
    {
        Directory.CreateDirectory(_root);
        var outside = Path.Combine(
            Path.GetTempPath(),
            "showvault-resolume-outside",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreatePlugin(TimeProvider.System).DiscoverAsync(
                    new DiscoveryRequest(outside),
                    CancellationToken.None));
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ResolumeDiscoveryPlugin CreatePlugin(TimeProvider timeProvider) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                ResolumeDiscoveryRoots = [_root]
            }),
            timeProvider);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
