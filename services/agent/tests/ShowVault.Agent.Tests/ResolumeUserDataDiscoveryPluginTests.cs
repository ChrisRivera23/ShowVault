using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using ShowVault.Agent.Recovery;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class ResolumeUserDataDiscoveryPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "showvault-resolume-user-data-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Discovery_captures_only_supported_categories_with_distinct_profile()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Compositions"));
        Directory.CreateDirectory(Path.Combine(_root, "Presets", "Advanced Output"));
        Directory.CreateDirectory(Path.Combine(_root, "Unrelated Private Notes"));
        var composition = Encoding.UTF8.GetBytes("composition");
        await File.WriteAllBytesAsync(
            Path.Combine(_root, "Compositions", "Venue.avc"),
            composition);
        await File.WriteAllTextAsync(
            Path.Combine(_root, "Presets", "Advanced Output", "Main.xml"),
            "output");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "Unrelated Private Notes", "notes.txt"),
            "private");

        var result = await CreatePlugin().DiscoverAsync(
            new DiscoveryRequest(_root),
            CancellationToken.None);

        Assert.Equal(ResolumeUserDataDiscoveryPlugin.PluginId, result.PluginId);
        Assert.Equal("1.0.0", result.PluginVersion);
        Assert.Equal(2, result.Files.Count);
        Assert.Contains(
            result.Files,
            file => file.RelativePath == Path.Combine("Compositions", "Venue.avc") &&
                file.Sha256 == Convert.ToHexStringLower(SHA256.HashData(composition)));
        Assert.DoesNotContain(result.Files, file => file.RelativePath.Contains("Private", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Discovery_does_not_open_unknown_linked_sibling()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Preferences"));
        await File.WriteAllTextAsync(
            Path.Combine(_root, "Preferences", "Arena.xml"),
            "preferences");
        var outside = Path.Combine(
            Path.GetTempPath(),
            "showvault-resolume-unknown-outside",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "secret.txt"), "secret");
        var linkedSibling = Path.Combine(_root, "Unrelated");
        Directory.CreateSymbolicLink(linkedSibling, outside);
        try
        {
            var result = await CreatePlugin().DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None);

            Assert.Single(result.Files);
            Assert.DoesNotContain(result.Files, file => file.RelativePath.Contains("secret", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(linkedSibling);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task Discovery_rejects_empty_supported_category_and_unrelated_only_content()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Compositions"));
        Directory.CreateDirectory(Path.Combine(_root, "Unrelated"));
        await File.WriteAllTextAsync(Path.Combine(_root, "Unrelated", "notes.txt"), "notes");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin().DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_empty_selected_sibling_alongside_content()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Preferences"));
        Directory.CreateDirectory(Path.Combine(_root, "Presets"));
        await File.WriteAllTextAsync(
            Path.Combine(_root, "Preferences", "Arena.xml"),
            "preferences");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin().DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_unrecognized_case_as_safe_locale_negative()
    {
        Directory.CreateDirectory(Path.Combine(_root, "compositions"));
        await File.WriteAllTextAsync(
            Path.Combine(_root, "compositions", "Venue.avc"),
            "composition");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin().DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_selected_category_link()
    {
        Directory.CreateDirectory(_root);
        var outside = Path.Combine(
            Path.GetTempPath(),
            "showvault-resolume-category-outside",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "Venue.avc"), "composition");
        var link = Path.Combine(_root, "Compositions");
        Directory.CreateSymbolicLink(link, outside);
        try
        {
            await Assert.ThrowsAsync<IOException>(() =>
                CreatePlugin().DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
        }
        finally
        {
            Directory.Delete(link);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task Discovery_rejects_overlarge_file()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Preferences"));
        await using (var stream = new FileStream(
            Path.Combine(_root, "Preferences", "large.bin"),
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        {
            stream.SetLength(ResolumeUserDataDiscoveryPlugin.MaximumFileBytes + 1);
        }

        await Assert.ThrowsAsync<IOException>(() =>
            CreatePlugin().DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_fails_instead_of_truncating_at_requested_file_limit()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Preferences"));
        await File.WriteAllTextAsync(Path.Combine(_root, "Preferences", "one.xml"), "one");
        await File.WriteAllTextAsync(Path.Combine(_root, "Preferences", "two.xml"), "two");

        await Assert.ThrowsAsync<IOException>(() =>
            CreatePlugin().DiscoverAsync(new DiscoveryRequest(_root, 1), CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_over_directory_bound()
    {
        var category = Path.Combine(_root, "Presets");
        Directory.CreateDirectory(category);
        for (var index = 0; index < ResolumeUserDataDiscoveryPlugin.MaximumDirectoryLimit; index++)
        {
            Directory.CreateDirectory(Path.Combine(category, $"preset-{index:D3}"));
        }

        await Assert.ThrowsAsync<IOException>(() =>
            CreatePlugin().DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_overlong_relative_path()
    {
        var directory = Path.Combine(_root, "Presets");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "a-preset-name-longer-than-test-bound.xml"),
            "preset");

        await Assert.ThrowsAsync<IOException>(() =>
            StableSourceSnapshot.CaptureSelectedRootDirectoriesAsync(
                _root,
                ResolumeUserDataDiscoveryPlugin.SupportedCategories,
                ResolumeUserDataDiscoveryPlugin.MaximumFileLimit,
                ResolumeUserDataDiscoveryPlugin.MaximumDirectoryLimit,
                maximumRelativePathLength: 32,
                ResolumeUserDataDiscoveryPlugin.MaximumFileBytes,
                ResolumeUserDataDiscoveryPlugin.MaximumTotalBytes,
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_over_total_byte_bound()
    {
        var category = Path.Combine(_root, "Preferences");
        Directory.CreateDirectory(category);
        var fullFiles = ResolumeUserDataDiscoveryPlugin.MaximumTotalBytes /
            ResolumeUserDataDiscoveryPlugin.MaximumFileBytes;
        for (var index = 0; index < fullFiles; index++)
        {
            await using var stream = new FileStream(
                Path.Combine(category, $"full-{index:D2}.bin"),
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            stream.SetLength(ResolumeUserDataDiscoveryPlugin.MaximumFileBytes);
        }

        await File.WriteAllTextAsync(Path.Combine(category, "overflow.txt"), "x");

        await Assert.ThrowsAsync<IOException>(() =>
            CreatePlugin().DiscoverAsync(new DiscoveryRequest(_root), CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_rejects_ambiguous_profile_configuration()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Preferences"));
        await File.WriteAllTextAsync(
            Path.Combine(_root, "Preferences", "Arena.xml"),
            "preferences");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlugin(portableRoots: [_root]).DiscoverAsync(
                new DiscoveryRequest(_root),
                CancellationToken.None));
    }

    [Fact]
    public async Task Discovery_honors_cancellation_without_partial_result()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Preferences"));
        await File.WriteAllTextAsync(
            Path.Combine(_root, "Preferences", "Arena.xml"),
            "preferences");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreatePlugin().DiscoverAsync(new DiscoveryRequest(_root), cancellation.Token));
    }

    [Fact]
    public async Task Discovery_rejects_non_exact_authorized_root()
    {
        var child = Path.Combine(_root, "Compositions");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "Venue.avc"), "composition");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreatePlugin().DiscoverAsync(new DiscoveryRequest(child), CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ResolumeUserDataDiscoveryPlugin CreatePlugin(
        IReadOnlyList<string>? portableRoots = null) =>
        new(
            Options.Create(new AgentOptions
            {
                ControlPlaneUri = new Uri("https://control.test"),
                Name = "Test Agent",
                ResolumeDiscoveryRoots = portableRoots ?? [],
                ResolumeUserDataRoots = [_root]
            }),
            TimeProvider.System);
}
