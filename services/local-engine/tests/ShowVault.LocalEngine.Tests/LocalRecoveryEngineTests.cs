using System.Security.Cryptography;
using System.Runtime.InteropServices;
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

    private static void MakeWritable(string path)
    {
        if (OperatingSystem.IsWindows())
            File.SetAttributes(path, FileAttributes.Normal);
        else
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int CreateHardLink(string existingPath, string newPath);

    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int MakeFifo(string path, uint mode);
}
