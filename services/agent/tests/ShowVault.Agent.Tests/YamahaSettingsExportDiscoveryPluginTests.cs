using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class YamahaSettingsExportDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-yamaha-export-tests",
        Guid.NewGuid().ToString("N"));
    private readonly string _socketRoot = Path.Combine(
        "/tmp",
        $"svy-{Guid.NewGuid():N}");

    [Fact]
    public async Task Dm7_captures_exact_root_with_companion_files()
    {
        var export = Path.Combine(_root, "dm7-export");
        Directory.CreateDirectory(Path.Combine(export, "notes"));
        var content = Encoding.UTF8.GetBytes("opaque-dm7-settings");
        await File.WriteAllBytesAsync(Path.Combine(export, "Venue.dm7f"), content);
        await File.WriteAllTextAsync(Path.Combine(export, "notes", "operator.txt"), "note");

        var result = await CreateDm7(export).DiscoverAsync(
            new DiscoveryRequest(export),
            CancellationToken.None);

        Assert.Equal(YamahaDm7SettingsExportDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Equal("1.0.0", result.PluginVersion);
        Assert.False(result.Truncated);
        Assert.Equal(2, result.Files.Count);
        var settings = Assert.Single(result.Files, file => file.RelativePath == "Venue.dm7f");
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(content)), settings.Sha256);
    }

    [Theory]
    [InlineData("Venue.RIVAGEPM")]
    [InlineData("Venue.PM10ALL")]
    [InlineData("Venue.PM7ALL")]
    [InlineData("Venue.PM10PART")]
    [InlineData("Venue.PM7PART")]
    public async Task Rivage_accepts_each_documented_settings_format(string fileName)
    {
        var export = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(export);
        await File.WriteAllTextAsync(Path.Combine(export, fileName), "opaque-rivage-settings");

        var result = await CreateRivage(export).DiscoverAsync(
            new DiscoveryRequest(export),
            CancellationToken.None);

        Assert.Equal(YamahaRivageSettingsExportDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Equal(fileName, Assert.Single(result.Files).RelativePath);
    }

    [Theory]
    [InlineData("Venue.CLF")]
    [InlineData("Venue.clf")]
    public async Task ClQl_accepts_the_shared_primary_settings_format(string fileName)
    {
        var export = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(export);
        await File.WriteAllTextAsync(Path.Combine(export, fileName), "opaque-clql-settings");

        var result = await CreateClQl(export).DiscoverAsync(
            new DiscoveryRequest(export), CancellationToken.None);

        Assert.Equal(YamahaClQlSettingsExportDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Equal("1.0.0", result.PluginVersion);
        Assert.Equal(fileName, Assert.Single(result.Files).RelativePath);
    }

    [Theory]
    [InlineData("Venue.TFF")]
    [InlineData("Venue.tff")]
    public async Task Tf_requires_tff_and_captures_tfp_and_tfs_companions(string settingsFile)
    {
        var export = Path.Combine(_root, "tf-with-companions");
        Directory.CreateDirectory(Path.Combine(export, "companions"));
        await File.WriteAllTextAsync(Path.Combine(export, settingsFile), "settings");
        await File.WriteAllTextAsync(Path.Combine(export, "companions", "Vocal.TFP"), "preset");
        await File.WriteAllTextAsync(Path.Combine(export, "companions", "Scene.TFS"), "scene");

        var result = await CreateTf(export).DiscoverAsync(
            new DiscoveryRequest(export), CancellationToken.None);

        Assert.Equal(YamahaTfSettingsExportDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Equal("1.0.0", result.PluginVersion);
        Assert.Equal(3, result.Files.Count);
        Assert.Contains(result.Files, file => file.RelativePath == settingsFile);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("companions", "Vocal.TFP"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("companions", "Scene.TFS"));
    }

    [Theory]
    [InlineData("Only.TFP")]
    [InlineData("Only.TFS")]
    public async Task Tf_companions_do_not_authorize_a_settings_export(string fileName)
    {
        var export = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(export);
        await File.WriteAllTextAsync(Path.Combine(export, fileName), "companion");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateTf(export).DiscoverAsync(
                new DiscoveryRequest(export), CancellationToken.None));
    }

    [Theory]
    [InlineData("Venue.DM3F")]
    [InlineData("Venue.dm3f")]
    public async Task Dm3_requires_dm3f_and_captures_scene_and_preset_companions(
        string settingsFile)
    {
        var export = Path.Combine(_root, "dm3-with-companions");
        Directory.CreateDirectory(Path.Combine(export, "companions"));
        await File.WriteAllTextAsync(Path.Combine(export, settingsFile), "settings");
        await File.WriteAllTextAsync(Path.Combine(export, "companions", "Scene.DM3S"), "scene");
        await File.WriteAllTextAsync(Path.Combine(export, "companions", "Vocal.DM3P"), "preset");

        var result = await CreateDm3(export).DiscoverAsync(
            new DiscoveryRequest(export), CancellationToken.None);

        Assert.Equal(YamahaDm3SettingsExportDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Equal("1.0.0", result.PluginVersion);
        Assert.Equal(3, result.Files.Count);
        Assert.Contains(result.Files, file => file.RelativePath == settingsFile);
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("companions", "Scene.DM3S"));
        Assert.Contains(result.Files,
            file => file.RelativePath == Path.Combine("companions", "Vocal.DM3P"));
    }

    [Theory]
    [InlineData("Only.DM3S")]
    [InlineData("Only.DM3P")]
    public async Task Dm3_companions_do_not_authorize_a_settings_export(string fileName)
    {
        var export = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(export);
        await File.WriteAllTextAsync(Path.Combine(export, fileName), "companion");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateDm3(export).DiscoverAsync(
                new DiscoveryRequest(export), CancellationToken.None));
    }

    [Fact]
    public async Task Dm3_requires_primary_settings_artifact_at_root_level()
    {
        var export = Path.Combine(_root, "dm3-descendant-marker");
        Directory.CreateDirectory(Path.Combine(export, "nested"));
        await File.WriteAllTextAsync(Path.Combine(export, "nested", "Venue.DM3F"), "settings");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateDm3(export).DiscoverAsync(
                new DiscoveryRequest(export), CancellationToken.None));
    }

    [Theory]
    [InlineData("clql", "Venue.TFF")]
    [InlineData("tf", "Venue.CLF")]
    [InlineData("clql", "Venue.dm7f")]
    [InlineData("tf", "Venue.RIVAGEPM")]
    [InlineData("dm3", "Venue.TFF")]
    [InlineData("tf", "Venue.DM3F")]
    [InlineData("dm7", "Venue.DM3F")]
    public async Task Discovery_rejects_a_primary_artifact_from_another_yamaha_family(
        string selectedFamily,
        string foreignFile)
    {
        var export = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(export);
        var selectedFile = selectedFamily switch
        {
            "clql" => "Venue.CLF",
            "tf" => "Venue.TFF",
            "dm3" => "Venue.DM3F",
            _ => "Venue.dm7f"
        };
        await File.WriteAllTextAsync(Path.Combine(export, selectedFile), "settings");
        await File.WriteAllTextAsync(Path.Combine(export, foreignFile), "foreign");

        var plugin = selectedFamily switch
        {
            "clql" => (IDiscoveryPlugin)CreateClQl(export),
            "tf" => CreateTf(export),
            "dm3" => CreateDm3(export),
            _ => CreateDm7(export)
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            plugin.DiscoverAsync(new DiscoveryRequest(export), CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_requires_recognized_artifact_at_root_level()
    {
        var export = Path.Combine(_root, "descendant-marker");
        Directory.CreateDirectory(Path.Combine(export, "nested"));
        await File.WriteAllTextAsync(Path.Combine(export, "nested", "Venue.dm7f"), "settings");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateDm7(export).DiscoverAsync(
                new DiscoveryRequest(export),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_non_exact_authorized_child()
    {
        var export = Path.Combine(_root, "authorized");
        var child = Path.Combine(export, "child");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.dm7f"), "settings");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateDm7(export).DiscoverAsync(
                new DiscoveryRequest(child),
                CancellationToken.None));
    }

    [Fact]
    public void Configuration_rejects_excess_duplicates_and_cross_profile_overlap()
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
            ["relative-export"]));
        Assert.False(YamahaSettingsExportDiscoveryPluginBase.HaveNoOverlap(
            [roots[0]],
            [roots[0]]));
        Assert.False(YamahaSettingsExportDiscoveryPluginBase.HaveNoOverlap(
            [roots[0]],
            [Path.Combine(roots[0], "nested")],
            [roots[1]],
            [roots[2]],
            [roots[3]]));
        Assert.False(YamahaSettingsExportDiscoveryPluginBase.HaveNoOverlap(
            [roots[0]],
            [roots[1]],
            [roots[2]],
            [roots[3]],
            [Path.Combine(roots[3], "dm3-nested")]));
    }

    [Fact]
    public async Task Product_profiles_keep_distinct_authorization_lists()
    {
        var dm7Export = Path.Combine(_root, "dm7-only");
        Directory.CreateDirectory(dm7Export);
        await File.WriteAllTextAsync(Path.Combine(dm7Export, "Venue.RIVAGEPM"), "settings");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new YamahaRivageSettingsExportDiscoveryPlugin(
                CreateOptions(dm7Roots: [dm7Export]),
                TimeProvider.System).DiscoverAsync(
                    new DiscoveryRequest(dm7Export),
                    CancellationToken.None));

        var clQlExport = Path.Combine(_root, "clql-only");
        Directory.CreateDirectory(clQlExport);
        await File.WriteAllTextAsync(Path.Combine(clQlExport, "Venue.TFF"), "settings");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new YamahaTfSettingsExportDiscoveryPlugin(
                CreateOptions(clQlRoots: [clQlExport]),
                TimeProvider.System).DiscoverAsync(
                    new DiscoveryRequest(clQlExport),
                    CancellationToken.None));

        var dm3Export = Path.Combine(_root, "dm3-only");
        Directory.CreateDirectory(dm3Export);
        await File.WriteAllTextAsync(Path.Combine(dm3Export, "Venue.DM3F"), "settings");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new YamahaDm3SettingsExportDiscoveryPlugin(
                CreateOptions(tfRoots: [dm3Export]),
                TimeProvider.System).DiscoverAsync(
                    new DiscoveryRequest(dm3Export),
                    CancellationToken.None));
    }

    [Theory]
    [InlineData("root")]
    [InlineData("ancestor")]
    [InlineData("descendant")]
    public async Task Discovery_rejects_filesystem_links(string linkLocation)
    {
        var outside = Path.Combine(_root, $"outside-{linkLocation}");
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "Venue.dm7f"), "private");

        string export;
        if (linkLocation == "root")
        {
            export = Path.Combine(_root, "linked-root");
            Directory.CreateSymbolicLink(export, outside);
        }
        else if (linkLocation == "ancestor")
        {
            var outsideExport = Path.Combine(outside, "export");
            Directory.CreateDirectory(outsideExport);
            await File.WriteAllTextAsync(
                Path.Combine(outsideExport, "Venue.dm7f"),
                "private");
            var linkedAncestor = Path.Combine(_root, "linked-ancestor");
            Directory.CreateSymbolicLink(linkedAncestor, outside);
            export = Path.Combine(linkedAncestor, "export");
        }
        else
        {
            export = Path.Combine(_root, "export-with-link");
            Directory.CreateDirectory(export);
            await File.WriteAllTextAsync(Path.Combine(export, "Venue.dm7f"), "settings");
            Directory.CreateSymbolicLink(Path.Combine(export, "linked"), outside);
        }

        await Assert.ThrowsAsync<IOException>(() =>
            CreateDm7(export).DiscoverAsync(
                new DiscoveryRequest(export),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_non_regular_entry()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var export = _socketRoot;
        Directory.CreateDirectory(export);
        await File.WriteAllTextAsync(Path.Combine(export, "Venue.dm7f"), "settings");
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(Path.Combine(export, "local.socket")));

        await Assert.ThrowsAsync<IOException>(() =>
            CreateDm7(export).DiscoverAsync(
                new DiscoveryRequest(export),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_excludes_unrelated_sibling_data()
    {
        var export = Path.Combine(_root, "selected");
        var sibling = Path.Combine(_root, "private-sibling");
        Directory.CreateDirectory(export);
        Directory.CreateDirectory(sibling);
        await File.WriteAllTextAsync(Path.Combine(export, "Venue.dm7f"), "settings");
        await File.WriteAllTextAsync(Path.Combine(sibling, "unrelated.txt"), "unrelated");

        var result = await CreateDm7(export).DiscoverAsync(
            new DiscoveryRequest(export),
            CancellationToken.None);

        Assert.Equal("Venue.dm7f", Assert.Single(result.Files).RelativePath);
    }

    [Fact]
    public async Task Discovery_fails_instead_of_truncating()
    {
        var export = Path.Combine(_root, "file-limit");
        Directory.CreateDirectory(export);
        await File.WriteAllTextAsync(Path.Combine(export, "Venue.dm7f"), "settings");
        await File.WriteAllTextAsync(Path.Combine(export, "companion.txt"), "companion");

        await Assert.ThrowsAsync<IOException>(() =>
            CreateDm7(export).DiscoverAsync(
                new DiscoveryRequest(export, MaxFiles: 1),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_empty_directory_topology()
    {
        var export = Path.Combine(_root, "empty-directory");
        Directory.CreateDirectory(Path.Combine(export, "empty"));
        await File.WriteAllTextAsync(Path.Combine(export, "Venue.dm7f"), "settings");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateDm7(export).DiscoverAsync(
                new DiscoveryRequest(export),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_overlarge_file_without_reading_it()
    {
        var export = Path.Combine(_root, "large-file");
        Directory.CreateDirectory(export);
        await using (var stream = new FileStream(
            Path.Combine(export, "Venue.dm7f"),
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        {
            stream.SetLength(YamahaSettingsExportDiscoveryPluginBase.MaximumFileBytes + 1);
        }

        await Assert.ThrowsAsync<IOException>(() =>
            CreateDm7(export).DiscoverAsync(
                new DiscoveryRequest(export),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_honors_cancellation_without_partial_result()
    {
        var export = Path.Combine(_root, "cancelled");
        Directory.CreateDirectory(export);
        await File.WriteAllTextAsync(Path.Combine(export, "Venue.dm7f"), "settings");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateDm7(export).DiscoverAsync(
                new DiscoveryRequest(export),
                cancellation.Token));
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("path")]
    [InlineData("file-bytes")]
    [InlineData("total-bytes")]
    public async Task Capture_enforces_each_explicit_structural_and_byte_bound(string bound)
    {
        var export = Path.Combine(_root, $"bound-{bound}");
        Directory.CreateDirectory(export);
        await File.WriteAllTextAsync(Path.Combine(export, "Venue.dm7f"), "12345");
        var bounds = new YamahaCaptureBounds(10, 10, 100, 100, 100);
        switch (bound)
        {
            case "directory":
                Directory.CreateDirectory(Path.Combine(export, "one"));
                Directory.CreateDirectory(Path.Combine(export, "two"));
                await File.WriteAllTextAsync(Path.Combine(export, "one", "companion.txt"), "x");
                await File.WriteAllTextAsync(Path.Combine(export, "two", "companion.txt"), "x");
                bounds = bounds with { MaximumDirectoryCount = 1 };
                break;
            case "path":
                bounds = bounds with { MaximumRelativePathLength = 5 };
                break;
            case "file-bytes":
                bounds = bounds with { MaximumFileBytes = 4 };
                break;
            case "total-bytes":
                await File.WriteAllTextAsync(Path.Combine(export, "companion.txt"), "12345");
                bounds = bounds with { MaximumTotalBytes = 9 };
                break;
        }

        await Assert.ThrowsAnyAsync<Exception>(() =>
            YamahaSettingsExportDiscoveryPluginBase.CaptureSnapshotAsync(
                YamahaDm7SettingsExportDiscoveryPlugin.PluginId,
                export,
                bounds,
                CancellationToken.None));
    }

    [Fact]
    public void Production_time_and_capture_bounds_remain_explicit()
    {
        Assert.Equal(4_096, YamahaSettingsExportDiscoveryPluginBase.MaximumFileLimit);
        Assert.Equal(1_024, YamahaSettingsExportDiscoveryPluginBase.MaximumDirectoryLimit);
        Assert.Equal(1_024, YamahaSettingsExportDiscoveryPluginBase.MaximumRelativePathLength);
        Assert.Equal(2L * 1_024 * 1_024 * 1_024,
            YamahaSettingsExportDiscoveryPluginBase.MaximumFileBytes);
        Assert.Equal(16L * 1_024 * 1_024 * 1_024,
            YamahaSettingsExportDiscoveryPluginBase.MaximumTotalBytes);
        Assert.Equal(TimeSpan.FromMinutes(2),
            YamahaSettingsExportDiscoveryPluginBase.MaximumCaptureDuration);
        Assert.Equal(TimeSpan.FromMinutes(15),
            YamahaSettingsExportDiscoveryPluginBase.MaximumPackageDuration);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        if (Directory.Exists(_socketRoot))
        {
            Directory.Delete(_socketRoot, recursive: true);
        }
    }

    private YamahaDm7SettingsExportDiscoveryPlugin CreateDm7(string root) =>
        new(CreateOptions(dm7Roots: [root]), TimeProvider.System);

    private YamahaRivageSettingsExportDiscoveryPlugin CreateRivage(string root) =>
        new(CreateOptions(rivageRoots: [root]), TimeProvider.System);

    private YamahaClQlSettingsExportDiscoveryPlugin CreateClQl(string root) =>
        new(CreateOptions(clQlRoots: [root]), TimeProvider.System);

    private YamahaTfSettingsExportDiscoveryPlugin CreateTf(string root) =>
        new(CreateOptions(tfRoots: [root]), TimeProvider.System);

    private YamahaDm3SettingsExportDiscoveryPlugin CreateDm3(string root) =>
        new(CreateOptions(dm3Roots: [root]), TimeProvider.System);

    private static IOptions<AgentOptions> CreateOptions(
        IReadOnlyList<string>? dm7Roots = null,
        IReadOnlyList<string>? rivageRoots = null,
        IReadOnlyList<string>? clQlRoots = null,
        IReadOnlyList<string>? tfRoots = null,
        IReadOnlyList<string>? dm3Roots = null) =>
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            YamahaDm7SettingsExportRoots = dm7Roots ?? [],
            YamahaRivageSettingsExportRoots = rivageRoots ?? [],
            YamahaClQlSettingsExportRoots = clQlRoots ?? [],
            YamahaTfSettingsExportRoots = tfRoots ?? [],
            YamahaDm3SettingsExportRoots = dm3Roots ?? []
        });
}
