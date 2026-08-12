using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using ShowVault.Agent.Recovery;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class RecoveryPackageWriterTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "showvault-package-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Package_is_content_addressed_immutable_and_idempotent()
    {
        var sourceRoot = Path.Combine(_testRoot, "source");
        Directory.CreateDirectory(sourceRoot);
        var content = Encoding.UTF8.GetBytes("console configuration");
        await File.WriteAllBytesAsync(Path.Combine(sourceRoot, "main.show"), content);
        var discovery = CreateDiscovery(
            sourceRoot,
            new DiscoveryFile(
                "main.show",
                content.Length,
                DateTimeOffset.UtcNow,
                Convert.ToHexStringLower(SHA256.HashData(content))));
        var writer = CreateWriter();
        var createdAt = DateTimeOffset.UtcNow;

        var first = await writer.CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            discovery,
            createdAt,
            CancellationToken.None);
        var second = await writer.CreateAsync(
            first.Manifest.AgentId,
            first.Manifest.DiscoveryCommandId,
            discovery,
            createdAt,
            CancellationToken.None);

        Assert.Equal(first.PackageId, second.PackageId);
        Assert.Equal(64, first.PackageId.Length);
        Assert.Equal(first.PackagePath, second.PackagePath);
        Assert.True(File.Exists(Path.Combine(
            first.PackagePath,
            RecoveryPackageFormat.ManifestFileName)));
        Assert.Equal(
            content,
            await File.ReadAllBytesAsync(Path.Combine(
                first.PackagePath,
                RecoveryPackageFormat.ContentDirectoryName,
                "main.show")));
        Assert.True((File.GetAttributes(Path.Combine(
            first.PackagePath,
            RecoveryPackageFormat.ManifestFileName)) & FileAttributes.ReadOnly) != 0);
    }

    [Fact]
    public async Task Package_fails_without_publication_when_source_changed_after_discovery()
    {
        var sourceRoot = Path.Combine(_testRoot, "changed-source");
        Directory.CreateDirectory(sourceRoot);
        var sourcePath = Path.Combine(sourceRoot, "main.show");
        await File.WriteAllTextAsync(sourcePath, "original");
        var original = Encoding.UTF8.GetBytes("original");
        var discovery = CreateDiscovery(
            sourceRoot,
            new DiscoveryFile(
                "main.show",
                original.Length,
                DateTimeOffset.UtcNow,
                Convert.ToHexStringLower(SHA256.HashData(original))));
        await File.WriteAllTextAsync(sourcePath, "changed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateWriter().CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            discovery,
            DateTimeOffset.UtcNow,
            CancellationToken.None));

        var packageRoot = Path.Combine(_testRoot, "packages");
        Assert.Empty(Directory.EnumerateDirectories(packageRoot));
    }

    [Fact]
    public async Task Resolume_package_rejects_a_late_unmanifested_source_file()
    {
        var sourceRoot = Path.Combine(_testRoot, "resolume-late-source");
        Directory.CreateDirectory(sourceRoot);
        var content = Encoding.UTF8.GetBytes("composition");
        await File.WriteAllBytesAsync(Path.Combine(sourceRoot, "Venue.avc"), content);
        var discovery = CreateDiscovery(
            sourceRoot,
            new DiscoveryFile(
                "Venue.avc",
                content.Length,
                DateTimeOffset.UtcNow,
                Convert.ToHexStringLower(SHA256.HashData(content)))) with
        {
            PluginId = ResolumeDiscoveryPlugin.PluginId
        };
        var writer = new RecoveryPackageWriter(
            CreateOptions(),
            new CallbackSourceSnapshotRaceProbe((point, _) =>
            {
                if (point == SourceSnapshotRacePoint.SnapshotCaptured)
                {
                    File.WriteAllText(Path.Combine(sourceRoot, "late-added.mov"), "late media");
                }
            }));

        await Assert.ThrowsAnyAsync<IOException>(() => writer.CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            discovery,
            DateTimeOffset.UtcNow,
            CancellationToken.None));

        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(_testRoot, "packages")));
    }

    [Fact]
    public async Task Resolume_package_rejects_a_late_source_file_replacement()
    {
        var sourceRoot = Path.Combine(_testRoot, "resolume-replaced-source");
        Directory.CreateDirectory(sourceRoot);
        var sourcePath = Path.Combine(sourceRoot, "Venue.avc");
        var content = Encoding.UTF8.GetBytes("composition");
        await File.WriteAllBytesAsync(sourcePath, content);
        var discovery = CreateDiscovery(
            sourceRoot,
            new DiscoveryFile(
                "Venue.avc",
                content.Length,
                DateTimeOffset.UtcNow,
                Convert.ToHexStringLower(SHA256.HashData(content)))) with
        {
            PluginId = ResolumeDiscoveryPlugin.PluginId
        };
        var writer = new RecoveryPackageWriter(
            CreateOptions(),
            new CallbackSourceSnapshotRaceProbe((point, relativePath) =>
            {
                if (point == SourceSnapshotRacePoint.SourceCopyStarted &&
                    relativePath == "Venue.avc")
                {
                    var replacement = Path.Combine(sourceRoot, "replacement.tmp");
                    File.WriteAllBytes(replacement, content);
                    File.Move(replacement, sourcePath, overwrite: true);
                }
            }));

        await Assert.ThrowsAnyAsync<IOException>(() => writer.CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            discovery,
            DateTimeOffset.UtcNow,
            CancellationToken.None));

        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(_testRoot, "packages")));
    }

    [Fact]
    public async Task Package_rejects_untrusted_paths_and_truncated_inventories()
    {
        var sourceRoot = Path.Combine(_testRoot, "unsafe-source");
        Directory.CreateDirectory(sourceRoot);
        var unsafeDiscovery = CreateDiscovery(
            sourceRoot,
            new DiscoveryFile("../secret", 1, DateTimeOffset.UtcNow, new string('0', 64)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateWriter().CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            unsafeDiscovery,
            DateTimeOffset.UtcNow,
            CancellationToken.None));

        var truncated = unsafeDiscovery with { Truncated = true, Files = [] };
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateWriter().CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            truncated,
            DateTimeOffset.UtcNow,
            CancellationToken.None));
    }

    [Theory]
    [InlineData("altered")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("linked")]
    public async Task Package_replay_rejects_damaged_or_unexpected_content(string scenario)
    {
        var sourceRoot = Path.Combine(_testRoot, $"replay-source-{scenario}");
        Directory.CreateDirectory(sourceRoot);
        var sourcePath = Path.Combine(sourceRoot, "main.show");
        var content = Encoding.UTF8.GetBytes("expected");
        await File.WriteAllBytesAsync(sourcePath, content);
        var discovery = CreateDiscovery(
            sourceRoot,
            new DiscoveryFile(
                "main.show",
                content.Length,
                DateTimeOffset.UtcNow,
                Convert.ToHexStringLower(SHA256.HashData(content))));
        var writer = CreateWriter();
        var agentId = Guid.NewGuid();
        var discoveryCommandId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var package = await writer.CreateAsync(
            agentId,
            discoveryCommandId,
            discovery,
            createdAt,
            CancellationToken.None);
        var packagedFile = Path.Combine(
            package.PackagePath,
            RecoveryPackageFormat.ContentDirectoryName,
            "main.show");

        switch (scenario)
        {
            case "altered":
                File.SetAttributes(packagedFile, FileAttributes.Normal);
                await File.WriteAllTextAsync(packagedFile, "tampered");
                break;
            case "missing":
                File.SetAttributes(packagedFile, FileAttributes.Normal);
                File.Delete(packagedFile);
                break;
            case "extra":
                await File.WriteAllTextAsync(
                    Path.Combine(package.PackagePath, "unexpected.txt"),
                    "unexpected");
                break;
            case "linked":
                File.SetAttributes(packagedFile, FileAttributes.Normal);
                File.Delete(packagedFile);
                File.CreateSymbolicLink(packagedFile, sourcePath);
                break;
            default:
                throw new InvalidOperationException("Unknown test scenario.");
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.CreateAsync(
            agentId,
            discoveryCommandId,
            discovery,
            createdAt,
            CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_creation_returns_one_fully_valid_shared_package()
    {
        var sourceRoot = Path.Combine(_testRoot, "concurrent-source");
        Directory.CreateDirectory(sourceRoot);
        var content = new byte[1_048_576];
        Random.Shared.NextBytes(content);
        await File.WriteAllBytesAsync(Path.Combine(sourceRoot, "main.show"), content);
        var discovery = CreateDiscovery(
            sourceRoot,
            new DiscoveryFile(
                "main.show",
                content.Length,
                DateTimeOffset.UtcNow,
                Convert.ToHexStringLower(SHA256.HashData(content))));
        var writer = CreateWriter();
        var agentId = Guid.NewGuid();
        var discoveryCommandId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var packages = await Task.WhenAll(
            writer.CreateAsync(
                agentId,
                discoveryCommandId,
                discovery,
                createdAt,
                CancellationToken.None),
            writer.CreateAsync(
                agentId,
                discoveryCommandId,
                discovery,
                createdAt,
                CancellationToken.None));

        Assert.Equal(packages[0].PackageId, packages[1].PackageId);
        Assert.Equal(packages[0].PackagePath, packages[1].PackagePath);
        Assert.Equal(
            content,
            await File.ReadAllBytesAsync(Path.Combine(
                packages[0].PackagePath,
                RecoveryPackageFormat.ContentDirectoryName,
                "main.show")));
    }

    [Fact]
    public async Task Package_rejects_a_linked_package_directory()
    {
        var sourceRoot = Path.Combine(_testRoot, "linked-package-source");
        Directory.CreateDirectory(sourceRoot);
        var content = Encoding.UTF8.GetBytes("expected");
        await File.WriteAllBytesAsync(Path.Combine(sourceRoot, "main.show"), content);
        var discovery = CreateDiscovery(
            sourceRoot,
            new DiscoveryFile(
                "main.show",
                content.Length,
                DateTimeOffset.UtcNow,
                Convert.ToHexStringLower(SHA256.HashData(content))));
        var outsideRoot = Path.Combine(
            Path.GetTempPath(),
            "showvault-linked-package-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideRoot);
        var packageDirectory = Path.Combine(_testRoot, "packages");
        Directory.CreateSymbolicLink(packageDirectory, outsideRoot);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateWriter().CreateAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                discovery,
                DateTimeOffset.UtcNow,
                CancellationToken.None));
            Assert.Empty(Directory.EnumerateFileSystemEntries(outsideRoot));
        }
        finally
        {
            Directory.Delete(packageDirectory);
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    public void Dispose()
    {
        if (!Directory.Exists(_testRoot))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(_testRoot, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(_testRoot, recursive: true);
    }

    private IOptions<AgentOptions> CreateOptions() => Options.Create(new AgentOptions
    {
        ControlPlaneUri = new Uri("https://control.test"),
        Name = "Test Agent",
        PackageDirectory = Path.Combine(_testRoot, "packages")
    });

    private RecoveryPackageWriter CreateWriter() => new(CreateOptions());

    private static DiscoveryResult CreateDiscovery(
        string rootPath,
        params DiscoveryFile[] files) =>
        new(
            FileSystemDiscoveryPlugin.PluginId,
            "0.1.0",
            rootPath,
            DateTimeOffset.UtcNow,
            false,
            files);

    private sealed class CallbackSourceSnapshotRaceProbe(
        Action<SourceSnapshotRacePoint, string> callback) : ISourceSnapshotRaceProbe
    {
        public void Reached(SourceSnapshotRacePoint point, string relativePath) =>
            callback(point, relativePath);
    }
}
