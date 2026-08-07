using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class SslLiveDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-ssl-live-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Venue.show")]
    [InlineData("Venue.SHOW")]
    public async Task Recognizes_showfile_and_preserves_recovery_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "revisions"));
        Directory.CreateDirectory(Path.Combine(_root, "DataBackup"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "SSL Live show");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "revisions", "Venue-before-upgrade.show"),
            "older show");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "DataBackup", "console.log"),
            "diagnostics");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "restore-prerequisites.md"),
            "Model, software, sample rate, clock, I/O and network settings.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(SslLiveDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine(
                "revisions", "Venue-before-upgrade.show"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("DataBackup", "console.log"));
    }

    [Fact]
    public async Task Rejects_diagnostics_without_operator_saved_showfile()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "console.log"), "diagnostics");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_show_root()
    {
        var child = Path.Combine(_root, "showfiles");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.show"), "show");

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

    private static SslLiveDiscoveryPlugin CreatePlugin(string showRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                SslLiveShowRoots = [showRoot]
            }),
            TimeProvider.System);
}
