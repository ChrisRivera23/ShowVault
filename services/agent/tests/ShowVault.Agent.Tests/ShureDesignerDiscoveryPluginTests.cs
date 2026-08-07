using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class ShureDesignerDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-shure-designer-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Boardroom.rdf")]
    [InlineData("Boardroom.RDF")]
    public async Task Recognizes_room_design_and_preserves_project_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "legacy"));
        Directory.CreateDirectory(Path.Combine(_root, "floor-plans"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "room design");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "legacy", "Campus.dprj"),
            "legacy project");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "floor-plans", "Boardroom.png"),
            "floor plan");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "deployment-notes.md"),
            "Verify device models and firmware before deployment.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(ShureDesignerDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("legacy", "Campus.dprj"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("floor-plans", "Boardroom.png"));
        Assert.Contains(result.Files, file => file.RelativePath == "deployment-notes.md");
    }

    [Fact]
    public async Task Rejects_legacy_project_without_current_room_design()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Campus.dprj"), "legacy project");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_room_root()
    {
        var child = Path.Combine(_root, "rooms");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Boardroom.rdf"), "room design");

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

    private static ShureDesignerDiscoveryPlugin CreatePlugin(string roomRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                ShureDesignerRoomRoots = [roomRoot]
            }),
            TimeProvider.System);
}
