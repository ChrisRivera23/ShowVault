using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class BlackmagicAtemDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-blackmagic-atem-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Venue.xml")]
    [InlineData("Venue-2026-08-07-193000.XML")]
    public async Task Recognizes_atem_profile_and_preserves_media_pool(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "Venue", "MediaPool"));
        await File.WriteAllTextAsync(
            Path.Combine(_root, fileName),
            "<?xml version=\"1.0\"?><Profile majorVersion=\"1\" minorVersion=\"3\" " +
            "product=\"ATEM Television Studio 4K8\"><MacroPool /></Profile>");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "Venue", "MediaPool", "Still1.png"),
            "graphic");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "restore-notes.md"),
            "Verify switcher model and select restore blocks before applying.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(BlackmagicAtemDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("Venue", "MediaPool", "Still1.png"));
        Assert.Contains(result.Files, file => file.RelativePath == "restore-notes.md");
    }

    [Fact]
    public async Task Rejects_unrelated_xml_and_deployment_artifacts()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(
            Path.Combine(_root, "Streaming.xml"),
            "<?xml version=\"1.0\"?><streaming><service /></streaming>");
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.drp"), "Resolve project");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_profile_with_document_type_declaration()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(
            Path.Combine(_root, "Venue.xml"),
            "<!DOCTYPE Profile [<!ENTITY file SYSTEM \"file:///etc/passwd\">]>" +
            "<Profile product=\"ATEM Mini Extreme\">&file;</Profile>");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_state_root()
    {
        var child = Path.Combine(_root, "autosaves");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(
            Path.Combine(child, "Venue.xml"),
            "<Profile product=\"ATEM Mini Extreme\" />");

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

    private static BlackmagicAtemDiscoveryPlugin CreatePlugin(string stateRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                BlackmagicAtemStateRoots = [stateRoot]
            }),
            TimeProvider.System);
}
