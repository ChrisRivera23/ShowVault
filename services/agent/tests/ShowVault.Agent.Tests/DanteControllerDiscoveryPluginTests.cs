using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class DanteControllerDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-dante-controller-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Venue-Normal.xml")]
    [InlineData("Venue-Failover.XML")]
    public async Task Recognizes_xml_preset_and_preserves_diagnostic_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "presets"));
        Directory.CreateDirectory(Path.Combine(_root, "diagnostics"));
        await File.WriteAllTextAsync(
            Path.Combine(_root, "presets", fileName),
            "<dantePreset><deviceRole /></dantePreset>");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "diagnostics", "events.log"),
            "event log");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "restore-notes.md"),
            "Review role assignments before applying.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(DanteControllerDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("presets", fileName));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("diagnostics", "events.log"));
        Assert.Contains(result.Files, file => file.RelativePath == "restore-notes.md");
    }

    [Fact]
    public async Task Rejects_diagnostics_without_xml_preset()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "events.log"), "event log");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_preset_root()
    {
        var child = Path.Combine(_root, "presets");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(
            Path.Combine(child, "Venue.xml"),
            "<dantePreset />");

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

    private static DanteControllerDiscoveryPlugin CreatePlugin(string presetRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                DanteControllerPresetRoots = [presetRoot]
            }),
            TimeProvider.System);
}
