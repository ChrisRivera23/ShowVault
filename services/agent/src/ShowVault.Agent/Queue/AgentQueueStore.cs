using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Plugins;
using ShowVault.AgentContracts;

namespace ShowVault.Agent.Queue;

public sealed class AgentQueueStore(IOptions<AgentOptions> options) : IApprovedRecoveryScopeProvider
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
            CREATE TABLE IF NOT EXISTS discovery_results (
                command_id TEXT PRIMARY KEY,
                result_json TEXT NOT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY(command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS recovery_packages (
                command_id TEXT PRIMARY KEY,
                package_id TEXT NOT NULL,
                package_path TEXT NOT NULL,
                manifest_json TEXT NOT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY(command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS package_verifications (
                command_id TEXT PRIMARY KEY,
                package_id TEXT NOT NULL,
                result_json TEXT NOT NULL,
                evidence_sha256 TEXT NOT NULL,
                verified_at TEXT NOT NULL,
                FOREIGN KEY(command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS recovery_restorations (
                command_id TEXT PRIMARY KEY,
                package_id TEXT NOT NULL,
                target_path TEXT NOT NULL,
                result_json TEXT NOT NULL,
                evidence_sha256 TEXT NOT NULL,
                restored_at TEXT NOT NULL,
                FOREIGN KEY(command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS recovery_restore_intents (
                command_id TEXT PRIMARY KEY,
                package_id TEXT NOT NULL,
                target_path TEXT NOT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY(command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS recovery_candidates (
                candidate_id TEXT PRIMARY KEY,
                plugin_id TEXT NOT NULL,
                product_name TEXT NOT NULL,
                candidate_type TEXT NOT NULL,
                local_path TEXT NOT NULL,
                evidence TEXT NOT NULL,
                detected_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS approved_recovery_scopes (
                candidate_id TEXT PRIMARY KEY,
                plugin_id TEXT NOT NULL,
                product_name TEXT NOT NULL,
                candidate_type TEXT NOT NULL,
                local_path TEXT NOT NULL,
                approved_at TEXT NOT NULL,
                FOREIGN KEY(candidate_id) REFERENCES recovery_candidates(candidate_id)
                    ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS subnet_proposals (
                proposal_id TEXT PRIMARY KEY,
                network TEXT NOT NULL,
                prefix_length INTEGER NOT NULL,
                interface_type TEXT NOT NULL,
                evidence TEXT NOT NULL,
                detected_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS approved_subnets (
                proposal_id TEXT PRIMARY KEY,
                network TEXT NOT NULL,
                prefix_length INTEGER NOT NULL,
                approved_at TEXT NOT NULL,
                FOREIGN KEY(proposal_id) REFERENCES subnet_proposals(proposal_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS subnet_reachable_hosts (
                authorization_command_id TEXT NOT NULL,
                proposal_id TEXT NOT NULL,
                address TEXT NOT NULL,
                discovered_at TEXT NOT NULL,
                PRIMARY KEY (authorization_command_id, address),
                FOREIGN KEY(authorization_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(proposal_id) REFERENCES approved_subnets(proposal_id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS ix_subnet_reachable_hosts_proposal
                ON subnet_reachable_hosts(proposal_id, authorization_command_id);
            CREATE TABLE IF NOT EXISTS ma_lighting_identifications (
                identification_command_id TEXT NOT NULL,
                discovery_command_id TEXT NOT NULL,
                proposal_id TEXT NOT NULL,
                address TEXT NOT NULL,
                product_family TEXT NOT NULL,
                identified_at TEXT NOT NULL,
                PRIMARY KEY (identification_command_id, address),
                FOREIGN KEY(identification_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(discovery_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(proposal_id) REFERENCES approved_subnets(proposal_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS yamaha_dme_identifications (
                identification_command_id TEXT NOT NULL,
                discovery_command_id TEXT NOT NULL,
                proposal_id TEXT NOT NULL,
                address TEXT NOT NULL,
                product_family TEXT NOT NULL,
                identified_at TEXT NOT NULL,
                PRIMARY KEY (identification_command_id, address),
                FOREIGN KEY(identification_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(discovery_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(proposal_id) REFERENCES approved_subnets(proposal_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS grandma2_identifications (
                identification_command_id TEXT NOT NULL,
                discovery_command_id TEXT NOT NULL,
                proposal_id TEXT NOT NULL,
                address TEXT NOT NULL,
                product_family TEXT NOT NULL,
                identified_at TEXT NOT NULL,
                PRIMARY KEY (identification_command_id, address),
                FOREIGN KEY(identification_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(discovery_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(proposal_id) REFERENCES approved_subnets(proposal_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS pjlink_identifications (
                identification_command_id TEXT NOT NULL,
                discovery_command_id TEXT NOT NULL,
                proposal_id TEXT NOT NULL,
                address TEXT NOT NULL,
                product_family TEXT NOT NULL,
                identified_at TEXT NOT NULL,
                PRIMARY KEY (identification_command_id, address),
                FOREIGN KEY(identification_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(discovery_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(proposal_id) REFERENCES approved_subnets(proposal_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS blackmagic_videohub_identifications (
                identification_command_id TEXT NOT NULL,
                discovery_command_id TEXT NOT NULL,
                proposal_id TEXT NOT NULL,
                address TEXT NOT NULL,
                product_family TEXT NOT NULL,
                identified_at TEXT NOT NULL,
                PRIMARY KEY (identification_command_id, address),
                FOREIGN KEY(identification_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(discovery_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(proposal_id) REFERENCES approved_subnets(proposal_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS newtek_tricaster_identifications (
                identification_command_id TEXT NOT NULL,
                discovery_command_id TEXT NOT NULL,
                proposal_id TEXT NOT NULL,
                address TEXT NOT NULL,
                product_family TEXT NOT NULL,
                identified_at TEXT NOT NULL,
                PRIMARY KEY (identification_command_id, address),
                FOREIGN KEY(identification_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(discovery_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(proposal_id) REFERENCES approved_subnets(proposal_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS birddog_identifications (
                identification_command_id TEXT NOT NULL,
                discovery_command_id TEXT NOT NULL,
                proposal_id TEXT NOT NULL,
                address TEXT NOT NULL,
                product_family TEXT NOT NULL,
                identified_at TEXT NOT NULL,
                PRIMARY KEY (identification_command_id, address),
                FOREIGN KEY(identification_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(discovery_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(proposal_id) REFERENCES approved_subnets(proposal_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS panasonic_camera_identifications (
                identification_command_id TEXT NOT NULL,
                discovery_command_id TEXT NOT NULL,
                proposal_id TEXT NOT NULL,
                address TEXT NOT NULL,
                product_family TEXT NOT NULL,
                identified_at TEXT NOT NULL,
                PRIMARY KEY (identification_command_id, address),
                FOREIGN KEY(identification_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(discovery_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(proposal_id) REFERENCES approved_subnets(proposal_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS sony_camera_identifications (
                identification_command_id TEXT NOT NULL,
                discovery_command_id TEXT NOT NULL,
                proposal_id TEXT NOT NULL,
                address TEXT NOT NULL,
                product_family TEXT NOT NULL,
                identified_at TEXT NOT NULL,
                PRIMARY KEY (identification_command_id, address),
                FOREIGN KEY(identification_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(discovery_command_id) REFERENCES command_queue(command_id) ON DELETE CASCADE,
                FOREIGN KEY(proposal_id) REFERENCES approved_subnets(proposal_id) ON DELETE CASCADE
            );
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

    public async Task StoreDiscoveryResultAsync(
        Guid commandId,
        string resultJson,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultJson);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO discovery_results (command_id, result_json, created_at)
            VALUES ($commandId, $resultJson, $createdAt)
            ON CONFLICT(command_id) DO UPDATE SET
                result_json = excluded.result_json,
                created_at = excluded.created_at;
            """;
        command.Parameters.AddWithValue("$commandId", commandId.ToString());
        command.Parameters.AddWithValue("$resultJson", resultJson);
        command.Parameters.AddWithValue("$createdAt", Format(createdAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<string?> GetDiscoveryResultJsonAsync(
        Guid commandId,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT result_json FROM discovery_results WHERE command_id = $commandId;
            """;
        command.Parameters.AddWithValue("$commandId", commandId.ToString());
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    public async Task StoreRecoveryCandidatesAsync(
        IReadOnlyList<LocalRecoveryCandidate> candidates,
        DateTimeOffset detectedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(
            cancellationToken);
        foreach (var candidate in candidates)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT OR IGNORE INTO recovery_candidates
                    (candidate_id, plugin_id, product_name, candidate_type, local_path, evidence, detected_at)
                VALUES ($candidateId, $pluginId, $productName, $candidateType, $localPath, $evidence, $detectedAt);
                """;
            command.Parameters.AddWithValue("$candidateId", candidate.CandidateId.ToString());
            command.Parameters.AddWithValue("$pluginId", candidate.PluginId);
            command.Parameters.AddWithValue("$productName", candidate.ProductName);
            command.Parameters.AddWithValue("$candidateType", candidate.CandidateType);
            command.Parameters.AddWithValue("$localPath", candidate.Path);
            command.Parameters.AddWithValue("$evidence", candidate.Evidence);
            command.Parameters.AddWithValue("$detectedAt", Format(detectedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task StoreSubnetProposalsAsync(
        IReadOnlyList<LocalSubnetProposal> proposals,
        DateTimeOffset detectedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var proposal in proposals)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO subnet_proposals
                    (proposal_id, network, prefix_length, interface_type, evidence, detected_at)
                VALUES ($id, $network, $prefix, $type, $evidence, $detectedAt);
                """;
            command.Parameters.AddWithValue("$id", proposal.ProposalId.ToString());
            command.Parameters.AddWithValue("$network", proposal.Network);
            command.Parameters.AddWithValue("$prefix", proposal.PrefixLength);
            command.Parameters.AddWithValue("$type", proposal.InterfaceType);
            command.Parameters.AddWithValue("$evidence", proposal.Evidence);
            command.Parameters.AddWithValue("$detectedAt", Format(detectedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<bool> ApplySubnetProposalDecisionAsync(
        Guid proposalId,
        bool approved,
        DateTimeOffset decidedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = approved
            ? """
                INSERT INTO approved_subnets (proposal_id, network, prefix_length, approved_at)
                SELECT proposal_id, network, prefix_length, $decidedAt
                FROM subnet_proposals WHERE proposal_id = $id
                ON CONFLICT(proposal_id) DO UPDATE SET approved_at = excluded.approved_at;
                """
            : "DELETE FROM approved_subnets WHERE proposal_id = $id;";
        command.Parameters.AddWithValue("$id", proposalId.ToString());
        command.Parameters.AddWithValue("$decidedAt", Format(decidedAt));
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (approved)
        {
            return affected == 1;
        }

        await using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT COUNT(*) FROM subnet_proposals WHERE proposal_id = $id;";
        exists.Parameters.AddWithValue("$id", proposalId.ToString());
        return Convert.ToInt64(await exists.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    public async Task<ApprovedSubnet?> GetApprovedSubnetAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT proposal_id, network, prefix_length FROM approved_subnets WHERE proposal_id = $id;";
        command.Parameters.AddWithValue("$id", proposalId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ApprovedSubnet(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetInt32(2))
            : null;
    }

    public async Task StoreReachableSubnetHostsAsync(
        Guid authorizationCommandId,
        Guid proposalId,
        IReadOnlyList<System.Net.IPAddress> addresses,
        DateTimeOffset discoveredAt,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        foreach (var address in addresses)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT OR IGNORE INTO subnet_reachable_hosts
                    (authorization_command_id, proposal_id, address, discovered_at)
                VALUES ($commandId, $proposalId, $address, $discoveredAt);
                """;
            command.Parameters.AddWithValue("$commandId", authorizationCommandId.ToString());
            command.Parameters.AddWithValue("$proposalId", proposalId.ToString());
            command.Parameters.AddWithValue("$address", address.ToString());
            command.Parameters.AddWithValue("$discoveredAt", Format(discoveredAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetReachableSubnetHostsAsync(
        Guid authorizationCommandId,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT address FROM subnet_reachable_hosts
            WHERE authorization_command_id = $commandId ORDER BY address;
            """;
        command.Parameters.AddWithValue("$commandId", authorizationCommandId.ToString());
        var addresses = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) addresses.Add(reader.GetString(0));
        return addresses;
    }

    public async Task<bool> IsReachableHostAuthorizationAsync(
        Guid proposalId, Guid authorizationCommandId, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM subnet_reachable_hosts
            WHERE proposal_id = $proposalId AND authorization_command_id = $commandId;
            """;
        command.Parameters.AddWithValue("$proposalId", proposalId.ToString());
        command.Parameters.AddWithValue("$commandId", authorizationCommandId.ToString());
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0;
    }

    public async Task StoreMaLightingIdentificationsAsync(
        Guid identificationCommandId,
        MaLightingIdentificationResult result,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var identification in result.Identifications)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO ma_lighting_identifications
                    (identification_command_id, discovery_command_id, proposal_id, address,
                     product_family, identified_at)
                VALUES ($identificationId, $discoveryId, $proposalId, $address, $family, $at);
                """;
            command.Parameters.AddWithValue("$identificationId", identificationCommandId.ToString());
            command.Parameters.AddWithValue("$discoveryId", result.DiscoveryCommandId.ToString());
            command.Parameters.AddWithValue("$proposalId", result.ProposalId.ToString());
            command.Parameters.AddWithValue("$address", identification.Address.ToString());
            command.Parameters.AddWithValue("$family", identification.ProductFamily);
            command.Parameters.AddWithValue("$at", Format(result.CompletedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task StoreYamahaDmeIdentificationsAsync(
        Guid identificationCommandId,
        YamahaDmeIdentificationResult result,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var identification in result.Identifications)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO yamaha_dme_identifications
                    (identification_command_id, discovery_command_id, proposal_id, address,
                     product_family, identified_at)
                VALUES ($identificationId, $discoveryId, $proposalId, $address, $family, $at);
                """;
            command.Parameters.AddWithValue("$identificationId", identificationCommandId.ToString());
            command.Parameters.AddWithValue("$discoveryId", result.DiscoveryCommandId.ToString());
            command.Parameters.AddWithValue("$proposalId", result.ProposalId.ToString());
            command.Parameters.AddWithValue("$address", identification.Address.ToString());
            command.Parameters.AddWithValue("$family", identification.ProductFamily);
            command.Parameters.AddWithValue("$at", Format(result.CompletedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task StoreGrandMa2IdentificationsAsync(
        Guid identificationCommandId,
        GrandMa2IdentificationResult result,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var identification in result.Identifications)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO grandma2_identifications
                    (identification_command_id, discovery_command_id, proposal_id, address,
                     product_family, identified_at)
                VALUES ($identificationId, $discoveryId, $proposalId, $address, $family, $at);
                """;
            command.Parameters.AddWithValue("$identificationId", identificationCommandId.ToString());
            command.Parameters.AddWithValue("$discoveryId", result.DiscoveryCommandId.ToString());
            command.Parameters.AddWithValue("$proposalId", result.ProposalId.ToString());
            command.Parameters.AddWithValue("$address", identification.Address.ToString());
            command.Parameters.AddWithValue("$family", identification.ProductFamily);
            command.Parameters.AddWithValue("$at", Format(result.CompletedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task StorePjLinkIdentificationsAsync(
        Guid identificationCommandId,
        PjLinkIdentificationResult result,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var identification in result.Identifications)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO pjlink_identifications
                    (identification_command_id, discovery_command_id, proposal_id, address,
                     product_family, identified_at)
                VALUES ($identificationId, $discoveryId, $proposalId, $address, $family, $at);
                """;
            command.Parameters.AddWithValue("$identificationId", identificationCommandId.ToString());
            command.Parameters.AddWithValue("$discoveryId", result.DiscoveryCommandId.ToString());
            command.Parameters.AddWithValue("$proposalId", result.ProposalId.ToString());
            command.Parameters.AddWithValue("$address", identification.Address.ToString());
            command.Parameters.AddWithValue("$family", identification.ProductFamily);
            command.Parameters.AddWithValue("$at", Format(result.CompletedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task StoreBlackmagicVideohubIdentificationsAsync(
        Guid identificationCommandId,
        BlackmagicVideohubIdentificationResult result,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var identification in result.Identifications)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO blackmagic_videohub_identifications
                    (identification_command_id, discovery_command_id, proposal_id, address,
                     product_family, identified_at)
                VALUES ($identificationId, $discoveryId, $proposalId, $address, $family, $at);
                """;
            command.Parameters.AddWithValue("$identificationId", identificationCommandId.ToString());
            command.Parameters.AddWithValue("$discoveryId", result.DiscoveryCommandId.ToString());
            command.Parameters.AddWithValue("$proposalId", result.ProposalId.ToString());
            command.Parameters.AddWithValue("$address", identification.Address.ToString());
            command.Parameters.AddWithValue("$family", identification.ProductFamily);
            command.Parameters.AddWithValue("$at", Format(result.CompletedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task StoreNewTekTriCasterIdentificationsAsync(
        Guid identificationCommandId,
        NewTekTriCasterIdentificationResult result,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var identification in result.Identifications)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO newtek_tricaster_identifications
                    (identification_command_id, discovery_command_id, proposal_id, address,
                     product_family, identified_at)
                VALUES ($identificationId, $discoveryId, $proposalId, $address, $family, $at);
                """;
            command.Parameters.AddWithValue("$identificationId", identificationCommandId.ToString());
            command.Parameters.AddWithValue("$discoveryId", result.DiscoveryCommandId.ToString());
            command.Parameters.AddWithValue("$proposalId", result.ProposalId.ToString());
            command.Parameters.AddWithValue("$address", identification.Address.ToString());
            command.Parameters.AddWithValue("$family", identification.ProductFamily);
            command.Parameters.AddWithValue("$at", Format(result.CompletedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task StoreBirdDogIdentificationsAsync(
        Guid identificationCommandId,
        BirdDogIdentificationResult result,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var identification in result.Identifications)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO birddog_identifications
                    (identification_command_id, discovery_command_id, proposal_id, address,
                     product_family, identified_at)
                VALUES ($identificationId, $discoveryId, $proposalId, $address, $family, $at);
                """;
            command.Parameters.AddWithValue("$identificationId", identificationCommandId.ToString());
            command.Parameters.AddWithValue("$discoveryId", result.DiscoveryCommandId.ToString());
            command.Parameters.AddWithValue("$proposalId", result.ProposalId.ToString());
            command.Parameters.AddWithValue("$address", identification.Address.ToString());
            command.Parameters.AddWithValue("$family", identification.ProductFamily);
            command.Parameters.AddWithValue("$at", Format(result.CompletedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task StorePanasonicCameraIdentificationsAsync(
        Guid identificationCommandId,
        PanasonicCameraIdentificationResult result,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var identification in result.Identifications)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO panasonic_camera_identifications
                    (identification_command_id, discovery_command_id, proposal_id, address,
                     product_family, identified_at)
                VALUES ($identificationId, $discoveryId, $proposalId, $address, $family, $at);
                """;
            command.Parameters.AddWithValue("$identificationId", identificationCommandId.ToString());
            command.Parameters.AddWithValue("$discoveryId", result.DiscoveryCommandId.ToString());
            command.Parameters.AddWithValue("$proposalId", result.ProposalId.ToString());
            command.Parameters.AddWithValue("$address", identification.Address.ToString());
            command.Parameters.AddWithValue("$family", identification.ProductFamily);
            command.Parameters.AddWithValue("$at", Format(result.CompletedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task StoreSonyCameraIdentificationsAsync(
        Guid identificationCommandId,
        SonyCameraIdentificationResult result,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var identification in result.Identifications)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO sony_camera_identifications
                    (identification_command_id, discovery_command_id, proposal_id, address,
                     product_family, identified_at)
                VALUES ($identificationId, $discoveryId, $proposalId, $address, $family, $at);
                """;
            command.Parameters.AddWithValue("$identificationId", identificationCommandId.ToString());
            command.Parameters.AddWithValue("$discoveryId", result.DiscoveryCommandId.ToString());
            command.Parameters.AddWithValue("$proposalId", result.ProposalId.ToString());
            command.Parameters.AddWithValue("$address", identification.Address.ToString());
            command.Parameters.AddWithValue("$family", identification.ProductFamily);
            command.Parameters.AddWithValue("$at", Format(result.CompletedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<bool> ApplyRecoveryCandidateDecisionAsync(
        Guid candidateId,
        bool approved,
        DateTimeOffset decidedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        if (approved)
        {
            command.CommandText = """
                INSERT INTO approved_recovery_scopes
                    (candidate_id, plugin_id, product_name, candidate_type, local_path, approved_at)
                SELECT candidate_id, plugin_id, product_name, candidate_type, local_path, $decidedAt
                FROM recovery_candidates WHERE candidate_id = $candidateId
                ON CONFLICT(candidate_id) DO UPDATE SET approved_at = excluded.approved_at;
                """;
            command.Parameters.AddWithValue("$decidedAt", Format(decidedAt));
        }
        else
        {
            command.CommandText = """
                DELETE FROM approved_recovery_scopes WHERE candidate_id = $candidateId;
                """;
        }

        command.Parameters.AddWithValue("$candidateId", candidateId.ToString());
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (!approved)
        {
            return await RecoveryCandidateExistsAsync(connection, candidateId, cancellationToken);
        }

        return affected == 1;
    }

    public async Task<IReadOnlyList<ApprovedRecoveryScope>> GetApprovedRecoveryScopesAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT candidate_id, plugin_id, product_name, candidate_type, local_path, approved_at
            FROM approved_recovery_scopes ORDER BY approved_at, candidate_id;
            """;
        var scopes = new List<ApprovedRecoveryScope>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            scopes.Add(new ApprovedRecoveryScope(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture)));
        }

        return scopes;
    }

    public async Task<ApprovedRecoveryScope?> GetApprovedRecoveryScopeAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT candidate_id, plugin_id, product_name, candidate_type, local_path, approved_at
            FROM approved_recovery_scopes WHERE candidate_id = $candidateId;
            """;
        command.Parameters.AddWithValue("$candidateId", candidateId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadApprovedRecoveryScope(reader)
            : null;
    }

    public async Task<bool> IsApprovedExactScopeAsync(
        string pluginId,
        string localPath,
        CancellationToken cancellationToken)
    {
        var normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(localPath));
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT local_path FROM approved_recovery_scopes WHERE plugin_id = $pluginId;
            """;
        command.Parameters.AddWithValue("$pluginId", pluginId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (Path.GetRelativePath(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(reader.GetString(0))),
                normalizedPath) == ".")
            {
                return true;
            }
        }

        return false;
    }

    private static ApprovedRecoveryScope ReadApprovedRecoveryScope(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture));

    private static async Task<bool> RecoveryCandidateExistsAsync(
        SqliteConnection connection,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM recovery_candidates WHERE candidate_id = $candidateId);";
        command.Parameters.AddWithValue("$candidateId", candidateId.ToString());
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    public async Task StoreRecoveryPackageAsync(
        Guid commandId,
        string packageId,
        string packagePath,
        string manifestJson,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO recovery_packages
                (command_id, package_id, package_path, manifest_json, created_at)
            VALUES ($commandId, $packageId, $packagePath, $manifestJson, $createdAt)
            ON CONFLICT(command_id) DO UPDATE SET
                package_id = excluded.package_id,
                package_path = excluded.package_path,
                manifest_json = excluded.manifest_json,
                created_at = excluded.created_at;
            """;
        command.Parameters.AddWithValue("$commandId", commandId.ToString());
        command.Parameters.AddWithValue("$packageId", packageId);
        command.Parameters.AddWithValue("$packagePath", packagePath);
        command.Parameters.AddWithValue("$manifestJson", manifestJson);
        command.Parameters.AddWithValue("$createdAt", Format(createdAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<StoredRecoveryPackage?> GetRecoveryPackageAsync(
        Guid commandId,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT package_id, package_path, manifest_json
            FROM recovery_packages WHERE command_id = $commandId;
            """;
        command.Parameters.AddWithValue("$commandId", commandId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new StoredRecoveryPackage(reader.GetString(0), reader.GetString(1), reader.GetString(2))
            : null;
    }

    public async Task StorePackageVerificationAsync(
        Guid commandId,
        string packageId,
        string resultJson,
        string evidenceSha256,
        DateTimeOffset verifiedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO package_verifications
                (command_id, package_id, result_json, evidence_sha256, verified_at)
            VALUES ($commandId, $packageId, $resultJson, $evidenceSha256, $verifiedAt);
            """;
        command.Parameters.AddWithValue("$commandId", commandId.ToString());
        command.Parameters.AddWithValue("$packageId", packageId);
        command.Parameters.AddWithValue("$resultJson", resultJson);
        command.Parameters.AddWithValue("$evidenceSha256", evidenceSha256);
        command.Parameters.AddWithValue("$verifiedAt", Format(verifiedAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<StoredPackageVerification?> GetPackageVerificationAsync(
        Guid commandId,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT package_id, result_json, evidence_sha256
            FROM package_verifications WHERE command_id = $commandId;
            """;
        command.Parameters.AddWithValue("$commandId", commandId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new StoredPackageVerification(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2))
            : null;
    }

    public async Task StoreRecoveryRestorationAsync(
        Guid commandId,
        string packageId,
        string targetPath,
        string resultJson,
        string evidenceSha256,
        DateTimeOffset restoredAt,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO recovery_restorations
                (command_id, package_id, target_path, result_json, evidence_sha256, restored_at)
            VALUES ($commandId, $packageId, $targetPath, $resultJson, $evidenceSha256, $restoredAt);
            """;
        command.Parameters.AddWithValue("$commandId", commandId.ToString());
        command.Parameters.AddWithValue("$packageId", packageId);
        command.Parameters.AddWithValue("$targetPath", targetPath);
        command.Parameters.AddWithValue("$resultJson", resultJson);
        command.Parameters.AddWithValue("$evidenceSha256", evidenceSha256);
        command.Parameters.AddWithValue("$restoredAt", Format(restoredAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task StoreRecoveryRestoreIntentAsync(
        Guid commandId,
        string packageId,
        string targetPath,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO recovery_restore_intents
                (command_id, package_id, target_path, created_at)
            VALUES ($commandId, $packageId, $targetPath, $createdAt);
            """;
        command.Parameters.AddWithValue("$commandId", commandId.ToString());
        command.Parameters.AddWithValue("$packageId", packageId);
        command.Parameters.AddWithValue("$targetPath", targetPath);
        command.Parameters.AddWithValue("$createdAt", Format(createdAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<StoredRecoveryRestoreIntent?> GetRecoveryRestoreIntentAsync(
        Guid commandId,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT package_id, target_path
            FROM recovery_restore_intents WHERE command_id = $commandId;
            """;
        command.Parameters.AddWithValue("$commandId", commandId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new StoredRecoveryRestoreIntent(reader.GetString(0), reader.GetString(1))
            : null;
    }

    public async Task<StoredRecoveryRestoration?> GetRecoveryRestorationAsync(
        Guid commandId,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT package_id, target_path, result_json, evidence_sha256
            FROM recovery_restorations WHERE command_id = $commandId;
            """;
        command.Parameters.AddWithValue("$commandId", commandId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new StoredRecoveryRestoration(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3))
            : null;
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

public sealed record StoredRecoveryPackage(
    string PackageId,
    string PackagePath,
    string ManifestJson);

public sealed record StoredPackageVerification(
    string PackageId,
    string ResultJson,
    string EvidenceSha256);

public sealed record StoredRecoveryRestoration(
    string PackageId,
    string TargetPath,
    string ResultJson,
    string EvidenceSha256);

public sealed record StoredRecoveryRestoreIntent(string PackageId, string TargetPath);

public sealed record ApprovedRecoveryScope(
    Guid CandidateId,
    string PluginId,
    string ProductName,
    string CandidateType,
    string LocalPath,
    DateTimeOffset ApprovedAt);

public enum LocalAgentCommandStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
