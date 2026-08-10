import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:crypto/crypto.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/recovery/local_package_verifier.dart';
import 'package:showvault_app/src/recovery/local_path_policy.dart';
import 'package:showvault_app/src/recovery/local_recovery_service.dart';

final localRestoreServiceProvider = Provider<LocalRestoreService>(
  (ref) => LocalRestoreService(),
);

class LocalRestoreService {
  LocalRestoreService({
    LocalRecoveryService? recoveryService,
    LocalPackageVerifier? packageVerifier,
    DateTime Function()? now,
    this.timeout = const Duration(minutes: 10),
    this.onFileCopied,
  }) : _recoveryService = recoveryService ?? LocalRecoveryService(),
       _packageVerifier = packageVerifier ?? const LocalPackageVerifier(),
       _now = now ?? DateTime.now;

  final LocalRecoveryService _recoveryService;
  final LocalPackageVerifier _packageVerifier;
  final DateTime Function() _now;
  final Duration timeout;
  final Future<void> Function(String relativePath)? onFileCopied;

  Future<LocalRestoreResult> restore({
    required String authorizedVaultRoot,
    required String recoveryPointId,
    required String targetPath,
    LocalRestoreCancellation? cancellation,
  }) async {
    final startedAt = _now().toUtc();
    final deadline = startedAt.add(timeout);
    void checkActive() {
      if (cancellation?.isCancelled ?? false) {
        throw const LocalRestoreException('Restore cancelled.');
      }
      if (_now().toUtc().isAfter(deadline)) {
        throw const LocalRestoreException('Restore timed out.');
      }
    }

    checkActive();
    final snapshot = await _recoveryService.inspectVault(authorizedVaultRoot);
    LocalRecoveryRecord? record;
    for (final candidate in snapshot.records) {
      if (candidate.recoveryPointId == recoveryPointId) {
        record = candidate;
        break;
      }
    }
    if (record == null) {
      throw const LocalRestoreException(
        'The selected recovery point is not in the authorized vault.',
      );
    }
    final VerifiedLocalPackage package;
    try {
      package = await _packageVerifier.verify(record);
    } on LocalPackageVerificationException catch (error) {
      throw LocalRestoreException(error.message);
    }
    checkActive();

    final target = await _validateTarget(
      snapshot.vaultRoot,
      targetPath,
      recoveryPointId,
    );
    final evidenceRoot = await _prepareEvidenceDirectory(snapshot.vaultRoot);
    final stagingName = _stageName(recoveryPointId, target.name);
    final stagingRoot = Directory(
      _join(target.existed ? target.path : target.parentPath, stagingName),
    );
    final publishedRoot = target.existed
        ? Directory(_join(target.path, 'ShowVault Restored Files'))
        : Directory(target.path);
    await _cleanOwnedInterruptedStage(
      stagingRoot,
      recoveryPointId,
      target.name,
    );
    final restoredRoot = Directory(_join(stagingRoot.path, 'restored'));
    final intentTemporary = File(
      '${stagingRoot.path}.intent-temp-${_randomHex(8)}',
    );
    var published = false;
    var stageOwned = false;
    try {
      await intentTemporary.writeAsString(
        jsonEncode({
          'formatVersion': 'showvault.restore-intent.v1',
          'packageId': recoveryPointId,
          'targetName': target.name,
        }),
        flush: true,
      );
      await stagingRoot.create();
      await intentTemporary.rename(_join(stagingRoot.path, 'intent.json'));
      stageOwned = true;
      await restoredRoot.create();
      for (final source in package.files) {
        checkActive();
        final destination = _logicalFile(
          restoredRoot.path,
          source.relativePath,
        );
        await _createSafeDestinationParents(
          restoredRoot.path,
          source.relativePath,
        );
        final copied = await _copyAndHash(
          source.file,
          destination,
          source.size,
          checkActive,
        );
        if (copied != source.sha256) {
          throw const LocalRestoreException(
            'Restored bytes did not match the recovery manifest.',
          );
        }
        if (onFileCopied != null) {
          await onFileCopied!(source.relativePath);
        }
        checkActive();
        if (await FileSystemEntity.type(source.file.path, followLinks: false) !=
                FileSystemEntityType.file ||
            await source.file.length() != source.size ||
            await LocalPackageVerifier.hashBoundedFile(
                  source.file,
                  source.size,
                ) !=
                source.sha256) {
          throw const LocalRestoreException(
            'The recovery point changed during restore.',
          );
        }
      }
      await _verifyRestoredTree(restoredRoot.path, package, checkActive);
      checkActive();

      final currentTargetType = await FileSystemEntity.type(
        target.path,
        followLinks: false,
      );
      if (target.existed) {
        if (currentTargetType != FileSystemEntityType.directory ||
            !await _directoryContainsOnly(target.path, stagingRoot.path)) {
          throw const LocalRestoreException(
            'The restore target changed or contains unrelated content.',
          );
        }
        if (await Directory(target.path).resolveSymbolicLinks() !=
            target.path) {
          throw const LocalRestoreException(
            'The restore target identity changed.',
          );
        }
      } else if (currentTargetType != FileSystemEntityType.notFound) {
        throw const LocalRestoreException(
          'The restore target appeared during restore.',
        );
      }
      if (await FileSystemEntity.type(publishedRoot.path, followLinks: false) !=
          FileSystemEntityType.notFound) {
        throw const LocalRestoreException(
          'The restore publication target already exists.',
        );
      }
      await restoredRoot.rename(publishedRoot.path);
      published = true;
      await _verifyRestoredTree(publishedRoot.path, package, checkActive);

      final completedAt = _now().toUtc();
      final evidencePath = await _writeEvidence(
        evidenceRoot,
        package,
        completedAt,
      );
      return LocalRestoreResult(
        recoveryPointId: recoveryPointId,
        restoredFileCount: package.files.length,
        restoredBytes: package.totalBytes,
        completedAt: completedAt,
        evidencePath: evidencePath,
      );
    } finally {
      if (await intentTemporary.exists()) await intentTemporary.delete();
      if (await stagingRoot.exists()) {
        if (stageOwned) {
          await _deleteOwnedStage(stagingRoot, recoveryPointId, target.name);
        } else if (await _directoryIsEmpty(stagingRoot.path)) {
          await stagingRoot.delete();
        }
      }
      if (!published && !target.existed) {
        final type = await FileSystemEntity.type(
          target.path,
          followLinks: false,
        );
        if (type == FileSystemEntityType.directory &&
            await _directoryIsEmpty(target.path)) {
          await Directory(target.path).delete();
        }
      } else if (!published && target.existed) {
        final type = await FileSystemEntity.type(
          target.path,
          followLinks: false,
        );
        if (type == FileSystemEntityType.notFound) {
          await Directory(target.path).create();
        }
      }
    }
  }

  Future<_RestoreTarget> _validateTarget(
    String vaultRoot,
    String requestedPath,
    String recoveryPointId,
  ) async {
    if (requestedPath.trim().isEmpty) {
      throw const LocalRestoreException('A restore target is required.');
    }
    final absolute = Directory(requestedPath).absolute.path;
    final name = absolute.split(Platform.pathSeparator).last;
    if (name.isEmpty || name == '.' || name == '..') {
      throw const LocalRestoreException('The restore target name is unsafe.');
    }
    final type = await FileSystemEntity.type(absolute, followLinks: false);
    late final String targetPath;
    late final String parentPath;
    late final bool existed;
    if (type == FileSystemEntityType.directory) {
      targetPath = await Directory(absolute).resolveSymbolicLinks();
      final expectedStageName = _stageName(recoveryPointId, name);
      if (!await _directoryIsEmpty(targetPath) &&
          !await _directoryContainsOnly(
            targetPath,
            _join(targetPath, expectedStageName),
          )) {
        throw const LocalRestoreException('The restore target must be empty.');
      }
      parentPath = Directory(targetPath).parent.path;
      existed = true;
    } else if (type == FileSystemEntityType.notFound) {
      final parent = Directory(absolute).parent;
      if (await FileSystemEntity.type(parent.path, followLinks: false) !=
          FileSystemEntityType.directory) {
        throw const LocalRestoreException(
          'The restore target parent is missing or unsafe.',
        );
      }
      parentPath = await parent.resolveSymbolicLinks();
      targetPath = _join(parentPath, name);
      existed = false;
    } else {
      throw const LocalRestoreException(
        'The restore target must be an absent or empty regular directory.',
      );
    }
    final canonicalVault = await Directory(vaultRoot).resolveSymbolicLinks();
    if (_isWithin(targetPath, canonicalVault) ||
        _isWithin(canonicalVault, targetPath)) {
      throw const LocalRestoreException(
        'The restore target must be outside the ShowVault Pro vault.',
      );
    }
    return _RestoreTarget(
      path: targetPath,
      parentPath: parentPath,
      name: name,
      existed: existed,
    );
  }

  Future<String> _copyAndHash(
    File source,
    File destination,
    int expectedBytes,
    void Function() checkActive,
  ) async {
    final sink = _DigestSink();
    final converter = sha256.startChunkedConversion(sink);
    final output = destination.openWrite(mode: FileMode.writeOnly);
    var bytes = 0;
    var converterClosed = false;
    var outputClosed = false;
    try {
      await for (final chunk in source.openRead()) {
        checkActive();
        bytes += chunk.length;
        if (bytes > expectedBytes) {
          throw const LocalRestoreException(
            'The recovery point changed during restore.',
          );
        }
        converter.add(chunk);
        output.add(chunk);
      }
      converter.close();
      converterClosed = true;
      await output.flush();
      await output.close();
      outputClosed = true;
    } catch (_) {
      if (!converterClosed) converter.close();
      if (!outputClosed) await output.close();
      rethrow;
    }
    if (bytes != expectedBytes || sink.value == null) {
      throw const LocalRestoreException(
        'The recovery point changed during restore.',
      );
    }
    return sink.value.toString();
  }

  Future<void> _verifyRestoredTree(
    String root,
    VerifiedLocalPackage package,
    void Function() checkActive,
  ) async {
    final actual = <String>{};
    await for (final entity in Directory(
      root,
    ).list(recursive: true, followLinks: false)) {
      checkActive();
      final type = await FileSystemEntity.type(entity.path, followLinks: false);
      if (type == FileSystemEntityType.directory) continue;
      if (type != FileSystemEntityType.file) {
        throw const LocalRestoreException(
          'The restored target contains an unsafe entry.',
        );
      }
      actual.add(LocalPackageVerifier.relativeLogicalPath(root, entity.path));
    }
    final expected = package.files.map((file) => file.relativePath).toSet();
    if (actual.length != expected.length || !actual.containsAll(expected)) {
      throw const LocalRestoreException(
        'The restored file set does not match the recovery manifest.',
      );
    }
    for (final expectedFile in package.files) {
      checkActive();
      final file = _logicalFile(root, expectedFile.relativePath);
      if (await FileSystemEntity.type(file.path, followLinks: false) !=
              FileSystemEntityType.file ||
          await file.length() != expectedFile.size ||
          await LocalPackageVerifier.hashBoundedFile(file, expectedFile.size) !=
              expectedFile.sha256) {
        throw const LocalRestoreException(
          'Restored checksum verification failed.',
        );
      }
    }
  }

  Future<Directory> _prepareEvidenceDirectory(String vaultRoot) async {
    final reportsRoot = Directory(_join(vaultRoot, 'Reports'));
    if (await FileSystemEntity.type(reportsRoot.path, followLinks: false) !=
        FileSystemEntityType.directory) {
      throw const LocalRestoreException(
        'The local restore-evidence location is unsafe.',
      );
    }
    final restoresRoot = Directory(_join(reportsRoot.path, 'Restores'));
    final type = await FileSystemEntity.type(
      restoresRoot.path,
      followLinks: false,
    );
    if (type != FileSystemEntityType.notFound &&
        type != FileSystemEntityType.directory) {
      throw const LocalRestoreException(
        'The local restore-evidence location is unsafe.',
      );
    }
    if (type == FileSystemEntityType.notFound) await restoresRoot.create();
    return restoresRoot;
  }

  Future<String> _writeEvidence(
    Directory restoresRoot,
    VerifiedLocalPackage package,
    DateTime completedAt,
  ) async {
    if (await FileSystemEntity.type(restoresRoot.path, followLinks: false) !=
        FileSystemEntityType.directory) {
      throw const LocalRestoreException(
        'The local restore-evidence location is unsafe.',
      );
    }
    final core = <String, Object?>{
      'formatVersion': 'showvault.restore-evidence.v1',
      'packageId': package.packageId,
      'candidateKey': package.candidateKey,
      'productName': package.productName,
      'completedAt': completedAt.toIso8601String(),
      'restoredFileCount': package.files.length,
      'restoredBytes': package.totalBytes,
      'target': 'operator-selected-empty-directory',
      'verification': 'exact-file-set-size-and-sha256',
    };
    final evidenceSha256 = sha256
        .convert(utf8.encode(jsonEncode(core)))
        .toString();
    final bytes = utf8.encode(
      jsonEncode({...core, 'evidenceSha256': evidenceSha256}),
    );
    final path = _join(
      restoresRoot.path,
      '${completedAt.microsecondsSinceEpoch}__${package.packageId}__'
      '${_randomHex(4)}.json',
    );
    final temporary = File('$path.tmp-${_randomHex(8)}');
    try {
      await temporary.writeAsBytes(bytes, flush: true);
      await temporary.rename(path);
    } finally {
      if (await temporary.exists()) await temporary.delete();
    }
    return path;
  }

  Future<void> _cleanOwnedInterruptedStage(
    Directory stage,
    String packageId,
    String targetName,
  ) async {
    final type = await FileSystemEntity.type(stage.path, followLinks: false);
    if (type == FileSystemEntityType.notFound) return;
    if (type != FileSystemEntityType.directory) {
      throw const LocalRestoreException(
        'An unsafe interrupted restore entry blocks this target.',
      );
    }
    await _deleteOwnedStage(stage, packageId, targetName);
  }

  Future<void> _deleteOwnedStage(
    Directory stage,
    String packageId,
    String targetName,
  ) async {
    final intent = File(_join(stage.path, 'intent.json'));
    if (await FileSystemEntity.type(intent.path, followLinks: false) !=
            FileSystemEntityType.file ||
        await intent.length() > 4096) {
      throw const LocalRestoreException(
        'An interrupted restore has no valid ownership marker.',
      );
    }
    try {
      final value = jsonDecode(await intent.readAsString());
      if (value is! Map<String, Object?> ||
          value['formatVersion'] != 'showvault.restore-intent.v1' ||
          value['packageId'] != packageId ||
          value['targetName'] != targetName) {
        throw const LocalRestoreException(
          'An interrupted restore ownership marker does not match.',
        );
      }
    } on FormatException {
      throw const LocalRestoreException(
        'An interrupted restore ownership marker is malformed.',
      );
    }
    await for (final entity in stage.list(
      recursive: true,
      followLinks: false,
    )) {
      final type = await FileSystemEntity.type(entity.path, followLinks: false);
      if (type != FileSystemEntityType.file &&
          type != FileSystemEntityType.directory) {
        throw const LocalRestoreException(
          'An interrupted restore contains an unsafe entry.',
        );
      }
    }
    await stage.delete(recursive: true);
  }

  static Future<bool> _directoryIsEmpty(String path) async {
    await for (final _ in Directory(path).list(followLinks: false)) {
      return false;
    }
    return true;
  }

  static Future<bool> _directoryContainsOnly(
    String path,
    String expectedChild,
  ) async {
    var count = 0;
    await for (final entry in Directory(path).list(followLinks: false)) {
      count++;
      if (count > 1 || entry.path != expectedChild) return false;
      if (await FileSystemEntity.type(entry.path, followLinks: false) !=
          FileSystemEntityType.directory) {
        return false;
      }
    }
    return count == 1;
  }

  static Future<void> _createSafeDestinationParents(
    String root,
    String relativePath,
  ) async {
    final segments = LocalPackageVerifier.safeLogicalPathSegments(relativePath);
    var current = root;
    for (final segment in segments.take(segments.length - 1)) {
      current = _join(current, segment);
      final type = await FileSystemEntity.type(current, followLinks: false);
      if (type == FileSystemEntityType.notFound) {
        await Directory(current).create();
      } else if (type != FileSystemEntityType.directory) {
        throw const LocalRestoreException(
          'The restore staging path contains an unsafe entry.',
        );
      }
    }
  }

  static File _logicalFile(String root, String relativePath) {
    var path = root;
    for (final segment in LocalPackageVerifier.safeLogicalPathSegments(
      relativePath,
    )) {
      path = _join(path, segment);
    }
    return File(path);
  }

  static bool _isWithin(String candidate, String root) {
    if (Platform.isWindows) {
      return WindowsLocalPathPolicy.isWithin(candidate, root);
    }
    final normalizedCandidate = candidate;
    final normalizedRoot = root;
    return normalizedCandidate == normalizedRoot ||
        normalizedCandidate.startsWith(
          '$normalizedRoot${Platform.pathSeparator}',
        );
  }

  static String _join(String left, String right) =>
      '$left${left.endsWith(Platform.pathSeparator) ? '' : Platform.pathSeparator}$right';

  static String _randomHex(int byteCount) {
    final random = Random.secure();
    return List<int>.generate(
      byteCount,
      (_) => random.nextInt(256),
    ).map((value) => value.toRadixString(16).padLeft(2, '0')).join();
  }

  static String _stageName(String recoveryPointId, String targetName) =>
      '.showvault-restore-${recoveryPointId.substring(0, 16)}-'
      '${sha256.convert(utf8.encode(targetName)).toString().substring(0, 8)}';
}

class LocalRestoreCancellation {
  bool _cancelled = false;
  bool get isCancelled => _cancelled;
  void cancel() => _cancelled = true;
}

class LocalRestoreResult {
  const LocalRestoreResult({
    required this.recoveryPointId,
    required this.restoredFileCount,
    required this.restoredBytes,
    required this.completedAt,
    required this.evidencePath,
  });

  final String recoveryPointId;
  final int restoredFileCount;
  final int restoredBytes;
  final DateTime completedAt;
  final String evidencePath;
}

class LocalRestoreException implements Exception {
  const LocalRestoreException(this.message);

  final String message;

  @override
  String toString() => message;
}

class _RestoreTarget {
  const _RestoreTarget({
    required this.path,
    required this.parentPath,
    required this.name,
    required this.existed,
  });

  final String path;
  final String parentPath;
  final String name;
  final bool existed;
}

class _DigestSink implements Sink<Digest> {
  Digest? value;

  @override
  void add(Digest data) => value = data;

  @override
  void close() {}
}
