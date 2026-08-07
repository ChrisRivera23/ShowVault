using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using ShowVault.Agent.Recovery;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class RecoveryPackageVerifierTests : IAsyncLifetime
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "showvault-verifier-tests",
        Guid.NewGuid().ToString("N"));

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_testRoot);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Valid_package_passes_structural_and_cryptographic_verification()
    {
        var package = await CreatePackageAsync();

        var result = await new RecoveryPackageVerifier().VerifyAsync(
            Guid.NewGuid(),
            package.Manifest.AgentId,
            package.PackageId,
            package.PackagePath,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.True(result.Passed);
        Assert.Collection(
            result.Levels,
            level => Assert.True(level.Passed),
            level => Assert.True(level.Passed));
    }

    [Fact]
    public async Task Modified_content_fails_cryptographic_verification()
    {
        var package = await CreatePackageAsync();
        var contentPath = Path.Combine(
            package.PackagePath,
            RecoveryPackageFormat.ContentDirectoryName,
            "main.show");
        File.SetAttributes(contentPath, FileAttributes.Normal);
        await File.WriteAllTextAsync(contentPath, "tampered-content");

        var result = await new RecoveryPackageVerifier().VerifyAsync(
            Guid.NewGuid(),
            package.Manifest.AgentId,
            package.PackageId,
            package.PackagePath,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.False(result.Passed);
        Assert.True(result.Levels.Single(level => level.Level == "structural").Passed);
        var cryptographic = result.Levels.Single(level => level.Level == "cryptographic");
        Assert.False(cryptographic.Passed);
        Assert.Contains(
            cryptographic.Evidence,
            evidence => evidence.Contains("mismatch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Unexpected_content_fails_exact_layout_verification()
    {
        var package = await CreatePackageAsync();
        await File.WriteAllTextAsync(Path.Combine(
            package.PackagePath,
            RecoveryPackageFormat.ContentDirectoryName,
            "unexpected.txt"), "unexpected");

        var result = await new RecoveryPackageVerifier().VerifyAsync(
            Guid.NewGuid(),
            package.Manifest.AgentId,
            package.PackageId,
            package.PackagePath,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.False(result.Passed);
        var structural = result.Levels.Single(level => level.Level == "structural");
        Assert.False(structural.Passed);
        Assert.Contains(
            structural.Evidence,
            evidence => evidence.Contains("Unexpected", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Malformed_manifest_is_reported_as_failed_evidence()
    {
        var package = await CreatePackageAsync();
        var manifestPath = Path.Combine(
            package.PackagePath,
            RecoveryPackageFormat.ManifestFileName);
        File.SetAttributes(manifestPath, FileAttributes.Normal);
        await File.WriteAllTextAsync(manifestPath, "{}");

        var result = await new RecoveryPackageVerifier().VerifyAsync(
            Guid.NewGuid(),
            package.Manifest.AgentId,
            package.PackageId,
            package.PackagePath,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.False(result.Passed);
        Assert.False(result.Levels.Single(level => level.Level == "structural").Passed);
        Assert.False(result.Levels.Single(level => level.Level == "cryptographic").Passed);
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_testRoot))
        {
            foreach (var path in Directory.EnumerateFiles(
                _testRoot,
                "*",
                SearchOption.AllDirectories))
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }

            Directory.Delete(_testRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    private async Task<CreatedRecoveryPackage> CreatePackageAsync()
    {
        var sourceRoot = Path.Combine(_testRoot, $"source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceRoot);
        var content = Encoding.UTF8.GetBytes("console configuration");
        await File.WriteAllBytesAsync(Path.Combine(sourceRoot, "main.show"), content);
        var discovery = new DiscoveryResult(
            FileSystemDiscoveryPlugin.PluginId,
            "0.1.0",
            sourceRoot,
            DateTimeOffset.UtcNow,
            false,
            [
                new DiscoveryFile(
                    "main.show",
                    content.Length,
                    DateTimeOffset.UtcNow,
                    Convert.ToHexStringLower(SHA256.HashData(content)))
            ]);
        var writer = new RecoveryPackageWriter(Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            PackageDirectory = Path.Combine(_testRoot, "packages")
        }));
        return await writer.CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            discovery,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
    }
}
