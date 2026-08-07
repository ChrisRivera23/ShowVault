using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class DynacordSonicueDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "showvault-dynacord-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Venue.snc")]
    [InlineData("Venue.SNC")]
    public async Task Recognizes_project_and_preserves_recovery_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "speaker-databases"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "SONICUE project");
        await File.WriteAllTextAsync(Path.Combine(_root, "speaker-databases", "venue.sdb"), "speaker database");
        await File.WriteAllTextAsync(Path.Combine(_root, "commissioning-report.pdf"), "report");
        await File.WriteAllTextAsync(Path.Combine(_root, "restore-prerequisites.md"), "SONICUE, firmware, devices, routing, loudspeaker data and Dante state.");

        var result = await CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None);

        Assert.Equal(DynacordSonicueDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("speaker-databases", "venue.sdb"));
        Assert.Contains(result.Files, file => file.RelativePath == "commissioning-report.pdf");
    }

    [Theory]
    [InlineData("speaker.sdb")]
    [InlineData("legacy-project.depz")]
    public async Task Rejects_companion_or_legacy_project_without_sonicue_project(string fileName)
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "not a SONICUE project");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_root()
    {
        var child = Path.Combine(_root, "projects");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.snc"), "SONICUE project");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(child), CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private static DynacordSonicueDiscoveryPlugin CreatePlugin(string root) => new(
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            DynacordSonicueProjectRoots = [root]
        }),
        TimeProvider.System);
}
