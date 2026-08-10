import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:crypto/crypto.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/recovery/local_recovery_service.dart';

final localSupportDiagnosticServiceProvider =
    Provider<LocalSupportDiagnosticService>(
      (ref) => LocalSupportDiagnosticService(),
    );

class LocalSupportDiagnosticService {
  LocalSupportDiagnosticService({
    LocalRecoveryService? recoveryService,
    DateTime Function()? now,
    this.appVersion = '0.1.0+1',
  }) : _recoveryService = recoveryService ?? LocalRecoveryService(),
       _now = now ?? DateTime.now;

  static const formatVersion = 'showvault.support-diagnostic.v1';
  static const _maxEvidenceFiles = 10000;
  static const _maxEvidenceBytes = 64 * 1024;
  static const _maxDiagnosticBytes = 2 * 1024 * 1024;

  final LocalRecoveryService _recoveryService;
  final DateTime Function() _now;
  final String appVersion;

  Future<LocalSupportDiagnosticResult> generate(
    String authorizedVaultRoot,
  ) async {
    try {
      final snapshot = await _recoveryService.inspectVault(authorizedVaultRoot);
      final restoreEvidenceCount = await _verifyRestoreEvidence(snapshot);
      final generatedAt = _now().toUtc();
      final packages = snapshot.records
          .map(
            (record) => <String, Object?>{
              'packageId': record.recoveryPointId,
              'candidateKey': record.candidateKey,
              'createdAt': record.createdAt.toIso8601String(),
              'fileCount': record.fileCount,
              'totalBytes': record.totalBytes,
              'localStatus': 'verified',
              'cloudStatus': _cloudStatus(record.cloudStatus),
              'queueAttemptCount': record.queueAttemptCount,
              'queueStateEventCount': record.queueStateEventCount,
              'errorCategory': record.queueErrorCategory.name,
            },
          )
          .toList(growable: false);
      final statusCounts = <String, int>{
        for (final status in LocalCloudSyncStatus.values)
          _cloudStatus(status): snapshot.records
              .where((record) => record.cloudStatus == status)
              .length,
      };
      final core = <String, Object?>{
        'formatVersion': formatVersion,
        'appVersion': appVersion,
        'vaultSchemaVersion': 'showvault.local-vault.v1',
        'generatedAt': generatedAt.toIso8601String(),
        'scope': 'operator-authorized-showvault-vault',
        'integrity': {
          'manifestAndQueueRecords': 'validated',
          'restoreEvidence': 'validated',
          'packageContents': 'not-read',
          'recoverySources': 'not-read',
        },
        'recoveryPointCount': snapshot.records.length,
        'restoreEvidenceCount': restoreEvidenceCount,
        'cloudStatusCounts': statusCounts,
        'packages': packages,
      };
      _requirePathFree(core);
      final evidenceSha256 = sha256
          .convert(utf8.encode(jsonEncode(core)))
          .toString();
      final bytes = utf8.encode(
        jsonEncode({...core, 'evidenceSha256': evidenceSha256}),
      );
      if (bytes.length > _maxDiagnosticBytes) {
        throw const LocalSupportDiagnosticException(
          'The support diagnostic exceeds its size limit.',
        );
      }
      final diagnosticsRoot = await _diagnosticsRoot(snapshot.vaultRoot);
      final stem =
          '${generatedAt.microsecondsSinceEpoch}__'
          '${evidenceSha256.substring(0, 16)}__${_randomHex(4)}';
      final diagnostic = File(_join(diagnosticsRoot.path, '$stem.json'));
      final checksum = File(_join(diagnosticsRoot.path, '$stem.sha256'));
      await _writeNewFile(diagnostic, bytes);
      try {
        await _writeNewFile(
          checksum,
          utf8.encode('$evidenceSha256  ${_fileName(diagnostic.path)}\n'),
        );
      } catch (_) {
        if (await diagnostic.exists()) await diagnostic.delete();
        rethrow;
      }
      return LocalSupportDiagnosticResult(
        diagnosticPath: diagnostic.path,
        checksumPath: checksum.path,
        evidenceSha256: evidenceSha256,
        recoveryPointCount: snapshot.records.length,
        restoreEvidenceCount: restoreEvidenceCount,
      );
    } on LocalSupportDiagnosticException {
      rethrow;
    } on LocalRecoveryException catch (error) {
      throw LocalSupportDiagnosticException(error.message);
    } catch (_) {
      throw const LocalSupportDiagnosticException(
        'The support diagnostic could not be generated safely.',
      );
    }
  }

  Future<int> _verifyRestoreEvidence(LocalVaultSnapshot snapshot) async {
    final reportsRoot = Directory(_join(snapshot.vaultRoot, 'Reports'));
    if (await FileSystemEntity.type(reportsRoot.path, followLinks: false) !=
        FileSystemEntityType.directory) {
      throw const LocalSupportDiagnosticException(
        'The vault Reports location is unsafe.',
      );
    }
    final restoresRoot = Directory(_join(reportsRoot.path, 'Restores'));
    final restoresType = await FileSystemEntity.type(
      restoresRoot.path,
      followLinks: false,
    );
    if (restoresType == FileSystemEntityType.notFound) return 0;
    if (restoresType != FileSystemEntityType.directory) {
      throw const LocalSupportDiagnosticException(
        'The restore evidence location is unsafe.',
      );
    }
    final packageIds = snapshot.records
        .map((record) => record.recoveryPointId)
        .toSet();
    var count = 0;
    await for (final entity in restoresRoot.list(followLinks: false)) {
      if (await FileSystemEntity.type(entity.path, followLinks: false) !=
          FileSystemEntityType.file) {
        throw const LocalSupportDiagnosticException(
          'A restore evidence entry is unsafe.',
        );
      }
      final file = File(entity.path);
      if (++count > _maxEvidenceFiles ||
          await file.length() > _maxEvidenceBytes) {
        throw const LocalSupportDiagnosticException(
          'The restore evidence set exceeds its limit.',
        );
      }
      final decoded = _decodeObject(
        await file.readAsBytes(),
        'A restore evidence entry is malformed.',
      );
      const requiredKeys = {
        'formatVersion',
        'packageId',
        'candidateKey',
        'productName',
        'completedAt',
        'restoredFileCount',
        'restoredBytes',
        'target',
        'verification',
        'evidenceSha256',
      };
      if (decoded.keys.toSet().difference(requiredKeys).isNotEmpty ||
          requiredKeys.difference(decoded.keys.toSet()).isNotEmpty ||
          decoded['formatVersion'] != 'showvault.restore-evidence.v1' ||
          decoded['packageId'] is! String ||
          !packageIds.contains(decoded['packageId']) ||
          decoded['candidateKey'] is! String ||
          decoded['productName'] is! String ||
          decoded['completedAt'] is! String ||
          DateTime.tryParse(decoded['completedAt'] as String) == null ||
          decoded['restoredFileCount'] is! int ||
          (decoded['restoredFileCount']! as int) < 0 ||
          decoded['restoredBytes'] is! int ||
          (decoded['restoredBytes']! as int) < 0 ||
          decoded['target'] != 'operator-selected-empty-directory' ||
          decoded['verification'] != 'exact-file-set-size-and-sha256' ||
          decoded['evidenceSha256'] is! String) {
        throw const LocalSupportDiagnosticException(
          'A restore evidence entry is invalid.',
        );
      }
      final expected = decoded.remove('evidenceSha256');
      if (expected !=
          sha256.convert(utf8.encode(jsonEncode(decoded))).toString()) {
        throw const LocalSupportDiagnosticException(
          'A restore evidence entry failed checksum validation.',
        );
      }
      _requirePathFree(decoded);
    }
    return count;
  }

  Future<Directory> _diagnosticsRoot(String vaultRoot) async {
    final reportsRoot = Directory(_join(vaultRoot, 'Reports'));
    final root = Directory(_join(reportsRoot.path, 'Diagnostics'));
    final type = await FileSystemEntity.type(root.path, followLinks: false);
    if (type == FileSystemEntityType.notFound) {
      await root.create();
    } else if (type != FileSystemEntityType.directory) {
      throw const LocalSupportDiagnosticException(
        'The support diagnostic location is unsafe.',
      );
    }
    return root;
  }

  static Future<void> _writeNewFile(File destination, List<int> bytes) async {
    if (await FileSystemEntity.type(destination.path, followLinks: false) !=
        FileSystemEntityType.notFound) {
      throw const LocalSupportDiagnosticException(
        'A support diagnostic identity already exists.',
      );
    }
    final temporary = File('${destination.path}.tmp-${_randomHex(8)}');
    try {
      await temporary.writeAsBytes(bytes, flush: true);
      await temporary.rename(destination.path);
    } finally {
      if (await temporary.exists()) await temporary.delete();
    }
  }

  static Map<String, Object?> _decodeObject(List<int> bytes, String message) {
    try {
      final decoded = jsonDecode(utf8.decode(bytes));
      if (decoded is Map<String, Object?>) return decoded;
    } on FormatException {
      // Replaced with the bounded product error below.
    }
    throw LocalSupportDiagnosticException(message);
  }

  static void _requirePathFree(Object? value) {
    if (value is Map) {
      for (final entry in value.entries) {
        _requirePathFree(entry.key);
        _requirePathFree(entry.value);
      }
      return;
    }
    if (value is Iterable) {
      for (final item in value) {
        _requirePathFree(item);
      }
      return;
    }
    if (value is! String) return;
    if (value.startsWith('/') ||
        value.startsWith(r'\\') ||
        RegExp(r'^[A-Za-z]:[\\/]').hasMatch(value) ||
        value.contains('file://')) {
      throw const LocalSupportDiagnosticException(
        'The support diagnostic contains a local path.',
      );
    }
  }

  static String _cloudStatus(LocalCloudSyncStatus status) => switch (status) {
    LocalCloudSyncStatus.queued => 'queued',
    LocalCloudSyncStatus.syncing => 'syncing',
    LocalCloudSyncStatus.retryScheduled => 'retry',
    LocalCloudSyncStatus.synchronized => 'synchronized',
    LocalCloudSyncStatus.queueFailed => 'attention',
  };

  static String _fileName(String path) =>
      path.split(Platform.pathSeparator).last;

  static String _join(String left, String right) =>
      '$left${left.endsWith(Platform.pathSeparator) ? '' : Platform.pathSeparator}$right';

  static String _randomHex(int byteCount) {
    final random = Random.secure();
    return List<int>.generate(
      byteCount,
      (_) => random.nextInt(256),
    ).map((value) => value.toRadixString(16).padLeft(2, '0')).join();
  }
}

class LocalSupportDiagnosticResult {
  const LocalSupportDiagnosticResult({
    required this.diagnosticPath,
    required this.checksumPath,
    required this.evidenceSha256,
    required this.recoveryPointCount,
    required this.restoreEvidenceCount,
  });

  final String diagnosticPath;
  final String checksumPath;
  final String evidenceSha256;
  final int recoveryPointCount;
  final int restoreEvidenceCount;
}

class LocalSupportDiagnosticException implements Exception {
  const LocalSupportDiagnosticException(this.message);
  final String message;
  @override
  String toString() => message;
}
