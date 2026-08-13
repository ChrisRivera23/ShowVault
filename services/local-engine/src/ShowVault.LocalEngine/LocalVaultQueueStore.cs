using Microsoft.Data.Sqlite;

namespace ShowVault.LocalEngine;

internal sealed class LocalVaultQueueStore(string databasePath)
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Private,
        Pooling = false
    }.ToString();

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = FULL;
            PRAGMA busy_timeout = 5000;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var schema = connection.CreateCommand();
        schema.Transaction = (SqliteTransaction)transaction;
        schema.CommandText = """
            CREATE TABLE IF NOT EXISTS local_recovery_points (
                operation_id TEXT PRIMARY KEY,
                recovery_point_id TEXT NULL UNIQUE,
                candidate_key TEXT NOT NULL,
                product_name TEXT NOT NULL,
                package_relative_path TEXT NULL UNIQUE,
                file_count INTEGER NULL,
                total_bytes INTEGER NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                status TEXT NOT NULL CHECK(status IN
                    ('staging','verified','queued','failed','cancelled')),
                attempt_count INTEGER NOT NULL DEFAULT 0,
                last_error_code TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_local_recovery_points_status
                ON local_recovery_points(status, updated_at);
            CREATE TABLE IF NOT EXISTS local_restore_attempts (
                attempt_id TEXT PRIMARY KEY,
                recovery_point_id TEXT NOT NULL,
                evidence_id TEXT NULL UNIQUE,
                manifest_sha256 TEXT NOT NULL,
                file_count INTEGER NOT NULL,
                total_bytes INTEGER NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                completed_at TEXT NULL,
                status TEXT NOT NULL CHECK(status IN
                    ('staging','published','verified','completed','failed','cancelled')),
                last_error_code TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_local_restore_attempts_recovery
                ON local_restore_attempts(recovery_point_id, updated_at DESC);
            CREATE TABLE IF NOT EXISTS local_sync_attempts (
                recovery_point_id TEXT PRIMARY KEY,
                organization_id TEXT NOT NULL,
                venue_id TEXT NOT NULL,
                status TEXT NOT NULL CHECK(status IN
                    ('synchronizing','retry_scheduled','attention','synchronized')),
                attempt_count INTEGER NOT NULL,
                next_retry_at TEXT NULL,
                remote_manifest_sha256 TEXT NOT NULL,
                remote_session_id TEXT NULL,
                receipt_sha256 TEXT NULL,
                completed_at TEXT NULL,
                updated_at TEXT NOT NULL,
                last_error_code TEXT NULL,
                FOREIGN KEY(recovery_point_id) REFERENCES local_recovery_points(recovery_point_id)
            );
            CREATE INDEX IF NOT EXISTS ix_local_sync_attempts_status
                ON local_sync_attempts(status, updated_at);
            """;
        await schema.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordStagingAsync(
        string operationId,
        string candidateKey,
        string productName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO local_recovery_points
                (operation_id, candidate_key, product_name, created_at, updated_at, status)
            VALUES ($operationId, $candidateKey, $productName, $now, $now, 'staging');
            """;
        command.Parameters.AddWithValue("$operationId", operationId);
        command.Parameters.AddWithValue("$candidateKey", candidateKey);
        command.Parameters.AddWithValue("$productName", productName);
        command.Parameters.AddWithValue("$now", Format(now));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordVerifiedAsync(
        string operationId,
        string recoveryPointId,
        string relativePath,
        int fileCount,
        long totalBytes,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE local_recovery_points
            SET recovery_point_id = $recoveryPointId,
                package_relative_path = $relativePath,
                file_count = $fileCount,
                total_bytes = $totalBytes,
                updated_at = $now,
                status = 'verified'
            WHERE operation_id = $operationId AND status = 'staging';
            """;
        AddPackageParameters(command, operationId, recoveryPointId, relativePath,
            fileCount, totalBytes, now);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new LocalEngineException("The local Save state changed unexpectedly.");
        }
    }

    public async Task RecordQueuedAsync(
        string operationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            UPDATE local_recovery_points
            SET status = 'queued', updated_at = $now, last_error_code = NULL
            WHERE operation_id = $operationId AND status = 'verified';
            """;
        command.Parameters.AddWithValue("$operationId", operationId);
        command.Parameters.AddWithValue("$now", Format(now));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new LocalEngineException("Only a verified recovery point can be queued.");
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordTerminalAsync(
        string operationId,
        string status,
        string errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (status is not ("failed" or "cancelled"))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE local_recovery_points
            SET status = $status, updated_at = $now, last_error_code = $error
            WHERE operation_id = $operationId AND status IN ('staging','verified');
            """;
        command.Parameters.AddWithValue("$operationId", operationId);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$now", Format(now));
        command.Parameters.AddWithValue("$error", errorCode);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QueuedRecoveryPoint>> ListQueuedAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT points.operation_id, points.recovery_point_id,
                   points.candidate_key, points.product_name,
                   points.package_relative_path, points.file_count,
                   points.total_bytes, points.created_at,
                   COALESCE(sync.status, 'queued')
            FROM local_recovery_points AS points
            LEFT JOIN local_sync_attempts AS sync
              ON sync.recovery_point_id = points.recovery_point_id
            WHERE points.status = 'queued'
            ORDER BY points.created_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        var records = new List<QueuedRecoveryPoint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new(
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.GetInt32(5),
                reader.GetInt64(6), DateTimeOffset.Parse(reader.GetString(7)),
                reader.GetString(8)));
        }
        return records;
    }

    public async Task<QueuedRecoveryPoint?> GetQueuedAsync(
        string recoveryPointId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT points.operation_id, points.recovery_point_id,
                   points.candidate_key, points.product_name,
                   points.package_relative_path, points.file_count,
                   points.total_bytes, points.created_at,
                   COALESCE(sync.status, 'queued')
            FROM local_recovery_points AS points
            LEFT JOIN local_sync_attempts AS sync
              ON sync.recovery_point_id = points.recovery_point_id
            WHERE points.status = 'queued'
              AND points.recovery_point_id = $recoveryPointId;
            """;
        command.Parameters.AddWithValue("$recoveryPointId", recoveryPointId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(
            reader.GetString(0), reader.GetString(1), reader.GetString(2),
            reader.GetString(3), reader.GetString(4), reader.GetInt32(5),
            reader.GetInt64(6), DateTimeOffset.Parse(reader.GetString(7)),
            reader.GetString(8));
    }

    public async Task<int> BeginSyncAsync(
        string recoveryPointId,
        Guid organizationId,
        Guid venueId,
        string remoteManifestSha256,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO local_sync_attempts
                (recovery_point_id, organization_id, venue_id, status,
                 attempt_count, remote_manifest_sha256, updated_at)
            VALUES ($recoveryPointId, $organizationId, $venueId,
                    'synchronizing', 1, $manifestSha256, $now)
            ON CONFLICT(recovery_point_id) DO UPDATE SET
                organization_id = excluded.organization_id,
                venue_id = excluded.venue_id,
                status = 'synchronizing',
                attempt_count = local_sync_attempts.attempt_count + 1,
                next_retry_at = NULL,
                remote_manifest_sha256 = excluded.remote_manifest_sha256,
                updated_at = excluded.updated_at,
                last_error_code = NULL
            WHERE local_sync_attempts.status != 'synchronized'
            RETURNING attempt_count;
            """;
        command.Parameters.AddWithValue("$recoveryPointId", recoveryPointId);
        command.Parameters.AddWithValue("$organizationId", organizationId.ToString("D"));
        command.Parameters.AddWithValue("$venueId", venueId.ToString("D"));
        command.Parameters.AddWithValue("$manifestSha256", remoteManifestSha256);
        command.Parameters.AddWithValue("$now", Format(now));
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is long attemptCount ? checked((int)attemptCount) : 1;
    }

    public async Task RecordSyncSessionAsync(
        string recoveryPointId,
        string sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE local_sync_attempts
            SET remote_session_id = $sessionId, updated_at = $now
            WHERE recovery_point_id = $recoveryPointId
              AND status = 'synchronizing';
            """;
        command.Parameters.AddWithValue("$recoveryPointId", recoveryPointId);
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$now", Format(now));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new LocalEngineException("The synchronization state changed unexpectedly.");
    }

    public Task<bool> RecordSyncRetryAsync(
        string recoveryPointId,
        string errorCode,
        DateTimeOffset nextRetryAt,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        RecordSyncStateAsync(recoveryPointId, "retry_scheduled", errorCode,
            nextRetryAt, now, cancellationToken);

    public Task<bool> RecordSyncAttentionAsync(
        string recoveryPointId,
        string errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        RecordSyncStateAsync(recoveryPointId, "attention", errorCode,
            null, now, cancellationToken);

    public async Task CompleteSyncAsync(
        string recoveryPointId,
        string receiptSha256,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            UPDATE local_sync_attempts
            SET status = 'synchronized', receipt_sha256 = $receiptSha256,
                completed_at = $completedAt, updated_at = $completedAt,
                next_retry_at = NULL, last_error_code = NULL
            WHERE recovery_point_id = $recoveryPointId
              AND status = 'synchronizing';
            """;
        command.Parameters.AddWithValue("$recoveryPointId", recoveryPointId);
        command.Parameters.AddWithValue("$receiptSha256", receiptSha256);
        command.Parameters.AddWithValue("$completedAt", Format(completedAt));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new LocalEngineException("The synchronization completion state changed unexpectedly.");
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<bool> RecordSyncStateAsync(
        string recoveryPointId,
        string status,
        string errorCode,
        DateTimeOffset? nextRetryAt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE local_sync_attempts
            SET status = $status, next_retry_at = $nextRetryAt,
                updated_at = $now, last_error_code = $errorCode
            WHERE recovery_point_id = $recoveryPointId
              AND status = 'synchronizing';
            """;
        command.Parameters.AddWithValue("$recoveryPointId", recoveryPointId);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$nextRetryAt",
            nextRetryAt is null ? DBNull.Value : Format(nextRetryAt.Value));
        command.Parameters.AddWithValue("$now", Format(now));
        command.Parameters.AddWithValue("$errorCode", errorCode);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task RecordQueuedIntegrityFailureAsync(
        string recoveryPointId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE local_recovery_points
            SET status = 'failed', updated_at = $now,
                last_error_code = 'sync_reverify_failed'
            WHERE recovery_point_id = $recoveryPointId AND status = 'queued';
            """;
        command.Parameters.AddWithValue("$recoveryPointId", recoveryPointId);
        command.Parameters.AddWithValue("$now", Format(now));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordRestoreStagingAsync(
        string attemptId,
        string recoveryPointId,
        string manifestSha256,
        int fileCount,
        long totalBytes,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO local_restore_attempts
                (attempt_id, recovery_point_id, manifest_sha256, file_count,
                 total_bytes, created_at, updated_at, status)
            VALUES ($attemptId, $recoveryPointId, $manifestSha256, $fileCount,
                    $totalBytes, $now, $now, 'staging');
            """;
        AddRestoreParameters(command, attemptId, recoveryPointId, manifestSha256,
            fileCount, totalBytes, now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<CompletedLocalRestore?> GetCompletedRestoreAsync(
        string recoveryPointId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT evidence_id, file_count, total_bytes, completed_at
            FROM local_restore_attempts
            WHERE recovery_point_id = $recoveryPointId AND status = 'completed'
            ORDER BY completed_at DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$recoveryPointId", recoveryPointId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(reader.GetString(0), reader.GetInt32(1), reader.GetInt64(2),
            DateTimeOffset.Parse(reader.GetString(3)));
    }

    public async Task TransitionRestoreAsync(
        string attemptId,
        string expectedStatus,
        string status,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (status is not ("published" or "verified"))
            throw new ArgumentOutOfRangeException(nameof(status));
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE local_restore_attempts
            SET status = $status, updated_at = $now, last_error_code = NULL
            WHERE attempt_id = $attemptId AND status = $expectedStatus;
            """;
        command.Parameters.AddWithValue("$attemptId", attemptId);
        command.Parameters.AddWithValue("$expectedStatus", expectedStatus);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$now", Format(now));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new LocalEngineException("The local Restore state changed unexpectedly.");
    }

    public async Task CompleteRestoreAsync(
        string attemptId,
        string evidenceId,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            UPDATE local_restore_attempts
            SET evidence_id = $evidenceId, completed_at = $completedAt,
                updated_at = $completedAt, status = 'completed', last_error_code = NULL
            WHERE attempt_id = $attemptId AND status = 'verified';
            """;
        command.Parameters.AddWithValue("$attemptId", attemptId);
        command.Parameters.AddWithValue("$evidenceId", evidenceId);
        command.Parameters.AddWithValue("$completedAt", Format(completedAt));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new LocalEngineException("The local Restore completion state changed unexpectedly.");
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordRestoreTerminalAsync(
        string attemptId,
        string status,
        string errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (status is not ("failed" or "cancelled"))
            throw new ArgumentOutOfRangeException(nameof(status));
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE local_restore_attempts
            SET status = $status, updated_at = $now, last_error_code = $errorCode
            WHERE attempt_id = $attemptId
              AND status IN ('staging','published','verified');
            """;
        command.Parameters.AddWithValue("$attemptId", attemptId);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$now", Format(now));
        command.Parameters.AddWithValue("$errorCode", errorCode);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkInterruptedRestoresFailedAsync(
        string recoveryPointId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE local_restore_attempts
            SET status = 'failed', updated_at = $now,
                last_error_code = 'restart_interrupted'
            WHERE recovery_point_id = $recoveryPointId
              AND status IN ('staging','published','verified');
            """;
        command.Parameters.AddWithValue("$recoveryPointId", recoveryPointId);
        command.Parameters.AddWithValue("$now", Format(now));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RepairableRecoveryPoint>> ListRepairableAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT operation_id, recovery_point_id, product_name, package_relative_path,
                   status
            FROM local_recovery_points
            WHERE status IN ('staging','verified')
            ORDER BY created_at
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        var records = new List<RepairableRecoveryPoint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4)));
        }
        return records;
    }

    public async Task<IReadOnlySet<string>> ListKnownPackagePathsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT package_relative_path
            FROM local_recovery_points
            WHERE package_relative_path IS NOT NULL
              AND status IN ('verified','queued')
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var paths = new HashSet<string>(comparer);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) paths.Add(reader.GetString(0));
        return paths;
    }

    internal async Task<string?> GetStatusAsync(
        string operationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT status FROM local_recovery_points WHERE operation_id = $operationId;";
        command.Parameters.AddWithValue("$operationId", operationId);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    public async Task<int> CountAttentionAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
              (SELECT COUNT(*) FROM local_recovery_points
               WHERE status IN ('staging','verified','failed')) +
              (SELECT COUNT(*) FROM local_sync_attempts
               WHERE status = 'attention');
            """;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<int> CountRestoreAttentionAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM local_restore_attempts AS attention
            WHERE attention.status IN ('staging','published','verified','failed')
              AND NOT EXISTS (
                  SELECT 1 FROM local_restore_attempts AS completed
                  WHERE completed.recovery_point_id = attention.recovery_point_id
                    AND completed.status = 'completed'
                    AND completed.updated_at >= attention.updated_at
              );
            """;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<int> CountRecoveryPointsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM local_recovery_points;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static void AddPackageParameters(
        SqliteCommand command,
        string operationId,
        string recoveryPointId,
        string relativePath,
        int fileCount,
        long totalBytes,
        DateTimeOffset now)
    {
        command.Parameters.AddWithValue("$operationId", operationId);
        command.Parameters.AddWithValue("$recoveryPointId", recoveryPointId);
        command.Parameters.AddWithValue("$relativePath", relativePath);
        command.Parameters.AddWithValue("$fileCount", fileCount);
        command.Parameters.AddWithValue("$totalBytes", totalBytes);
        command.Parameters.AddWithValue("$now", Format(now));
    }

    private static void AddRestoreParameters(
        SqliteCommand command,
        string attemptId,
        string recoveryPointId,
        string manifestSha256,
        int fileCount,
        long totalBytes,
        DateTimeOffset now)
    {
        command.Parameters.AddWithValue("$attemptId", attemptId);
        command.Parameters.AddWithValue("$recoveryPointId", recoveryPointId);
        command.Parameters.AddWithValue("$manifestSha256", manifestSha256);
        command.Parameters.AddWithValue("$fileCount", fileCount);
        command.Parameters.AddWithValue("$totalBytes", totalBytes);
        command.Parameters.AddWithValue("$now", Format(now));
    }

    private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O");
}

internal sealed record QueuedRecoveryPoint(
    string OperationId,
    string RecoveryPointId,
    string CandidateKey,
    string ProductName,
    string PackageRelativePath,
    int FileCount,
    long TotalBytes,
    DateTimeOffset CreatedAt,
    string CloudStatus);

internal sealed record RepairableRecoveryPoint(
    string OperationId,
    string? RecoveryPointId,
    string ProductName,
    string? PackageRelativePath,
    string Status);

internal sealed record CompletedLocalRestore(
    string EvidenceId,
    int FileCount,
    long TotalBytes,
    DateTimeOffset CompletedAt);
