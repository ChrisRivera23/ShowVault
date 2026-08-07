using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class DigicoSdQuantumDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-digico-sd-quantum-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("session001.ses")]
    [InlineData("Venue-Quantum338.SES")]
    public async Task Recognizes_session_and_preserves_recovery_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "templates"));
        Directory.CreateDirectory(Path.Combine(_root, "converter-output"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "SD/Quantum session");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "templates", "Festival.ses"),
            "session template");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "converter-output", "Festival-Q225.ses"),
            "converted session");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "compatibility.md"),
            "Console model, software build, sample rate, I/O and extensions.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(DigicoSdQuantumDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("templates", "Festival.ses"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine(
                "converter-output", "Festival-Q225.ses"));
        Assert.Contains(result.Files, file => file.RelativePath == "compatibility.md");
    }

    [Fact]
    public async Task Rejects_presets_and_notes_without_session()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Preset.xml"), "preset");
        await File.WriteAllTextAsync(Path.Combine(_root, "compatibility.md"), "notes");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_session_root()
    {
        var child = Path.Combine(_root, "sessions");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.ses"), "session");

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

    private static DigicoSdQuantumDiscoveryPlugin CreatePlugin(string sessionRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                DigicoSdQuantumSessionRoots = [sessionRoot]
            }),
            TimeProvider.System);
}
