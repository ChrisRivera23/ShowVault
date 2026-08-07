using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class TascamModelMtrDiscoveryPluginTests : IDisposable
{
    private readonly string _cardRoot = Path.Combine(
        Path.GetTempPath(),
        "showvault-tascam-model-mtr-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Track01.wav")]
    [InlineData("Track01.WAV")]
    public async Task Recognizes_complete_song_folder_and_preserves_internal_files(
        string trackName)
    {
        var songRoot = Path.Combine(_cardRoot, "MTR", "Festival");
        Directory.CreateDirectory(songRoot);
        await File.WriteAllTextAsync(Path.Combine(songRoot, trackName), "track audio");
        await File.WriteAllTextAsync(Path.Combine(songRoot, "Track02.wav"), "track audio");
        await File.WriteAllTextAsync(Path.Combine(songRoot, "song.sys"), "song metadata");
        await File.WriteAllTextAsync(
            Path.Combine(songRoot, "restore-prerequisites.md"),
            "Model 12, 16, 24 or 2400; firmware, sample format and channel layout.");

        var result = await CreatePlugin(songRoot).DiscoverAsync(
            new DiscoveryRequest(songRoot),
            CancellationToken.None);

        Assert.Equal(TascamModelMtrDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == trackName);
        Assert.Contains(result.Files, file => file.RelativePath == "Track02.wav");
        Assert.Contains(result.Files, file => file.RelativePath == "song.sys");
        Assert.Contains(result.Files,
            file => file.RelativePath == "restore-prerequisites.md");
    }

    [Fact]
    public async Task Rejects_music_export_folder()
    {
        var exportRoot = Path.Combine(_cardRoot, "MUSIC", "Festival");
        Directory.CreateDirectory(exportRoot);
        await File.WriteAllTextAsync(Path.Combine(exportRoot, "Festival.wav"), "mixdown");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(exportRoot).DiscoverAsync(
                new DiscoveryRequest(exportRoot),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_song_folder_without_track_data()
    {
        var songRoot = Path.Combine(_cardRoot, "MTR", "EmptySong");
        Directory.CreateDirectory(songRoot);
        await File.WriteAllTextAsync(Path.Combine(songRoot, "song.sys"), "metadata");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(songRoot).DiscoverAsync(
                new DiscoveryRequest(songRoot),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_song_root()
    {
        var songRoot = Path.Combine(_cardRoot, "MTR", "Festival");
        var child = Path.Combine(songRoot, "tracks");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Track01.wav"), "track");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreatePlugin(songRoot).DiscoverAsync(
                new DiscoveryRequest(child),
                CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_cardRoot))
        {
            Directory.Delete(_cardRoot, recursive: true);
        }
    }

    private static TascamModelMtrDiscoveryPlugin CreatePlugin(string songRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                TascamModelMtrSongRoots = [songRoot]
            }),
            TimeProvider.System);
}
