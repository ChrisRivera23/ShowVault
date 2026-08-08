import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:showvault_app/src/config/app_config.dart';
import 'package:showvault_app/src/recovery/recovery_run.dart';

class RecoveryHistory {
  const RecoveryHistory({
    required this.organizationId,
    required this.organizationName,
    required this.venueId,
    required this.venueName,
    required this.agents,
    required this.candidates,
    required this.runs,
  });

  final String organizationId;
  final String organizationName;
  final String venueId;
  final String venueName;
  final List<VenueAgent> agents;
  final List<RecoveryCandidate> candidates;
  final List<RecoveryRun> runs;
}

class RecoveryCandidate {
  const RecoveryCandidate({
    required this.id,
    required this.agentName,
    required this.productName,
    required this.candidateType,
    required this.evidence,
    required this.decision,
  });

  factory RecoveryCandidate.fromJson(Map<String, Object?> json) =>
      RecoveryCandidate(
        id: json['id']! as String,
        agentName: json['agentName']! as String,
        productName: json['productName']! as String,
        candidateType: json['candidateType']! as String,
        evidence: json['evidence']! as String,
        decision: json['decision']! as String,
      );

  final String id;
  final String agentName;
  final String productName;
  final String candidateType;
  final String evidence;
  final String decision;
}

class VenueAgent {
  const VenueAgent({required this.id, required this.name});

  factory VenueAgent.fromJson(Map<String, Object?> json) =>
      VenueAgent(id: json['id']! as String, name: json['name']! as String);

  final String id;
  final String name;
}

class ShowVaultApi {
  ShowVaultApi({http.Client? client}) : _client = client ?? http.Client();

  final http.Client _client;

  Future<RecoveryHistory> loadRecoveryHistory(String accessToken) async {
    final organizations = await _getList('/api/v1/organizations', accessToken);
    if (organizations.isEmpty) {
      return const RecoveryHistory(
        organizationId: '',
        organizationName: 'No organization',
        venueId: '',
        venueName: 'No venue',
        agents: [],
        candidates: [],
        runs: [],
      );
    }

    final organization = organizations.first;
    final organizationId = organization['id']! as String;
    final venues = await _getList(
      '/api/v1/organizations/$organizationId/venues',
      accessToken,
    );
    if (venues.isEmpty) {
      return RecoveryHistory(
        organizationId: organizationId,
        organizationName: organization['name']! as String,
        venueId: '',
        venueName: 'No venue',
        agents: const [],
        candidates: const [],
        runs: const [],
      );
    }

    final venue = venues.first;
    final venueId = venue['id']! as String;
    final agents = await _getList(
      '/api/v1/organizations/$organizationId/venues/$venueId/agents',
      accessToken,
    );
    final runs = await _getList(
      '/api/v1/organizations/$organizationId/venues/$venueId/recovery-runs',
      accessToken,
    );
    final candidates = await _getList(
      '/api/v1/organizations/$organizationId/venues/$venueId/recovery-candidates',
      accessToken,
    );
    return RecoveryHistory(
      organizationId: organizationId,
      organizationName: organization['name']! as String,
      venueId: venueId,
      venueName: venue['name']! as String,
      agents: agents.map(VenueAgent.fromJson).toList(growable: false),
      candidates: candidates
          .map(RecoveryCandidate.fromJson)
          .toList(growable: false),
      runs: runs.map(RecoveryRun.fromJson).toList(growable: false),
    );
  }

  Future<void> decideRecoveryCandidate({
    required String accessToken,
    required RecoveryHistory history,
    required String candidateId,
    required bool approved,
  }) async {
    final response = await _client.put(
      Uri.parse(
        '${AppConfig.apiBaseUrl}/api/v1/organizations/${history.organizationId}'
        '/venues/${history.venueId}/recovery-candidates/$candidateId/decision',
      ),
      headers: {
        'Authorization': 'Bearer $accessToken',
        'Content-Type': 'application/json',
      },
      body: jsonEncode({'approved': approved}),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ShowVaultApiException(response.statusCode);
    }
  }

  Future<String> validateRecoveryCandidate({
    required String accessToken,
    required RecoveryHistory history,
    required String candidateId,
  }) async {
    final response = await _client.post(
      Uri.parse(
        '${AppConfig.apiBaseUrl}/api/v1/organizations/${history.organizationId}'
        '/venues/${history.venueId}/recovery-candidates/$candidateId/validate',
      ),
      headers: {
        'Authorization': 'Bearer $accessToken',
        'Content-Type': 'application/json',
      },
      body: jsonEncode({'maxFiles': 1000}),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ShowVaultApiException(response.statusCode);
    }
    final body = jsonDecode(response.body) as Map<String, Object?>;
    final command = body['payload']! as Map<String, Object?>;
    return command['commandId']! as String;
  }

  Future<String> startDiscovery({
    required String accessToken,
    required RecoveryHistory history,
    required String agentId,
    required String pluginId,
    required String rootPath,
  }) => _issueRecoveryCommand(accessToken, history, agentId, 'discover', {
    'pluginId': pluginId,
    'rootPath': rootPath,
    'maxFiles': 1000,
  });

  Future<String> createBackup({
    required String accessToken,
    required RecoveryHistory history,
    required String agentId,
    required String discoveryCommandId,
  }) => _issueRecoveryCommand(accessToken, history, agentId, 'backup', {
    'discoveryCommandId': discoveryCommandId,
  });

  Future<String> verifyBackup({
    required String accessToken,
    required RecoveryHistory history,
    required String agentId,
    required String backupCommandId,
  }) => _issueRecoveryCommand(accessToken, history, agentId, 'verify', {
    'backupCommandId': backupCommandId,
  });

  Future<String> startRestore({
    required String accessToken,
    required RecoveryHistory history,
    required String agentId,
    required String backupCommandId,
    required String verificationCommandId,
    required String targetPath,
  }) => _issueRecoveryCommand(accessToken, history, agentId, 'restore', {
    'backupCommandId': backupCommandId,
    'verificationCommandId': verificationCommandId,
    'targetPath': targetPath,
  });

  Future<String> _issueRecoveryCommand(
    String accessToken,
    RecoveryHistory history,
    String agentId,
    String stage,
    Map<String, Object?> payload,
  ) async {
    final response = await _client.post(
      Uri.parse(
        '${AppConfig.apiBaseUrl}/api/v1/organizations/${history.organizationId}'
        '/venues/${history.venueId}/agents/$agentId/recovery/$stage',
      ),
      headers: {
        'Authorization': 'Bearer $accessToken',
        'Content-Type': 'application/json',
      },
      body: jsonEncode(payload),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ShowVaultApiException(response.statusCode);
    }
    final body = jsonDecode(response.body) as Map<String, Object?>;
    final command = body['payload']! as Map<String, Object?>;
    return command['commandId']! as String;
  }

  Future<List<Map<String, Object?>>> _getList(
    String path,
    String accessToken,
  ) async {
    final response = await _client.get(
      Uri.parse('${AppConfig.apiBaseUrl}$path'),
      headers: {'Authorization': 'Bearer $accessToken'},
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ShowVaultApiException(response.statusCode);
    }
    final body = jsonDecode(response.body) as Map<String, Object?>;
    final payload = body['payload']! as List<Object?>;
    return payload
        .map((item) => item! as Map<String, Object?>)
        .toList(growable: false);
  }
}

class ShowVaultApiException implements Exception {
  const ShowVaultApiException(this.statusCode);
  final int statusCode;

  @override
  String toString() => 'ShowVault API request failed ($statusCode).';
}
