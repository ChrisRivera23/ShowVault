using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class YamahaProVisionaireControlDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-yamaha-control-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Venue.pvcppj", "FrontDesk.pvksk")]
    [InlineData("Venue.PVCPPJ", "FrontDesk.PVKSK")]
    public async Task Captures_root_level_project_Kiosk_controllers_and_opaque_companions(
        string projectFile,
        string controllerFile)
    {
        Directory.CreateDirectory(Path.Combine(_root, "controllers"));
        var projectBytes = "opaque-control-plus-project"u8.ToArray();
        await File.WriteAllBytesAsync(Path.Combine(_root, projectFile), projectBytes);
        await File.WriteAllTextAsync(
            Path.Combine(_root, "controllers", controllerFile),
            "opaque-kiosk-controller");
        await File.WriteAllTextAsync(Path.Combine(_root, "operator-note.txt"), "companion");

        var result = await CreatePlugin(_root).DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(YamahaProVisionaireControlDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Equal("1.0.0", result.PluginVersion);
        Assert.False(result.Truncated);
        Assert.Equal(3, result.Files.Count);
        var project = Assert.Single(result.Files, file => file.RelativePath == projectFile);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(projectBytes)), project.Sha256);
        Assert.Contains(result.Files, file =>
            file.RelativePath == Path.Combine("controllers", controllerFile));
        Assert.Equal([".PVKSK"], YamahaSettingsExportDiscoveryPluginBase.GetCompanionFormats(
            result.PluginId,
            result.Files.Select(file => file.RelativePath)));
    }

    [Theory]
    [InlineData("FrontDesk.pvksk")]
    [InlineData("FrontDesk.PVKSK")]
    public async Task Kiosk_controller_without_editable_project_cannot_authorize_capture(
        string controllerFile)
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, controllerFile), "controller");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), default));
    }

    [Fact]
    public async Task Descendant_project_marker_never_authorizes_parent_or_sibling_content()
    {
        var child = Path.Combine(_root, "child");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.pvcppj"), "project");
        await File.WriteAllTextAsync(Path.Combine(_root, "private.txt"), "private");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), default));
    }

    [Theory]
    [InlineData("Venue.pvd")]
    [InlineData("Venue.mtx")]
    [InlineData("Venue.DM3F")]
    [InlineData("Venue.TFF")]
    [InlineData("Venue.dm7f")]
    public async Task Rejects_known_primary_artifact_from_another_Yamaha_profile(
        string foreignFile)
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.pvcppj"), "project");
        await File.WriteAllTextAsync(Path.Combine(_root, foreignFile), "foreign");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), default));
    }

    [Fact]
    public async Task Exact_authorization_rejects_child_and_excludes_external_sibling()
    {
        var child = Path.Combine(_root, "child");
        var sibling = $"{_root}-private";
        Directory.CreateDirectory(child);
        Directory.CreateDirectory(sibling);
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.pvcppj"), "project");
        await File.WriteAllTextAsync(Path.Combine(child, "Child.pvcppj"), "child project");
        await File.WriteAllTextAsync(Path.Combine(sibling, "private.txt"), "private");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(child), default));

        var result = await CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), default);
        Assert.DoesNotContain(result.Files, file => file.RelativePath.Contains("private.txt"));
    }

    [Theory]
    [InlineData("root")]
    [InlineData("ancestor")]
    [InlineData("child")]
    public async Task Rejects_linked_root_ancestor_or_child(string location)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var outside = $"{_root}-outside";
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "Venue.pvcppj"), "outside");
        string selected;
        if (location == "root")
        {
            selected = _root;
            Directory.CreateSymbolicLink(selected, outside);
        }
        else if (location == "ancestor")
        {
            var outsideProject = Path.Combine(outside, "project");
            Directory.CreateDirectory(outsideProject);
            await File.WriteAllTextAsync(
                Path.Combine(outsideProject, "Venue.pvcppj"),
                "outside");
            var linkedAncestor = Path.Combine(_root, "linked");
            Directory.CreateDirectory(_root);
            Directory.CreateSymbolicLink(linkedAncestor, outside);
            selected = Path.Combine(linkedAncestor, "project");
        }
        else
        {
            selected = _root;
            Directory.CreateDirectory(selected);
            await File.WriteAllTextAsync(Path.Combine(selected, "Venue.pvcppj"), "project");
            Directory.CreateSymbolicLink(Path.Combine(selected, "linked"), outside);
        }

        await Assert.ThrowsAsync<IOException>(() =>
            CreatePlugin(selected).DiscoverAsync(new DiscoveryRequest(selected), default));
    }

    [Fact]
    public async Task Fails_instead_of_truncating_and_honors_cancellation()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.pvcppj"), "project");
        await File.WriteAllTextAsync(Path.Combine(_root, "companion.txt"), "companion");

        await Assert.ThrowsAsync<IOException>(() =>
            CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root, MaxFiles: 1), default));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreatePlugin(_root).DiscoverAsync(
                new DiscoveryRequest(_root),
                cancellation.Token));
    }

    [Fact]
    public async Task Rejects_empty_directory_topology()
    {
        Directory.CreateDirectory(Path.Combine(_root, "empty"));
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.pvcppj"), "project");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(_root).DiscoverAsync(new DiscoveryRequest(_root), default));
    }

    [Theory]
    [InlineData("files")]
    [InlineData("directories")]
    [InlineData("path")]
    [InlineData("file-bytes")]
    [InlineData("total-bytes")]
    public async Task Capture_fails_closed_at_every_resource_bound(string bound)
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.pvcppj"), "12345");
        var bounds = new YamahaCaptureBounds(10, 10, 100, 100, 100);
        switch (bound)
        {
            case "files":
                await File.WriteAllTextAsync(Path.Combine(_root, "extra.txt"), "x");
                bounds = bounds with { MaximumFileCount = 1 };
                break;
            case "directories":
                Directory.CreateDirectory(Path.Combine(_root, "one"));
                Directory.CreateDirectory(Path.Combine(_root, "two"));
                await File.WriteAllTextAsync(Path.Combine(_root, "one", "a.txt"), "x");
                await File.WriteAllTextAsync(Path.Combine(_root, "two", "b.txt"), "x");
                bounds = bounds with { MaximumDirectoryCount = 1 };
                break;
            case "path":
                bounds = bounds with { MaximumRelativePathLength = 5 };
                break;
            case "file-bytes":
                bounds = bounds with { MaximumFileBytes = 4 };
                break;
            case "total-bytes":
                await File.WriteAllTextAsync(Path.Combine(_root, "extra.txt"), "12345");
                bounds = bounds with { MaximumTotalBytes = 9 };
                break;
        }

        await Assert.ThrowsAnyAsync<Exception>(() =>
            YamahaSettingsExportDiscoveryPluginBase.CaptureSnapshotAsync(
                YamahaProVisionaireControlDiscoveryPlugin.PluginId,
                _root,
                bounds,
                default));
    }

    [Fact]
    public void Configuration_rejects_duplicates_nesting_and_cross_profile_overlap()
    {
        var roots = Enumerable.Range(0, 33)
            .Select(index => Path.Combine(_root, index.ToString()))
            .ToList();
        Assert.False(YamahaSettingsExportDiscoveryPluginBase.AreConfiguredRootsValid(roots));
        Assert.False(YamahaSettingsExportDiscoveryPluginBase.AreConfiguredRootsValid(
            [roots[0], roots[0]]));
        Assert.False(YamahaSettingsExportDiscoveryPluginBase.AreConfiguredRootsValid(
            [roots[0], Path.Combine(roots[0], "nested")]));
        Assert.False(YamahaSettingsExportDiscoveryPluginBase.AreConfiguredRootsValid(
            ["relative-project"]));
        Assert.False(YamahaSettingsExportDiscoveryPluginBase.HaveNoOverlap(
            [roots[0]], [roots[1]], [roots[2]], [roots[3]], [roots[4]], [roots[5]],
            [roots[6]], [Path.Combine(roots[6], "control-nested")]));
    }

    [Fact]
    public async Task Control_profile_keeps_a_distinct_authorization_list()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.pvcppj"), "project");
        var plugin = new YamahaProVisionaireControlDiscoveryPlugin(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaProVisionaireDesignProjectRoots = [_root]
            }),
            TimeProvider.System);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            plugin.DiscoverAsync(new DiscoveryRequest(_root), default));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        if (Directory.Exists($"{_root}-outside"))
        {
            Directory.Delete($"{_root}-outside", recursive: true);
        }

        if (Directory.Exists($"{_root}-private"))
        {
            Directory.Delete($"{_root}-private", recursive: true);
        }
    }

    private static YamahaProVisionaireControlDiscoveryPlugin CreatePlugin(string root) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                YamahaProVisionaireControlProjectRoots = [root]
            }),
            TimeProvider.System);
}
