using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class CrestronSimplDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-crestron-simpl-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Venue.smw")]
    [InlineData("Venue.SMW")]
    public async Task Recognizes_editable_program_and_preserves_project_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "modules"));
        Directory.CreateDirectory(Path.Combine(_root, "ui"));
        Directory.CreateDirectory(Path.Combine(_root, "output"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "SIMPL source");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "modules", "Display.usp"),
            "SIMPL+ module");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "ui", "TouchPanel.vtp"),
            "VT Pro-e source");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "output", "Venue.cpz"),
            "compiled program");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(CrestronSimplDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("modules", "Display.usp"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("ui", "TouchPanel.vtp"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("output", "Venue.cpz"));
    }

    [Fact]
    public async Task Rejects_compiled_artifacts_without_editable_program()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.cpz"), "compiled program");
        await File.WriteAllTextAsync(Path.Combine(_root, "TouchPanel.vtz"), "compiled UI");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_project_root()
    {
        var child = Path.Combine(_root, "source");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.smw"), "SIMPL source");

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

    private static CrestronSimplDiscoveryPlugin CreatePlugin(string projectRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                CrestronSimplProjectRoots = [projectRoot]
            }),
            TimeProvider.System);
}
