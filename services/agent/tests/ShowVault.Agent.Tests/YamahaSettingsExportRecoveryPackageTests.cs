using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using ShowVault.Agent.Recovery;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class YamahaSettingsExportRecoveryPackageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-yamaha-package-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Dm7_package_records_opaque_format_and_assisted_restore_boundaries()
    {
        var export = await CreateExportAsync("Venue.dm7f");
        var discovery = await CreateDm7(export).DiscoverAsync(
            new DiscoveryRequest(export),
            CancellationToken.None);

        var package = await CreateWriter(dm7Roots: [export]).CreateAsync(
            Guid.NewGuid(), Guid.NewGuid(), discovery, DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Equal(YamahaDm7SettingsExportDiscoveryPlugin.PluginId, package.Manifest.Source.PluginId);
        Assert.Equal("1.0.0", package.Manifest.Source.PluginVersion);
        Assert.Null(package.Manifest.Source.ProductVersion);
        Assert.Null(package.Manifest.Source.FirmwareVersion);
        Assert.Contains(package.Manifest.RestorePrerequisites,
            value => value.Contains("lower all outputs", StringComparison.Ordinal));
        Assert.Contains(package.Manifest.CompatibilityRules,
            rule => rule.Kind == "opaque-settings-format" &&
                rule.Requirement.Contains(".dm7f", StringComparison.Ordinal));
        Assert.Contains(package.Manifest.CompatibilityRules,
            rule => rule.Kind == "operator-confirmation-required");
        Assert.Contains(package.Manifest.CompatibilityRules,
            rule => rule.Kind == "dependency-closure");
    }

    [Fact]
    public async Task Rivage_partial_export_is_labeled_without_completeness_claim()
    {
        var export = await CreateExportAsync("Venue.PM10PART");
        var discovery = await CreateRivage(export).DiscoverAsync(
            new DiscoveryRequest(export),
            CancellationToken.None);

        var package = await CreateWriter(rivageRoots: [export]).CreateAsync(
            Guid.NewGuid(), Guid.NewGuid(), discovery, DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Contains(package.Manifest.CompatibilityRules,
            rule => rule.Kind == "opaque-settings-format" &&
                rule.Requirement.Contains(".PM10PART", StringComparison.Ordinal));
        Assert.Contains(package.Manifest.CompatibilityRules,
            rule => rule.Kind == "operator-confirmation-required" &&
                rule.Requirement.Contains("export completeness", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Package_rejects_late_file_without_publishing_stale_package()
    {
        var export = await CreateExportAsync("Venue.dm7f");
        var discovery = await CreateDm7(export).DiscoverAsync(
            new DiscoveryRequest(export), CancellationToken.None);
        var writer = new RecoveryPackageWriter(
            CreateOptions(dm7Roots: [export]),
            new CallbackSourceSnapshotRaceProbe((point, _) =>
            {
                if (point == SourceSnapshotRacePoint.SnapshotCaptured)
                {
                    File.WriteAllText(Path.Combine(export, "Late.txt"), "late");
                }
            }));

        await Assert.ThrowsAnyAsync<IOException>(() => writer.CreateAsync(
            Guid.NewGuid(), Guid.NewGuid(), discovery, DateTimeOffset.UtcNow,
            CancellationToken.None));

        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(_root, "packages")));
    }

    [Fact]
    public async Task Existing_package_is_not_reused_after_source_topology_changes()
    {
        var export = await CreateExportAsync("Venue.dm7f");
        var discovery = await CreateDm7(export).DiscoverAsync(
            new DiscoveryRequest(export), CancellationToken.None);
        var writer = CreateWriter(dm7Roots: [export]);
        var agentId = Guid.NewGuid();
        var discoveryId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var package = await writer.CreateAsync(
            agentId, discoveryId, discovery, createdAt, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(export, "Late.txt"), "late");

        await Assert.ThrowsAnyAsync<Exception>(() => writer.CreateAsync(
            agentId, discoveryId, discovery, createdAt, CancellationToken.None));

        Assert.True(Directory.Exists(package.PackagePath));
        Assert.False(File.Exists(Path.Combine(
            package.PackagePath,
            RecoveryPackageFormat.ContentDirectoryName,
            "Late.txt")));
    }

    [Fact]
    public async Task Package_rejects_root_identity_swap()
    {
        var export = await CreateExportAsync("Venue.dm7f");
        var discovery = await CreateDm7(export).DiscoverAsync(
            new DiscoveryRequest(export), CancellationToken.None);
        var original = $"{export}-original";
        var writer = new RecoveryPackageWriter(
            CreateOptions(dm7Roots: [export]),
            new CallbackSourceSnapshotRaceProbe((point, _) =>
            {
                if (point != SourceSnapshotRacePoint.SnapshotCaptured)
                {
                    return;
                }

                Directory.Move(export, original);
                Directory.CreateDirectory(export);
                File.WriteAllText(Path.Combine(export, "Venue.dm7f"), "opaque-settings");
            }));

        await Assert.ThrowsAnyAsync<IOException>(() => writer.CreateAsync(
            Guid.NewGuid(), Guid.NewGuid(), discovery, DateTimeOffset.UtcNow,
            CancellationToken.None));
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("renamed")]
    [InlineData("replaced")]
    [InlineData("resized")]
    [InlineData("rehashed")]
    public async Task Package_rejects_every_late_discovered_file_mutation(string mutation)
    {
        var export = await CreateExportAsync("Venue.dm7f");
        var settingsPath = Path.Combine(export, "Venue.dm7f");
        var discovery = await CreateDm7(export).DiscoverAsync(
            new DiscoveryRequest(export), CancellationToken.None);
        var writer = new RecoveryPackageWriter(
            CreateOptions(dm7Roots: [export]),
            new CallbackSourceSnapshotRaceProbe((point, _) =>
            {
                if (point != SourceSnapshotRacePoint.SnapshotCaptured)
                {
                    return;
                }

                switch (mutation)
                {
                    case "removed":
                        File.Delete(settingsPath);
                        break;
                    case "renamed":
                        File.Move(settingsPath, Path.Combine(export, "Renamed.dm7f"));
                        break;
                    case "replaced":
                        var replacement = Path.Combine(export, "replacement.tmp");
                        File.WriteAllText(replacement, "opaque-settings");
                        File.Move(replacement, settingsPath, overwrite: true);
                        break;
                    case "resized":
                        File.AppendAllText(settingsPath, "-larger");
                        break;
                    case "rehashed":
                        File.WriteAllText(settingsPath, "changed-content");
                        break;
                }
            }));

        await Assert.ThrowsAnyAsync<Exception>(() => writer.CreateAsync(
            Guid.NewGuid(), Guid.NewGuid(), discovery, DateTimeOffset.UtcNow,
            CancellationToken.None));
    }

    [Fact]
    public async Task Package_rechecks_root_level_recognized_structure()
    {
        var export = await CreateExportAsync("Venue.dm7f");
        var discovery = await CreateDm7(export).DiscoverAsync(
            new DiscoveryRequest(export), CancellationToken.None);
        File.Move(
            Path.Combine(export, "Venue.dm7f"),
            Path.Combine(export, "Venue.bin"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateWriter(dm7Roots: [export]).CreateAsync(
                Guid.NewGuid(), Guid.NewGuid(), discovery, DateTimeOffset.UtcNow,
                CancellationToken.None));
    }

    [Fact]
    public async Task Package_rechecks_local_root_authorization()
    {
        var export = await CreateExportAsync("Venue.dm7f");
        var discovery = await CreateDm7(export).DiscoverAsync(
            new DiscoveryRequest(export), CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateWriter().CreateAsync(
                Guid.NewGuid(), Guid.NewGuid(), discovery, DateTimeOffset.UtcNow,
                CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<string> CreateExportAsync(string fileName)
    {
        var export = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(export);
        await File.WriteAllTextAsync(Path.Combine(export, fileName), "opaque-settings");
        return export;
    }

    private YamahaDm7SettingsExportDiscoveryPlugin CreateDm7(string root) =>
        new(CreateOptions(dm7Roots: [root]), TimeProvider.System);

    private YamahaRivageSettingsExportDiscoveryPlugin CreateRivage(string root) =>
        new(CreateOptions(rivageRoots: [root]), TimeProvider.System);

    private RecoveryPackageWriter CreateWriter(
        IReadOnlyList<string>? dm7Roots = null,
        IReadOnlyList<string>? rivageRoots = null) =>
        new(CreateOptions(dm7Roots, rivageRoots));

    private IOptions<AgentOptions> CreateOptions(
        IReadOnlyList<string>? dm7Roots = null,
        IReadOnlyList<string>? rivageRoots = null) =>
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            PackageDirectory = Path.Combine(_root, "packages"),
            YamahaDm7SettingsExportRoots = dm7Roots ?? [],
            YamahaRivageSettingsExportRoots = rivageRoots ?? []
        });

    private sealed class CallbackSourceSnapshotRaceProbe(
        Action<SourceSnapshotRacePoint, string> callback) : ISourceSnapshotRaceProbe
    {
        public void Reached(SourceSnapshotRacePoint point, string relativePath) =>
            callback(point, relativePath);
    }
}
