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
    public async Task Discovery_hashes_recognized_user_data_tree()
    {
        var userData = Path.Combine(_root, "Resolume Arena");
        Directory.CreateDirectory(Path.Combine(userData, "Compositions"));
        Directory.CreateDirectory(Path.Combine(userData, "Fixture Library"));
        Directory.CreateDirectory(Path.Combine(userData, "Presets", "Advanced Output"));
        Directory.CreateDirectory(Path.Combine(userData, "Shortcuts"));
        await File.WriteAllTextAsync(
            Path.Combine(userData, "Compositions", "Venue Show.avc"),
            "composition");
        await File.WriteAllTextAsync(
            Path.Combine(userData, "Fixture Library", "Custom Fixture.xml"),
            "fixture");
        await File.WriteAllTextAsync(
            Path.Combine(userData, "Presets", "Advanced Output", "Main.xml"),
            "output");
        await File.WriteAllTextAsync(
            Path.Combine(userData, "Shortcuts", "OSC.xml"),
            "shortcuts");
        var plugin = CreatePlugin(
            TimeProvider.System,
            discoveryRoots: [],
            userDataRoots: [userData]);

        var result = await plugin.DiscoverAsync(
            new DiscoveryRequest(userData),
            CancellationToken.None);

        Assert.Equal("0.2.0", result.PluginVersion);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("Compositions", "Venue Show.avc"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("Fixture Library", "Custom Fixture.xml"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("Presets", "Advanced Output", "Main.xml"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("Shortcuts", "OSC.xml"));
    }

    [Fact]
    public async Task Discovery_rejects_user_data_root_without_resolume_directories()
    {
        var userData = Path.Combine(_root, "not-resolume");
        Directory.CreateDirectory(userData);
        await File.WriteAllTextAsync(Path.Combine(userData, "notes.txt"), "notes");
        var plugin = CreatePlugin(
            TimeProvider.System,
            discoveryRoots: [],
            userDataRoots: [userData]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            plugin.DiscoverAsync(new DiscoveryRequest(userData), CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_child_of_user_data_root()
    {
        var userData = Path.Combine(_root, "Resolume Arena");
        var compositions = Path.Combine(userData, "Compositions");
        Directory.CreateDirectory(compositions);
        var plugin = CreatePlugin(
            TimeProvider.System,
            discoveryRoots: [],
            userDataRoots: [userData]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            plugin.DiscoverAsync(new DiscoveryRequest(compositions), CancellationToken.None));
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

    private ResolumeDiscoveryPlugin CreatePlugin(
        TimeProvider timeProvider,
        IReadOnlyList<string>? discoveryRoots = null,
        IReadOnlyList<string>? userDataRoots = null) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                ResolumeDiscoveryRoots = discoveryRoots ?? [_root],
                ResolumeUserDataRoots = userDataRoots ?? []
            }),
            timeProvider);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
