using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class JblVenueSynthesisDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "showvault-jbl-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Arena.vysn")]
    [InlineData("Arena.VYSN")]
    public async Task Recognizes_project_and_preserves_design_and_deployment_companions(string name)
    {
        Directory.CreateDirectory(Path.Combine(_root, "reports"));
        await File.WriteAllTextAsync(Path.Combine(_root, name), "Venue Synthesis project");
        await File.WriteAllTextAsync(Path.Combine(_root, "Arena.lac3"), "LAC design");
        await File.WriteAllTextAsync(Path.Combine(_root, "deployment.al"), "ArrayLink project");
        await File.WriteAllTextAsync(Path.Combine(_root, "venue.dxf"), "venue drawing");
        await File.WriteAllTextAsync(Path.Combine(_root, "reports", "rigging.pdf"), "report");

        var result = await CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None);

        Assert.Equal(JblVenueSynthesisDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == name);
        Assert.Contains(result.Files, file => file.RelativePath == "Arena.lac3");
        Assert.Contains(result.Files, file => file.RelativePath == "deployment.al");
        Assert.Contains(result.Files, file => file.RelativePath == "venue.dxf");
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("reports", "rigging.pdf"));
    }

    [Fact]
    public async Task Rejects_empty_project()
    {
        Directory.CreateDirectory(_root);
        File.Create(Path.Combine(_root, "empty.vysn")).Dispose();

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_lac_project_without_venue_synthesis_project()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Arena.lac3"), "LAC only");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_arraylink_project_without_venue_synthesis_project()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "deployment.al"), "ArrayLink only");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_root()
    {
        var child = Path.Combine(_root, "project");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Arena.vysn"), "project");

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

    private static JblVenueSynthesisDiscoveryPlugin CreatePlugin(string root) => new(
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            JblVenueSynthesisProjectRoots = [root]
        }),
        TimeProvider.System);
}
