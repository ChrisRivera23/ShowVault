import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:showvault_app/src/config/app_config.dart';
import 'package:showvault_app/src/recovery/recovery_run.dart';

class RecoveryHistory {
  const RecoveryHistory({
    this.organizationId = '',
    required this.organizationName,
    this.organizationRole = '',
    this.venueId = '',
    required this.venueName,
    required this.runs,
  });

  final String organizationId;
  final String organizationName;
  final String organizationRole;
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
        organizationRole: organization['role']! as String,
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
      organizationRole: organization['role']! as String,
      venueId: venue['id']! as String,
      venueName: venue['name']! as String,
      runs: runs.map(RecoveryRun.fromJson).toList(growable: false),
    );
  }

  Future<OrganizationPlan> loadOrganizationPlan({
    required String accessToken,
    required String organizationId,
  }) async {
    if (accessToken.trim().isEmpty || organizationId.trim().isEmpty) {
      throw ArgumentError('An access token and organization are required.');
    }
    final response = await _client.get(
      _baseUri.resolve('/api/v1/organizations/$organizationId/plan'),
      headers: {'Authorization': 'Bearer $accessToken'},
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ShowVaultApiException(response.statusCode);
    }
    final body = jsonDecode(response.body) as Map<String, Object?>;
    return OrganizationPlan.fromJson(body['payload']! as Map<String, Object?>);
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
    final secureOrigin = uri?.scheme == 'https';
    final guardedLoopback =
        uri?.scheme == 'http' &&
        AppConfig.personalBetaBypassAuth &&
        (uri?.host == 'localhost' ||
            uri?.host == '127.0.0.1' ||
            uri?.host == '::1');
    if (uri == null ||
        (!secureOrigin && !guardedLoopback) ||
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

class OrganizationPlan {
  const OrganizationPlan({
    required this.planCode,
    required this.licenseStatus,
    required this.subscriptionStatus,
    required this.currentPeriodEndsAt,
    required this.graceEndsAt,
    required this.logicalStorageLimitBytes,
    required this.committedBytes,
    required this.reservedBytes,
    required this.eligible,
    required this.reasonCode,
  });

  factory OrganizationPlan.fromJson(Map<String, Object?> json) =>
      OrganizationPlan(
        planCode: json['planCode'] as String?,
        licenseStatus: json['licenseStatus']! as String,
        subscriptionStatus: json['subscriptionStatus']! as String,
        currentPeriodEndsAt: _date(json['currentPeriodEndsAt']),
        graceEndsAt: _date(json['graceEndsAt']),
        logicalStorageLimitBytes: json['logicalStorageLimitBytes']! as int,
        committedBytes: json['committedBytes']! as int,
        reservedBytes: json['reservedBytes']! as int,
        eligible: json['eligible']! as bool,
        reasonCode: json['reasonCode']! as String,
      );

  final String? planCode;
  final String licenseStatus;
  final String subscriptionStatus;
  final DateTime? currentPeriodEndsAt;
  final DateTime? graceEndsAt;
  final int logicalStorageLimitBytes;
  final int committedBytes;
  final int reservedBytes;
  final bool eligible;
  final String reasonCode;

  static DateTime? _date(Object? value) =>
      value is String ? DateTime.parse(value) : null;
}

class ShowVaultApiException implements Exception {
  const ShowVaultApiException(this.statusCode);
  final int statusCode;

  @override
  String toString() => 'ShowVault API request failed ($statusCode).';
}
