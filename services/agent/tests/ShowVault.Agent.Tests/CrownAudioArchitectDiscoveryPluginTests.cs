using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class CrownAudioArchitectDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "showvault-crown-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Arena.audioarchitect")]
    [InlineData("Arena.AUDIOARCHITECT")]
    public async Task Recognizes_venue_and_preserves_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "logs"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "Audio Architect venue");
        await File.WriteAllTextAsync(Path.Combine(_root, "parameters.json"), "parameter export");
        await File.WriteAllTextAsync(Path.Combine(_root, "logs", "SAEventLog.sdf"), "event evidence");
        await File.WriteAllTextAsync(Path.Combine(_root, "restore-prerequisites.md"), "Audio Architect version, Crown models, firmware, HiQnet IDs and network routing.");
        var result = await CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None);
        Assert.Equal(CrownAudioArchitectDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files, file => file.RelativePath == "parameters.json");
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("logs", "SAEventLog.sdf"));
    }

    [Theory]
    [InlineData("parameters.json")]
    [InlineData("SAEventLog.sdf")]
    public async Task Rejects_companion_without_venue(string fileName)
    {
        Directory.CreateDirectory(_root); await File.WriteAllTextAsync(Path.Combine(_root, fileName), "companion");
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_root()
    {
        var child = Path.Combine(_root, "venues"); Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.audioarchitect"), "venue");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(child), CancellationToken.None));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private static CrownAudioArchitectDiscoveryPlugin CreatePlugin(string root) => new(
        Options.Create(new AgentOptions { ControlPlaneUri = new Uri("https://control.test"), Name = "Test Agent", CrownAudioArchitectVenueRoots = [root] }), TimeProvider.System);
}
