using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class SymetrixComposerDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "showvault-symetrix-composer-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Performing-Arts-Center.symx")]
    [InlineData("Performing-Arts-Center.SYMX")]
    public async Task Recognizes_site_file_and_preserves_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "revisions"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "Composer site file");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "revisions", "Performing-Arts-Center-before-upgrade.symx"),
            "revision");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "restore-prerequisites.md"),
            "Composer and firmware versions, device models, site identifier and network audio.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root), CancellationToken.None);

        Assert.Equal(SymetrixComposerDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files, file => file.RelativePath ==
            Path.Combine("revisions", "Performing-Arts-Center-before-upgrade.symx"));
        Assert.Contains(result.Files, file => file.RelativePath == "restore-prerequisites.md");
    }

    [Theory]
    [InlineData("LegacySite.sym")]
    [InlineData("ControlScreen.svlx")]
    public async Task Rejects_non_composer_artifact_without_site_file(string fileName)
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "not a Composer site file");
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_site_root()
    {
        var child = Path.Combine(_root, "sites");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.symx"), "site file");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(child), CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static SymetrixComposerDiscoveryPlugin CreatePlugin(string root) => new(
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            SymetrixComposerSiteRoots = [root]
        }),
        TimeProvider.System);
}
