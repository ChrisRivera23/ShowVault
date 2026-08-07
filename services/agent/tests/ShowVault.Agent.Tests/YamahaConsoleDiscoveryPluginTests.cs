using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class YamahaConsoleDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-yamaha-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Dm7_discovers_settings_export_and_companion_files()
    {
        var exportRoot = Path.Combine(_root, "DM7 Venue Export");
        Directory.CreateDirectory(Path.Combine(exportRoot, "notes"));
        await File.WriteAllTextAsync(Path.Combine(exportRoot, "VENUE.dm7f"), "settings");
        await File.WriteAllTextAsync(Path.Combine(exportRoot, "notes", "restore.txt"), "notes");
        var now = DateTimeOffset.UtcNow;

        var result = await CreateDm7(exportRoot, new FixedTimeProvider(now)).DiscoverAsync(
            new DiscoveryRequest(exportRoot),
            CancellationToken.None);

        Assert.Equal(YamahaDm7DiscoveryPlugin.PluginId, result.PluginId);
        Assert.Equal(now, result.CompletedAt);
        Assert.Contains(result.Files,
            file => file.RelativePath == "VENUE.dm7f");
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("notes", "restore.txt"));
    }

    [Theory]
    [InlineData("Festival.RIVAGEPM")]
    [InlineData("Festival.PM10ALL")]
    [InlineData("Festival.PM7ALL")]
    [InlineData("Festival.PM10PART")]
    [InlineData("Festival.PM7PART")]
    public async Task Rivage_recognizes_current_and_legacy_settings_formats(string fileName)
    {
        var exportRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(exportRoot);
        await File.WriteAllTextAsync(Path.Combine(exportRoot, fileName), "settings");

        var result = await CreateRivage(exportRoot).DiscoverAsync(
            new DiscoveryRequest(exportRoot),
            CancellationToken.None);

        Assert.Equal(YamahaRivageDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
    }

    [Fact]
    public async Task Dm7_rejects_export_without_dm7_settings_file()
    {
        var exportRoot = Path.Combine(_root, "not-dm7");
        Directory.CreateDirectory(exportRoot);
        await File.WriteAllTextAsync(Path.Combine(exportRoot, "notes.txt"), "notes");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateDm7(exportRoot, TimeProvider.System).DiscoverAsync(
                new DiscoveryRequest(exportRoot),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rivage_rejects_child_of_exact_export_root()
    {
        var exportRoot = Path.Combine(_root, "rivage");
        var child = Path.Combine(exportRoot, "child");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.RIVAGEPM"), "settings");
        var plugin = CreateRivage(exportRoot);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            plugin.DiscoverAsync(new DiscoveryRequest(child), CancellationToken.None));
    }

    [Theory]
    [InlineData("Venue.CLF")]
    [InlineData("Venue.clf")]
    public async Task ClQl_recognizes_shared_settings_format(string fileName)
    {
        var exportRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(exportRoot);
        await File.WriteAllTextAsync(Path.Combine(exportRoot, fileName), "settings");

        var result = await CreateClQl(exportRoot).DiscoverAsync(
            new DiscoveryRequest(exportRoot),
            CancellationToken.None);

        Assert.Equal(YamahaClQlDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
    }

    [Fact]
    public async Task Tf_recognizes_console_data_and_preset_companions()
    {
        var exportRoot = Path.Combine(_root, "tf-export");
        Directory.CreateDirectory(Path.Combine(exportRoot, "presets"));
        await File.WriteAllTextAsync(Path.Combine(exportRoot, "Venue.tff"), "settings");
        await File.WriteAllTextAsync(
            Path.Combine(exportRoot, "presets", "Vocal.tfp"),
            "preset");

        var result = await CreateTf(exportRoot).DiscoverAsync(
            new DiscoveryRequest(exportRoot),
            CancellationToken.None);

        Assert.Equal(YamahaTfDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == "Venue.tff");
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("presets", "Vocal.tfp"));
    }

    [Fact]
    public async Task Tf_rejects_clql_settings_export()
    {
        var exportRoot = Path.Combine(_root, "wrong-family");
        Directory.CreateDirectory(exportRoot);
        await File.WriteAllTextAsync(Path.Combine(exportRoot, "Venue.CLF"), "settings");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateTf(exportRoot).DiscoverAsync(
                new DiscoveryRequest(exportRoot),
                CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private YamahaDm7DiscoveryPlugin CreateDm7(
        string exportRoot,
        TimeProvider timeProvider) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaDm7ExportRoots = [exportRoot]
            }),
            timeProvider);

    private YamahaRivageDiscoveryPlugin CreateRivage(string exportRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaRivageExportRoots = [exportRoot]
            }),
            TimeProvider.System);

    private YamahaClQlDiscoveryPlugin CreateClQl(string exportRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaClQlExportRoots = [exportRoot]
            }),
            TimeProvider.System);

    private YamahaTfDiscoveryPlugin CreateTf(string exportRoot) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaTfExportRoots = [exportRoot]
            }),
            TimeProvider.System);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
