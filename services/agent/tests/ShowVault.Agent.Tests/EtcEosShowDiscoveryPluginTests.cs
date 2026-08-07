using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class EtcEosShowDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-etc-eos-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Venue 2026-08-07 19-30-00.esf3d")]
    [InlineData("Venue 2026-08-07 19-30-00.ESF2")]
    [InlineData("Venue 2026-08-07 19-30-00.esf")]
    public async Task Recognizes_native_show_formats_and_preserves_archive_companions(
        string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "ShowArchive", "Venue"));
        Directory.CreateDirectory(Path.Combine(_root, "exports"));
        Directory.CreateDirectory(Path.Combine(_root, "settings"));
        await File.WriteAllTextAsync(
            Path.Combine(_root, "ShowArchive", "Venue", fileName),
            "show");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "exports", "Venue.asc"),
            "ascii export");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "settings", "eos-settings.ini"),
            "settings");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(EtcEosShowDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("ShowArchive", "Venue", fileName));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("exports", "Venue.asc"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("settings", "eos-settings.ini"));
    }

    [Fact]
    public async Task Rejects_exports_without_native_show_file()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.asc"), "ascii export");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_archive_root()
    {
        var child = Path.Combine(_root, "ShowArchive");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.esf3d"), "show");

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

    private static EtcEosShowDiscoveryPlugin CreatePlugin(string archiveRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                EtcEosShowArchiveRoots = [archiveRoot]
            }),
            TimeProvider.System);
}
