using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class YamahaDspProjectDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "showvault-yamaha-dsp-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Venue.pvd", "provisionaire")]
    [InlineData("Venue.PVD", "provisionaire")]
    [InlineData("Venue.mtx", "mtx-mrx")]
    [InlineData("Venue.MTX", "mtx-mrx")]
    public async Task Profiles_accept_only_their_root_level_primary_format(
        string fileName,
        string family)
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, fileName), "opaque-project");

        var plugin = family == "provisionaire"
            ? (IDiscoveryPlugin)CreateProVisionaire(_root)
            : CreateMtxMrx(_root);
        var result = await plugin.DiscoverAsync(new DiscoveryRequest(_root), default);

        Assert.Equal(fileName, Assert.Single(result.Files).RelativePath);
        Assert.Equal("1.0.0", result.PluginVersion);
    }

    [Theory]
    [InlineData("Lobby.pvksk")]
    [InlineData("Lobby.PVKSK")]
    public async Task ProVisionaire_preserves_Control_PLUS_controller_as_opaque_companion(
        string controllerFile)
    {
        Directory.CreateDirectory(Path.Combine(_root, "controllers"));
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.pvd"), "opaque-project");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "controllers", controllerFile), "opaque-controller");

        var result = await CreateProVisionaire(_root).DiscoverAsync(
            new DiscoveryRequest(_root), default);

        Assert.Contains(result.Files, file => file.RelativePath == "Venue.pvd");
        Assert.Contains(result.Files, file =>
            file.RelativePath == Path.Combine("controllers", controllerFile));
        Assert.Equal([".PVKSK"], YamahaSettingsExportDiscoveryPluginBase.GetCompanionFormats(
            result.PluginId,
            result.Files.Select(file => file.RelativePath)));
    }

    [Theory]
    [InlineData("Lobby.pvksk")]
    [InlineData("Lobby.PVKSK")]
    public async Task Control_PLUS_controller_without_pvd_cannot_authorize_capture(
        string controllerFile)
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, controllerFile), "controller");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateProVisionaire(_root).DiscoverAsync(new DiscoveryRequest(_root), default));
    }

    [Theory]
    [InlineData("provisionaire", "Venue.mtx")]
    [InlineData("mtx-mrx", "Venue.pvd")]
    [InlineData("provisionaire", "Venue.DM3F")]
    [InlineData("mtx-mrx", "Venue.TFF")]
    public async Task Profiles_reject_mixed_known_Yamaha_primary_formats(
        string family,
        string foreignFile)
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(
            Path.Combine(_root, family == "provisionaire" ? "Venue.pvd" : "Venue.mtx"),
            "project");
        await File.WriteAllTextAsync(Path.Combine(_root, foreignFile), "foreign");
        var plugin = family == "provisionaire"
            ? (IDiscoveryPlugin)CreateProVisionaire(_root)
            : CreateMtxMrx(_root);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            plugin.DiscoverAsync(new DiscoveryRequest(_root), default));
    }

    [Theory]
    [InlineData("provisionaire")]
    [InlineData("mtx-mrx")]
    public async Task Descendant_marker_never_authorizes_parent_or_sibling_content(string family)
    {
        var child = Path.Combine(_root, "child");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(_root, "private.txt"), "private");
        await File.WriteAllTextAsync(
            Path.Combine(child, family == "provisionaire" ? "Venue.pvd" : "Venue.mtx"),
            "project");
        var plugin = family == "provisionaire"
            ? (IDiscoveryPlugin)CreateProVisionaire(_root)
            : CreateMtxMrx(_root);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            plugin.DiscoverAsync(new DiscoveryRequest(_root), default));
    }

    [Theory]
    [InlineData("root")]
    [InlineData("ancestor")]
    [InlineData("child")]
    public async Task Discovery_rejects_linked_root_ancestor_or_child(string location)
    {
        if (OperatingSystem.IsWindows()) return;

        var outside = $"{_root}-outside";
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "Venue.pvd"), "outside");
        string selected;
        if (location == "root")
        {
            selected = _root;
            Directory.CreateSymbolicLink(selected, outside);
        }
        else if (location == "ancestor")
        {
            var outsideChild = Path.Combine(outside, "project");
            Directory.CreateDirectory(outsideChild);
            await File.WriteAllTextAsync(Path.Combine(outsideChild, "Venue.pvd"), "outside");
            var linked = Path.Combine(_root, "linked");
            Directory.CreateDirectory(_root);
            Directory.CreateSymbolicLink(linked, outside);
            selected = Path.Combine(linked, "project");
        }
        else
        {
            selected = _root;
            Directory.CreateDirectory(selected);
            await File.WriteAllTextAsync(Path.Combine(selected, "Venue.pvd"), "project");
            Directory.CreateSymbolicLink(Path.Combine(selected, "linked"), outside);
        }

        await Assert.ThrowsAsync<IOException>(() =>
            CreateProVisionaire(selected).DiscoverAsync(new DiscoveryRequest(selected), default));
    }

    [Fact]
    public async Task Discovery_is_exact_root_only_and_honors_cancellation()
    {
        var child = Path.Combine(_root, "child");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.pvd"), "project");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateProVisionaire(_root).DiscoverAsync(new DiscoveryRequest(child), default));

        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.pvd"), "project");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateProVisionaire(_root).DiscoverAsync(
                new DiscoveryRequest(_root), cancellation.Token));
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
        await File.WriteAllTextAsync(Path.Combine(_root, "Venue.pvd"), "12345");
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
                YamahaProVisionaireDesignProjectDiscoveryPlugin.PluginId,
                _root,
                bounds,
                default));
    }

    [Fact]
    public void Configuration_rejects_duplicates_nesting_and_cross_family_overlap()
    {
        var roots = Enumerable.Range(0, 33)
            .Select(index => Path.Combine(_root, index.ToString()))
            .ToList();
        Assert.False(YamahaSettingsExportDiscoveryPluginBase.AreConfiguredRootsValid(roots));
        Assert.False(YamahaSettingsExportDiscoveryPluginBase.AreConfiguredRootsValid(
            [roots[0], roots[0]]));
        Assert.False(YamahaSettingsExportDiscoveryPluginBase.AreConfiguredRootsValid(
            [roots[0], Path.Combine(roots[0], "nested")]));
        Assert.False(YamahaSettingsExportDiscoveryPluginBase.HaveNoOverlap(
            [roots[0]], [roots[1]], [roots[2]], [roots[3]], [roots[4]],
            [roots[5]], [Path.Combine(roots[5], "nested")]));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        if (Directory.Exists($"{_root}-outside")) Directory.Delete($"{_root}-outside", true);
    }

    private YamahaProVisionaireDesignProjectDiscoveryPlugin CreateProVisionaire(string root) =>
        new(CreateOptions(proVisionaireRoots: [root]), TimeProvider.System);

    private YamahaMtxMrxProjectDiscoveryPlugin CreateMtxMrx(string root) =>
        new(CreateOptions(mtxMrxRoots: [root]), TimeProvider.System);

    private static IOptions<AgentOptions> CreateOptions(
        IReadOnlyList<string>? proVisionaireRoots = null,
        IReadOnlyList<string>? mtxMrxRoots = null) => Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            YamahaProVisionaireDesignProjectRoots = proVisionaireRoots ?? [],
            YamahaMtxMrxProjectRoots = mtxMrxRoots ?? []
        });
}
