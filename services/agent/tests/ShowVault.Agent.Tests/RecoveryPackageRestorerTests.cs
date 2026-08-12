using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using ShowVault.Agent.Queue;
using ShowVault.Agent.Recovery;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class RecoveryPackageRestorerTests : IAsyncLifetime
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "showvault-restorer-tests",
        Guid.NewGuid().ToString("N"));
    private AgentQueueStore _store = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(RestoreRoot);
        _store = new AgentQueueStore(CreateOptions());
        return _store.InitializeAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Restore_rejects_targets_outside_local_allowlist()
    {
        var restorer = CreateRestorer();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => restorer.RestoreAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new StoredRecoveryPackage("package", "missing", "{}"),
            Guid.NewGuid(),
            Path.Combine(_testRoot, "outside"),
            DateTimeOffset.UtcNow,
            CancellationToken.None));
    }

    [Fact]
    public async Task Restore_rejects_nonempty_target_before_reading_package()
    {
        var targetPath = Path.Combine(RestoreRoot, "occupied");
        Directory.CreateDirectory(targetPath);
        await File.WriteAllTextAsync(Path.Combine(targetPath, "keep.txt"), "user data");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateRestorer().RestoreAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new StoredRecoveryPackage("package", "missing", "{}"),
            Guid.NewGuid(),
            targetPath,
            DateTimeOffset.UtcNow,
            CancellationToken.None));

        Assert.Equal("user data", await File.ReadAllTextAsync(Path.Combine(targetPath, "keep.txt")));
    }

    [Fact]
    public async Task Restore_revalidates_package_and_does_not_publish_tampered_content()
    {
        var package = await CreatePackageAsync();
        var contentPath = Path.Combine(
            package.PackagePath,
            RecoveryPackageFormat.ContentDirectoryName,
            "main.show");
        File.SetAttributes(contentPath, FileAttributes.Normal);
        await File.WriteAllTextAsync(contentPath, "tampered");
        var targetPath = Path.Combine(RestoreRoot, "target");
        var restorationId = Guid.NewGuid();
        await _store.EnqueueCommandAsync(
            new ShowVault.AgentContracts.AgentCommandEnvelope(
                restorationId,
                package.Manifest.AgentId,
                ShowVault.AgentContracts.AgentCommandType.StartRestore,
                ShowVault.AgentContracts.AgentProtocol.Version,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                "restore",
                "{}"),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateRestorer().RestoreAsync(
            restorationId,
            package.Manifest.AgentId,
            new StoredRecoveryPackage(
                package.PackageId,
                package.PackagePath,
                RecoveryPackageVerifier.Serialize(new RecoveryPackageVerificationResult(
                    Guid.NewGuid(),
                    package.PackageId,
                    DateTimeOffset.UtcNow,
                    true,
                    []))),
            Guid.NewGuid(),
            targetPath,
            DateTimeOffset.UtcNow,
            CancellationToken.None));

        Assert.False(Directory.Exists(targetPath));
        Assert.DoesNotContain(
            Directory.EnumerateDirectories(RestoreRoot),
            path => Path.GetFileName(path).StartsWith(".showvault-restore-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Restore_resumes_after_atomic_publication_using_durable_intent()
    {
        var package = await CreatePackageAsync();
        var restorationId = Guid.NewGuid();
        await EnqueueRestoreCommandAsync(restorationId, package.Manifest.AgentId);
        var verificationId = Guid.NewGuid();
        var targetPath = Path.Combine(RestoreRoot, "resumed-target");
        var restorer = CreateRestorer();

        var first = await restorer.RestoreAsync(
            restorationId,
            package.Manifest.AgentId,
            new StoredRecoveryPackage(package.PackageId, package.PackagePath, "{}"),
            verificationId,
            targetPath,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        var resumed = await restorer.RestoreAsync(
            restorationId,
            package.Manifest.AgentId,
            new StoredRecoveryPackage(package.PackageId, package.PackagePath, "{}"),
            verificationId,
            targetPath,
            first.RestoredAt,
            CancellationToken.None);

        Assert.Equal(first.RestorationId, resumed.RestorationId);
        Assert.Equal(first.PackageId, resumed.PackageId);
        Assert.Equal(first.TargetPath, resumed.TargetPath);
        Assert.Equal(first.RestoredAt, resumed.RestoredAt);
        Assert.Equal(first.Evidence, resumed.Evidence);
        Assert.Equal("configuration", await File.ReadAllTextAsync(Path.Combine(targetPath, "main.show")));
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

    private string RestoreRoot => Path.Combine(_testRoot, "restores");

    private RecoveryPackageRestorer CreateRestorer()
    {
        var verifier = new RecoveryPackageVerifier(CreateOptions());
        return new RecoveryPackageRestorer(
            CreateOptions(),
            verifier,
            _store);
    }

    private Task EnqueueRestoreCommandAsync(Guid commandId, Guid agentId) =>
        _store.EnqueueCommandAsync(
            new ShowVault.AgentContracts.AgentCommandEnvelope(
                commandId,
                agentId,
                ShowVault.AgentContracts.AgentCommandType.StartRestore,
                ShowVault.AgentContracts.AgentProtocol.Version,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                "restore",
                "{}"),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

    private IOptions<AgentOptions> CreateOptions() => Options.Create(new AgentOptions
    {
        ControlPlaneUri = new Uri("https://control.test"),
        Name = "Test Agent",
        DataDirectory = Path.Combine(_testRoot, "data"),
        PackageDirectory = Path.Combine(_testRoot, "packages"),
        RestoreRoots = [RestoreRoot]
    });

    private async Task<CreatedRecoveryPackage> CreatePackageAsync()
    {
        var sourceRoot = Path.Combine(_testRoot, "source");
        Directory.CreateDirectory(sourceRoot);
        var content = Encoding.UTF8.GetBytes("configuration");
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
