using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class QsysDesignerDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-qsys-designer-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Venue.qsys")]
    [InlineData("Venue.QSYS")]
    public async Task Recognizes_editable_design_and_preserves_local_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "plugins"));
        Directory.CreateDirectory(Path.Combine(_root, "user-components"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "design");
        await File.WriteAllTextAsync(Path.Combine(_root, "plugins", "Lighting.qplug"), "plugin");
        await File.WriteAllTextAsync(Path.Combine(_root, "user-components", "Paging.quc"), "component");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(QsysDesignerDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("plugins", "Lighting.qplug"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("user-components", "Paging.quc"));
    }

    [Fact]
    public async Task Rejects_companions_without_editable_design()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Lighting.qplug"), "plugin");

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
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.qsys"), "design");

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

    private static QsysDesignerDiscoveryPlugin CreatePlugin(string projectRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                QsysDesignerProjectRoots = [projectRoot]
            }),
            TimeProvider.System);
}
