using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class MartinAudioVuNetDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "showvault-martin-audio-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Festival.vun")]
    [InlineData("Festival.VUN")]
    public async Task Recognizes_project_and_preserves_snapshots_presets_and_documentation(string name)
    {
        Directory.CreateDirectory(Path.Combine(_root, "snapshots"));
        Directory.CreateDirectory(Path.Combine(_root, "presets"));
        await File.WriteAllTextAsync(Path.Combine(_root, name), "Vu-Net project");
        await File.WriteAllTextAsync(Path.Combine(_root, "Festival revision.vun"), "project revision");
        await File.WriteAllTextAsync(Path.Combine(_root, "snapshots", "soundcheck.snapshot"), "snapshot");
        await File.WriteAllTextAsync(Path.Combine(_root, "presets", "array.preset"), "preset");
        await File.WriteAllTextAsync(Path.Combine(_root, "restore-notes.pdf"), "notes");

        var result = await CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None);

        Assert.Equal(MartinAudioVuNetDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == name);
        Assert.Contains(result.Files, file => file.RelativePath == "Festival revision.vun");
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("snapshots", "soundcheck.snapshot"));
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("presets", "array.preset"));
        Assert.Contains(result.Files, file => file.RelativePath == "restore-notes.pdf");
    }

    [Fact]
    public async Task Rejects_empty_project()
    {
        Directory.CreateDirectory(_root);
        File.Create(Path.Combine(_root, "empty.vun")).Dispose();

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Theory]
    [InlineData("legacy.vup")]
    [InlineData("controller.prj")]
    public async Task Rejects_other_martin_audio_project_families(string name)
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, name), "other project family");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_root()
    {
        var child = Path.Combine(_root, "project");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Festival.vun"), "project");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(child), CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private static MartinAudioVuNetDiscoveryPlugin CreatePlugin(string root) => new(
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            MartinAudioVuNetProjectRoots = [root]
        }),
        TimeProvider.System);
}
