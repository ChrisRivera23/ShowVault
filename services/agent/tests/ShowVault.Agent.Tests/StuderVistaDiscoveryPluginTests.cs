using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class StuderVistaDiscoveryPluginTests : IDisposable
{
    private readonly string _parent = Path.Combine(
        Path.GetTempPath(),
        "showvault-studer-vista-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("BCK_D950_BACKUP_News_20260807")]
    [InlineData("bck_d950_backup_Sports_20260807")]
    public async Task Recognizes_generated_backup_and_preserves_complete_tree(
        string directoryName)
    {
        var root = Path.Combine(_parent, directoryName);
        Directory.CreateDirectory(Path.Combine(root, "title", "snapshots"));
        Directory.CreateDirectory(Path.Combine(root, "configuration"));
        await File.WriteAllTextAsync(
            Path.Combine(root, "title", "snapshots", "Opening.snp"),
            "snapshot");
        await File.WriteAllTextAsync(
            Path.Combine(root, "configuration", "session.cor"),
            "session configuration");
        await File.WriteAllTextAsync(
            Path.Combine(root, "restore-prerequisites.md"),
            "Vista model, software release, Session Configuration ID and hardware topology.");

        var result = await CreatePlugin(root).DiscoverAsync(
            new DiscoveryRequest(root),
            CancellationToken.None);

        Assert.Equal(StuderVistaDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine(
                "title", "snapshots", "Opening.snp"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine(
                "configuration", "session.cor"));
        Assert.Contains(result.Files,
            file => file.RelativePath == "restore-prerequisites.md");
    }

    [Fact]
    public async Task Rejects_loose_snapshot_and_preset_folder()
    {
        var root = Path.Combine(_parent, "operator-files");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "Opening.snp"), "snapshot");
        await File.WriteAllTextAsync(Path.Combine(root, "Voice.pre"), "preset");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(root).DiscoverAsync(
                new DiscoveryRequest(root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_empty_generated_backup_directory()
    {
        var root = Path.Combine(_parent, "BCK_D950_BACKUP_Empty");
        Directory.CreateDirectory(root);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(root).DiscoverAsync(
                new DiscoveryRequest(root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_backup_root()
    {
        var root = Path.Combine(_parent, "BCK_D950_BACKUP_News");
        var child = Path.Combine(root, "title");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Opening.snp"), "snapshot");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreatePlugin(root).DiscoverAsync(
                new DiscoveryRequest(child),
                CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_parent))
        {
            Directory.Delete(_parent, recursive: true);
        }
    }

    private static StuderVistaDiscoveryPlugin CreatePlugin(string backupRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                StuderVistaTitleBackupRoots = [backupRoot]
            }),
            TimeProvider.System);
}
