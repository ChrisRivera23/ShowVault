using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using ShowVault.AgentContracts;

namespace ShowVault.Agent.Queue;

public sealed class AgentQueueStore(IOptions<AgentOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString = BuildConnectionString(options.Value.DataDirectory);

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS event_outbox (
                event_id TEXT PRIMARY KEY,
                envelope_json TEXT NOT NULL,
                occurred_at TEXT NOT NULL,
                attempt_count INTEGER NOT NULL DEFAULT 0,
                next_attempt_at TEXT NOT NULL,
                delivered_at TEXT NULL,
                rejected_at TEXT NULL,
                delivery_status TEXT NOT NULL DEFAULT 'pending'
            );
            CREATE INDEX IF NOT EXISTS ix_event_outbox_pending
                ON event_outbox(delivered_at, next_attempt_at, occurred_at);
            CREATE TABLE IF NOT EXISTS command_queue (
                command_id TEXT PRIMARY KEY,
                envelope_json TEXT NOT NULL,
                issued_at TEXT NOT NULL,
                status TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_command_queue_status
                ON command_queue(status, issued_at);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureEventOutboxColumnAsync(
            connection,
            "rejected_at",
            "rejected_at TEXT NULL",
            cancellationToken);
        await EnsureEventOutboxColumnAsync(
            connection,
            "delivery_status",
            "delivery_status TEXT NOT NULL DEFAULT 'pending'",
            cancellationToken);
    }

    public async Task EnqueueEventAsync(
        AgentEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (!AgentEventValidation.TryValidate(envelope, out var validationError))
        {
            throw new ArgumentException(validationError, nameof(envelope));
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO event_outbox
                (event_id, envelope_json, occurred_at, next_attempt_at)
            VALUES ($eventId, $envelope, $occurredAt, $nextAttemptAt);
            """;
        command.Parameters.AddWithValue("$eventId", envelope.EventId.ToString());
        command.Parameters.AddWithValue("$envelope", JsonSerializer.Serialize(envelope, JsonOptions));
        command.Parameters.AddWithValue("$occurredAt", Format(envelope.OccurredAt));
        command.Parameters.AddWithValue("$nextAttemptAt", Format(envelope.OccurredAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QueuedAgentEvent>> GetPendingEventsAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT envelope_json, attempt_count
            FROM event_outbox
            WHERE delivery_status = 'pending' AND next_attempt_at <= $now
            ORDER BY occurred_at
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$now", Format(now));
        command.Parameters.AddWithValue("$limit", limit);

        var events = new List<QueuedAgentEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var envelope = JsonSerializer.Deserialize<AgentEventEnvelope>(reader.GetString(0), JsonOptions)
                ?? throw new InvalidOperationException("Stored Agent event is invalid.");
            events.Add(new QueuedAgentEvent(envelope, reader.GetInt32(1)));
        }

        return events;
    }

    public Task MarkEventDeliveredAsync(
        Guid eventId,
        DateTimeOffset deliveredAt,
        CancellationToken cancellationToken) =>
        UpdateEventAsync(
            """
            UPDATE event_outbox
            SET delivered_at = $value, delivery_status = 'delivered'
            WHERE event_id = $eventId AND delivery_status = 'pending';
            """,
            eventId,
            deliveredAt,
            cancellationToken);

    public Task MarkEventPermanentlyRejectedAsync(
        Guid eventId,
        DateTimeOffset rejectedAt,
        CancellationToken cancellationToken) =>
        UpdateEventAsync(
            """
            UPDATE event_outbox
            SET rejected_at = $value, delivery_status = 'rejected'
            WHERE event_id = $eventId AND delivery_status = 'pending';
            """,
            eventId,
            rejectedAt,
            cancellationToken);

    public async Task RecordEventFailureAsync(
        Guid eventId,
        DateTimeOffset nextAttemptAt,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE event_outbox
            SET attempt_count = attempt_count + 1, next_attempt_at = $value
            WHERE event_id = $eventId AND delivery_status = 'pending';
            """;
        command.Parameters.AddWithValue("$eventId", eventId.ToString());
        command.Parameters.AddWithValue("$value", Format(nextAttemptAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task EnqueueCommandAsync(
        AgentCommandEnvelope envelope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO command_queue
                (command_id, envelope_json, issued_at, status, updated_at)
            VALUES ($commandId, $envelope, $issuedAt, 'pending', $updatedAt);
            """;
        command.Parameters.AddWithValue("$commandId", envelope.CommandId.ToString());
        command.Parameters.AddWithValue("$envelope", JsonSerializer.Serialize(envelope, JsonOptions));
        command.Parameters.AddWithValue("$issuedAt", Format(envelope.IssuedAt));
        command.Parameters.AddWithValue("$updatedAt", Format(now));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AgentCommandEnvelope>> GetPendingCommandsAsync(
        CancellationToken cancellationToken)
        => await GetCommandsAsync(LocalAgentCommandStatus.Pending, cancellationToken);

    public async Task<IReadOnlyList<AgentCommandEnvelope>> GetCommandsAsync(
        LocalAgentCommandStatus status,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT envelope_json FROM command_queue
            WHERE status = $status ORDER BY issued_at;
            """;
        command.Parameters.AddWithValue("$status", Format(status));
        var commands = new List<AgentCommandEnvelope>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            commands.Add(JsonSerializer.Deserialize<AgentCommandEnvelope>(reader.GetString(0), JsonOptions)
                ?? throw new InvalidOperationException("Stored Agent command is invalid."));
        }

        return commands;
    }

    public async Task<bool> TryTransitionCommandAsync(
        Guid commandId,
        LocalAgentCommandStatus expectedStatus,
        LocalAgentCommandStatus nextStatus,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!IsAllowedTransition(expectedStatus, nextStatus))
        {
            return false;
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE command_queue SET status = $nextStatus, updated_at = $updatedAt
            WHERE command_id = $commandId AND status = $expectedStatus;
            """;
        command.Parameters.AddWithValue("$commandId", commandId.ToString());
        command.Parameters.AddWithValue("$expectedStatus", Format(expectedStatus));
        command.Parameters.AddWithValue("$nextStatus", Format(nextStatus));
        command.Parameters.AddWithValue("$updatedAt", Format(now));
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private async Task UpdateEventAsync(
        string sql,
        Guid eventId,
        DateTimeOffset value,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$eventId", eventId.ToString());
        command.Parameters.AddWithValue("$value", Format(value));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureEventOutboxColumnAsync(
        SqliteConnection connection,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        await using var inspect = connection.CreateCommand();
        inspect.CommandText = "PRAGMA table_info(event_outbox);";
        var exists = false;
        await using (var reader = await inspect.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.Ordinal))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (exists)
        {
            return;
        }

        await using var migrate = connection.CreateCommand();
        migrate.CommandText = $"ALTER TABLE event_outbox ADD COLUMN {columnDefinition};";
        await migrate.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string BuildConnectionString(string? configuredDirectory)
    {
        var directory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ShowVault",
                "Agent")
            : Path.GetFullPath(configuredDirectory);
        Directory.CreateDirectory(directory);
        return new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, "agent-queue.db"),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    private static string Format(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static string Format(LocalAgentCommandStatus status) =>
        status.ToString().ToLowerInvariant();

    private static bool IsAllowedTransition(
        LocalAgentCommandStatus current,
        LocalAgentCommandStatus next) =>
        (current, next) switch
        {
            (LocalAgentCommandStatus.Pending, LocalAgentCommandStatus.Running) => true,
            (LocalAgentCommandStatus.Pending, LocalAgentCommandStatus.Cancelled) => true,
            (LocalAgentCommandStatus.Running, LocalAgentCommandStatus.Completed) => true,
            (LocalAgentCommandStatus.Running, LocalAgentCommandStatus.Failed) => true,
            (LocalAgentCommandStatus.Running, LocalAgentCommandStatus.Cancelled) => true,
            _ => false
        };
}

public sealed record QueuedAgentEvent(AgentEventEnvelope Envelope, int AttemptCount);

public enum LocalAgentCommandStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
