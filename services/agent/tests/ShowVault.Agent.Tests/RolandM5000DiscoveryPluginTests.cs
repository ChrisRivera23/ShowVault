using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class RolandM5000DiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-roland-m5000-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Festival.m5pj")]
    [InlineData("Festival.M5PJ")]
    public async Task Recognizes_project_and_preserves_recovery_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "revisions"));
        Directory.CreateDirectory(Path.Combine(_root, "documentation"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "M-5000 project");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "revisions", "Festival-rehearsal.m5pj"),
            "earlier project");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "documentation", "system-topology.md"),
            "REAC, expansion slots, word clock, remotes and network topology.");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "restore-prerequisites.md"),
            "M-5000 or M-5000C firmware and matching M-5000 RCS version.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(RolandM5000DiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine(
                "revisions", "Festival-rehearsal.m5pj"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine(
                "documentation", "system-topology.md"));
    }

    [Fact]
    public async Task Rejects_audio_recordings_without_console_project()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "recording.wav"), "audio");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_firmware_update_as_project()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "M-5000.PRG"), "firmware");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_project_root()
    {
        var child = Path.Combine(_root, "PROJ");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Festival.m5pj"), "project");

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

    private static RolandM5000DiscoveryPlugin CreatePlugin(string projectRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                RolandM5000ProjectRoots = [projectRoot]
            }),
            TimeProvider.System);
}
