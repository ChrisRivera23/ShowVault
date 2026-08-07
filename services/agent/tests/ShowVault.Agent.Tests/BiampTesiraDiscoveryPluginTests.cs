using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class BiampTesiraDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "showvault-biamp-tesira-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Ballroom.tmf")]
    [InlineData("Ballroom.TMF")]
    public async Task Recognizes_configuration_and_preserves_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "revisions"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "Tesira configuration");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "revisions", "Ballroom-before-firmware.tmf"), "revision");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "restore-prerequisites.md"),
            "Tesira software/firmware, equipment table, serial assignments and topology.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root), CancellationToken.None);

        Assert.Equal(BiampTesiraDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files, file => file.RelativePath ==
            Path.Combine("revisions", "Ballroom-before-firmware.tmf"));
        Assert.Contains(result.Files, file => file.RelativePath == "restore-prerequisites.md");
    }

    [Fact]
    public async Task Rejects_canvas_file_without_tesira_configuration()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Controls.bcv"), "Canvas project");
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_configuration_root()
    {
        var child = Path.Combine(_root, "designs");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Ballroom.tmf"), "configuration");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(child), CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static BiampTesiraDiscoveryPlugin CreatePlugin(string root) => new(
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            BiampTesiraConfigurationRoots = [root]
        }),
        TimeProvider.System);
}
