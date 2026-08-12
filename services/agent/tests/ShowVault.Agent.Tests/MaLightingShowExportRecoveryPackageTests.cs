using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using ShowVault.Agent.Recovery;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class MaLightingShowExportRecoveryPackageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-grandma-package-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GrandMa2_package_records_profile_version_and_assisted_restore_rules()
    {
        var shows = Path.Combine(_root, "gma2", "3.9", "shows");
        Directory.CreateDirectory(shows);
        await File.WriteAllTextAsync(Path.Combine(shows, "Venue.show"), "show");
        var discovery = await CreateGrandMa2(shows).DiscoverAsync(
            new DiscoveryRequest(shows),
            CancellationToken.None);

        var package = await CreateWriter(grandMa2Roots: [shows]).CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            discovery,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Equal(GrandMa2ShowExportDiscoveryPlugin.PluginId, package.Manifest.Source.PluginId);
        Assert.Equal("1.0.0", package.Manifest.Source.PluginVersion);
        Assert.Equal("3.9", package.Manifest.Source.ProductVersion);
        Assert.Equal(2, package.Manifest.RestorePrerequisites.Count);
        Assert.Contains(
            package.Manifest.CompatibilityRules,
            rule => rule.Kind == "vendor-forward-only-show-file");
        Assert.True(File.Exists(Path.Combine(
            package.PackagePath,
            RecoveryPackageFormat.ContentDirectoryName,
            "Venue.show")));
    }

    [Fact]
    public async Task GrandMa3_package_records_unknown_version_honestly()
    {
        var shows = Path.Combine(_root, "grandMA3", "shared", "shows");
        Directory.CreateDirectory(shows);
        await File.WriteAllTextAsync(Path.Combine(shows, "Venue.show"), "show");
        var discovery = await CreateGrandMa3(shows).DiscoverAsync(
            new DiscoveryRequest(shows),
            CancellationToken.None);

        var package = await CreateWriter(grandMa3Roots: [shows]).CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            discovery,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Null(package.Manifest.Source.ProductVersion);
        Assert.Contains(
            package.Manifest.CompatibilityRules,
            rule => rule.Kind == "source-version-evidence" &&
                rule.Requirement.Contains("does not encode", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Package_rejects_late_file_in_authorized_export()
    {
        var shows = Path.Combine(_root, "grandMA3", "shared", "shows");
        Directory.CreateDirectory(shows);
        await File.WriteAllTextAsync(Path.Combine(shows, "Venue.show"), "show");
        var discovery = await CreateGrandMa3(shows).DiscoverAsync(
            new DiscoveryRequest(shows),
            CancellationToken.None);
        var writer = new RecoveryPackageWriter(
            CreateOptions(grandMa3Roots: [shows]),
            new CallbackSourceSnapshotRaceProbe((point, _) =>
            {
                if (point == SourceSnapshotRacePoint.SnapshotCaptured)
                {
                    File.WriteAllText(Path.Combine(shows, "Late.show"), "late");
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
    public async Task Package_rejects_root_identity_swap()
    {
        var shows = Path.Combine(_root, "grandMA3", "shared", "shows");
        Directory.CreateDirectory(shows);
        await File.WriteAllTextAsync(Path.Combine(shows, "Venue.show"), "show");
        var discovery = await CreateGrandMa3(shows).DiscoverAsync(
            new DiscoveryRequest(shows),
            CancellationToken.None);
        var original = $"{shows}-original";
        var writer = new RecoveryPackageWriter(
            CreateOptions(grandMa3Roots: [shows]),
            new CallbackSourceSnapshotRaceProbe((point, _) =>
            {
                if (point != SourceSnapshotRacePoint.SnapshotCaptured)
                {
                    return;
                }

                Directory.Move(shows, original);
                Directory.CreateDirectory(shows);
                File.WriteAllText(Path.Combine(shows, "Venue.show"), "show");
            }));

        await Assert.ThrowsAnyAsync<IOException>(() => writer.CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            discovery,
            DateTimeOffset.UtcNow,
            CancellationToken.None));
    }

    [Fact]
    public async Task Package_rechecks_local_root_authorization()
    {
        var shows = Path.Combine(_root, "gma2", "3.9", "shows");
        Directory.CreateDirectory(shows);
        await File.WriteAllTextAsync(Path.Combine(shows, "Venue.show"), "show");
        var discovery = await CreateGrandMa2(shows).DiscoverAsync(
            new DiscoveryRequest(shows),
            CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateWriter().CreateAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                discovery,
                DateTimeOffset.UtcNow,
                CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private GrandMa2ShowExportDiscoveryPlugin CreateGrandMa2(string root) =>
        new(CreateOptions(grandMa2Roots: [root]), TimeProvider.System);

    private GrandMa3ShowExportDiscoveryPlugin CreateGrandMa3(string root) =>
        new(CreateOptions(grandMa3Roots: [root]), TimeProvider.System);

    private RecoveryPackageWriter CreateWriter(
        IReadOnlyList<string>? grandMa2Roots = null,
        IReadOnlyList<string>? grandMa3Roots = null) =>
        new(CreateOptions(grandMa2Roots, grandMa3Roots));

    private IOptions<AgentOptions> CreateOptions(
        IReadOnlyList<string>? grandMa2Roots = null,
        IReadOnlyList<string>? grandMa3Roots = null) =>
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            PackageDirectory = Path.Combine(_root, "packages"),
            GrandMa2ShowExportRoots = grandMa2Roots ?? [],
            GrandMa3ShowExportRoots = grandMa3Roots ?? []
        });

    private sealed class CallbackSourceSnapshotRaceProbe(
        Action<SourceSnapshotRacePoint, string> callback) : ISourceSnapshotRaceProbe
    {
        public void Reached(SourceSnapshotRacePoint point, string relativePath) =>
            callback(point, relativePath);
    }
}
