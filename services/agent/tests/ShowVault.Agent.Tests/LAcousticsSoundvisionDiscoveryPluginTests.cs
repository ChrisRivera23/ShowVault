using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class LAcousticsSoundvisionDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "showvault-l-acoustics-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Venue.xmlp")]
    [InlineData("Venue.XMLP")]
    public async Task Recognizes_project_and_preserves_companions(string name)
    {
        Directory.CreateDirectory(Path.Combine(_root, "commissioning"));
        await File.WriteAllTextAsync(Path.Combine(_root, name), "Soundvision project");
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.xmls"), "venue model");
        await File.WriteAllTextAsync(Path.Combine(_root, "commissioning", "session-backup.dat"), "LA NWM session");
        var result = await CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None);
        Assert.Equal(LAcousticsSoundvisionDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == name);
        Assert.Contains(result.Files, file => file.RelativePath == "Venue.xmls");
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("commissioning", "session-backup.dat"));
    }

    [Fact]
    public async Task Rejects_empty_project() { Directory.CreateDirectory(_root); File.Create(Path.Combine(_root, "empty.xmlp")).Dispose(); await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None)); }

    [Fact]
    public async Task Rejects_venue_without_project() { Directory.CreateDirectory(_root); await File.WriteAllTextAsync(Path.Combine(_root, "Venue.xmls"), "venue only"); await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None)); }

    [Fact]
    public async Task Rejects_child_of_exact_root() { var child = Path.Combine(_root, "projects"); Directory.CreateDirectory(child); await File.WriteAllTextAsync(Path.Combine(child, "Venue.xmlp"), "project"); await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(child), CancellationToken.None)); }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private static LAcousticsSoundvisionDiscoveryPlugin CreatePlugin(string root) => new(Options.Create(new AgentOptions { ControlPlaneUri = new Uri("https://control.test"), Name = "Test Agent", LAcousticsSoundvisionProjectRoots = [root] }), TimeProvider.System);
}
