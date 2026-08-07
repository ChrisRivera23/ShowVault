using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class YamahaDspDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-yamaha-dsp-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Venue.pvd")]
    [InlineData("Venue.PVD")]
    public async Task Dme7_recognizes_ProVisionaire_Design_project_and_companions(string fileName)
    {
        var projectRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "notes"));
        await File.WriteAllTextAsync(Path.Combine(projectRoot, fileName), "project");
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "notes", "restore.txt"), "notes");

        var result = await CreateDme7(projectRoot).DiscoverAsync(
            new DiscoveryRequest(projectRoot),
            CancellationToken.None);

        Assert.Equal(YamahaDme7DiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("notes", "restore.txt"));
    }

    [Fact]
    public async Task MtxMrx_recognizes_editor_project_and_companions()
    {
        var projectRoot = Path.Combine(_root, "mtx-mrx");
        Directory.CreateDirectory(Path.Combine(projectRoot, "audio"));
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "Venue.mtx"), "project");
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "audio", "chime.wav"), "audio");

        var result = await CreateMtxMrx(projectRoot).DiscoverAsync(
            new DiscoveryRequest(projectRoot),
            CancellationToken.None);

        Assert.Equal(YamahaMtxMrxDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == "Venue.mtx");
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("audio", "chime.wav"));
    }

    [Fact]
    public async Task Dme7_rejects_MtxMrx_project()
    {
        var projectRoot = Path.Combine(_root, "wrong-family");
        Directory.CreateDirectory(projectRoot);
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "Venue.mtx"), "project");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateDme7(projectRoot).DiscoverAsync(
                new DiscoveryRequest(projectRoot),
                CancellationToken.None));
    }

    [Fact]
    public async Task MtxMrx_rejects_child_of_exact_project_root()
    {
        var projectRoot = Path.Combine(_root, "allowed");
        var child = Path.Combine(projectRoot, "child");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.mtx"), "project");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateMtxMrx(projectRoot).DiscoverAsync(
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

    private YamahaDme7DiscoveryPlugin CreateDme7(string projectRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaDme7ProjectRoots = [projectRoot]
            }),
            TimeProvider.System);

    private YamahaMtxMrxDiscoveryPlugin CreateMtxMrx(string projectRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaMtxMrxProjectRoots = [projectRoot]
            }),
            TimeProvider.System);
}
