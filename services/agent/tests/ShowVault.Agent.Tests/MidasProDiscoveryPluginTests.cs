using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class MidasProDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-midas-pro-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Festival.show")]
    [InlineData("Festival.SHOW")]
    public async Task Recognizes_show_and_preserves_recovery_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "revisions"));
        Directory.CreateDirectory(Path.Combine(_root, "presets"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "Midas PRO show");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "revisions", "Festival-rehearsal.show"),
            "earlier show");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "presets", "system-patching.preset"),
            "supervised patching companion");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "restore-prerequisites.md"),
            "PRO/XL8 model, software, channel capacity, I/O hardware and patching.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(MidasProDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine(
                "revisions", "Festival-rehearsal.show"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine(
                "presets", "system-patching.preset"));
    }

    [Fact]
    public async Task Rejects_presets_without_exported_show()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Vocal.preset"), "preset");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_m32_show_format()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Festival.shw"), "M32 show");
        await File.WriteAllTextAsync(Path.Combine(_root, "Opening.scn"), "M32 scene");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_show_root()
    {
        var child = Path.Combine(_root, "shows");
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

    private static MidasProDiscoveryPlugin CreatePlugin(string showRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                MidasProShowRoots = [showRoot]
            }),
            TimeProvider.System);
}
