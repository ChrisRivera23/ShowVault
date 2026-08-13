using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using ShowVault.Agent.Recovery;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class YamahaProVisionaireControlRecoveryPackageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-yamaha-control-package-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Package_records_honest_project_and_Kiosk_controller_evidence()
    {
        var project = await CreateProjectAsync(includeController: true);
        var discovery = await CreatePlugin(project).DiscoverAsync(
            new DiscoveryRequest(project),
            default);

        var package = await CreateWriter(project).CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            discovery,
            DateTimeOffset.UtcNow,
            default);

        Assert.Contains(package.Manifest.RestorePrerequisites,
            value => value.Contains("new empty ShowVault-controlled target", StringComparison.Ordinal));
        Assert.Contains(package.Manifest.RestorePrerequisites,
            value => value.Contains("never restore directly", StringComparison.Ordinal));
        Assert.Contains(package.Manifest.CompatibilityRules,
            rule => rule.Kind == "opaque-settings-format" &&
                rule.Requirement.Contains("ProVisionaire Control PLUS", StringComparison.Ordinal) &&
                rule.Requirement.Contains(".pvcppj", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(package.Manifest.CompatibilityRules,
            rule => rule.Kind == "opaque-companion-formats" &&
                rule.Requirement.Contains(".PVKSK", StringComparison.Ordinal) &&
                rule.Requirement.Contains("Kiosk controller", StringComparison.Ordinal) &&
                rule.Requirement.Contains("does not replace", StringComparison.Ordinal));
        Assert.Contains(package.Manifest.CompatibilityRules,
            rule => rule.Kind == "operator-confirmation-required" &&
                rule.Requirement.Contains("do not prove", StringComparison.Ordinal));
        Assert.DoesNotContain(package.Manifest.CompatibilityRules,
            rule => rule.Requirement.Contains(project, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Package_without_Kiosk_export_does_not_invent_companion_evidence()
    {
        var project = await CreateProjectAsync(includeController: false);
        var discovery = await CreatePlugin(project).DiscoverAsync(
            new DiscoveryRequest(project),
            default);

        var package = await CreateWriter(project).CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            discovery,
            DateTimeOffset.UtcNow,
            default);

        Assert.DoesNotContain(package.Manifest.CompatibilityRules,
            rule => rule.Kind == "opaque-companion-formats");
    }

    [Fact]
    public async Task Existing_package_is_reused_only_while_source_is_stable()
    {
        var project = await CreateProjectAsync(includeController: true);
        var discovery = await CreatePlugin(project).DiscoverAsync(
            new DiscoveryRequest(project),
            default);
        var writer = CreateWriter(project);
        var agentId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var first = await writer.CreateAsync(agentId, commandId, discovery, createdAt, default);
        var second = await writer.CreateAsync(agentId, commandId, discovery, createdAt, default);

        Assert.Equal(first.PackageId, second.PackageId);
        Assert.Equal(first.PackagePath, second.PackagePath);
    }

    [Theory]
    [InlineData("addition")]
    [InlineData("deletion")]
    [InlineData("replacement")]
    [InlineData("same-content-replacement")]
    public async Task Package_rejects_late_topology_content_or_identity_changes(string mutation)
    {
        var project = await CreateProjectAsync(includeController: true);
        var primary = Path.Combine(project, "Venue.pvcppj");
        var discovery = await CreatePlugin(project).DiscoverAsync(
            new DiscoveryRequest(project),
            default);
        var writer = new RecoveryPackageWriter(
            CreateOptions(project),
            new CallbackProbe((point, _) =>
            {
                if (point != SourceSnapshotRacePoint.SnapshotCaptured)
                {
                    return;
                }

                switch (mutation)
                {
                    case "addition":
                        File.WriteAllText(Path.Combine(project, "late.txt"), "late");
                        break;
                    case "deletion":
                        File.Delete(primary);
                        break;
                    case "replacement":
                        File.WriteAllText(primary, "changed");
                        break;
                    case "same-content-replacement":
                        var replacement = Path.Combine(project, "replacement.tmp");
                        File.WriteAllText(replacement, "opaque-project");
                        File.Move(replacement, primary, overwrite: true);
                        break;
                }
            }));

        await Assert.ThrowsAnyAsync<Exception>(() => writer.CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            discovery,
            DateTimeOffset.UtcNow,
            default));
        Assert.Empty(Directory.Exists(PackageRoot)
            ? Directory.EnumerateDirectories(PackageRoot)
            : []);
    }

    [Fact]
    public async Task Package_rechecks_authorization_and_mixed_profile_structure()
    {
        var project = await CreateProjectAsync(includeController: false);
        var discovery = await CreatePlugin(project).DiscoverAsync(
            new DiscoveryRequest(project),
            default);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new RecoveryPackageWriter(CreateOptions(null)).CreateAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                discovery,
                DateTimeOffset.UtcNow,
                default));

        await File.WriteAllTextAsync(Path.Combine(project, "Foreign.pvd"), "foreign");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateWriter(project).CreateAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                discovery,
                DateTimeOffset.UtcNow,
                default));
    }

    [Fact]
    public async Task Package_honors_cancellation_before_publication()
    {
        var project = await CreateProjectAsync(includeController: false);
        var discovery = await CreatePlugin(project).DiscoverAsync(
            new DiscoveryRequest(project),
            default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateWriter(project).CreateAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                discovery,
                DateTimeOffset.UtcNow,
                cancellation.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string PackageRoot => Path.Combine(_root, "packages");

    private async Task<string> CreateProjectAsync(bool includeController)
    {
        var project = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(project);
        await File.WriteAllTextAsync(Path.Combine(project, "Venue.pvcppj"), "opaque-project");
        if (includeController)
        {
            await File.WriteAllTextAsync(
                Path.Combine(project, "FrontDesk.pvksk"),
                "opaque-controller");
        }

        return project;
    }

    private YamahaProVisionaireControlDiscoveryPlugin CreatePlugin(string project) =>
        new(CreateOptions(project), TimeProvider.System);

    private RecoveryPackageWriter CreateWriter(string project) =>
        new(CreateOptions(project));

    private IOptions<AgentOptions> CreateOptions(string? project) =>
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            PackageDirectory = PackageRoot,
            YamahaProVisionaireControlProjectRoots = project is null ? [] : [project]
        });

    private sealed class CallbackProbe(Action<SourceSnapshotRacePoint, string> callback)
        : ISourceSnapshotRaceProbe
    {
        public void Reached(SourceSnapshotRacePoint point, string relativePath) =>
            callback(point, relativePath);
    }
}
