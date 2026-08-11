using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

        var result = await CreateVerifier().VerifyAsync(
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

        var result = await CreateVerifier().VerifyAsync(
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

        var result = await CreateVerifier().VerifyAsync(
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

        var result = await CreateVerifier().VerifyAsync(
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

    [Fact]
    public async Task Manifest_missing_required_source_metadata_fails_verification()
    {
        var package = await CreatePackageAsync();
        var incompleteManifest = package.Manifest with
        {
            Source = new RecoveryPackageSource(null!, null!, null!, null, null)
        };
        package = await RewriteManifestAsync(package, incompleteManifest);

        var result = await CreateVerifier().VerifyAsync(
            Guid.NewGuid(),
            package.Manifest.AgentId,
            package.PackageId,
            package.PackagePath,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.False(result.Passed);
        Assert.Contains(
            result.Levels.Single(level => level.Level == "structural").Evidence,
            evidence => evidence.Contains("source metadata", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Package_outside_configured_store_fails_verification()
    {
        var package = await CreatePackageAsync();
        var outsideRoot = Path.Combine(_testRoot, "outside-package-store");
        Directory.CreateDirectory(outsideRoot);
        var outsidePath = Path.Combine(outsideRoot, package.PackageId);
        Directory.Move(package.PackagePath, outsidePath);

        var result = await CreateVerifier().VerifyAsync(
            Guid.NewGuid(),
            package.Manifest.AgentId,
            package.PackageId,
            outsidePath,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.False(result.Passed);
        Assert.Contains(
            result.Levels.Single(level => level.Level == "structural").Evidence,
            evidence => evidence.Contains("configured package store", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Linked_package_directory_fails_without_inspecting_its_target()
    {
        var package = await CreatePackageAsync();
        var outsideRoot = Path.Combine(_testRoot, "linked-package-target");
        Directory.CreateDirectory(outsideRoot);
        var targetPath = Path.Combine(outsideRoot, package.PackageId);
        Directory.Move(package.PackagePath, targetPath);
        Directory.CreateSymbolicLink(package.PackagePath, targetPath);

        var result = await CreateVerifier().VerifyAsync(
            Guid.NewGuid(),
            package.Manifest.AgentId,
            package.PackageId,
            package.PackagePath,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.False(result.Passed);
        Assert.Equal(
            ["Package directory cannot be a filesystem link."],
            result.Levels.Single(level => level.Level == "structural").Evidence);
        Assert.Contains(
            "could not be evaluated",
            result.Levels.Single(level => level.Level == "cryptographic").Evidence[0],
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_content_fails_exact_layout_verification()
    {
        var package = await CreatePackageAsync();
        var contentPath = Path.Combine(
            package.PackagePath,
            RecoveryPackageFormat.ContentDirectoryName,
            "main.show");
        File.SetAttributes(contentPath, FileAttributes.Normal);
        File.Delete(contentPath);

        var result = await CreateVerifier().VerifyAsync(
            Guid.NewGuid(),
            package.Manifest.AgentId,
            package.PackageId,
            package.PackagePath,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.False(result.Passed);
        Assert.Contains(
            result.Levels.Single(level => level.Level == "structural").Evidence,
            evidence => evidence.Contains("Missing package file", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Linked_content_fails_without_hashing_its_target()
    {
        var package = await CreatePackageAsync();
        var contentPath = Path.Combine(
            package.PackagePath,
            RecoveryPackageFormat.ContentDirectoryName,
            "main.show");
        var externalPath = Path.Combine(_testRoot, "external.show");
        File.SetAttributes(contentPath, FileAttributes.Normal);
        File.Move(contentPath, externalPath);
        File.CreateSymbolicLink(contentPath, externalPath);

        var result = await CreateVerifier().VerifyAsync(
            Guid.NewGuid(),
            package.Manifest.AgentId,
            package.PackageId,
            package.PackagePath,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.False(result.Passed);
        Assert.Contains(
            result.Levels.Single(level => level.Level == "structural").Evidence,
            evidence => evidence.Contains("cannot contain links", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Non_regular_content_fails_verification()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var package = await CreatePackageAsync();
        var socketPath = Path.Combine(
            package.PackagePath,
            RecoveryPackageFormat.ContentDirectoryName,
            "local.sock");
        var boundSocketPath = Path.Combine(Path.GetTempPath(), $"sv-{Guid.NewGuid():N}.sock");
        using var socket = new Socket(
            AddressFamily.Unix,
            SocketType.Stream,
            ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(boundSocketPath));
        File.Move(boundSocketPath, socketPath);

        var result = await CreateVerifier().VerifyAsync(
            Guid.NewGuid(),
            package.Manifest.AgentId,
            package.PackageId,
            package.PackagePath,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.False(result.Passed);
        Assert.Contains(
            result.Levels.Single(level => level.Level == "structural").Evidence,
            evidence => evidence.Contains("local.sock", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Manifest_declared_unix_socket_returns_failed_evidence()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var package = await CreatePackageAsync();
        var socketPath = Path.Combine(
            package.PackagePath,
            RecoveryPackageFormat.ContentDirectoryName,
            "local.sock");
        var boundSocketPath = Path.Combine("/tmp", $"sv-{Guid.NewGuid():N}.sock");
        using var socket = new Socket(
            AddressFamily.Unix,
            SocketType.Stream,
            ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(boundSocketPath));
        File.Move(boundSocketPath, socketPath);
        package = await DeclareEmptyEntryAsync(package, "local.sock");

        var result = await CreateVerifier().VerifyAsync(
            Guid.NewGuid(),
            package.Manifest.AgentId,
            package.PackageId,
            package.PackagePath,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        AssertNonRegularFailure(result, "local.sock");
    }

    [Fact]
    public async Task Manifest_declared_fifo_returns_failed_evidence_without_blocking()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var package = await CreatePackageAsync();
        var fifoPath = Path.Combine(
            package.PackagePath,
            RecoveryPackageFormat.ContentDirectoryName,
            "local.fifo");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "mkfifo",
            UseShellExecute = false,
            ArgumentList = { fifoPath }
        });
        Assert.NotNull(process);
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
        package = await DeclareEmptyEntryAsync(package, "local.fifo");

        var verification = CreateVerifier().VerifyAsync(
            Guid.NewGuid(),
            package.Manifest.AgentId,
            package.PackageId,
            package.PackagePath,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        var completed = await Task.WhenAny(verification, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(verification, completed);
        AssertNonRegularFailure(await verification, "local.fifo");
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

    private RecoveryPackageVerifier CreateVerifier() => new(Options.Create(new AgentOptions
    {
        ControlPlaneUri = new Uri("https://control.test"),
        Name = "Test Agent",
        PackageDirectory = Path.Combine(_testRoot, "packages")
    }));

    private static async Task<CreatedRecoveryPackage> RewriteManifestAsync(
        CreatedRecoveryPackage package,
        RecoveryPackageManifest manifest)
    {
        var manifestPath = Path.Combine(
            package.PackagePath,
            RecoveryPackageFormat.ManifestFileName);
        File.SetAttributes(manifestPath, FileAttributes.Normal);
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(
            manifest,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await File.WriteAllBytesAsync(manifestPath, manifestBytes);
        var packageId = Convert.ToHexStringLower(SHA256.HashData(manifestBytes));
        var packagePath = Path.Combine(Path.GetDirectoryName(package.PackagePath)!, packageId);
        Directory.Move(package.PackagePath, packagePath);
        return new CreatedRecoveryPackage(packageId, packagePath, manifest);
    }

    private static Task<CreatedRecoveryPackage> DeclareEmptyEntryAsync(
        CreatedRecoveryPackage package,
        string relativePath) =>
        RewriteManifestAsync(
            package,
            package.Manifest with
            {
                Files = package.Manifest.Files
                    .Append(new RecoveryPackageFile(
                        relativePath,
                        0,
                        Convert.ToHexStringLower(SHA256.HashData([]))))
                    .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                    .ToList()
            });

    private static void AssertNonRegularFailure(
        RecoveryPackageVerificationResult result,
        string relativePath)
    {
        Assert.False(result.Passed);
        Assert.Contains(
            result.Levels.Single(level => level.Level == "structural").Evidence,
            evidence => evidence.Contains(relativePath, StringComparison.Ordinal));
        Assert.Contains(
            result.Levels.Single(level => level.Level == "cryptographic").Evidence,
            evidence => evidence.Contains("could not be evaluated", StringComparison.Ordinal));
    }
}
