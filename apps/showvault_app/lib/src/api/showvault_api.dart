import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:showvault_app/src/config/app_config.dart';
import 'package:showvault_app/src/recovery/recovery_run.dart';

class RecoveryHistory {
  const RecoveryHistory({
    required this.organizationName,
    required this.venueName,
    required this.runs,
  });

  final String organizationName;
  final String venueName;
  final List<RecoveryRun> runs;
}

class ShowVaultApi {
  ShowVaultApi({http.Client? client}) : _client = client ?? http.Client();

  final http.Client _client;

  Future<RecoveryHistory> loadRecoveryHistory(String accessToken) async {
    final organizations = await _getList('/api/v1/organizations', accessToken);
    if (organizations.isEmpty) {
      return const RecoveryHistory(
        organizationName: 'No organization',
        venueName: 'No venue',
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
        organizationName: organization['name']! as String,
        venueName: 'No venue',
        runs: const [],
      );
    }

    final venue = venues.first;
    final runs = await _getList(
      '/api/v1/organizations/$organizationId/venues/${venue['id']}/recovery-runs',
      accessToken,
    );
    return RecoveryHistory(
      organizationName: organization['name']! as String,
      venueName: venue['name']! as String,
      runs: runs.map(RecoveryRun.fromJson).toList(growable: false),
    );
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
