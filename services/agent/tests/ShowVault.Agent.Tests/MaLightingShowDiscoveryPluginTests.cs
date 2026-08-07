using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class MaLightingShowDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-ma-lighting-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GrandMa3_discovers_shows_backups_and_version_independent_library()
    {
        var exportRoot = Path.Combine(_root, "grandMA3");
        Directory.CreateDirectory(Path.Combine(exportRoot, "shared", "shows"));
        Directory.CreateDirectory(Path.Combine(exportRoot, "shared", "backups"));
        Directory.CreateDirectory(Path.Combine(exportRoot, "gma3_library", "fixturetypes"));
        var show = Encoding.UTF8.GetBytes("grandMA3 show");
        await File.WriteAllBytesAsync(
            Path.Combine(exportRoot, "shared", "shows", "Festival.show"),
            show);
        await File.WriteAllTextAsync(
            Path.Combine(exportRoot, "shared", "backups", "Festival.backup"),
            "backup");
        await File.WriteAllTextAsync(
            Path.Combine(exportRoot, "gma3_library", "fixturetypes", "Custom.xml"),
            "fixture");
        var now = DateTimeOffset.UtcNow;

        var result = await CreateGrandMa3(exportRoot, new FixedTimeProvider(now)).DiscoverAsync(
            new DiscoveryRequest(exportRoot),
            CancellationToken.None);

        Assert.Equal(GrandMa3ShowDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Equal(now, result.CompletedAt);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("shared", "shows", "Festival.show") &&
                file.Sha256 == Convert.ToHexStringLower(SHA256.HashData(show)));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine(
                "gma3_library", "fixturetypes", "Custom.xml"));
    }

    [Fact]
    public async Task GrandMa2_discovers_version_specific_show_and_exports()
    {
        var exportRoot = Path.Combine(_root, "gma2");
        Directory.CreateDirectory(Path.Combine(exportRoot, "gma2_3.9.60", "shows"));
        Directory.CreateDirectory(Path.Combine(exportRoot, "importexport"));
        await File.WriteAllTextAsync(
            Path.Combine(exportRoot, "gma2_3.9.60", "shows", "Venue.show.gz"),
            "grandMA2 show");
        await File.WriteAllTextAsync(
            Path.Combine(exportRoot, "importexport", "macros.xml"),
            "macros");

        var result = await CreateGrandMa2(exportRoot).DiscoverAsync(
            new DiscoveryRequest(exportRoot),
            CancellationToken.None);

        Assert.Equal(GrandMa2ShowDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine(
                "gma2_3.9.60", "shows", "Venue.show.gz"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("importexport", "macros.xml"));
    }

    [Fact]
    public async Task GrandMa_plugins_reject_wrong_product_structure()
    {
        var exportRoot = Path.Combine(_root, "grandMA3");
        Directory.CreateDirectory(Path.Combine(exportRoot, "shared", "shows"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateGrandMa2(exportRoot).DiscoverAsync(
                new DiscoveryRequest(exportRoot),
                CancellationToken.None));
    }

    [Fact]
    public async Task GrandMa3_rejects_child_of_exact_export_root()
    {
        var exportRoot = Path.Combine(_root, "grandMA3");
        var shows = Path.Combine(exportRoot, "shared", "shows");
        Directory.CreateDirectory(shows);
        var plugin = CreateGrandMa3(exportRoot, TimeProvider.System);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            plugin.DiscoverAsync(new DiscoveryRequest(shows), CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private GrandMa2ShowDiscoveryPlugin CreateGrandMa2(string exportRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                GrandMa2ExportRoots = [exportRoot]
            }),
            TimeProvider.System);

    private GrandMa3ShowDiscoveryPlugin CreateGrandMa3(
        string exportRoot,
        TimeProvider timeProvider) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                GrandMa3ExportRoots = [exportRoot]
            }),
            timeProvider);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
