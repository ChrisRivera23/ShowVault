import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:showvault_app/src/recovery/local_sync_object_store.dart';

class HostedSyncObjectStore
    implements LocalSyncObjectStore, LocalSyncSessionObjectStore {
  HostedSyncObjectStore({
    required this.apiBaseUrl,
    required this.accessToken,
    required this.organizationId,
    required this.venueId,
    http.Client? client,
  }) : _client = client ?? http.Client();

  final String apiBaseUrl;
  final String accessToken;
  final String organizationId;
  final String venueId;
  final http.Client _client;

  String _packageUrl(String packageId) =>
      '${apiBaseUrl.replaceFirst(RegExp(r'/$'), '')}/api/v1/organizations/'
      '$organizationId/venues/$venueId/hosted-sync/$packageId';

  Map<String, String> get _headers => {
    'Authorization': 'Bearer $accessToken',
    'Content-Type': 'application/json',
  };

  @override
  Future<LocalSyncReceipt?> committedReceipt(String packageId) async {
    final response = await _client.get(
      Uri.parse('${_packageUrl(packageId)}/receipt'),
      headers: _headers,
    );
    if (response.statusCode == 404) return null;
    _requireSuccess(response);
    return _decodeReceipt(response.body, packageId);
  }

  @override
  Future<LocalSyncReceipt?> beginUpload(
    String packageId,
    List<int> remoteManifestBytes,
    List<LocalSyncFileDescriptor> files,
  ) async {
    final response = await _client.post(
      Uri.parse('${_packageUrl(packageId)}/begin'),
      headers: _headers,
      body: jsonEncode({'remoteManifest': base64Encode(remoteManifestBytes)}),
    );
    _requireSuccess(response);
    if (response.statusCode == 204 || response.body.isEmpty) return null;
    return _decodeReceipt(response.body, packageId);
  }

  @override
  Future<int> uploadedLength(String packageId, String relativePath) async {
    final response = await _client.post(
      Uri.parse('${_packageUrl(packageId)}/file-state'),
      headers: _headers,
      body: jsonEncode({'relativePath': relativePath}),
    );
    _requireSuccess(response);
    final payload = _decodePayload(response.body);
    final length = payload['uploadedLength'];
    if (length is! int || length < 0) {
      throw const LocalObjectStoreIntegrityException(
        'The hosted file state is malformed.',
      );
    }
    return length;
  }

  @override
  Future<void> appendChunk(
    String packageId,
    String relativePath,
    int offset,
    List<int> bytes,
  ) async {
    final response = await _client.post(
      Uri.parse('${_packageUrl(packageId)}/chunks'),
      headers: _headers,
      body: jsonEncode({
        'relativePath': relativePath,
        'offset': offset,
        'bytes': base64Encode(bytes),
      }),
    );
    _requireSuccess(response);
  }

  @override
  Future<LocalSyncReceipt> verifyAndCommit(
    String packageId,
    List<int> remoteManifestBytes,
    List<LocalSyncFileDescriptor> files,
  ) async {
    final response = await _client.post(
      Uri.parse('${_packageUrl(packageId)}/commit'),
      headers: _headers,
      body: jsonEncode({'remoteManifest': base64Encode(remoteManifestBytes)}),
    );
    _requireSuccess(response);
    return _decodeReceipt(response.body, packageId);
  }

  static Map<String, Object?> _decodePayload(String body) {
    try {
      final decoded = jsonDecode(body);
      if (decoded is! Map<String, Object?> ||
          decoded['payload'] is! Map<String, Object?>) {
        throw const FormatException();
      }
      return decoded['payload']! as Map<String, Object?>;
    } on FormatException {
      throw const LocalObjectStoreIntegrityException(
        'The hosted synchronization response is malformed.',
      );
    }
  }

  static LocalSyncReceipt _decodeReceipt(String body, String packageId) {
    final payload = _decodePayload(body);
    try {
      if (payload['packageId'] != packageId ||
          payload['remoteManifestSha256'] is! String ||
          payload['completedAt'] is! String) {
        throw const FormatException();
      }
      return LocalSyncReceipt(
        packageId: packageId,
        remoteManifestSha256: payload['remoteManifestSha256']! as String,
        completedAt: DateTime.parse(payload['completedAt']! as String).toUtc(),
      );
    } on FormatException {
      throw const LocalObjectStoreIntegrityException(
        'The hosted synchronization receipt is malformed.',
      );
    }
  }

  static void _requireSuccess(http.Response response) {
    if (response.statusCode >= 200 && response.statusCode < 300) return;
    if (response.statusCode == 400 ||
        response.statusCode == 403 ||
        response.statusCode == 409) {
      throw const LocalObjectStoreIntegrityException(
        'Hosted synchronization rejected the package.',
      );
    }
    throw const LocalObjectStoreUnavailableException(
      'Hosted synchronization is unavailable.',
    );
  }
}
