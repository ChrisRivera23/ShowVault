using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class LawoMc2DiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-lawo-mc2-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Venue.lpn")]
    [InlineData("Venue.LPN")]
    public async Task Recognizes_production_and_preserves_user_data_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "presets"));
        Directory.CreateDirectory(Path.Combine(_root, "waves-integrated-sessions"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "mc2 production");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "presets", "Festival.lsf"),
            "snapshot folder");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "presets", "Vocal.pch"),
            "channel preset");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "waves-integrated-sessions", "Venue.session"),
            "Waves session");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "restore-prerequisites.md"),
            "MCX/mxGUI build, hardware, DSP, I/O, HOME topology and sample rate.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(LawoMc2DiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("presets", "Festival.lsf"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("presets", "Vocal.pch"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine(
                "waves-integrated-sessions", "Venue.session"));
    }

    [Fact]
    public async Task Rejects_presets_without_complete_production()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Festival.lsf"), "snapshot folder");
        await File.WriteAllTextAsync(Path.Combine(_root, "Vocal.pch"), "channel preset");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_production_root()
    {
        var child = Path.Combine(_root, "productions");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.lpn"), "production");

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

    private static LawoMc2DiscoveryPlugin CreatePlugin(string productionRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                LawoMc2ProductionRoots = [productionRoot]
            }),
            TimeProvider.System);
}
