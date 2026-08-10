using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using ShowVault.Agent.Queue;
using ShowVault.Agent.Recovery;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class LocalVaultLayoutTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "showvault-local-vault-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Initialization_creates_the_required_local_first_vault_structure()
    {
        var vaultRoot = Path.Combine(_testRoot, "custom-vault");
        var layout = new LocalVaultLayout(CreateOptions(vaultRoot));

        layout.EnsureInitialized();

        Assert.Equal(Path.GetFullPath(vaultRoot), layout.RootPath);
        Assert.All(
            new[]
            {
                "Backups",
                "Manifests",
                "Device Exports",
                "Upload Queue",
                "Reports",
                "Logs",
                "Quarantine"
            },
            directory => Assert.True(Directory.Exists(Path.Combine(vaultRoot, directory))));
    }

    [Fact]
    public async Task Default_writer_publishes_an_immutable_named_recovery_point_in_the_vault()
    {
        var vaultRoot = Path.Combine(_testRoot, "ShowVault Pro");
        var sourceRoot = Path.Combine(_testRoot, "source");
        Directory.CreateDirectory(sourceRoot);
        var content = Encoding.UTF8.GetBytes("show state");
        await File.WriteAllBytesAsync(Path.Combine(sourceRoot, "venue.show"), content);
        var createdAt = new DateTimeOffset(2026, 8, 10, 21, 15, 0, TimeSpan.Zero);
        var discovery = new DiscoveryResult(
            "showvault.resolume",
            "0.1.0",
            sourceRoot,
            createdAt,
            false,
            [
                new DiscoveryFile(
                    "venue.show",
                    content.Length,
                    createdAt,
                    Convert.ToHexStringLower(SHA256.HashData(content)))
            ]);

        var package = await new RecoveryPackageWriter(CreateOptions(vaultRoot)).CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            discovery,
            createdAt,
            CancellationToken.None);

        Assert.Equal("resolume", Path.GetFileName(Path.GetDirectoryName(package.PackagePath)));
        Assert.Equal(
            $"2026-08-10T21-15-00Z__{package.PackageId}",
            Path.GetFileName(package.PackagePath));
        Assert.True(File.Exists(Path.Combine(package.PackagePath, "manifest.json")));
        Assert.True(Directory.Exists(Path.Combine(vaultRoot, "Upload Queue")));

        var verification = await new RecoveryPackageVerifier().VerifyAsync(
            Guid.NewGuid(),
            package.Manifest.AgentId,
            package.PackageId,
            package.PackagePath,
            createdAt,
            CancellationToken.None);
        Assert.True(verification.Passed);
    }

    [Fact]
    public async Task Default_durable_queue_database_lives_in_the_vault_upload_queue()
    {
        var vaultRoot = Path.Combine(_testRoot, "queue-vault");
        var store = new AgentQueueStore(CreateOptions(vaultRoot));

        await store.InitializeAsync(CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(vaultRoot, "Upload Queue", "agent-queue.db")));
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

    private static IOptions<AgentOptions> CreateOptions(string vaultRoot) =>
        Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            VaultDirectory = vaultRoot
        });
}
