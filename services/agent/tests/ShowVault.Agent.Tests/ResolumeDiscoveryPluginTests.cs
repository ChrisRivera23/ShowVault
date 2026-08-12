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
    public async Task Discovery_hashes_exact_bundle_with_root_level_composition()
    {
        var bundle = Path.Combine(_root, "Festival Show");
        Directory.CreateDirectory(Path.Combine(bundle, "media"));
        var composition = Encoding.UTF8.GetBytes("portable composition");
        var media = Encoding.UTF8.GetBytes("collected media");
        await File.WriteAllBytesAsync(Path.Combine(bundle, "Festival.avc"), composition);
        await File.WriteAllBytesAsync(Path.Combine(bundle, "media", "intro.mov"), media);
        var now = DateTimeOffset.UtcNow;
        var plugin = CreatePlugin([bundle], new FixedTimeProvider(now));

        var result = await plugin.DiscoverAsync(
            new DiscoveryRequest(bundle, ResolumeDiscoveryPlugin.MaximumFileLimit),
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
        Assert.Contains(AgentPluginPermission.ReadFiles, plugin.Manifest.Permissions);
    }

    [Fact]
    public async Task Discovery_rejects_descendant_of_exact_authorized_root()
    {
        var bundle = Path.Combine(_root, "bundle");
        var child = Path.Combine(bundle, "child");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Child.avc"), "composition");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreatePlugin([bundle], TimeProvider.System).DiscoverAsync(
                new DiscoveryRequest(child),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_exact_root_without_resolume_composition()
    {
        var unrelated = Path.Combine(_root, "unrelated-notes");
        Directory.CreateDirectory(unrelated);
        await File.WriteAllTextAsync(Path.Combine(unrelated, "notes.txt"), "private notes");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin([unrelated], TimeProvider.System).DiscoverAsync(
                new DiscoveryRequest(unrelated),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_exact_root_that_is_a_symbolic_link()
    {
        Directory.CreateDirectory(_root);
        var outside = Path.Combine(
            Path.GetTempPath(),
            "showvault-resolume-linked-outside",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "Outside.avc"), "outside");
        var linkedRoot = Path.Combine(_root, "linked-bundle");
        Directory.CreateSymbolicLink(linkedRoot, outside);
        try
        {
            await Assert.ThrowsAsync<IOException>(() =>
                CreatePlugin([linkedRoot], TimeProvider.System).DiscoverAsync(
                    new DiscoveryRequest(linkedRoot),
                    CancellationToken.None));
        }
        finally
        {
            Directory.Delete(linkedRoot);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task Discovery_rejects_linked_content()
    {
        var bundle = Path.Combine(_root, "linked-content");
        Directory.CreateDirectory(bundle);
        await File.WriteAllTextAsync(Path.Combine(bundle, "Venue.avc"), "composition");
        var outside = Path.Combine(_root, "outside.mov");
        await File.WriteAllTextAsync(outside, "outside");
        var linkedFile = Path.Combine(bundle, "linked.mov");
        File.CreateSymbolicLink(linkedFile, outside);
        try
        {
            await Assert.ThrowsAsync<IOException>(() =>
                CreatePlugin([bundle], TimeProvider.System).DiscoverAsync(
                    new DiscoveryRequest(bundle),
                    CancellationToken.None));
        }
        finally
        {
            File.Delete(linkedFile);
        }
    }

    [Fact]
    public async Task Discovery_fails_instead_of_returning_a_truncated_bundle()
    {
        var bundle = Path.Combine(_root, "large-bundle");
        Directory.CreateDirectory(bundle);
        await File.WriteAllTextAsync(Path.Combine(bundle, "Venue.avc"), "composition");
        for (var index = 0; index < ResolumeDiscoveryPlugin.MaximumFileLimit; index++)
        {
            await File.WriteAllTextAsync(
                Path.Combine(bundle, $"media-{index:D3}.mov"),
                index.ToString());
        }

        await Assert.ThrowsAsync<IOException>(() =>
            CreatePlugin([bundle], TimeProvider.System).DiscoverAsync(
                new DiscoveryRequest(bundle, ResolumeDiscoveryPlugin.MaximumFileLimit),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_honors_cancellation_without_returning_a_partial_result()
    {
        var bundle = Path.Combine(_root, "cancelled-bundle");
        Directory.CreateDirectory(bundle);
        await File.WriteAllTextAsync(Path.Combine(bundle, "Venue.avc"), "composition");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreatePlugin([bundle], TimeProvider.System).DiscoverAsync(
                new DiscoveryRequest(bundle),
                cancellation.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static ResolumeDiscoveryPlugin CreatePlugin(
        IReadOnlyList<string> allowedRoots,
        TimeProvider timeProvider) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                ResolumeDiscoveryRoots = allowedRoots
            }),
            timeProvider);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
