using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class CalrecApolloArtemisDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-calrec-apollo-artemis-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("EveningNews.CalrecShow")]
    [InlineData("EveningNews.CALRECSHOW")]
    public async Task Recognizes_native_show_and_preserves_recovery_companions(
        string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "revisions"));
        Directory.CreateDirectory(Path.Combine(_root, "legacy-backup"));
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "Calrec show");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "revisions", "EveningNews-rehearsal.CalrecShow"),
            "earlier show");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "legacy-backup", "migration-notes.md"),
            "Legacy folder-based backup retained for supervised import.");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "restore-prerequisites.md"),
            "Apollo or Artemis model, software version, sample rate and system topology.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(CalrecApolloArtemisDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine(
                "revisions", "EveningNews-rehearsal.CalrecShow"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine(
                "legacy-backup", "migration-notes.md"));
    }

    [Fact]
    public async Task Rejects_notes_without_native_show_export()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "restore-notes.md"), "notes");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_show_root()
    {
        var child = Path.Combine(_root, "shows");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(
            Path.Combine(child, "EveningNews.CalrecShow"),
            "show");

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

    private static CalrecApolloArtemisDiscoveryPlugin CreatePlugin(string showRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                CalrecApolloArtemisShowRoots = [showRoot]
            }),
            TimeProvider.System);
}
