using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class NexoNs1DiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "showvault-nexo-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Arena.nexo3")]
    [InlineData("Arena.NEXO")]
    public async Task Recognizes_project_and_preserves_revisions_imports_and_exports(string name)
    {
        Directory.CreateDirectory(Path.Combine(_root, "exports"));
        await File.WriteAllTextAsync(Path.Combine(_root, name), "NS-1 project");
        await File.WriteAllTextAsync(Path.Combine(_root, "Arena revision.nexo"), "project revision");
        await File.WriteAllTextAsync(Path.Combine(_root, "venue.dxf"), "venue model");
        await File.WriteAllTextAsync(Path.Combine(_root, "exports", "speaker-list.pdf"), "report");

        var result = await CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None);

        Assert.Equal(NexoNs1DiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == name);
        Assert.Contains(result.Files, file => file.RelativePath == "Arena revision.nexo");
        Assert.Contains(result.Files, file => file.RelativePath == "venue.dxf");
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("exports", "speaker-list.pdf"));
    }

    [Fact]
    public async Task Rejects_empty_project()
    {
        Directory.CreateDirectory(_root);
        File.Create(Path.Combine(_root, "empty.nexo3")).Dispose();

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_imported_venue_without_project()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "venue.dxf"), "venue only");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_project_only_below_top_level()
    {
        Directory.CreateDirectory(Path.Combine(_root, "revisions"));
        await File.WriteAllTextAsync(Path.Combine(_root, "revisions", "Arena.nexo3"), "project");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_root()
    {
        var child = Path.Combine(_root, "project");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Arena.nexo3"), "project");

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

    private static NexoNs1DiscoveryPlugin CreatePlugin(string root) => new(
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            NexoNs1ProjectRoots = [root]
        }),
        TimeProvider.System);
}
