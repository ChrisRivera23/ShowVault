using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class BoseControlSpaceDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "showvault-bose-controlspace-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Convention-Center.csp")]
    [InlineData("Convention-Center.CSP")]
    public async Task Recognizes_project_and_preserves_recovery_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "revisions"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "ControlSpace project");
        await File.WriteAllTextAsync(Path.Combine(_root, "Lobby.cpf"), "control panel");
        await File.WriteAllTextAsync(Path.Combine(_root, "Mobile-Controls.cpz"), "remote package");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "revisions", "retrieved-design.cab"), "retrieved archive");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "restore-prerequisites.md"),
            "Designer and firmware versions, hardware models, IP assignments and Dante state.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root), CancellationToken.None);

        Assert.Equal(BoseControlSpaceDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files, file => file.RelativePath == "Lobby.cpf");
        Assert.Contains(result.Files, file => file.RelativePath == "Mobile-Controls.cpz");
        Assert.Contains(result.Files, file => file.RelativePath ==
            Path.Combine("revisions", "retrieved-design.cab"));
        Assert.Contains(result.Files, file => file.RelativePath == "restore-prerequisites.md");
    }

    [Theory]
    [InlineData("Controls.cpf")]
    [InlineData("Mobile-Controls.cpz")]
    public async Task Rejects_companion_without_native_project(string fileName)
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "companion");
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_project_root()
    {
        var child = Path.Combine(_root, "projects");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.csp"), "project");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreatePlugin(_root)
            .DiscoverAsync(new DiscoveryRequest(child), CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static BoseControlSpaceDiscoveryPlugin CreatePlugin(string root) => new(
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            BoseControlSpaceProjectRoots = [root]
        }),
        TimeProvider.System);
}
