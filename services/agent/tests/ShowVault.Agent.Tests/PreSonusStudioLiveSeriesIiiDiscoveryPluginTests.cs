using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class PreSonusStudioLiveSeriesIiiDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-presonus-series-iii-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("StudioLive-2026-08-07.bak")]
    [InlineData("StudioLive-2026-08-07.BAK")]
    public async Task Recognizes_full_backup_and_preserves_recovery_companions(
        string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "revisions"));
        Directory.CreateDirectory(Path.Combine(_root, "documentation"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "Series III full backup");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "revisions", "StudioLive-before-update.bak"),
            "earlier backup");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "documentation", "saved-content.md"),
            "Saved projects, scenes and presets; unsaved changes excluded.");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "restore-prerequisites.md"),
            "StudioLive Series III model, firmware and Universal Control version.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(
            PreSonusStudioLiveSeriesIiiDiscoveryPlugin.PluginId,
            result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine(
                "revisions", "StudioLive-before-update.bak"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine(
                "documentation", "saved-content.md"));
    }

    [Fact]
    public async Task Rejects_individual_scene_without_full_backup()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Festival.scn"), "scene");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_capture_session_without_mixer_backup()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Festival.capture"), "recording");
        await File.WriteAllTextAsync(Path.Combine(_root, "Track01.wav"), "audio");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_backup_root()
    {
        var child = Path.Combine(_root, "backups");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "StudioLive.bak"), "backup");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(child),
                CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static PreSonusStudioLiveSeriesIiiDiscoveryPlugin CreatePlugin(
        string backupRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                PreSonusStudioLiveSeriesIiiBackupRoots = [backupRoot]
            }),
            TimeProvider.System);
}
