using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class PowersoftArmoniaPlusDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "showvault-powersoft-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Arena.paw4")]
    [InlineData("Arena.PAW4")]
    public async Task Recognizes_current_project_and_preserves_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "revisions"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "ArmoniaPlus project");
        await File.WriteAllTextAsync(Path.Combine(_root, "revisions", "Arena.paw3"), "legacy revision");
        await File.WriteAllTextAsync(Path.Combine(_root, "speaker-library.pam2"), "speaker presets");
        await File.WriteAllTextAsync(Path.Combine(_root, "restore-prerequisites.md"), "ArmoniaPlus and firmware versions, models, routing and AES67 flows.");
        var result = await CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None);
        Assert.Equal(PowersoftArmoniaPlusDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("revisions", "Arena.paw3"));
        Assert.Contains(result.Files, file => file.RelativePath == "speaker-library.pam2");
    }

    [Theory]
    [InlineData("Legacy.paw3")]
    [InlineData("Speakers.pam2")]
    public async Task Rejects_legacy_or_preset_file_without_current_project(string fileName)
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "companion");
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_root()
    {
        var child = Path.Combine(_root, "projects"); Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.paw4"), "project");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(child), CancellationToken.None));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private static PowersoftArmoniaPlusDiscoveryPlugin CreatePlugin(string root) => new(
        Options.Create(new AgentOptions { ControlPlaneUri = new Uri("https://control.test"), Name = "Test Agent", PowersoftArmoniaPlusProjectRoots = [root] }), TimeProvider.System);
}
