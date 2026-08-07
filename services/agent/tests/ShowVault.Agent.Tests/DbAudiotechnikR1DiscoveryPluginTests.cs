using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class DbAudiotechnikR1DiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-db-audiotechnik-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Venue.dbpr")]
    [InlineData("Venue.DBPR")]
    public async Task Recognizes_nonempty_dbpr_project_and_preserves_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "commissioning"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "d&b R1 project data");
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.r1p"), "legacy R1 project data");
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.dbac2"), "legacy ArrayCalc project data");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "commissioning", "amplifier-inventory.csv"),
            "device,firmware,remote-id,address");
        await File.WriteAllTextAsync(Path.Combine(_root, "workspace-background.bmp"), "graphic");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(DbAudiotechnikR1DiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files, file => file.RelativePath == "Venue.r1p");
        Assert.Contains(result.Files, file => file.RelativePath == "Venue.dbac2");
        Assert.Contains(result.Files, file =>
            file.RelativePath == Path.Combine("commissioning", "amplifier-inventory.csv"));
        Assert.Contains(result.Files, file => file.RelativePath == "workspace-background.bmp");
    }

    [Fact]
    public async Task Rejects_empty_dbpr_lookalike()
    {
        Directory.CreateDirectory(_root);
        File.Create(Path.Combine(_root, "empty.dbpr")).Dispose();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_legacy_companions_without_current_project()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.r1p"), "legacy R1 only");
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.dbac2"), "legacy ArrayCalc only");
        await File.WriteAllTextAsync(Path.Combine(_root, "event-log.csv"), "events only");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_root()
    {
        var child = Path.Combine(_root, "projects");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.dbpr"), "d&b project data");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(child), CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private static DbAudiotechnikR1DiscoveryPlugin CreatePlugin(string root) => new(
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            DbAudiotechnikR1ProjectRoots = [root]
        }),
        TimeProvider.System);
}
