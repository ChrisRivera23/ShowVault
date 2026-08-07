using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class YamahaControlDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-yamaha-control-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Venue.pvcppj")]
    [InlineData("Venue.PVCPPJ")]
    public async Task Discovers_design_project_Kiosk_controllers_and_assets(string projectFile)
    {
        Directory.CreateDirectory(Path.Combine(_root, "controllers"));
        Directory.CreateDirectory(Path.Combine(_root, "images"));
        await File.WriteAllTextAsync(Path.Combine(_root, projectFile), "design project");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "controllers", "FrontDesk.pvksk"),
            "controller");
        await File.WriteAllTextAsync(Path.Combine(_root, "images", "logo.png"), "image");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(YamahaProVisionaireControlDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == projectFile);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("controllers", "FrontDesk.pvksk"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("images", "logo.png"));
    }

    [Fact]
    public async Task Rejects_exported_Kiosk_controller_without_editable_design_project()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "FrontDesk.pvksk"), "controller");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_project_root()
    {
        var child = Path.Combine(_root, "child");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.pvcppj"), "design project");

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

    private static YamahaProVisionaireControlDiscoveryPlugin CreatePlugin(string projectRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaProVisionaireControlProjectRoots = [projectRoot]
            }),
            TimeProvider.System);
}
