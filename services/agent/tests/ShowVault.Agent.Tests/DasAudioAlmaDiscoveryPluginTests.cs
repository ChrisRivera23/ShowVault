using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class DasAudioAlmaDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "showvault-das-audio-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Recognizes_alma_data_root_and_preserves_projects_backups_reports_and_configuration()
    {
        Directory.CreateDirectory(Path.Combine(_root, "prj", "reports"));
        Directory.CreateDirectory(Path.Combine(_root, "backup"));
        Directory.CreateDirectory(Path.Combine(_root, "cfg"));
        await WriteProjectAsync(Path.Combine(_root, "prj", "Festival.prj"));
        await File.WriteAllTextAsync(Path.Combine(_root, "prj", "reports", "health.almahc"), "report");
        await File.WriteAllTextAsync(Path.Combine(_root, "backup", "Festival.prj"), "backup");
        await File.WriteAllTextAsync(Path.Combine(_root, "cfg", "state.cfg"), "configuration");

        var result = await CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None);

        Assert.Equal(DasAudioAlmaDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("prj", "Festival.prj"));
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("prj", "reports", "health.almahc"));
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("backup", "Festival.prj"));
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("cfg", "state.cfg"));
    }

    [Fact]
    public async Task Rejects_generic_or_malformed_prj_files()
    {
        Directory.CreateDirectory(Path.Combine(_root, "prj"));
        await File.WriteAllTextAsync(Path.Combine(_root, "prj", "generic.prj"), "not json");
        await File.WriteAllTextAsync(Path.Combine(_root, "prj", "other.prj"), "{\"name\":\"Other\"}");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_loose_project_without_alma_directory_boundary()
    {
        Directory.CreateDirectory(_root);
        await WriteProjectAsync(Path.Combine(_root, "Festival.prj"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_root()
    {
        var child = Path.Combine(_root, "child");
        Directory.CreateDirectory(Path.Combine(child, "prj"));
        await WriteProjectAsync(Path.Combine(child, "prj", "Festival.prj"));

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

    private static Task WriteProjectAsync(string path) => File.WriteAllTextAsync(path, """
        {
          "name": "Festival",
          "guid": "d3b6334f-f18f-43ed-8963-73b7850ef861",
          "version": "3.1.0",
          "zones": [],
          "snapshots": []
        }
        """);

    private static DasAudioAlmaDiscoveryPlugin CreatePlugin(string root) => new(
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            DasAudioAlmaDataRoots = [root]
        }),
        TimeProvider.System);
}
