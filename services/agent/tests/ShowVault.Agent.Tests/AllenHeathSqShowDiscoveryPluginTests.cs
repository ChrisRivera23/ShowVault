using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class AllenHeathSqShowDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-allen-heath-sq-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("SHOW.DAT")]
    [InlineData("show.dat")]
    public async Task Recognizes_numbered_sq_show_and_preserves_all_show_data(string showFileName)
    {
        var showRoot = Path.Combine(_root, "AHSQ", "SHOWS", "SHOW0000");
        Directory.CreateDirectory(Path.Combine(showRoot, "SCENES"));
        Directory.CreateDirectory(Path.Combine(showRoot, "LIBRARY"));
        await File.WriteAllTextAsync(Path.Combine(showRoot, showFileName), "Venue");
        await File.WriteAllTextAsync(
            Path.Combine(showRoot, "SCENES", "SCENE000.DAT"),
            "scene");
        await File.WriteAllTextAsync(
            Path.Combine(showRoot, "LIBRARY", "VOCAL.DAT"),
            "library");

        var result = await CreatePlugin(showRoot).DiscoverAsync(
            new DiscoveryRequest(showRoot),
            CancellationToken.None);

        Assert.Equal(AllenHeathSqShowDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == showFileName);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("SCENES", "SCENE000.DAT"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("LIBRARY", "VOCAL.DAT"));
    }

    [Fact]
    public async Task Rejects_numbered_folder_without_show_manifest()
    {
        var showRoot = Path.Combine(_root, "AHSQ", "SHOWS", "SHOW0001");
        Directory.CreateDirectory(showRoot);
        await File.WriteAllTextAsync(Path.Combine(showRoot, "NVDATA.DAT"), "data");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(showRoot).DiscoverAsync(
                new DiscoveryRequest(showRoot),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_sq_plus_show_structure()
    {
        var showRoot = Path.Combine(_root, "AHSQ", "SQP-SHW", "SHOW0000");
        Directory.CreateDirectory(showRoot);
        await File.WriteAllTextAsync(Path.Combine(showRoot, "SHOW.DAT"), "SQ+ show");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(showRoot).DiscoverAsync(
                new DiscoveryRequest(showRoot),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_show_root()
    {
        var showRoot = Path.Combine(_root, "AHSQ", "SHOWS", "SHOW0000");
        var child = Path.Combine(showRoot, "SCENES");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(showRoot, "SHOW.DAT"), "Venue");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreatePlugin(showRoot).DiscoverAsync(
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

    private static AllenHeathSqShowDiscoveryPlugin CreatePlugin(string showRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                AllenHeathSqShowRoots = [showRoot]
            }),
            TimeProvider.System);
}
