using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ShowVault.LocalEngine.Tests;

public sealed class LocalRecoveryEngineTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "showvault-local-engine-tests", Guid.NewGuid().ToString("N"));
    private string Source => Path.Combine(_root, "home", "Music", "_Serato_");
    private string Vault => Path.Combine(_root, "vault");
    private const string Key = "macos.serato-dj-pro.user-data";

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(Source);
        await File.WriteAllTextAsync(Path.Combine(Source, "database V2"), "library");
        Directory.CreateDirectory(Path.Combine(Source, "Subcrates"));
        await File.WriteAllTextAsync(Path.Combine(Source, "Subcrates", "fixture.crate"), "crate");
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Save_publishes_verified_immutable_point_and_path_free_queue()
    {
        var engine = CreateEngine();

        var result = await engine.SaveAsync(new(Key, Source, Vault));

        Assert.Equal("verified", result.LocalStatus);
        Assert.Equal("queued", result.CloudStatus);
        Assert.Equal(2, result.FileCount);
        Assert.Equal(64, result.RecoveryPointId.Length);
        var package = Assert.Single(Directory.EnumerateDirectories(
            Path.Combine(Vault, "Backups", "Serato DJ Pro")));
        Assert.EndsWith(result.RecoveryPointId, package, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(package, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(package, "verification.json")));

        await using var connection = new SqliteConnection(
            $"Data Source={Path.Combine(Vault, "Upload Queue", LocalVaultLayout.QueueDatabaseName)}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT status, package_relative_path, candidate_key, last_error_code
            FROM local_recovery_points;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("queued", reader.GetString(0));
        Assert.StartsWith("Backups/", reader.GetString(1), StringComparison.Ordinal);
        Assert.Equal(Key, reader.GetString(2));
        Assert.True(reader.IsDBNull(3));
        var databaseBytes = await File.ReadAllBytesAsync(connection.DataSource);
        Assert.DoesNotContain(Source, System.Text.Encoding.UTF8.GetString(databaseBytes),
            StringComparison.Ordinal);
        Assert.DoesNotContain(Vault, System.Text.Encoding.UTF8.GetString(databaseBytes),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Save_rejects_source_vault_overlap_before_creating_vault_content(int kind)
    {
        var selectedVault = kind switch
        {
            0 => Source,
            1 => Path.Combine(Source, "nested-vault"),
            _ => Path.Combine(_root, "home")
        };

        await Assert.ThrowsAsync<LocalEngineException>(() =>
            CreateEngine().SaveAsync(new(Key, Source, selectedVault)));

        Assert.False(Directory.Exists(Path.Combine(selectedVault, "Upload Queue")));
    }

    [Fact]
    public async Task Save_rejects_linked_descendant_and_queues_nothing()
    {
        if (OperatingSystem.IsWindows()) return;
        var outside = Path.Combine(_root, "outside");
        await File.WriteAllTextAsync(outside, "private");
        File.CreateSymbolicLink(Path.Combine(Source, "escape"), outside);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateEngine().SaveAsync(new(Key, Source, Vault)));

        Assert.Empty(Directory.EnumerateDirectories(
            Path.Combine(Vault, "Backups", "Serato DJ Pro")));
        Assert.Empty(await ReadStatusesAsync());
    }

    [Fact]
    public async Task Save_rejects_linked_source_root()
    {
        if (OperatingSystem.IsWindows()) return;
        var realSource = $"{Source}-real";
        Directory.Move(Source, realSource);
        Directory.CreateSymbolicLink(Source, realSource);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateEngine().SaveAsync(new(Key, Source, Vault)));

        Assert.False(Directory.Exists(Vault));
    }

    [Fact]
    public async Task Save_rejects_linked_vault_component()
    {
        if (OperatingSystem.IsWindows()) return;
        var outside = Path.Combine(_root, "outside-vault");
        Directory.CreateDirectory(outside);
        Directory.CreateDirectory(Vault);
        Directory.CreateSymbolicLink(Path.Combine(Vault, "Backups"), outside);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateEngine().SaveAsync(new(Key, Source, Vault)));

        Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
    }

    [Fact]
    public async Task Save_rejects_non_regular_entry()
    {
        if (OperatingSystem.IsWindows()) return;
        Assert.Equal(0, MakeFifo(Path.Combine(Source, "unsupported"), Convert.ToUInt32("600", 8)));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateEngine().SaveAsync(new(Key, Source, Vault)));

        Assert.Empty(await ReadStatusesAsync());
    }

    [Fact]
    public async Task Save_rejects_hard_linked_content()
    {
        if (OperatingSystem.IsWindows()) return;
        var outside = Path.Combine(_root, "outside");
        await File.WriteAllTextAsync(outside, "private");
        Assert.Equal(0, CreateHardLink(outside, Path.Combine(Source, "hard-link")));

        var error = await Assert.ThrowsAsync<LocalEngineException>(() =>
            CreateEngine().SaveAsync(new(Key, Source, Vault)));

        Assert.Contains("multiply-linked", error.Message, StringComparison.Ordinal);
        Assert.Empty(await ReadStatusesAsync());
    }

    [Fact]
    public async Task Cancel_persists_closed_terminal_state_and_publishes_nothing()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateEngine().SaveAsync(new(Key, Source, Vault), cancellation.Token));

        Assert.Empty(Directory.Exists(Path.Combine(Vault, "Backups", "Serato DJ Pro"))
            ? Directory.EnumerateDirectories(Path.Combine(Vault, "Backups", "Serato DJ Pro"))
            : []);
    }

    [Theory]
    [InlineData("snapshot_captured")]
    [InlineData("file_copied")]
    [InlineData("staging_verified")]
    [InlineData("published")]
    [InlineData("independent_written")]
    [InlineData("queue_verified")]
    public async Task Cancellation_at_each_durable_boundary_never_queues(string stage)
    {
        using var cancellation = new CancellationTokenSource();
        var engine = CreateEngine(testHook: reached =>
        {
            if (reached == stage) cancellation.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engine.SaveAsync(new(Key, Source, Vault), cancellation.Token));

        Assert.DoesNotContain("queued", await ReadAllStatusesAsync());
        Assert.Empty(Directory.EnumerateDirectories(
            Path.Combine(Vault, "Backups", "Serato DJ Pro")));
    }

    [Theory]
    [InlineData("root")]
    [InlineData("directory")]
    [InlineData("file")]
    [InlineData("late")]
    [InlineData("removed")]
    [InlineData("bytes")]
    public async Task Source_changes_after_snapshot_fail_closed(string mutation)
    {
        var engine = CreateEngine(testHook: stage =>
        {
            if (stage != "snapshot_captured") return;
            var file = Path.Combine(Source, "database V2");
            switch (mutation)
            {
                case "root":
                    Directory.Move(Source, $"{Source}-old");
                    Directory.CreateDirectory(Source);
                    break;
                case "directory":
                    Directory.Move(Path.Combine(Source, "Subcrates"),
                        Path.Combine(Source, "Subcrates-old"));
                    Directory.CreateDirectory(Path.Combine(Source, "Subcrates"));
                    break;
                case "file":
                    File.Move(file, $"{file}-old");
                    File.WriteAllText(file, "library");
                    break;
                case "late":
                    File.WriteAllText(Path.Combine(Source, "late"), "late");
                    break;
                case "removed":
                    File.Delete(file);
                    break;
                case "bytes":
                    var timestamp = File.GetLastWriteTimeUtc(file);
                    File.WriteAllText(file, "changed");
                    File.SetLastWriteTimeUtc(file, timestamp);
                    break;
            }
        });

        await Assert.ThrowsAnyAsync<Exception>(() =>
            engine.SaveAsync(new(Key, Source, Vault)));

        Assert.DoesNotContain("queued", await ReadAllStatusesAsync());
    }

    [Fact]
    public async Task File_identity_swap_during_copy_fails_closed()
    {
        var mutated = false;
        var engine = CreateEngine(testHook: stage =>
        {
            if (stage != "file_copied" || mutated) return;
            mutated = true;
            var file = Path.Combine(Source, "database V2");
            File.Move(file, $"{file}-old");
            File.WriteAllText(file, "library");
        });

        await Assert.ThrowsAnyAsync<Exception>(() =>
            engine.SaveAsync(new(Key, Source, Vault)));

        Assert.DoesNotContain("queued", await ReadAllStatusesAsync());
    }

    [Fact]
    public async Task Failure_after_publication_moves_the_unqueued_package_to_quarantine()
    {
        var engine = CreateEngine(testHook: stage =>
        {
            if (stage == "published") throw new IOException("synthetic failure");
        });

        await Assert.ThrowsAsync<LocalEngineException>(() =>
            engine.SaveAsync(new(Key, Source, Vault)));

        Assert.Empty(Directory.EnumerateDirectories(
            Path.Combine(Vault, "Backups", "Serato DJ Pro")));
        Assert.Single(Directory.EnumerateDirectories(Path.Combine(Vault, "Quarantine")));
        Assert.Equal(["failed"], await ReadAllStatusesAsync());
        var inspection = await CreateEngine().InspectVaultStateAsync(Vault);
        Assert.Empty(inspection.RecoveryPoints);
        Assert.Equal(1, inspection.QueueAttentionCount);
    }

    [Fact]
    public async Task Restart_reverifies_and_queues_a_durable_verified_state()
    {
        var engine = CreateEngine();
        var saved = await engine.SaveAsync(new(Key, Source, Vault));
        await SetOnlyStatusAsync("verified");

        var inspection = await CreateEngine().InspectVaultStateAsync(Vault);

        Assert.Equal(saved.RecoveryPointId, inspection.RecoveryPoints.Single().RecoveryPointId);
        Assert.Equal(0, inspection.QueueAttentionCount);
        Assert.Equal(["queued"], await ReadAllStatusesAsync());
    }

    [Fact]
    public async Task Restart_quarantines_interrupted_staging_and_marks_attention()
    {
        using var layout = LocalVaultLayout.OpenOrCreate(Vault);
        var queue = new LocalVaultQueueStore(layout.QueueDatabasePath);
        await queue.InitializeAsync(TestContext.Current.CancellationToken);
        const string operationId = "0123456789abcdef0123456789abcdef";
        await queue.RecordStagingAsync(
            operationId, Key, "Serato DJ Pro", DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);
        var staging = Path.Combine(
            Vault, "Backups", "Serato DJ Pro", $".staging-{operationId}");
        Directory.CreateDirectory(staging);
        await File.WriteAllTextAsync(Path.Combine(staging, "partial"), "partial");

        var inspection = await CreateEngine().InspectVaultStateAsync(Vault);

        Assert.Empty(inspection.RecoveryPoints);
        Assert.Equal(1, inspection.QueueAttentionCount);
        Assert.Equal(["failed"], await ReadAllStatusesAsync());
        Assert.Single(Directory.EnumerateDirectories(Path.Combine(Vault, "Quarantine")));
    }

    [Fact]
    public async Task Restart_quarantines_an_untracked_package_directory()
    {
        var engine = CreateEngine();
        await engine.SaveAsync(new(Key, Source, Vault));
        var orphan = Path.Combine(Vault, "Backups", "Serato DJ Pro", "orphan-package");
        Directory.CreateDirectory(orphan);
        await File.WriteAllTextAsync(Path.Combine(orphan, "unknown"), "unknown");

        var inspection = await engine.InspectVaultStateAsync(Vault);

        Assert.Single(inspection.RecoveryPoints);
        Assert.False(Directory.Exists(orphan));
        Assert.Single(Directory.EnumerateDirectories(Path.Combine(Vault, "Quarantine")));
    }

    [Fact]
    public async Task Restart_quarantines_a_verified_state_that_no_longer_reverifies()
    {
        await CreateEngine().SaveAsync(new(Key, Source, Vault));
        await SetOnlyStatusAsync("verified");
        var package = Assert.Single(Directory.EnumerateDirectories(
            Path.Combine(Vault, "Backups", "Serato DJ Pro")));
        var content = Path.Combine(package, "content", "database V2");
        MakeWritable(content);
        await File.WriteAllTextAsync(content, "changed");

        var inspection = await CreateEngine().InspectVaultStateAsync(Vault);

        Assert.Empty(inspection.RecoveryPoints);
        Assert.Equal(1, inspection.QueueAttentionCount);
        Assert.Equal(["failed"], await ReadAllStatusesAsync());
        Assert.Single(Directory.EnumerateDirectories(Path.Combine(Vault, "Quarantine")));
    }

    [Fact]
    public async Task SQLite_open_failure_publishes_nothing()
    {
        Directory.CreateDirectory(Path.Combine(
            Vault, "Upload Queue", LocalVaultLayout.QueueDatabaseName));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateEngine().SaveAsync(new(Key, Source, Vault)));

        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(Vault, "Backups")));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unsafe")]
    public async Task Verifier_rejects_duplicate_or_unsafe_manifest_paths(string kind)
    {
        var package = Path.Combine(_root, $"malicious-{kind}");
        Directory.CreateDirectory(Path.Combine(package, "content"));
        var content = Path.Combine(package, "content", "file");
        await File.WriteAllTextAsync(content, "content");
        await File.WriteAllTextAsync(Path.Combine(package, "summary.txt"), "summary");
        var hash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(content)));
        var files = kind == "duplicate"
            ? new[] { new LocalRecoveryFile("file", 7, hash), new LocalRecoveryFile("file", 7, hash) }
            : new[] { new LocalRecoveryFile("../escape", 7, hash) };
        var manifest = new LocalRecoveryManifest(
            "1.0", Key, "showvault.test", "desktop-test", "Test", DateTimeOffset.UtcNow,
            files, [], []);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await File.WriteAllBytesAsync(Path.Combine(package, "manifest.json"), bytes);
        var id = Convert.ToHexStringLower(SHA256.HashData(bytes));

        await Assert.ThrowsAnyAsync<Exception>(() => LocalRecoveryVerifier.VerifyAsync(
            package, id, DateTimeOffset.UtcNow, new LocalEngineLimits(),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Vault_reopening_rehashes_content_and_rejects_tamper()
    {
        var engine = CreateEngine();
        var result = await engine.SaveAsync(new(Key, Source, Vault));
        var package = Assert.Single(Directory.EnumerateDirectories(
            Path.Combine(Vault, "Backups", "Serato DJ Pro")));
        var content = Path.Combine(package, "content", "database V2");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(content, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        else
            File.SetAttributes(content, FileAttributes.Normal);
        await File.WriteAllTextAsync(content, "tampered");

        await Assert.ThrowsAnyAsync<Exception>(() => engine.InspectVaultAsync(Vault));
        Assert.Equal(64, result.RecoveryPointId.Length);
    }

    [Fact]
    public async Task Vault_reopening_never_rescans_the_deleted_source()
    {
        var engine = CreateEngine();
        var result = await engine.SaveAsync(new(Key, Source, Vault));
        Directory.Delete(Path.Combine(_root, "home"), recursive: true);

        var record = Assert.Single(await engine.InspectVaultAsync(Vault));

        Assert.Equal(result.RecoveryPointId, record.RecoveryPointId);
        Assert.Equal("verified", record.LocalStatus);
        Assert.Equal("queued", record.CloudStatus);
    }

    [Fact]
    public async Task Repeated_save_never_overwrites_an_existing_identity()
    {
        var engine = CreateEngine();
        await engine.SaveAsync(new(Key, Source, Vault));

        await Assert.ThrowsAsync<LocalEngineException>(() =>
            engine.SaveAsync(new(Key, Source, Vault)));

        Assert.Single(Directory.EnumerateDirectories(
            Path.Combine(Vault, "Backups", "Serato DJ Pro")));
    }

    [Fact]
    public async Task Save_enforces_empty_count_size_and_path_bounds()
    {
        Directory.Delete(Source, recursive: true);
        Directory.CreateDirectory(Source);
        await Assert.ThrowsAsync<LocalEngineException>(() =>
            CreateEngine().SaveAsync(new(Key, Source, Vault)));

        await File.WriteAllTextAsync(Path.Combine(Source, "one"), "1234");
        await File.WriteAllTextAsync(Path.Combine(Source, "two"), "5678");
        await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateEngine(new LocalEngineLimits(MaximumFileCount: 1))
                .SaveAsync(new(Key, Source, Vault)));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateEngine(new LocalEngineLimits(MaximumFileBytes: 3))
                .SaveAsync(new(Key, Source, Path.Combine(_root, "vault-two"))));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateEngine(new LocalEngineLimits(MaximumRelativePathLength: 1))
                .SaveAsync(new(Key, Source, Path.Combine(_root, "vault-three"))));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateEngine(new LocalEngineLimits(MaximumTotalBytes: 5))
                .SaveAsync(new(Key, Source, Path.Combine(_root, "vault-four"))));
        Directory.CreateDirectory(Path.Combine(Source, "another-directory"));
        await File.WriteAllTextAsync(Path.Combine(Source, "another-directory", "file"), "x");
        Directory.CreateDirectory(Path.Combine(Source, "second-directory"));
        await File.WriteAllTextAsync(Path.Combine(Source, "second-directory", "file"), "x");
        await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateEngine(new LocalEngineLimits(MaximumDirectoryCount: 1))
                .SaveAsync(new(Key, Source, Path.Combine(_root, "vault-five"))));
    }

    [Fact]
    public async Task Save_enforces_duration_bound()
    {
        await File.WriteAllBytesAsync(
            Path.Combine(Source, "bounded-large-file"), new byte[4 * 1024 * 1024]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateEngine(new LocalEngineLimits(Timeout: TimeSpan.Zero))
                .SaveAsync(new(Key, Source, Vault)));

        Assert.DoesNotContain("queued", await ReadAllStatusesAsync());
    }

    [Fact]
    public async Task Independent_evidence_and_package_manifest_are_identical()
    {
        var result = await CreateEngine().SaveAsync(new(Key, Source, Vault));
        var package = Assert.Single(Directory.EnumerateDirectories(
            Path.Combine(Vault, "Backups", "Serato DJ Pro")));
        Assert.Equal(
            await File.ReadAllBytesAsync(Path.Combine(package, "manifest.json")),
            await File.ReadAllBytesAsync(Path.Combine(
                Vault, "Manifests", result.RecoveryPointId, "manifest.json")));
        var evidence = JsonDocument.Parse(await File.ReadAllBytesAsync(
            Path.Combine(package, "verification.json")));
        Assert.True(evidence.RootElement.GetProperty("passed").GetBoolean());
    }

    [Theory]
    [InlineData("extra")]
    [InlineData("missing")]
    [InlineData("manifest")]
    [InlineData("evidence")]
    public async Task Vault_reopening_rejects_package_or_evidence_mutation(string mutation)
    {
        var result = await CreateEngine().SaveAsync(new(Key, Source, Vault));
        var package = Assert.Single(Directory.EnumerateDirectories(
            Path.Combine(Vault, "Backups", "Serato DJ Pro")));
        switch (mutation)
        {
            case "extra":
                File.WriteAllText(Path.Combine(package, "extra"), "unexpected");
                break;
            case "missing":
                File.Delete(Path.Combine(package, "summary.txt"));
                break;
            case "manifest":
                MakeWritable(Path.Combine(package, "manifest.json"));
                File.AppendAllText(Path.Combine(package, "manifest.json"), " ");
                break;
            case "evidence":
                var evidence = Path.Combine(
                    Vault, "Manifests", result.RecoveryPointId, "verification.json");
                MakeWritable(evidence);
                File.AppendAllText(evidence, " ");
                break;
        }

        await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateEngine().InspectVaultAsync(Vault));
    }

    [Fact]
    public async Task Restore_publishes_verified_copy_and_path_free_durable_evidence()
    {
        var engine = CreateEngine();
        var saved = await engine.SaveAsync(new(Key, Source, Vault));
        var target = Path.Combine(_root, "restore-sandbox");
        Directory.CreateDirectory(target);

        var result = await engine.RestoreAsync(new(saved.RecoveryPointId, Vault, target));

        Assert.Equal("restored", result.LocalStatus);
        Assert.Equal(saved.RecoveryPointId, result.RecoveryPointId);
        Assert.Equal(2, result.FileCount);
        Assert.Equal(64, result.RestoreEvidenceId.Length);
        var published = Path.Combine(target, LocalRestoreCoordinator.PublicationName);
        Assert.Equal("library", await File.ReadAllTextAsync(Path.Combine(published, "database V2")));
        Assert.Equal("crate", await File.ReadAllTextAsync(
            Path.Combine(published, "Subcrates", "fixture.crate")));
        Assert.True(File.Exists(Path.Combine(
            Vault, "Reports", "Restores", $"{result.RestoreEvidenceId}.json")));
        var evidenceText = await File.ReadAllTextAsync(Path.Combine(
            Vault, "Reports", "Restores", $"{result.RestoreEvidenceId}.json"));
        Assert.DoesNotContain(target, evidenceText, StringComparison.Ordinal);
        Assert.DoesNotContain(Vault, evidenceText, StringComparison.Ordinal);

        var database = Path.Combine(Vault, "Upload Queue", LocalVaultLayout.QueueDatabaseName);
        var databaseText = System.Text.Encoding.UTF8.GetString(await File.ReadAllBytesAsync(database));
        Assert.DoesNotContain(target, databaseText, StringComparison.Ordinal);
        Assert.DoesNotContain(Vault, databaseText, StringComparison.Ordinal);
        await using var connection = new SqliteConnection($"Data Source={database}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT status, evidence_id FROM local_restore_attempts;";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("completed", reader.GetString(0));
        Assert.Equal(result.RestoreEvidenceId, reader.GetString(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Restore_rejects_vault_target_overlap(int kind)
    {
        var engine = CreateEngine();
        var saved = await engine.SaveAsync(new(Key, Source, Vault));
        var target = kind switch
        {
            0 => Vault,
            1 => Path.Combine(Vault, "nested"),
            _ => _root
        };
        Directory.CreateDirectory(target);

        await Assert.ThrowsAsync<LocalEngineException>(() =>
            engine.RestoreAsync(new(saved.RecoveryPointId, Vault, target)));

        Assert.False(Directory.Exists(Path.Combine(target, LocalRestoreCoordinator.PublicationName)));
    }

    [Fact]
    public async Task Restore_rejects_non_empty_or_linked_target_without_modifying_it()
    {
        var engine = CreateEngine();
        var saved = await engine.SaveAsync(new(Key, Source, Vault));
        var target = Path.Combine(_root, "occupied");
        Directory.CreateDirectory(target);
        var operatorFile = Path.Combine(target, "operator-file");
        await File.WriteAllTextAsync(operatorFile, "preserve");

        await Assert.ThrowsAsync<LocalEngineException>(() =>
            engine.RestoreAsync(new(saved.RecoveryPointId, Vault, target)));
        Assert.Equal("preserve", await File.ReadAllTextAsync(operatorFile));

        if (OperatingSystem.IsWindows()) return;
        var real = Path.Combine(_root, "real-target");
        var linked = Path.Combine(_root, "linked-target");
        Directory.CreateDirectory(real);
        Directory.CreateSymbolicLink(linked, real);
        await Assert.ThrowsAnyAsync<Exception>(() =>
            engine.RestoreAsync(new(saved.RecoveryPointId, Vault, linked)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(real));
    }

    [Fact]
    public async Task Restore_cancel_before_publication_removes_only_owned_staging()
    {
        using var cancellation = new CancellationTokenSource();
        var engine = CreateEngine(testHook: stage =>
        {
            if (stage == "restore_file_copied") cancellation.Cancel();
        });
        var saved = await engine.SaveAsync(new(Key, Source, Vault));
        var target = Path.Combine(_root, "cancel-target");
        Directory.CreateDirectory(target);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engine.RestoreAsync(new(saved.RecoveryPointId, Vault, target), cancellation.Token));

        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
        await using var connection = new SqliteConnection(
            $"Data Source={Path.Combine(Vault, "Upload Queue", LocalVaultLayout.QueueDatabaseName)}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT status FROM local_restore_attempts;";
        Assert.Equal("cancelled", await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Restore_reselect_recognizes_matching_published_copy()
    {
        var engine = CreateEngine();
        var saved = await engine.SaveAsync(new(Key, Source, Vault));
        var target = Path.Combine(_root, "repeat-target");
        Directory.CreateDirectory(target);
        var first = await engine.RestoreAsync(new(saved.RecoveryPointId, Vault, target));

        var retainedIntent = Path.Combine(
            target, $".showvault-restore-{saved.RecoveryPointId}", "intent.json");
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(Path.GetDirectoryName(retainedIntent)!));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(retainedIntent));
        }
        Assert.Contains(saved.RecoveryPointId, await File.ReadAllTextAsync(retainedIntent),
            StringComparison.Ordinal);

        var second = await engine.RestoreAsync(new(saved.RecoveryPointId, Vault, target));

        Assert.Equal(first.RecoveryPointId, second.RecoveryPointId);
        Assert.Single(Directory.EnumerateDirectories(target), path =>
            Path.GetFileName(path) == LocalRestoreCoordinator.PublicationName);
    }

    [Fact]
    public async Task Restore_rejects_unverified_identity_and_tampered_package()
    {
        var engine = CreateEngine();
        var saved = await engine.SaveAsync(new(Key, Source, Vault));
        var target = Path.Combine(_root, "verification-target");
        Directory.CreateDirectory(target);

        await Assert.ThrowsAsync<LocalEngineException>(() => engine.RestoreAsync(
            new(new string('a', 64), Vault, target)));

        var package = Assert.Single(Directory.EnumerateDirectories(
            Path.Combine(Vault, "Backups", "Serato DJ Pro")));
        var content = Path.Combine(package, "content", "database V2");
        MakeWritable(content);
        await File.WriteAllTextAsync(content, "tampered");
        await Assert.ThrowsAnyAsync<Exception>(() => engine.RestoreAsync(
            new(saved.RecoveryPointId, Vault, target)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
    }

    [Fact]
    public async Task Restore_target_late_entry_is_preserved_and_prevents_publication()
    {
        var target = Path.Combine(_root, "late-entry-target");
        Directory.CreateDirectory(target);
        var lateEntry = Path.Combine(target, "operator-late-entry");
        var engine = CreateEngine(testHook: stage =>
        {
            if (stage == "restore_file_copied" && !File.Exists(lateEntry))
                File.WriteAllText(lateEntry, "preserve");
        });
        var saved = await engine.SaveAsync(new(Key, Source, Vault));

        await Assert.ThrowsAsync<LocalEngineException>(() => engine.RestoreAsync(
            new(saved.RecoveryPointId, Vault, target)));

        Assert.Equal("preserve", await File.ReadAllTextAsync(lateEntry));
        Assert.False(Directory.Exists(Path.Combine(target, LocalRestoreCoordinator.PublicationName)));
    }

    [Fact]
    public async Task Restore_failure_after_publication_rolls_back_exact_owned_child()
    {
        var target = Path.Combine(_root, "rollback-target");
        Directory.CreateDirectory(target);
        var engine = CreateEngine(testHook: stage =>
        {
            if (stage == "restore_published") throw new IOException("synthetic failure");
        });
        var saved = await engine.SaveAsync(new(Key, Source, Vault));

        await Assert.ThrowsAsync<LocalEngineException>(() => engine.RestoreAsync(
            new(saved.RecoveryPointId, Vault, target)));

        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
        await using var connection = new SqliteConnection(
            $"Data Source={Path.Combine(Vault, "Upload Queue", LocalVaultLayout.QueueDatabaseName)}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT status FROM local_restore_attempts;";
        Assert.Equal("failed", await command.ExecuteScalarAsync());
        var inspection = await engine.InspectVaultStateAsync(Vault);
        Assert.Equal(1, inspection.RestoreAttentionCount);
    }

    [Fact]
    public async Task Restore_preserves_unknown_staging_and_conflicting_publication()
    {
        var engine = CreateEngine();
        var saved = await engine.SaveAsync(new(Key, Source, Vault));
        var target = Path.Combine(_root, "unknown-stage-target");
        Directory.CreateDirectory(target);
        var stage = Path.Combine(target, $".showvault-restore-{saved.RecoveryPointId}");
        Directory.CreateDirectory(stage);
        var unknown = Path.Combine(stage, "operator-file");
        await File.WriteAllTextAsync(unknown, "preserve");

        await Assert.ThrowsAsync<LocalEngineException>(() => engine.RestoreAsync(
            new(saved.RecoveryPointId, Vault, target)));

        Assert.Equal("preserve", await File.ReadAllTextAsync(unknown));
    }

    [Fact]
    public async Task Restore_reselect_rejects_mutated_copy_or_evidence()
    {
        var engine = CreateEngine();
        var saved = await engine.SaveAsync(new(Key, Source, Vault));
        var target = Path.Combine(_root, "mutated-repeat-target");
        Directory.CreateDirectory(target);
        var restored = await engine.RestoreAsync(new(saved.RecoveryPointId, Vault, target));
        var restoredFile = Path.Combine(
            target, LocalRestoreCoordinator.PublicationName, "database V2");
        await File.WriteAllTextAsync(restoredFile, "tampered");

        await Assert.ThrowsAnyAsync<Exception>(() => engine.RestoreAsync(
            new(saved.RecoveryPointId, Vault, target)));

        await File.WriteAllTextAsync(restoredFile, "library");
        var evidence = Path.Combine(
            Vault, "Reports", "Restores", $"{restored.RestoreEvidenceId}.json");
        MakeWritable(evidence);
        await File.AppendAllTextAsync(evidence, " ");
        await Assert.ThrowsAsync<LocalEngineException>(() => engine.RestoreAsync(
            new(saved.RecoveryPointId, Vault, target)));
    }

    [Fact]
    public async Task Restore_detects_package_mutation_during_copy()
    {
        string? packageContent = null;
        var engine = CreateEngine(testHook: stage =>
        {
            if (stage != "restore_file_copied" || packageContent is null) return;
            MakeWritable(packageContent);
            File.WriteAllText(packageContent, "changed");
            packageContent = null;
        });
        var saved = await engine.SaveAsync(new(Key, Source, Vault));
        var package = Assert.Single(Directory.EnumerateDirectories(
            Path.Combine(Vault, "Backups", "Serato DJ Pro")));
        packageContent = Path.Combine(package, "content", "database V2");
        var target = Path.Combine(_root, "package-race-target");
        Directory.CreateDirectory(target);

        await Assert.ThrowsAnyAsync<Exception>(() => engine.RestoreAsync(
            new(saved.RecoveryPointId, Vault, target)));

        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
    }

    [Fact]
    public async Task Restore_detects_multiply_linked_staging_file()
    {
        if (OperatingSystem.IsWindows()) return;
        var target = Path.Combine(_root, "hard-link-target");
        Directory.CreateDirectory(target);
        var linked = false;
        string? recoveryPointId = null;
        var engine = CreateEngine(testHook: stage =>
        {
            if (stage != "restore_file_copied" || linked || recoveryPointId is null) return;
            var restored = Path.Combine(
                target, $".showvault-restore-{recoveryPointId}", "restored");
            var file = Directory.EnumerateFiles(restored, "*", SearchOption.AllDirectories).First();
            Assert.Equal(0, CreateHardLink(file, Path.Combine(_root, "outside-hard-link")));
            linked = true;
        });
        var saved = await engine.SaveAsync(new(Key, Source, Vault));
        recoveryPointId = saved.RecoveryPointId;

        await Assert.ThrowsAsync<LocalEngineException>(() => engine.RestoreAsync(
            new(saved.RecoveryPointId, Vault, target)));

        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
    }

    [Fact]
    public async Task Restore_detects_selected_target_identity_swap_and_rolls_back_original()
    {
        if (OperatingSystem.IsWindows()) return;
        var target = Path.Combine(_root, "swapped-target");
        var original = $"{target}-original";
        Directory.CreateDirectory(target);
        var engine = CreateEngine(testHook: stage =>
        {
            if (stage != "restore_staging_verified" || Directory.Exists(original)) return;
            Directory.Move(target, original);
            Directory.CreateDirectory(target);
        });
        var saved = await engine.SaveAsync(new(Key, Source, Vault));

        await Assert.ThrowsAsync<LocalEngineException>(() => engine.RestoreAsync(
            new(saved.RecoveryPointId, Vault, target)));

        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
        Assert.Empty(Directory.EnumerateFileSystemEntries(original));
    }

    [Fact]
    public async Task Restore_cancellation_after_publication_finishes_durable_success()
    {
        using var cancellation = new CancellationTokenSource();
        var target = Path.Combine(_root, "late-cancel-target");
        Directory.CreateDirectory(target);
        var engine = CreateEngine(testHook: stage =>
        {
            if (stage == "restore_published") cancellation.Cancel();
        });
        var saved = await engine.SaveAsync(new(Key, Source, Vault));

        var restored = await engine.RestoreAsync(
            new(saved.RecoveryPointId, Vault, target), cancellation.Token);

        Assert.Equal("restored", restored.LocalStatus);
        Assert.True(Directory.Exists(Path.Combine(target, LocalRestoreCoordinator.PublicationName)));
    }

    [Fact]
    public async Task Packaged_host_restore_protocol_is_closed_and_path_free()
    {
        var engine = CreateEngine();
        var saved = await engine.SaveAsync(new(Key, Source, Vault));
        var target = Path.Combine(_root, "host-target");
        Directory.CreateDirectory(target);
        var start = new ProcessStartInfo(HostExecutablePath())
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException();
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new
        {
            operation = "restore",
            recoveryPointId = saved.RecoveryPointId,
            selectedVault = Vault,
            selectedTarget = target
        }));
        process.StandardInput.Close();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, process.ExitCode);
        Assert.Empty(error);
        Assert.Contains("\"type\":\"progress\"", output, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"result\"", output, StringComparison.Ordinal);
        Assert.Contains("\"localStatus\":\"restored\"", output, StringComparison.Ordinal);
        Assert.DoesNotContain(Vault, output, StringComparison.Ordinal);
        Assert.DoesNotContain(target, output, StringComparison.Ordinal);
        Assert.DoesNotContain(Source, output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restore_reselect_repairs_owned_interrupted_stage_and_durable_state()
    {
        var engine = CreateEngine();
        var saved = await engine.SaveAsync(new(Key, Source, Vault));
        var target = Path.Combine(_root, "restart-target");
        Directory.CreateDirectory(target);
        var stage = Path.Combine(target, $".showvault-restore-{saved.RecoveryPointId}");
        Directory.CreateDirectory(Path.Combine(stage, "restored"));
        await File.WriteAllBytesAsync(Path.Combine(stage, "intent.json"),
            JsonSerializer.SerializeToUtf8Bytes(new LocalRestoreIntent(
                "1.0", saved.RecoveryPointId, LocalRestoreCoordinator.PublicationName),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        await File.WriteAllTextAsync(Path.Combine(stage, "restored", "partial"), "partial");
        using (var layout = LocalVaultLayout.OpenOrCreate(Vault))
        {
            var queue = new LocalVaultQueueStore(layout.QueueDatabasePath);
            await queue.InitializeAsync(TestContext.Current.CancellationToken);
            await queue.RecordRestoreStagingAsync(
                "0123456789abcdef0123456789abcdef", saved.RecoveryPointId,
                saved.RecoveryPointId, saved.FileCount, saved.TotalBytes,
                DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        }

        var result = await engine.RestoreAsync(new(saved.RecoveryPointId, Vault, target));

        Assert.Equal("restored", result.LocalStatus);
        var inspection = await engine.InspectVaultStateAsync(Vault);
        Assert.Equal(0, inspection.RestoreAttentionCount);
        await using var connection = new SqliteConnection(
            $"Data Source={Path.Combine(Vault, "Upload Queue", LocalVaultLayout.QueueDatabaseName)}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT status FROM local_restore_attempts ORDER BY created_at;";
        var statuses = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) statuses.Add(reader.GetString(0));
        Assert.Contains("failed", statuses);
        Assert.Contains("completed", statuses);
    }

    private LocalRecoveryEngine CreateEngine(
        LocalEngineLimits? limits = null,
        Action<string>? testHook = null)
    {
        var authorizer = new LocalCatalogAuthorizer(
            new Dictionary<string, string>(),
            Path.Combine(_root, "home"));
        var time = new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero));
        return testHook is null
            ? new LocalRecoveryEngine(authorizer, limits, time)
            : new LocalRecoveryEngine(authorizer, limits, time, testHook);
    }

    private async Task<IReadOnlyList<string>> ReadStatusesAsync()
    {
        var database = Path.Combine(Vault, "Upload Queue", LocalVaultLayout.QueueDatabaseName);
        if (!File.Exists(database)) return [];
        await using var connection = new SqliteConnection($"Data Source={database}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT status FROM local_recovery_points WHERE status = 'queued';";
        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result;
    }

    private async Task<IReadOnlyList<string>> ReadAllStatusesAsync()
    {
        var database = Path.Combine(Vault, "Upload Queue", LocalVaultLayout.QueueDatabaseName);
        if (!File.Exists(database)) return [];
        await using var connection = new SqliteConnection($"Data Source={database}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT status FROM local_recovery_points ORDER BY created_at;";
        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result;
    }

    private async Task SetOnlyStatusAsync(string status)
    {
        var database = Path.Combine(Vault, "Upload Queue", LocalVaultLayout.QueueDatabaseName);
        await using var connection = new SqliteConnection($"Data Source={database}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE local_recovery_points SET status = $status;";
        command.Parameters.AddWithValue("$status", status);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static void MakeWritable(string path)
    {
        if (OperatingSystem.IsWindows())
            File.SetAttributes(path, FileAttributes.Normal);
        else
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static string HostExecutablePath(
        [CallerFilePath] string sourceFile = "") => Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(sourceFile)!, "..", "..", "src",
            "ShowVault.LocalEngine.Host", "bin", "Release", "net10.0",
            OperatingSystem.IsWindows() ? "showvault-local-engine.exe" : "showvault-local-engine"));

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int CreateHardLink(string existingPath, string newPath);

    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int MakeFifo(string path, uint mode);
}
