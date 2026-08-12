using System.Diagnostics;
using System.Net.Sockets;
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
        OperatingSystem.IsWindows() ? Path.GetTempPath() : "/tmp",
        $"sv-restorer-{Guid.NewGuid():N}");
    private AgentQueueStore _store = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(RestoreRoot);
        _store = new AgentQueueStore(CreateOptions());
        return _store.InitializeAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(true, false, 0x80)]
    [InlineData(false, true, 0x200)]
    public void Unix_directory_removal_flag_matches_platform(
        bool isMacOS,
        bool isLinux,
        int expected)
    {
        Assert.Equal(
            expected,
            StableDirectoryTree.ResolveUnixAtRemoveDirectoryFlag(isMacOS, isLinux));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Unix_directory_removal_flag_rejects_unsupported_platforms(
        bool isMacOS,
        bool isLinux)
    {
        Assert.Throws<PlatformNotSupportedException>(() =>
            StableDirectoryTree.ResolveUnixAtRemoveDirectoryFlag(isMacOS, isLinux));
    }

    [Fact]
    public void Windows_staging_handle_blocks_redirection_and_controls_publication_cleanup()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var parentPath = Path.Combine(_testRoot, "windows-staging-parent");
        var stagingPath = Path.Combine(parentPath, "staging");
        var outsidePath = Path.Combine(_testRoot, "windows-staging-outside");
        Directory.CreateDirectory(parentPath);
        using var parent = StableDirectoryTree.Open(parentPath);
        var staging = parent.CreateDirectory("staging");
        using (var file = staging.CreateFile("main.show"))
        {
            file.Write(Encoding.UTF8.GetBytes("configuration"));
        }

        Assert.ThrowsAny<IOException>(() => Directory.Move(stagingPath, outsidePath));
        parent.RenameChild("staging", staging, "target");
        staging.Dispose();

        var targetPath = Path.Combine(parentPath, "target");
        Assert.Equal("configuration", File.ReadAllText(Path.Combine(targetPath, "main.show")));
        using var target = parent.OpenDirectory("target");
        parent.DeleteChildTreeIfSame("target", target);
        Assert.False(Directory.Exists(targetPath));
        Assert.False(Directory.Exists(outsidePath));
    }

    [Fact]
    public void Windows_child_handle_retains_ancestor_path_guards()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var rootPath = Path.Combine(_testRoot, "windows-guard-root");
        var outsidePath = Path.Combine(_testRoot, "windows-guard-outside");
        Directory.CreateDirectory(rootPath);
        var root = StableDirectoryTree.Open(rootPath);
        using var child = root.CreateDirectory("nested");
        root.Dispose();

        Assert.ThrowsAny<IOException>(() => Directory.Move(rootPath, outsidePath));
        Assert.Empty(child.EnumerateNames());
        Assert.False(Directory.Exists(outsidePath));
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
    public async Task Restore_rejects_linked_target_before_reading_package()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var outside = Path.Combine(_testRoot, "outside-target");
        Directory.CreateDirectory(outside);
        var targetPath = Path.Combine(RestoreRoot, "linked-target");
        Directory.CreateSymbolicLink(targetPath, outside);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateRestorer().RestoreAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new StoredRecoveryPackage("package", "missing", "{}"),
            Guid.NewGuid(),
            targetPath,
            DateTimeOffset.UtcNow,
            CancellationToken.None));
    }

    [Fact]
    public async Task Restore_rejects_linked_target_parent_before_reading_package()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var outside = Path.Combine(_testRoot, "outside-parent");
        Directory.CreateDirectory(outside);
        var linkedParent = Path.Combine(RestoreRoot, "linked-parent");
        Directory.CreateSymbolicLink(linkedParent, outside);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateRestorer().RestoreAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new StoredRecoveryPackage("package", "missing", "{}"),
            Guid.NewGuid(),
            Path.Combine(linkedParent, "target"),
            DateTimeOffset.UtcNow,
            CancellationToken.None));
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
    public async Task Restore_refuses_replaced_staging_file_after_hash()
    {
        var package = await CreatePackageAsync();
        var restorationId = Guid.NewGuid();
        await EnqueueRestoreCommandAsync(restorationId, package.Manifest.AgentId);
        var targetPath = Path.Combine(RestoreRoot, "staging-file-swap-target");
        var stagingPath = Path.Combine(
            RestoreRoot,
            $".showvault-restore-{restorationId:N}");
        var replacementAttempted = false;
        var probe = new ActionRaceProbe((point, relativePath) =>
        {
            if (point != RestoreRacePoint.DestinationFileOpened || relativePath != "main.show")
            {
                return;
            }

            var temporaryPath = Assert.Single(Directory.EnumerateFiles(
                stagingPath,
                ".showvault-file-*",
                SearchOption.TopDirectoryOnly));
            replacementAttempted = true;
            File.Delete(temporaryPath);
            File.WriteAllText(temporaryPath, "attacker-controlled");
        });

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRestorer(probe).RestoreAsync(
                restorationId,
                package.Manifest.AgentId,
                new StoredRecoveryPackage(package.PackageId, package.PackagePath, "{}"),
                Guid.NewGuid(),
                targetPath,
                DateTimeOffset.UtcNow,
                CancellationToken.None));

        Assert.True(replacementAttempted);
        Assert.DoesNotContain(_testRoot, failure.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(targetPath));
        Assert.False(Directory.Exists(stagingPath));
    }

    [Theory]
    [InlineData("root-file")]
    [InlineData("nested-file")]
    [InlineData("root-directory")]
    [InlineData("publication-file")]
    public async Task Restore_refuses_unexpected_staging_tree_entries(string scenario)
    {
        var package = scenario == "nested-file"
            ? await CreateNestedPackageAsync(includeLargeFile: false)
            : await CreatePackageAsync();
        var restorationId = Guid.NewGuid();
        await EnqueueRestoreCommandAsync(restorationId, package.Manifest.AgentId);
        var targetPath = Path.Combine(RestoreRoot, $"unexpected-{scenario}-target");
        var stagingPath = Path.Combine(
            RestoreRoot,
            $".showvault-restore-{restorationId:N}");
        var injected = false;
        var probe = new ActionRaceProbe((point, relativePath) =>
        {
            if (scenario == "publication-file")
            {
                if (point == RestoreRacePoint.StagingTreeValidated)
                {
                    injected = true;
                    File.WriteAllText(
                        Path.Combine(stagingPath, "unexpected.txt"),
                        "attacker-controlled");
                }

                return;
            }

            var expectedRacePath = scenario == "nested-file"
                ? "nested/a-large.bin"
                : "main.show";
            if (point != RestoreRacePoint.DestinationFileOpened ||
                relativePath != expectedRacePath)
            {
                return;
            }

            injected = true;
            if (scenario == "root-file")
            {
                File.WriteAllText(
                    Path.Combine(stagingPath, "unexpected.txt"),
                    "attacker-controlled");
            }
            else if (scenario == "nested-file")
            {
                File.WriteAllText(
                    Path.Combine(stagingPath, "nested", "unexpected.txt"),
                    "attacker-controlled");
            }
            else
            {
                var unexpectedDirectory = Path.Combine(stagingPath, "unexpected");
                Directory.CreateDirectory(unexpectedDirectory);
                File.WriteAllText(
                    Path.Combine(unexpectedDirectory, "payload.txt"),
                    "attacker-controlled");
            }
        });

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRestorer(probe).RestoreAsync(
                restorationId,
                package.Manifest.AgentId,
                new StoredRecoveryPackage(package.PackageId, package.PackagePath, "{}"),
                Guid.NewGuid(),
                targetPath,
                DateTimeOffset.UtcNow,
                CancellationToken.None));

        Assert.True(injected);
        Assert.Contains("unexpected entries", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(_testRoot, failure.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(targetPath));
        Assert.False(Directory.Exists(stagingPath));
    }

    [Fact]
    public async Task Restore_refuses_root_entry_injected_during_post_publication_hash()
    {
        var package = await CreateNestedPackageAsync(includeLargeFile: true);
        var restorationId = Guid.NewGuid();
        await EnqueueRestoreCommandAsync(restorationId, package.Manifest.AgentId);
        var targetPath = Path.Combine(RestoreRoot, "published-hash-injection-target");
        var stagingPath = Path.Combine(
            RestoreRoot,
            $".showvault-restore-{restorationId:N}");
        var injected = false;
        var probe = new ActionRaceProbe((point, relativePath) =>
        {
            if (point != RestoreRacePoint.PublishedFileHashStarted ||
                relativePath != "nested/a-large.bin")
            {
                return;
            }

            injected = true;
            File.WriteAllText(
                Path.Combine(targetPath, "unexpected.txt"),
                "attacker-controlled");
        });

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRestorer(probe).RestoreAsync(
                restorationId,
                package.Manifest.AgentId,
                new StoredRecoveryPackage(package.PackageId, package.PackagePath, "{}"),
                Guid.NewGuid(),
                targetPath,
                DateTimeOffset.UtcNow,
                CancellationToken.None));

        Assert.True(injected);
        Assert.Contains("unexpected entries", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(_testRoot, failure.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(targetPath));
        Assert.False(Directory.Exists(stagingPath));
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

    [Fact]
    public async Task Restore_restart_refuses_root_entry_injected_during_validation()
    {
        var package = await CreateNestedPackageAsync(includeLargeFile: true);
        var restorationId = Guid.NewGuid();
        await EnqueueRestoreCommandAsync(restorationId, package.Manifest.AgentId);
        var verificationId = Guid.NewGuid();
        var targetPath = Path.Combine(RestoreRoot, "replay-root-injection-target");
        var stagingPath = Path.Combine(
            RestoreRoot,
            $".showvault-restore-{restorationId:N}");
        var first = await CreateRestorer().RestoreAsync(
            restorationId,
            package.Manifest.AgentId,
            new StoredRecoveryPackage(package.PackageId, package.PackagePath, "{}"),
            verificationId,
            targetPath,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        var injected = false;
        var probe = new ActionRaceProbe((point, relativePath) =>
        {
            if (point != RestoreRacePoint.AdoptionDirectoryOpened || relativePath != "nested")
            {
                return;
            }

            injected = true;
            File.WriteAllText(
                Path.Combine(targetPath, "unexpected.txt"),
                "attacker-controlled");
        });

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRestorer(probe).RestoreAsync(
                restorationId,
                package.Manifest.AgentId,
                new StoredRecoveryPackage(package.PackageId, package.PackagePath, "{}"),
                verificationId,
                targetPath,
                first.RestoredAt,
                CancellationToken.None));

        Assert.True(injected);
        Assert.Contains("unexpected entries", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(_testRoot, failure.ToString(), StringComparison.Ordinal);
        Assert.True(Directory.Exists(targetPath));
        Assert.Equal(
            "attacker-controlled",
            await File.ReadAllTextAsync(Path.Combine(targetPath, "unexpected.txt")));
        Assert.True(File.Exists(Path.Combine(targetPath, "nested", "a-large.bin")));
        Assert.True(File.Exists(Path.Combine(targetPath, "nested", "z-escaped.show")));
        Assert.False(Directory.Exists(stagingPath));
    }

    [Fact]
    public async Task Restore_restart_adoption_rejects_fifo_without_blocking()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var (restorer, package, restorationId, verificationId, targetPath) =
            await CreatePublishedRestoreAsync("fifo-target");
        var targetFile = Path.Combine(targetPath, "main.show");
        File.Delete(targetFile);
        using (var process = Process.Start(new ProcessStartInfo("mkfifo")
        {
            UseShellExecute = false,
            ArgumentList = { targetFile }
        })!)
        {
            await process.WaitForExitAsync();
            Assert.Equal(0, process.ExitCode);
        }

        var rejection = Assert.ThrowsAsync<InvalidOperationException>(() => restorer.RestoreAsync(
            restorationId,
            package.Manifest.AgentId,
            new StoredRecoveryPackage(package.PackageId, package.PackagePath, "{}"),
            verificationId,
            targetPath,
            DateTimeOffset.UtcNow,
            CancellationToken.None));

        Assert.Same(rejection, await Task.WhenAny(rejection, Task.Delay(500)));
        Assert.Contains("regular files", (await rejection).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restore_restart_adoption_rejects_socket_without_blocking()
    {
        if (!Socket.OSSupportsUnixDomainSockets)
        {
            return;
        }

        var (restorer, package, restorationId, verificationId, targetPath) =
            await CreatePublishedRestoreAsync("socket-target");
        var targetFile = Path.Combine(targetPath, "main.show");
        File.Delete(targetFile);
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(targetFile));

        var rejection = Assert.ThrowsAsync<InvalidOperationException>(() => restorer.RestoreAsync(
            restorationId,
            package.Manifest.AgentId,
            new StoredRecoveryPackage(package.PackageId, package.PackagePath, "{}"),
            verificationId,
            targetPath,
            DateTimeOffset.UtcNow,
            CancellationToken.None));

        Assert.Same(rejection, await Task.WhenAny(rejection, Task.Delay(500)));
        Assert.Contains("regular files", (await rejection).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restore_refuses_nested_staging_directory_swap_without_redirected_file()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var package = await CreateNestedPackageAsync(includeLargeFile: true);
        var restorationId = Guid.NewGuid();
        await EnqueueRestoreCommandAsync(restorationId, package.Manifest.AgentId);
        var targetPath = Path.Combine(RestoreRoot, "swap-target");
        var stagingPath = Path.Combine(
            RestoreRoot,
            $".showvault-restore-{restorationId:N}");
        var outside = Path.Combine(_testRoot, "outside-staging-swap");
        var probe = new ActionRaceProbe((point, relativePath) =>
        {
            if (point != RestoreRacePoint.DestinationFileOpened ||
                relativePath != "nested/a-large.bin")
            {
                return;
            }

            var nested = Path.Combine(stagingPath, "nested");
            Directory.Move(nested, outside);
            Directory.CreateSymbolicLink(nested, outside);
        });

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRestorer(probe).RestoreAsync(
                restorationId,
                package.Manifest.AgentId,
                new StoredRecoveryPackage(package.PackageId, package.PackagePath, "{}"),
                Guid.NewGuid(),
                targetPath,
                DateTimeOffset.UtcNow,
                CancellationToken.None));

        Assert.DoesNotContain(_testRoot, failure.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(outside, "a-large.bin")));
        Assert.False(File.Exists(Path.Combine(outside, "z-escaped.show")));
        Assert.False(Directory.Exists(targetPath));
        Assert.False(Directory.Exists(stagingPath));
        Directory.Delete(outside, recursive: true);
    }

    [Fact]
    public async Task Restore_restart_adoption_refuses_nested_directory_swap()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var package = await CreateNestedPackageAsync(includeLargeFile: false);
        var restorationId = Guid.NewGuid();
        var verificationId = Guid.NewGuid();
        await EnqueueRestoreCommandAsync(restorationId, package.Manifest.AgentId);
        var targetPath = Path.Combine(RestoreRoot, "adoption-swap-target");
        await CreateRestorer().RestoreAsync(
            restorationId,
            package.Manifest.AgentId,
            new StoredRecoveryPackage(package.PackageId, package.PackagePath, "{}"),
            verificationId,
            targetPath,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        var outside = Path.Combine(_testRoot, "outside-adoption-swap");
        var probe = new ActionRaceProbe((point, relativePath) =>
        {
            if (point != RestoreRacePoint.AdoptionDirectoryOpened || relativePath != "nested")
            {
                return;
            }

            var nested = Path.Combine(targetPath, "nested");
            Directory.Move(nested, outside);
            Directory.CreateSymbolicLink(nested, outside);
        });

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRestorer(probe).RestoreAsync(
                restorationId,
                package.Manifest.AgentId,
                new StoredRecoveryPackage(package.PackageId, package.PackagePath, "{}"),
                verificationId,
                targetPath,
                DateTimeOffset.UtcNow,
                CancellationToken.None));

        Assert.Contains("identity changed", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(_testRoot, failure.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(outside, "unexpected.show")));
        Directory.Delete(Path.Combine(targetPath, "nested"));
        Directory.Delete(targetPath);
        Directory.Delete(outside, recursive: true);
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

    private async Task<(
        RecoveryPackageRestorer Restorer,
        CreatedRecoveryPackage Package,
        Guid RestorationId,
        Guid VerificationId,
        string TargetPath)> CreatePublishedRestoreAsync(string targetName)
    {
        var package = await CreatePackageAsync();
        var restorationId = Guid.NewGuid();
        var verificationId = Guid.NewGuid();
        await EnqueueRestoreCommandAsync(restorationId, package.Manifest.AgentId);
        var targetPath = Path.Combine(RestoreRoot, targetName);
        var restorer = CreateRestorer();
        await restorer.RestoreAsync(
            restorationId,
            package.Manifest.AgentId,
            new StoredRecoveryPackage(package.PackageId, package.PackagePath, "{}"),
            verificationId,
            targetPath,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        return (restorer, package, restorationId, verificationId, targetPath);
    }

    private RecoveryPackageRestorer CreateRestorer(IRestoreRaceProbe? raceProbe = null)
    {
        var verifier = new RecoveryPackageVerifier(CreateOptions());
        return new RecoveryPackageRestorer(
            CreateOptions(),
            verifier,
            _store,
            raceProbe);
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

    private async Task<CreatedRecoveryPackage> CreateNestedPackageAsync(bool includeLargeFile)
    {
        var sourceRoot = Path.Combine(_testRoot, "nested-source");
        var nested = Path.Combine(sourceRoot, "nested");
        Directory.CreateDirectory(nested);
        var firstPath = Path.Combine(nested, "a-large.bin");
        await using (var stream = new FileStream(
            firstPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        {
            stream.SetLength(includeLargeFile ? 128L * 1024 * 1024 : 1024);
        }

        var secondPath = Path.Combine(nested, "z-escaped.show");
        await File.WriteAllTextAsync(secondPath, "configuration");
        var discoveries = new List<DiscoveryFile>();
        foreach (var (path, relativePath) in new[]
        {
            (firstPath, "nested/a-large.bin"),
            (secondPath, "nested/z-escaped.show")
        })
        {
            await using var stream = File.OpenRead(path);
            discoveries.Add(new DiscoveryFile(
                relativePath,
                stream.Length,
                DateTimeOffset.UtcNow,
                Convert.ToHexStringLower(await SHA256.HashDataAsync(stream))));
        }

        var writer = new RecoveryPackageWriter(Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            PackageDirectory = Path.Combine(_testRoot, "packages")
        }));
        return await writer.CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DiscoveryResult(
                FileSystemDiscoveryPlugin.PluginId,
                "0.1.0",
                sourceRoot,
                DateTimeOffset.UtcNow,
                false,
                discoveries),
            DateTimeOffset.UtcNow,
            CancellationToken.None);
    }

    private sealed class ActionRaceProbe(Action<RestoreRacePoint, string> action)
        : IRestoreRaceProbe
    {
        public void Reached(RestoreRacePoint point, string relativePath) =>
            action(point, relativePath);
    }
}
