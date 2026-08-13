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
            SELECT operation_id, recovery_point_id, candidate_key, product_name,
                   package_relative_path, file_count, total_bytes, created_at
            FROM local_recovery_points
            WHERE status = 'queued'
            ORDER BY created_at DESC
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
                reader.GetInt64(6), DateTimeOffset.Parse(reader.GetString(7))));
        }
        return records;
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
            SELECT COUNT(*) FROM local_recovery_points
            WHERE status IN ('staging','verified','failed');
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
    DateTimeOffset CreatedAt);

internal sealed record RepairableRecoveryPoint(
    string OperationId,
    string? RecoveryPointId,
    string ProductName,
    string? PackageRelativePath,
    string Status);
