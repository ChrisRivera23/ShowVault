using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class PeaveyNwareDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "showvault-peavey-nware-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Airport-Paging.npa")]
    [InlineData("Airport-Paging.NPA")]
    public async Task Recognizes_project_archive_and_preserves_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "plugins"));
        Directory.CreateDirectory(Path.Combine(_root, "media"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "NWare project archive");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "plugins", "AmplifierControl.npp"), "project plugin");
        await File.WriteAllTextAsync(Path.Combine(_root, "kiosk.xml"), "kiosk personality");
        await File.WriteAllTextAsync(Path.Combine(_root, "media", "chime.wav"), "media");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "restore-prerequisites.md"),
            "NWare version, processor models, firmware, cards, network audio and node roles.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root), CancellationToken.None);

        Assert.Equal(PeaveyNwareDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files, file => file.RelativePath ==
            Path.Combine("plugins", "AmplifierControl.npp"));
        Assert.Contains(result.Files, file => file.RelativePath == "kiosk.xml");
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("media", "chime.wav"));
        Assert.Contains(result.Files, file => file.RelativePath == "restore-prerequisites.md");
    }

    [Theory]
    [InlineData("DevicePlugin.npp")]
    [InlineData("kiosk.xml")]
    public async Task Rejects_companion_without_project_archive(string fileName)
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "companion");
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_project_root()
    {
        var child = Path.Combine(_root, "projects");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.npa"), "project");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(child), CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static PeaveyNwareDiscoveryPlugin CreatePlugin(string root) => new(
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            PeaveyNwareProjectRoots = [root]
        }),
        TimeProvider.System);
}
