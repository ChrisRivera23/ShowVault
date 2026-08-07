using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class SoundcraftViDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-soundcraft-vi-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Opening.snp")]
    [InlineData("Opening.SNP")]
    public async Task Recognizes_showfolder_and_preserves_complete_tree(string snapshotName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "Snapshots"));
        Directory.CreateDirectory(Path.Combine(_root, "revisions"));
        await File.WriteAllTextAsync(
            Path.Combine(_root, "Snapshots", snapshotName),
            "Vi snapshot");
        await File.WriteAllTextAsync(Path.Combine(_root, "ShowData.xml"), "ancillary data");
        await File.WriteAllTextAsync(Path.Combine(_root, "console.bk1"), "restart backup 1");
        await File.WriteAllTextAsync(Path.Combine(_root, "console.bk2"), "restart backup 2");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "revisions", "compatibility-notes.md"),
            "Vi model, software, Vistonics bays, GEQ mode and installed hardware.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(SoundcraftViDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("Snapshots", snapshotName));
        Assert.Contains(result.Files, file => file.RelativePath == "ShowData.xml");
        Assert.Contains(result.Files, file => file.RelativePath == "console.bk1");
        Assert.Contains(result.Files, file => file.RelativePath == "console.bk2");
    }

    [Fact]
    public async Task Rejects_snapshot_outside_required_snapshots_folder()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Opening.snp"), "snapshot");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_empty_snapshots_folder()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Snapshots"));
        await File.WriteAllTextAsync(Path.Combine(_root, "ShowData.xml"), "ancillary data");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_show_root()
    {
        var snapshots = Path.Combine(_root, "Snapshots");
        Directory.CreateDirectory(snapshots);
        await File.WriteAllTextAsync(Path.Combine(snapshots, "Opening.snp"), "snapshot");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(snapshots),
                CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static SoundcraftViDiscoveryPlugin CreatePlugin(string showRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                SoundcraftViShowRoots = [showRoot]
            }),
            TimeProvider.System);
}
