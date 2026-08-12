using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using ShowVault.Agent.Recovery;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class ResolumeUserDataRecoveryPackageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-resolume-user-data-package-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Package_preserves_distinct_profile_and_selected_content_only()
    {
        var source = Path.Combine(_root, "source");
        Directory.CreateDirectory(Path.Combine(source, "Compositions"));
        Directory.CreateDirectory(Path.Combine(source, "Private Notes"));
        await File.WriteAllTextAsync(
            Path.Combine(source, "Compositions", "Venue.avc"),
            "composition");
        await File.WriteAllTextAsync(
            Path.Combine(source, "Private Notes", "notes.txt"),
            "private");
        var discovery = await CreatePlugin(source).DiscoverAsync(
            new DiscoveryRequest(source),
            CancellationToken.None);

        var package = await CreateWriter().CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            discovery,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Equal(
            ResolumeUserDataDiscoveryPlugin.PluginId,
            package.Manifest.Source.PluginId);
        Assert.Single(package.Manifest.Files);
        Assert.True(File.Exists(Path.Combine(
            package.PackagePath,
            RecoveryPackageFormat.ContentDirectoryName,
            "Compositions",
            "Venue.avc")));
        Assert.False(Directory.Exists(Path.Combine(
            package.PackagePath,
            RecoveryPackageFormat.ContentDirectoryName,
            "Private Notes")));
    }

    [Fact]
    public async Task Package_rejects_late_file_in_selected_category()
    {
        var source = Path.Combine(_root, "late-selected");
        Directory.CreateDirectory(Path.Combine(source, "Preferences"));
        await File.WriteAllTextAsync(
            Path.Combine(source, "Preferences", "Arena.xml"),
            "preferences");
        var discovery = await CreatePlugin(source).DiscoverAsync(
            new DiscoveryRequest(source),
            CancellationToken.None);
        var writer = new RecoveryPackageWriter(
            CreateOptions(),
            new CallbackSourceSnapshotRaceProbe((point, _) =>
            {
                if (point == SourceSnapshotRacePoint.SnapshotCaptured)
                {
                    File.WriteAllText(
                        Path.Combine(source, "Preferences", "late.xml"),
                        "late");
                }
            }));

        await Assert.ThrowsAnyAsync<IOException>(() => writer.CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            discovery,
            DateTimeOffset.UtcNow,
            CancellationToken.None));

        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(_root, "packages")));
    }

    [Fact]
    public async Task Package_ignores_late_unknown_sibling()
    {
        var source = Path.Combine(_root, "late-unknown");
        Directory.CreateDirectory(Path.Combine(source, "Shortcuts"));
        await File.WriteAllTextAsync(
            Path.Combine(source, "Shortcuts", "OSC.xml"),
            "shortcuts");
        var discovery = await CreatePlugin(source).DiscoverAsync(
            new DiscoveryRequest(source),
            CancellationToken.None);
        var writer = new RecoveryPackageWriter(
            CreateOptions(),
            new CallbackSourceSnapshotRaceProbe((point, _) =>
            {
                if (point == SourceSnapshotRacePoint.SnapshotCaptured)
                {
                    Directory.CreateDirectory(Path.Combine(source, "Private Notes"));
                    File.WriteAllText(
                        Path.Combine(source, "Private Notes", "notes.txt"),
                        "private");
                }
            }));

        var package = await writer.CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            discovery,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Single(package.Manifest.Files);
        Assert.DoesNotContain(
            package.Manifest.Files,
            file => file.RelativePath.Contains("Private", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Package_rejects_late_empty_selected_category()
    {
        var source = Path.Combine(_root, "late-empty-selected");
        Directory.CreateDirectory(Path.Combine(source, "Shortcuts"));
        await File.WriteAllTextAsync(
            Path.Combine(source, "Shortcuts", "OSC.xml"),
            "shortcuts");
        var discovery = await CreatePlugin(source).DiscoverAsync(
            new DiscoveryRequest(source),
            CancellationToken.None);
        var writer = new RecoveryPackageWriter(
            CreateOptions(),
            new CallbackSourceSnapshotRaceProbe((point, _) =>
            {
                if (point == SourceSnapshotRacePoint.SnapshotCaptured)
                {
                    Directory.CreateDirectory(Path.Combine(source, "Presets"));
                }
            }));

        await Assert.ThrowsAnyAsync<IOException>(() => writer.CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            discovery,
            DateTimeOffset.UtcNow,
            CancellationToken.None));

        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(_root, "packages")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ResolumeUserDataDiscoveryPlugin CreatePlugin(string source) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                ResolumeUserDataRoots = [source]
            }),
            TimeProvider.System);

    private RecoveryPackageWriter CreateWriter() => new(CreateOptions());

    private IOptions<AgentOptions> CreateOptions() => Options.Create(new AgentOptions
    {
        ControlPlaneUri = new Uri("https://control.test"),
        Name = "Test Agent",
        PackageDirectory = Path.Combine(_root, "packages")
    });

    private sealed class CallbackSourceSnapshotRaceProbe(
        Action<SourceSnapshotRacePoint, string> callback) : ISourceSnapshotRaceProbe
    {
        public void Reached(SourceSnapshotRacePoint point, string relativePath) =>
            callback(point, relativePath);
    }
}
