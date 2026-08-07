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
                delivered_at TEXT NULL
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
    }

    public async Task EnqueueEventAsync(
        AgentEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
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
            WHERE delivered_at IS NULL AND next_attempt_at <= $now
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
            "UPDATE event_outbox SET delivered_at = $value WHERE event_id = $eventId;",
            eventId,
            deliveredAt,
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
            WHERE event_id = $eventId AND delivered_at IS NULL;
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
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT envelope_json FROM command_queue
            WHERE status = 'pending' ORDER BY issued_at;
            """;
        var commands = new List<AgentCommandEnvelope>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            commands.Add(JsonSerializer.Deserialize<AgentCommandEnvelope>(reader.GetString(0), JsonOptions)
                ?? throw new InvalidOperationException("Stored Agent command is invalid."));
        }

        return commands;
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
}

public sealed record QueuedAgentEvent(AgentEventEnvelope Envelope, int AttemptCount);
