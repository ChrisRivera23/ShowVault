using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using ShowVault.Agent.Recovery;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class MaLightingShowExportDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-grandma-export-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GrandMa2_captures_exact_versioned_show_export_with_distinct_profile()
    {
        var shows = Path.Combine(_root, "gma2", "3.9", "shows");
        Directory.CreateDirectory(Path.Combine(shows, "venue"));
        var content = Encoding.UTF8.GetBytes("grandMA2-show");
        await File.WriteAllBytesAsync(Path.Combine(shows, "venue", "Main.show"), content);

        var result = await CreateGrandMa2(shows).DiscoverAsync(
            new DiscoveryRequest(shows),
            CancellationToken.None);

        Assert.Equal(GrandMa2ShowExportDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Equal("1.0.0", result.PluginVersion);
        var file = Assert.Single(result.Files);
        Assert.Equal(Path.Combine("venue", "Main.show"), file.RelativePath);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(content)), file.Sha256);
    }

    [Fact]
    public async Task GrandMa3_captures_only_the_exact_authorized_export_directory()
    {
        var shows = Path.Combine(_root, "grandMA3", "shared", "shows");
        var backups = Path.Combine(_root, "grandMA3", "shared", "backups");
        var library = Path.Combine(_root, "grandMA3", "gma3_library", "users");
        Directory.CreateDirectory(shows);
        Directory.CreateDirectory(backups);
        Directory.CreateDirectory(library);
        await File.WriteAllTextAsync(Path.Combine(shows, "Venue.show"), "show");
        await File.WriteAllTextAsync(Path.Combine(backups, "Private.backup"), "backup");
        await File.WriteAllTextAsync(Path.Combine(library, "user.xml"), "private");

        var result = await CreateGrandMa3(shows).DiscoverAsync(
            new DiscoveryRequest(shows),
            CancellationToken.None);

        Assert.Equal(GrandMa3ShowExportDiscoveryPlugin.PluginId, result.PluginId);
        var file = Assert.Single(result.Files);
        Assert.Equal("Venue.show", file.RelativePath);
        Assert.DoesNotContain(result.Files, candidate => candidate.RelativePath.Contains("Private"));
        Assert.DoesNotContain(result.Files, candidate => candidate.RelativePath.Contains("user"));
    }

    [Fact]
    public async Task GrandMa3_accepts_exact_backups_export_as_a_separate_root_choice()
    {
        var backups = Path.Combine(_root, "grandMA3", "shared", "backups");
        Directory.CreateDirectory(backups);
        await File.WriteAllTextAsync(Path.Combine(backups, "Venue.backup"), "backup");

        var result = await CreateGrandMa3(backups).DiscoverAsync(
            new DiscoveryRequest(backups),
            CancellationToken.None);

        Assert.Equal("Venue.backup", Assert.Single(result.Files).RelativePath);
    }

    [Fact]
    public async Task Discovery_rejects_non_exact_authorized_parent()
    {
        var shared = Path.Combine(_root, "grandMA3", "shared");
        var shows = Path.Combine(shared, "shows");
        Directory.CreateDirectory(shows);
        await File.WriteAllTextAsync(Path.Combine(shows, "Venue.show"), "show");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateGrandMa3(shows).DiscoverAsync(
                new DiscoveryRequest(shared),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_unrecognized_case_and_structure()
    {
        var shows = Path.Combine(_root, "grandma3", "shared", "Shows");
        Directory.CreateDirectory(shows);
        await File.WriteAllTextAsync(Path.Combine(shows, "Venue.show"), "show");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateGrandMa3(shows).DiscoverAsync(
                new DiscoveryRequest(shows),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_a_linked_export_root()
    {
        var outside = Path.Combine(_root, "outside-root");
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "Private.show"), "private");
        var shows = Path.Combine(_root, "grandMA3", "shared", "shows");
        Directory.CreateDirectory(Path.GetDirectoryName(shows)!);
        Directory.CreateSymbolicLink(shows, outside);

        await Assert.ThrowsAsync<IOException>(() =>
            CreateGrandMa3(shows).DiscoverAsync(
                new DiscoveryRequest(shows),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_a_linked_ancestor()
    {
        var outside = Path.Combine(_root, "outside-ancestor");
        var outsideShows = Path.Combine(outside, "grandMA3", "shared", "shows");
        Directory.CreateDirectory(outsideShows);
        await File.WriteAllTextAsync(Path.Combine(outsideShows, "Private.show"), "private");
        var linkedAncestor = Path.Combine(_root, "linked");
        Directory.CreateSymbolicLink(linkedAncestor, outside);
        var shows = Path.Combine(linkedAncestor, "grandMA3", "shared", "shows");

        await Assert.ThrowsAsync<IOException>(() =>
            CreateGrandMa3(shows).DiscoverAsync(
                new DiscoveryRequest(shows),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_a_linked_descendant()
    {
        var shows = Path.Combine(_root, "grandMA3", "shared", "shows");
        var outside = Path.Combine(_root, "outside-descendant");
        Directory.CreateDirectory(shows);
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(shows, "Venue.show"), "show");
        await File.WriteAllTextAsync(Path.Combine(outside, "Private.show"), "private");
        Directory.CreateSymbolicLink(Path.Combine(shows, "linked"), outside);

        await Assert.ThrowsAsync<IOException>(() =>
            CreateGrandMa3(shows).DiscoverAsync(
                new DiscoveryRequest(shows),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_fails_instead_of_truncating()
    {
        var shows = Path.Combine(_root, "gma2", "3.9", "shows");
        Directory.CreateDirectory(shows);
        await File.WriteAllTextAsync(Path.Combine(shows, "one.show"), "one");
        await File.WriteAllTextAsync(Path.Combine(shows, "two.show"), "two");

        await Assert.ThrowsAsync<IOException>(() =>
            CreateGrandMa2(shows).DiscoverAsync(
                new DiscoveryRequest(shows, MaxFiles: 1),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_empty_directory_topology()
    {
        var shows = Path.Combine(_root, "grandMA3", "shared", "shows");
        Directory.CreateDirectory(Path.Combine(shows, "empty"));
        await File.WriteAllTextAsync(Path.Combine(shows, "Venue.show"), "show");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateGrandMa3(shows).DiscoverAsync(
                new DiscoveryRequest(shows),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_overlarge_file_without_reading_it()
    {
        var shows = Path.Combine(_root, "grandMA3", "shared", "shows");
        Directory.CreateDirectory(shows);
        await using (var stream = new FileStream(
            Path.Combine(shows, "large.show"),
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        {
            stream.SetLength(MaLightingShowExportDiscoveryPluginBase.MaximumFileBytes + 1);
        }

        await Assert.ThrowsAsync<IOException>(() =>
            CreateGrandMa3(shows).DiscoverAsync(
                new DiscoveryRequest(shows),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_honors_cancellation_without_partial_result()
    {
        var shows = Path.Combine(_root, "gma2", "shows");
        Directory.CreateDirectory(shows);
        await File.WriteAllTextAsync(Path.Combine(shows, "Venue.show"), "show");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateGrandMa2(shows).DiscoverAsync(
                new DiscoveryRequest(shows),
                cancellation.Token));
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

    private static IOptions<AgentOptions> CreateOptions(
        IReadOnlyList<string>? grandMa2Roots = null,
        IReadOnlyList<string>? grandMa3Roots = null) =>
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            GrandMa2ShowExportRoots = grandMa2Roots ?? [],
            GrandMa3ShowExportRoots = grandMa3Roots ?? []
        });
}
