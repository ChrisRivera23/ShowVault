using System.IO.Compression;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class ElectroVoiceIrisNetDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-electro-voice-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Venue.ds")]
    [InlineData("Venue.DS")]
    public async Task Recognizes_iris_net_archive_and_preserves_companions(string fileName)
    {
        Directory.CreateDirectory(Path.Combine(_root, "commissioning"));
        CreateProjectArchive(Path.Combine(_root, fileName));
        await File.WriteAllTextAsync(
            Path.Combine(_root, "commissioning", "device-inventory.csv"),
            "device,firmware,address");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "restore-prerequisites.md"),
            "IRIS-Net version, device models, firmware, addressing, network audio, presets and supervision state.");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(ElectroVoiceIrisNetDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Contains(result.Files, file => file.RelativePath == fileName);
        Assert.Contains(result.Files, file =>
            file.RelativePath == Path.Combine("commissioning", "device-inventory.csv"));
        Assert.Contains(result.Files, file => file.RelativePath == "restore-prerequisites.md");
    }

    [Fact]
    public async Task Rejects_ds_file_that_is_not_an_iris_net_project_archive()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "lookalike.ds"), "not a ZIP archive");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_archive_without_nonempty_main_project_entry()
    {
        Directory.CreateDirectory(_root);
        using (var archive = ZipFile.Open(Path.Combine(_root, "incomplete.ds"), ZipArchiveMode.Create))
        {
            archive.CreateEntry("main.ds");
            WriteEntry(archive, "Bitmaps\\system.png", "image");
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_loose_inner_project_files()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Scripts"));
        await File.WriteAllTextAsync(Path.Combine(_root, "main.ds"), "inner project data");
        await File.WriteAllTextAsync(Path.Combine(_root, "Scripts", "venue.scn"), "script");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_child_of_exact_root()
    {
        var child = Path.Combine(_root, "projects");
        Directory.CreateDirectory(child);
        CreateProjectArchive(Path.Combine(child, "Venue.ds"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(child), CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private static void CreateProjectArchive(string path)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "main.ds", "IRIS-Net project");
        WriteEntry(archive, "mainp.ds", "project metadata");
        WriteEntry(archive, "DX46_1 Configuration Panel (Electro Voice Dx46).ds", "device panel");
        WriteEntry(archive, "Bitmaps\\venue.png", "image");
        WriteEntry(archive, "Scripts\\venue.scn", "script");
        WriteEntry(archive, "User Controls\\Level Panel.ds", "user control");
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private static ElectroVoiceIrisNetDiscoveryPlugin CreatePlugin(string root) => new(
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            ElectroVoiceIrisNetProjectRoots = [root]
        }),
        TimeProvider.System);
}
