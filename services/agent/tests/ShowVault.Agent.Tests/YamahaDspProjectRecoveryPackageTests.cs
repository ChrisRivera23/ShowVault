using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using ShowVault.Agent.Recovery;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class YamahaDspProjectRecoveryPackageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "showvault-yamaha-dsp-package-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("provisionaire")]
    [InlineData("mtx-mrx")]
    public async Task Package_records_honest_opaque_Assisted_compatibility(string family)
    {
        var project = await CreateProjectAsync(family);
        await File.WriteAllTextAsync(Path.Combine(project, "operator-note.txt"), "opaque companion");
        var plugin = CreatePlugin(family, project);
        var discovery = await plugin.DiscoverAsync(new DiscoveryRequest(project), default);

        var package = await CreateWriter(family, project).CreateAsync(
            Guid.NewGuid(), Guid.NewGuid(), discovery, DateTimeOffset.UtcNow, default);

        var expectedFormat = family == "provisionaire" ? ".pvd" : ".mtx";
        Assert.Contains(package.Manifest.RestorePrerequisites,
            value => value.Contains("new empty ShowVault-controlled target", StringComparison.Ordinal));
        Assert.Contains(package.Manifest.RestorePrerequisites,
            value => value.Contains("never restore directly", StringComparison.Ordinal));
        Assert.Contains(package.Manifest.CompatibilityRules,
            rule => rule.Kind == "opaque-settings-format" &&
                rule.Requirement.Contains(expectedFormat, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(package.Manifest.CompatibilityRules,
            rule => rule.Kind == "operator-confirmation-required" &&
                rule.Requirement.Contains("do not prove", StringComparison.Ordinal));
        Assert.DoesNotContain(package.Manifest.CompatibilityRules,
            rule => rule.Requirement.Contains(project, StringComparison.Ordinal));
        if (family == "provisionaire")
        {
            Assert.DoesNotContain(package.Manifest.CompatibilityRules,
                rule => rule.Requirement.Contains("contains a DME7", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Existing_package_is_reused_only_while_source_is_stable()
    {
        var project = await CreateProjectAsync("provisionaire");
        var discovery = await CreatePlugin("provisionaire", project)
            .DiscoverAsync(new DiscoveryRequest(project), default);
        var writer = CreateWriter("provisionaire", project);
        var agentId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var first = await writer.CreateAsync(agentId, commandId, discovery, createdAt, default);
        var second = await writer.CreateAsync(agentId, commandId, discovery, createdAt, default);

        Assert.Equal(first.PackageId, second.PackageId);
        Assert.Equal(first.PackagePath, second.PackagePath);
    }

    [Theory]
    [InlineData("Lobby.pvksk")]
    [InlineData("Lobby.PVKSK")]
    public async Task ProVisionaire_package_records_model_neutral_Control_PLUS_companion_evidence(
        string controllerFile)
    {
        var project = await CreateProjectAsync("provisionaire");
        Directory.CreateDirectory(Path.Combine(project, "controllers"));
        await File.WriteAllTextAsync(
            Path.Combine(project, "controllers", controllerFile), "opaque-controller");
        var plugin = CreatePlugin("provisionaire", project);
        var discovery = await plugin.DiscoverAsync(new DiscoveryRequest(project), default);

        var package = await CreateWriter("provisionaire", project).CreateAsync(
            Guid.NewGuid(), Guid.NewGuid(), discovery, DateTimeOffset.UtcNow, default);

        Assert.Contains(package.Manifest.Files, file =>
            file.RelativePath == $"controllers/{controllerFile}");
        var evidence = Assert.Single(package.Manifest.CompatibilityRules,
            rule => rule.Kind == "opaque-companion-formats");
        Assert.Contains(".PVKSK", evidence.Requirement, StringComparison.Ordinal);
        Assert.Contains("separately created", evidence.Requirement, StringComparison.Ordinal);
        Assert.Contains("may support a DME5/DME3", evidence.Requirement, StringComparison.Ordinal);
        Assert.Contains("does not prove the DME model", evidence.Requirement, StringComparison.Ordinal);
        Assert.Contains(".pvd project completeness", evidence.Requirement, StringComparison.Ordinal);
        Assert.DoesNotContain(project, evidence.Requirement, StringComparison.Ordinal);
        Assert.DoesNotContain("contains a DME5", evidence.Requirement, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contains a DME3", evidence.Requirement, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("addition")]
    [InlineData("deletion")]
    [InlineData("replacement")]
    [InlineData("same-content-replacement")]
    public async Task Package_rejects_late_topology_content_or_identity_changes(string mutation)
    {
        var project = await CreateProjectAsync("provisionaire");
        var primary = Path.Combine(project, "Venue.pvd");
        var discovery = await CreatePlugin("provisionaire", project)
            .DiscoverAsync(new DiscoveryRequest(project), default);
        var writer = new RecoveryPackageWriter(
            CreateOptions("provisionaire", project),
            new CallbackProbe((point, _) =>
            {
                if (point != SourceSnapshotRacePoint.SnapshotCaptured) return;
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
            Guid.NewGuid(), Guid.NewGuid(), discovery, DateTimeOffset.UtcNow, default));
        Assert.Empty(Directory.Exists(Path.Combine(_root, "packages"))
            ? Directory.EnumerateDirectories(Path.Combine(_root, "packages"))
            : []);
    }

    [Fact]
    public async Task Package_rechecks_authorization_and_mixed_family_structure()
    {
        var project = await CreateProjectAsync("mtx-mrx");
        var discovery = await CreatePlugin("mtx-mrx", project)
            .DiscoverAsync(new DiscoveryRequest(project), default);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new RecoveryPackageWriter(CreateOptions(null, null)).CreateAsync(
                Guid.NewGuid(), Guid.NewGuid(), discovery, DateTimeOffset.UtcNow, default));

        await File.WriteAllTextAsync(Path.Combine(project, "Foreign.pvd"), "foreign");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateWriter("mtx-mrx", project).CreateAsync(
                Guid.NewGuid(), Guid.NewGuid(), discovery, DateTimeOffset.UtcNow, default));
    }

    [Fact]
    public async Task Package_honors_cancellation_before_publication()
    {
        var project = await CreateProjectAsync("provisionaire");
        var discovery = await CreatePlugin("provisionaire", project)
            .DiscoverAsync(new DiscoveryRequest(project), default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateWriter("provisionaire", project).CreateAsync(
                Guid.NewGuid(), Guid.NewGuid(), discovery, DateTimeOffset.UtcNow,
                cancellation.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private async Task<string> CreateProjectAsync(string family)
    {
        var project = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(project);
        await File.WriteAllTextAsync(
            Path.Combine(project, family == "provisionaire" ? "Venue.pvd" : "Venue.mtx"),
            "opaque-project");
        return project;
    }

    private IDiscoveryPlugin CreatePlugin(string family, string project) =>
        family == "provisionaire"
            ? new YamahaProVisionaireDesignProjectDiscoveryPlugin(
                CreateOptions(family, project), TimeProvider.System)
            : new YamahaMtxMrxProjectDiscoveryPlugin(
                CreateOptions(family, project), TimeProvider.System);

    private RecoveryPackageWriter CreateWriter(string family, string project) =>
        new(CreateOptions(family, project));

    private IOptions<AgentOptions> CreateOptions(string? family, string? project) =>
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            PackageDirectory = Path.Combine(_root, "packages"),
            YamahaProVisionaireDesignProjectRoots = family == "provisionaire" ? [project!] : [],
            YamahaMtxMrxProjectRoots = family == "mtx-mrx" ? [project!] : []
        });

    private sealed class CallbackProbe(Action<SourceSnapshotRacePoint, string> callback)
        : ISourceSnapshotRaceProbe
    {
        public void Reached(SourceSnapshotRacePoint point, string relativePath) =>
            callback(point, relativePath);
    }
}
