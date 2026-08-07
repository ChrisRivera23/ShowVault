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

    private RecoveryPackageWriter CreateWriter() => new(Options.Create(new AgentOptions
    {
        ControlPlaneUri = new Uri("https://control.test"),
        Name = "Test Agent",
        PackageDirectory = Path.Combine(_testRoot, "packages")
    }));

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
}
