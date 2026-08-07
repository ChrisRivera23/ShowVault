using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class LabGruppenLakeDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "showvault-lab-gruppen-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Festival.csc")]
    [InlineData("Festival.CSC")]
    public async Task Recognizes_system_configuration_and_preserves_modules(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "modules"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "Lake system configuration");
        await File.WriteAllTextAsync(Path.Combine(_root, "modules", "Main-Array.csm"), "Contour module");
        await File.WriteAllTextAsync(Path.Combine(_root, "modules", "Fills.msm"), "Mesa module");
        await File.WriteAllTextAsync(Path.Combine(_root, "modules", "Manufacturer.cbm"), "base configuration");
        await File.WriteAllTextAsync(Path.Combine(_root, "restore-prerequisites.md"), "Lake Controller, firmware, frames, modules, groups, routing and Dante state.");
        var result = await CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None);
        Assert.Equal(LabGruppenLakeDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("modules", "Main-Array.csm"));
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("modules", "Fills.msm"));
    }

    [Theory]
    [InlineData("Module.csm")]
    [InlineData("Base.cbm")]
    public async Task Rejects_module_without_system_configuration(string fileName)
    {
        Directory.CreateDirectory(_root); await File.WriteAllTextAsync(Path.Combine(_root, fileName), "module");
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_root()
    {
        var child = Path.Combine(_root, "systems"); Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.csc"), "system");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(child), CancellationToken.None));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private static LabGruppenLakeDiscoveryPlugin CreatePlugin(string root) => new(
        Options.Create(new AgentOptions { ControlPlaneUri = new Uri("https://control.test"), Name = "Test Agent", LabGruppenLakeSystemRoots = [root] }), TimeProvider.System);
}
