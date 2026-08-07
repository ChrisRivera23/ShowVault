using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class MeyerSoundMapp3dDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "showvault-meyer-sound-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Venue.mapp")]
    [InlineData("Venue.MAPP")]
    public async Task Recognizes_project_and_preserves_backups_drawings_and_exports(string name)
    {
        Directory.CreateDirectory(Path.Combine(_root, "MAPP Backup"));
        Directory.CreateDirectory(Path.Combine(_root, "exports"));
        await File.WriteAllTextAsync(Path.Combine(_root, name), "MAPP 3D project");
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.dxf"), "venue drawing");
        await File.WriteAllTextAsync(Path.Combine(_root, "MAPP Backup", "Venue autosave.mapp"), "autosave");
        await File.WriteAllTextAsync(Path.Combine(_root, "exports", "equipment-list.pdf"), "report");

        var result = await CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None);

        Assert.Equal(MeyerSoundMapp3dDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == name);
        Assert.Contains(result.Files, file => file.RelativePath == "Venue.dxf");
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("MAPP Backup", "Venue autosave.mapp"));
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("exports", "equipment-list.pdf"));
    }

    [Fact]
    public async Task Rejects_empty_project()
    {
        Directory.CreateDirectory(_root);
        File.Create(Path.Combine(_root, "empty.mapp")).Dispose();

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_backup_folder_without_top_level_project()
    {
        Directory.CreateDirectory(Path.Combine(_root, "MAPP Backup"));
        await File.WriteAllTextAsync(Path.Combine(_root, "MAPP Backup", "Venue.mapp"), "autosave only");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_imported_drawing_without_project()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.skp"), "drawing only");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_root()
    {
        var child = Path.Combine(_root, "project");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.mapp"), "project");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(child), CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private static MeyerSoundMapp3dDiscoveryPlugin CreatePlugin(string root) => new(
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            MeyerSoundMapp3dProjectRoots = [root]
        }),
        TimeProvider.System);
}
