using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class BehringerWingDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-behringer-wing-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Festival.show")]
    [InlineData("Festival.SHOW")]
    public async Task Recognizes_show_and_preserves_referenced_files(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "snapshots"));
        Directory.CreateDirectory(Path.Combine(_root, "snippets"));
        Directory.CreateDirectory(Path.Combine(_root, "clips"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "WING show references");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "snapshots", "Opening.snap"),
            "full console snapshot");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "snippets", "GuestMic.snip"),
            "snippet");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "clips", "WalkIn.wav"),
            "audio clip");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "restore-prerequisites.md"),
            "WING model, firmware, I/O topology and intact relative references.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(BehringerWingDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("snapshots", "Opening.snap"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("snippets", "GuestMic.snip"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("clips", "WalkIn.wav"));
    }

    [Fact]
    public async Task Rejects_referenced_files_without_show_index()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Opening.snap"), "snapshot");
        await File.WriteAllTextAsync(Path.Combine(_root, "GuestMic.snip"), "snippet");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_x32_show_format()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Festival.shw"), "X32 show");
        await File.WriteAllTextAsync(Path.Combine(_root, "Opening.scn"), "X32 scene");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_show_root()
    {
        var child = Path.Combine(_root, "show");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Festival.show"), "show");

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

    private static BehringerWingDiscoveryPlugin CreatePlugin(string showRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                BehringerWingShowRoots = [showRoot]
            }),
            TimeProvider.System);
}
