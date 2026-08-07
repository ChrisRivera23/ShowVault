using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class AshlyProteaNeDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "showvault-ashly-protea-ne-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Auditorium.cpj")]
    [InlineData("Auditorium.CPJ")]
    public async Task Recognizes_canvas_project_and_preserves_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "presets"));
        Directory.CreateDirectory(Path.Combine(_root, "filters"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "Protea NE canvas project");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "presets", "System.pre"), "all device presets");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "presets", "Lobby.pne"), "single device preset");
        await File.WriteAllTextAsync(Path.Combine(_root, "filters", "Array.fir"), "FIR coefficients");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "restore-prerequisites.md"),
            "Protea NE version, exact device models, firmware, options and network settings.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root), CancellationToken.None);

        Assert.Equal(AshlyProteaNeDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("presets", "System.pre"));
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("presets", "Lobby.pne"));
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("filters", "Array.fir"));
        Assert.Contains(result.Files, file => file.RelativePath == "restore-prerequisites.md");
    }

    [Theory]
    [InlineData("System.pre")]
    [InlineData("Lobby.pne")]
    [InlineData("Legacy.pmc")]
    public async Task Rejects_preset_or_legacy_artifact_without_canvas_project(string fileName)
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "not a canvas project");
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_project_root()
    {
        var child = Path.Combine(_root, "projects");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.cpj"), "project");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(child), CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static AshlyProteaNeDiscoveryPlugin CreatePlugin(string root) => new(
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            AshlyProteaNeProjectRoots = [root]
        }),
        TimeProvider.System);
}
