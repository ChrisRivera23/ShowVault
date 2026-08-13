import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:showvault_app/src/config/app_config.dart';
import 'package:showvault_app/src/recovery/recovery_run.dart';

class RecoveryHistory {
  const RecoveryHistory({
    this.organizationId = '',
    required this.organizationName,
    this.venueId = '',
    required this.venueName,
    required this.runs,
  });

  final String organizationId;
  final String organizationName;
  final String venueId;
  final String venueName;
  final List<RecoveryRun> runs;
}

class ShowVaultApi {
  ShowVaultApi({http.Client? client, String? baseUrl})
    : _client = client ?? http.Client(),
      _baseUri = _parseBaseUri(baseUrl ?? AppConfig.apiBaseUrl);

  final http.Client _client;
  final Uri _baseUri;

  Future<RecoveryHistory> loadRecoveryHistory(String accessToken) async {
    if (accessToken.trim().isEmpty) {
      throw ArgumentError.value(
        accessToken,
        'accessToken',
        'must not be empty',
      );
    }

    final organizations = await _getList('/api/v1/organizations', accessToken);
    if (organizations.isEmpty) {
      return const RecoveryHistory(
        organizationId: '',
        organizationName: 'No organization',
        venueId: '',
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
        organizationId: organizationId,
        organizationName: organization['name']! as String,
        venueId: '',
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
      organizationId: organizationId,
      organizationName: organization['name']! as String,
      venueId: venue['id']! as String,
      venueName: venue['name']! as String,
      runs: runs.map(RecoveryRun.fromJson).toList(growable: false),
    );
  }

  Future<int> submitComputerScan({
    required String accessToken,
    required RecoveryHistory history,
    required List<String> candidateKeys,
  }) async {
    if (history.organizationId.isEmpty || history.venueId.isEmpty) {
      throw ArgumentError('A tenant-scoped venue is required.');
    }
    final response = await _client.post(
      _baseUri.resolve(
        '/api/v1/organizations/${history.organizationId}'
        '/venues/${history.venueId}/computer-scans',
      ),
      headers: {
        'Authorization': 'Bearer $accessToken',
        'Content-Type': 'application/json',
      },
      body: jsonEncode({'candidateKeys': candidateKeys}),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ShowVaultApiException(response.statusCode);
    }
    final body = jsonDecode(response.body) as Map<String, Object?>;
    return (body['payload']! as Map<String, Object?>)['candidateCount']! as int;
  }

  Future<List<Map<String, Object?>>> _getList(
    String path,
    String accessToken,
  ) async {
    final response = await _client.get(
      _baseUri.resolve(path),
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

  static Uri _parseBaseUri(String value) {
    final uri = Uri.tryParse(value);
    if (uri == null ||
        uri.scheme != 'https' ||
        !uri.hasAuthority ||
        uri.host.isEmpty ||
        uri.userInfo.isNotEmpty ||
        (uri.path.isNotEmpty && uri.path != '/') ||
        uri.hasQuery ||
        uri.hasFragment) {
      throw ArgumentError.value(
        value,
        'baseUrl',
        'must be an HTTPS origin without credentials, a path, query, or fragment',
      );
    }
    return uri;
  }
}

class ShowVaultApiException implements Exception {
  const ShowVaultApiException(this.statusCode);
  final int statusCode;

  @override
  String toString() => 'ShowVault API request failed ($statusCode).';
}
