import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:crypto/crypto.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/config/app_config.dart';
import 'package:showvault_app/src/recovery/local_recovery_service.dart';
import 'package:showvault_app/src/recovery/local_sync_object_store.dart';

final localSyncServiceProvider = Provider<LocalSyncService?>((ref) {
  if (AppConfig.syntheticFixtureHome.isEmpty ||
      AppConfig.syntheticObjectStoreRoot.isEmpty) {
    return null;
  }
  return LocalSyncService(
    objectStore: LocalFolderObjectStore(AppConfig.syntheticObjectStoreRoot),
  );
});

class LocalSyncService {
  LocalSyncService({
    required this.objectStore,
    LocalRecoveryService? recoveryService,
    DateTime Function()? now,
    this.chunkBytes = 256 * 1024,
    this.maxAttempts = 5,
    this.baseRetryDelay = const Duration(seconds: 30),
    this.maxRetryDelay = const Duration(minutes: 30),
    this.maxFiles = 10000,
    this.maxFileBytes = 512 * 1024 * 1024,
    this.maxTotalBytes = 5 * 1024 * 1024 * 1024,
  }) : _recoveryService = recoveryService ?? LocalRecoveryService(),
       _now = now ?? DateTime.now {
    if (chunkBytes <= 0 || maxAttempts <= 0) {
      throw ArgumentError('Synchronization limits must be positive.');
    }
  }

  final LocalSyncObjectStore objectStore;
  final LocalRecoveryService _recoveryService;
  final DateTime Function() _now;
  final int chunkBytes;
  final int maxAttempts;
  final Duration baseRetryDelay;
  final Duration maxRetryDelay;
  final int maxFiles;
  final int maxFileBytes;
  final int maxTotalBytes;

  Future<LocalSyncRunResult> syncPending(
    String authorizedVaultRoot, {
    int maxJobs = 25,
  }) async {
    if (maxJobs <= 0 || maxJobs > 100) {
      throw ArgumentError.value(maxJobs, 'maxJobs', 'Must be from 1 to 100.');
    }
    final snapshot = await _recoveryService.inspectVault(authorizedVaultRoot);
    var synchronized = 0;
    var retriedLater = 0;
    var failed = 0;
    var skipped = 0;
    var considered = 0;

    for (final record in snapshot.records) {
      if (considered >= maxJobs) break;
      final state = await _loadState(snapshot.vaultRoot, record);
      if (state == null || state.status == _SyncStatus.failed) {
        skipped++;
        continue;
      }
      if (state.status == _SyncStatus.retryScheduled &&
          state.nextAttemptAt != null &&
          _now().toUtc().isBefore(state.nextAttemptAt!)) {
        skipped++;
        continue;
      }
      considered++;

      if (state.status == _SyncStatus.synchronized) {
        try {
          final receipt = await objectStore.committedReceipt(
            record.recoveryPointId,
          );
          if (receipt != null &&
              receipt.remoteManifestSha256 == state.remoteManifestSha256) {
            skipped++;
            continue;
          }
        } catch (_) {
          // The normal retry path below persists a bounded, path-free error.
        }
      }

      final attempt = state.attemptCount + 1;
      var current = state.copyWith(
        status: _SyncStatus.syncing,
        attemptCount: attempt,
        updatedAt: _now().toUtc(),
        clearNextAttemptAt: true,
        clearLastError: true,
      );
      await _appendState(snapshot.vaultRoot, current);

      try {
        final package = await _preparePackage(record);
        final existing = await objectStore.committedReceipt(package.packageId);
        if (existing != null) {
          if (existing.remoteManifestSha256 != package.remoteManifestSha256) {
            throw const LocalObjectStoreIntegrityException(
              'The committed remote identity does not match this package.',
            );
          }
          current = current.copyWith(
            status: _SyncStatus.synchronized,
            updatedAt: _now().toUtc(),
            remoteManifestSha256: existing.remoteManifestSha256,
            completedAt: existing.completedAt,
          );
          await _appendState(snapshot.vaultRoot, current);
          synchronized++;
          continue;
        }

        for (final file in package.files) {
          var offset = await objectStore.uploadedLength(
            package.packageId,
            file.descriptor.relativePath,
          );
          if (offset < 0 || offset > file.descriptor.size) {
            throw const LocalObjectStoreIntegrityException(
              'A remote partial object has an invalid length.',
            );
          }
          final handle = await file.file.open();
          try {
            await handle.setPosition(offset);
            while (offset < file.descriptor.size) {
              final remaining = file.descriptor.size - offset;
              final chunk = await handle.read(min(chunkBytes, remaining));
              if (chunk.isEmpty) {
                throw const LocalSyncPackageException(
                  'A local package file changed during synchronization.',
                );
              }
              await objectStore.appendChunk(
                package.packageId,
                file.descriptor.relativePath,
                offset,
                chunk,
              );
              offset += chunk.length;
            }
          } finally {
            await handle.close();
          }
        }

        final receipt = await objectStore.verifyAndCommit(
          package.packageId,
          package.remoteManifestBytes,
          package.files.map((file) => file.descriptor).toList(growable: false),
        );
        if (receipt.remoteManifestSha256 != package.remoteManifestSha256) {
          throw const LocalObjectStoreIntegrityException(
            'The remote receipt checksum does not match the upload.',
          );
        }
        current = current.copyWith(
          status: _SyncStatus.synchronized,
          updatedAt: _now().toUtc(),
          completedAt: receipt.completedAt,
          remoteManifestSha256: receipt.remoteManifestSha256,
        );
        await _appendState(snapshot.vaultRoot, current);
        synchronized++;
      } on LocalSyncPackageException catch (error) {
        await _appendState(
          snapshot.vaultRoot,
          current.copyWith(
            status: _SyncStatus.failed,
            updatedAt: _now().toUtc(),
            lastError: error.message,
          ),
        );
        failed++;
      } on LocalObjectStoreIntegrityException catch (error) {
        await _appendState(
          snapshot.vaultRoot,
          current.copyWith(
            status: _SyncStatus.failed,
            updatedAt: _now().toUtc(),
            lastError: error.message,
          ),
        );
        failed++;
      } catch (_) {
        final exhausted = attempt >= maxAttempts;
        await _appendState(
          snapshot.vaultRoot,
          current.copyWith(
            status: exhausted ? _SyncStatus.failed : _SyncStatus.retryScheduled,
            updatedAt: _now().toUtc(),
            nextAttemptAt: exhausted
                ? null
                : _now().toUtc().add(_retryDelay(attempt)),
            clearNextAttemptAt: exhausted,
            lastError: exhausted
                ? 'Synchronization stopped after the retry limit.'
                : 'Synchronization is unavailable and will retry.',
          ),
        );
        if (exhausted) {
          failed++;
        } else {
          retriedLater++;
        }
      }
    }

    return LocalSyncRunResult(
      synchronized: synchronized,
      retriedLater: retriedLater,
      failed: failed,
      skipped: skipped,
    );
  }

  Future<_PreparedPackage> _preparePackage(LocalRecoveryRecord record) async {
    try {
      final manifestFile = File(
        _join(record.recoveryPointPath, 'manifest.json'),
      );
      if (await FileSystemEntity.type(manifestFile.path, followLinks: false) !=
              FileSystemEntityType.file ||
          await manifestFile.length() > 2 * 1024 * 1024) {
        throw const LocalSyncPackageException(
          'The local package manifest is unavailable or oversized.',
        );
      }
      final manifestBytes = await manifestFile.readAsBytes();
      if (sha256.convert(manifestBytes).toString() != record.recoveryPointId) {
        throw const LocalSyncPackageException(
          'The local package manifest identity changed.',
        );
      }
      final decoded = jsonDecode(utf8.decode(manifestBytes));
      if (decoded is! Map<String, Object?> ||
          decoded['source'] is! Map<String, Object?> ||
          decoded['files'] is! List<Object?>) {
        throw const LocalSyncPackageException(
          'The local package manifest is malformed.',
        );
      }
      final source = decoded['source']! as Map<String, Object?>;
      final candidateKey = _boundedString(source, 'candidateKey');
      final pluginId = _boundedString(source, 'pluginId');
      final productName = _boundedString(source, 'productName');
      if (candidateKey != record.candidateKey ||
          productName != record.productName) {
        throw const LocalSyncPackageException(
          'The local package metadata does not match its recovery record.',
        );
      }
      final entries = decoded['files']! as List<Object?>;
      if (entries.isEmpty || entries.length > maxFiles) {
        throw const LocalSyncPackageException(
          'The local package has an invalid file count.',
        );
      }
      final contentRoot = _join(record.recoveryPointPath, 'content');
      if (await FileSystemEntity.type(contentRoot, followLinks: false) !=
          FileSystemEntityType.directory) {
        throw const LocalSyncPackageException(
          'The local package content directory is unsafe.',
        );
      }
      final files = <_PreparedFile>[];
      final paths = <String>{};
      var totalBytes = 0;
      for (final entry in entries) {
        if (entry is! Map<String, Object?>) {
          throw const LocalSyncPackageException(
            'The local package file metadata is malformed.',
          );
        }
        final relativePath = _boundedString(entry, 'relativePath', max: 4096);
        final segments = _safeSegments(relativePath);
        if (!paths.add(relativePath)) {
          throw const LocalSyncPackageException(
            'The local package contains a duplicate logical path.',
          );
        }
        final size = entry['size'];
        final digest = entry['sha256'];
        if (size is! int ||
            size < 0 ||
            size > maxFileBytes ||
            digest is! String ||
            !_isSha256(digest)) {
          throw const LocalSyncPackageException(
            'The local package file metadata is invalid.',
          );
        }
        totalBytes += size;
        if (totalBytes > maxTotalBytes) {
          throw const LocalSyncPackageException(
            'The local package exceeds the synchronization size limit.',
          );
        }
        var path = contentRoot;
        for (var index = 0; index < segments.length; index++) {
          path = _join(path, segments[index]);
          final type = await FileSystemEntity.type(path, followLinks: false);
          final expected = index == segments.length - 1
              ? FileSystemEntityType.file
              : FileSystemEntityType.directory;
          if (type != expected) {
            throw const LocalSyncPackageException(
              'The local package contains a missing or linked entry.',
            );
          }
        }
        final file = File(path);
        if (await file.length() != size ||
            await _hashFile(file, size) != digest) {
          throw const LocalSyncPackageException(
            'The local package content failed checksum verification.',
          );
        }
        files.add(
          _PreparedFile(
            file,
            LocalSyncFileDescriptor(
              relativePath: relativePath,
              size: size,
              sha256: digest,
            ),
          ),
        );
      }
      final actualPaths = <String>{};
      await for (final entity in Directory(
        contentRoot,
      ).list(recursive: true, followLinks: false)) {
        final type = await FileSystemEntity.type(
          entity.path,
          followLinks: false,
        );
        if (type == FileSystemEntityType.directory) continue;
        if (type != FileSystemEntityType.file) {
          throw const LocalSyncPackageException(
            'The local package contains an unsupported or linked entry.',
          );
        }
        actualPaths.add(_relativeLogicalPath(contentRoot, entity.path));
        if (actualPaths.length > maxFiles) {
          throw const LocalSyncPackageException(
            'The local package exceeds the synchronization file limit.',
          );
        }
      }
      if (actualPaths.length != paths.length ||
          !actualPaths.containsAll(paths)) {
        throw const LocalSyncPackageException(
          'The local package content set does not match its manifest.',
        );
      }
      final remoteManifestBytes = utf8.encode(
        jsonEncode({
          'formatVersion': 'showvault.remote-package.v1',
          'packageId': record.recoveryPointId,
          'createdAt': record.createdAt.toIso8601String(),
          'source': {
            'candidateKey': candidateKey,
            'pluginId': pluginId,
            'productName': productName,
          },
          'files': files
              .map(
                (file) => {
                  'relativePath': file.descriptor.relativePath,
                  'size': file.descriptor.size,
                  'sha256': file.descriptor.sha256,
                },
              )
              .toList(growable: false),
          'localManifestSha256': record.recoveryPointId,
        }),
      );
      return _PreparedPackage(
        packageId: record.recoveryPointId,
        files: files,
        remoteManifestBytes: remoteManifestBytes,
        remoteManifestSha256: sha256.convert(remoteManifestBytes).toString(),
      );
    } on LocalSyncPackageException {
      rethrow;
    } catch (_) {
      throw const LocalSyncPackageException(
        'The local package could not be safely reverified.',
      );
    }
  }

  Future<_JobState?> _loadState(
    String vaultRoot,
    LocalRecoveryRecord record,
  ) async {
    final queueFile = File(
      _join(_join(vaultRoot, 'Upload Queue'), '${record.recoveryPointId}.json'),
    );
    if (await FileSystemEntity.type(queueFile.path, followLinks: false) !=
        FileSystemEntityType.file) {
      return null;
    }
    if (await queueFile.length() > 64 * 1024) {
      throw const LocalSyncPackageException('A queue record is oversized.');
    }
    final base = _decodeObject(await queueFile.readAsBytes());
    if (base['packageId'] != record.recoveryPointId ||
        base['packagePath'] is! String) {
      throw const LocalSyncPackageException(
        'A queue record does not match its local recovery point.',
      );
    }
    final queuedPackagePath = base['packagePath']! as String;
    if (await FileSystemEntity.type(queuedPackagePath, followLinks: false) !=
            FileSystemEntityType.directory ||
        await Directory(queuedPackagePath).resolveSymbolicLinks() !=
            await Directory(record.recoveryPointPath).resolveSymbolicLinks()) {
      throw const LocalSyncPackageException(
        'A queue record does not match its local recovery point.',
      );
    }
    var state = _JobState(
      packageId: record.recoveryPointId,
      status: _parseStatus(base['status']),
      attemptCount: _attemptCount(base['attemptCount']),
      createdAt: _parseDate(base['createdAt']),
      updatedAt: _parseDate(base['updatedAt']),
      sequence: 0,
      nextAttemptAt: null,
      lastError: null,
      remoteManifestSha256: null,
      completedAt: null,
    );
    final stateRoot = Directory(
      _join(
        _join(_join(vaultRoot, 'Upload Queue'), 'State'),
        record.recoveryPointId,
      ),
    );
    final type = await FileSystemEntity.type(
      stateRoot.path,
      followLinks: false,
    );
    if (type == FileSystemEntityType.notFound) return state;
    if (type != FileSystemEntityType.directory) {
      throw const LocalSyncPackageException(
        'A queue state directory is unsafe.',
      );
    }
    final events = <File>[];
    await for (final entity in stateRoot.list(followLinks: false)) {
      if (await FileSystemEntity.type(entity.path, followLinks: false) !=
          FileSystemEntityType.file) {
        throw const LocalSyncPackageException('A queue state entry is unsafe.');
      }
      final name = entity.path.split(Platform.pathSeparator).last;
      if (!RegExp(r'^\d{8}\.json$').hasMatch(name)) {
        throw const LocalSyncPackageException(
          'A queue state entry has an invalid name.',
        );
      }
      events.add(File(entity.path));
      if (events.length > 1000) {
        throw const LocalSyncPackageException(
          'A queue job exceeds the state-event limit.',
        );
      }
    }
    events.sort((left, right) => left.path.compareTo(right.path));
    if (events.isEmpty) return state;
    final event = events.last;
    if (await event.length() > 64 * 1024) {
      throw const LocalSyncPackageException(
        'A queue state event is oversized.',
      );
    }
    final value = _decodeObject(await event.readAsBytes());
    if (value['packageId'] != record.recoveryPointId) {
      throw const LocalSyncPackageException(
        'A queue state event has the wrong identity.',
      );
    }
    state = _JobState(
      packageId: record.recoveryPointId,
      status: _parseStatus(value['status']),
      attemptCount: _attemptCount(value['attemptCount']),
      createdAt: state.createdAt,
      updatedAt: _parseDate(value['updatedAt']),
      sequence: int.parse(
        event.path.split(Platform.pathSeparator).last.substring(0, 8),
      ),
      nextAttemptAt: _optionalDate(value['nextAttemptAt']),
      lastError: _optionalBoundedString(value['lastError'], 512),
      remoteManifestSha256: _optionalSha256(value['remoteManifestSha256']),
      completedAt: _optionalDate(value['completedAt']),
    );
    return state;
  }

  Future<void> _appendState(String vaultRoot, _JobState state) async {
    final queueRoot = Directory(_join(vaultRoot, 'Upload Queue'));
    if (await FileSystemEntity.type(queueRoot.path, followLinks: false) !=
        FileSystemEntityType.directory) {
      throw const LocalSyncPackageException('The queue location is unsafe.');
    }
    final stateParent = Directory(_join(queueRoot.path, 'State'));
    final stateParentType = await FileSystemEntity.type(
      stateParent.path,
      followLinks: false,
    );
    if (stateParentType != FileSystemEntityType.notFound &&
        stateParentType != FileSystemEntityType.directory) {
      throw const LocalSyncPackageException('The queue state path is unsafe.');
    }
    if (stateParentType == FileSystemEntityType.notFound) {
      await stateParent.create();
    }
    final root = Directory(_join(stateParent.path, state.packageId));
    final type = await FileSystemEntity.type(root.path, followLinks: false);
    if (type != FileSystemEntityType.notFound &&
        type != FileSystemEntityType.directory) {
      throw const LocalSyncPackageException('The queue state path is unsafe.');
    }
    if (type == FileSystemEntityType.notFound) await root.create();
    final next = state.sequence + 1;
    if (next > 99999999) {
      throw const LocalSyncPackageException(
        'The queue state sequence is exhausted.',
      );
    }
    final destination = File(
      _join(root.path, '${next.toString().padLeft(8, '0')}.json'),
    );
    if (await destination.exists()) {
      throw const LocalSyncPackageException(
        'Concurrent synchronization is not supported.',
      );
    }
    final bytes = utf8.encode(
      jsonEncode({
        'packageId': state.packageId,
        'status': state.status.value,
        'attemptCount': state.attemptCount,
        'updatedAt': state.updatedAt.toIso8601String(),
        'nextAttemptAt': state.nextAttemptAt?.toIso8601String(),
        'lastError': state.lastError,
        'remoteManifestSha256': state.remoteManifestSha256,
        'completedAt': state.completedAt?.toIso8601String(),
      }),
    );
    final temporary = File('${destination.path}.tmp-${_randomHex(8)}');
    try {
      await temporary.writeAsBytes(bytes, flush: true);
      await temporary.rename(destination.path);
    } finally {
      if (await temporary.exists()) await temporary.delete();
    }
    state.sequence = next;
  }

  Duration _retryDelay(int attempt) {
    final multiplier = 1 << min(attempt - 1, 20);
    final milliseconds = min(
      baseRetryDelay.inMilliseconds * multiplier,
      maxRetryDelay.inMilliseconds,
    );
    return Duration(milliseconds: milliseconds);
  }

  static Future<String> _hashFile(File file, int expectedBytes) async {
    final sink = _DigestSink();
    final converter = sha256.startChunkedConversion(sink);
    var bytes = 0;
    await for (final chunk in file.openRead()) {
      bytes += chunk.length;
      if (bytes > expectedBytes) {
        throw const LocalSyncPackageException(
          'The local package content changed during verification.',
        );
      }
      converter.add(chunk);
    }
    converter.close();
    if (bytes != expectedBytes) {
      throw const LocalSyncPackageException(
        'The local package content changed during verification.',
      );
    }
    if (sink.value == null) {
      throw const LocalSyncPackageException(
        'The local package content could not be hashed.',
      );
    }
    return sink.value.toString();
  }

  static Map<String, Object?> _decodeObject(List<int> bytes) {
    try {
      final value = jsonDecode(utf8.decode(bytes));
      if (value is Map<String, Object?>) return value;
    } catch (_) {
      // Replaced with the bounded error below.
    }
    throw const LocalSyncPackageException('A queue record is malformed.');
  }

  static String _boundedString(
    Map<String, Object?> value,
    String key, {
    int max = 256,
  }) {
    final candidate = value[key];
    if (candidate is String &&
        candidate.isNotEmpty &&
        candidate.length <= max) {
      return candidate;
    }
    throw const LocalSyncPackageException(
      'The local package contains invalid bounded metadata.',
    );
  }

  static List<String> _safeSegments(String path) {
    if (path.startsWith('/') || path.contains('\\')) {
      throw const LocalSyncPackageException(
        'The local package contains an unsafe logical path.',
      );
    }
    final segments = path.split('/');
    if (segments.any(
      (segment) => segment.isEmpty || segment == '.' || segment == '..',
    )) {
      throw const LocalSyncPackageException(
        'The local package contains an unsafe logical path.',
      );
    }
    return segments;
  }

  static String _relativeLogicalPath(String root, String path) {
    final prefix = root.endsWith(Platform.pathSeparator)
        ? root
        : '$root${Platform.pathSeparator}';
    if (!path.startsWith(prefix)) {
      throw const LocalSyncPackageException(
        'The local package content escaped its root.',
      );
    }
    final value = path
        .substring(prefix.length)
        .split(Platform.pathSeparator)
        .join('/');
    _safeSegments(value);
    return value;
  }

  static bool _isSha256(String value) =>
      RegExp(r'^[0-9a-f]{64}$').hasMatch(value);

  static DateTime _parseDate(Object? value) {
    if (value is String) {
      final parsed = DateTime.tryParse(value);
      if (parsed != null) return parsed.toUtc();
    }
    throw const LocalSyncPackageException(
      'A queue record has an invalid date.',
    );
  }

  static DateTime? _optionalDate(Object? value) =>
      value == null ? null : _parseDate(value);

  static int _attemptCount(Object? value) {
    if (value is int && value >= 0 && value <= 1000000) return value;
    throw const LocalSyncPackageException(
      'A queue record has an invalid attempt count.',
    );
  }

  static String? _optionalBoundedString(Object? value, int max) {
    if (value == null) return null;
    if (value is String && value.length <= max) return value;
    throw const LocalSyncPackageException(
      'A queue state field exceeds its limit.',
    );
  }

  static String? _optionalSha256(Object? value) {
    if (value == null) return null;
    if (value is String && _isSha256(value)) return value;
    throw const LocalSyncPackageException('A queue state checksum is invalid.');
  }

  static _SyncStatus _parseStatus(Object? value) => switch (value) {
    'queued' => _SyncStatus.queued,
    'syncing' => _SyncStatus.syncing,
    'retry' => _SyncStatus.retryScheduled,
    'failed' => _SyncStatus.failed,
    'synchronized' => _SyncStatus.synchronized,
    _ => throw const LocalSyncPackageException(
      'A queue record has an invalid status.',
    ),
  };

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

class LocalSyncRunResult {
  const LocalSyncRunResult({
    required this.synchronized,
    required this.retriedLater,
    required this.failed,
    required this.skipped,
  });

  final int synchronized;
  final int retriedLater;
  final int failed;
  final int skipped;
}

class LocalSyncPackageException implements Exception {
  const LocalSyncPackageException(this.message);

  final String message;

  @override
  String toString() => message;
}

class _PreparedPackage {
  const _PreparedPackage({
    required this.packageId,
    required this.files,
    required this.remoteManifestBytes,
    required this.remoteManifestSha256,
  });

  final String packageId;
  final List<_PreparedFile> files;
  final List<int> remoteManifestBytes;
  final String remoteManifestSha256;
}

class _PreparedFile {
  const _PreparedFile(this.file, this.descriptor);

  final File file;
  final LocalSyncFileDescriptor descriptor;
}

enum _SyncStatus {
  queued('queued'),
  syncing('syncing'),
  retryScheduled('retry'),
  failed('failed'),
  synchronized('synchronized');

  const _SyncStatus(this.value);
  final String value;
}

class _JobState {
  _JobState({
    required this.packageId,
    required this.status,
    required this.attemptCount,
    required this.createdAt,
    required this.updatedAt,
    required this.sequence,
    required this.nextAttemptAt,
    required this.lastError,
    required this.remoteManifestSha256,
    required this.completedAt,
  });

  final String packageId;
  final _SyncStatus status;
  final int attemptCount;
  final DateTime createdAt;
  final DateTime updatedAt;
  int sequence;
  final DateTime? nextAttemptAt;
  final String? lastError;
  final String? remoteManifestSha256;
  final DateTime? completedAt;

  _JobState copyWith({
    _SyncStatus? status,
    int? attemptCount,
    DateTime? updatedAt,
    DateTime? nextAttemptAt,
    bool clearNextAttemptAt = false,
    String? lastError,
    bool clearLastError = false,
    String? remoteManifestSha256,
    DateTime? completedAt,
  }) => _JobState(
    packageId: packageId,
    status: status ?? this.status,
    attemptCount: attemptCount ?? this.attemptCount,
    createdAt: createdAt,
    updatedAt: updatedAt ?? this.updatedAt,
    sequence: sequence,
    nextAttemptAt: clearNextAttemptAt
        ? null
        : nextAttemptAt ?? this.nextAttemptAt,
    lastError: clearLastError ? null : lastError ?? this.lastError,
    remoteManifestSha256: remoteManifestSha256 ?? this.remoteManifestSha256,
    completedAt: completedAt ?? this.completedAt,
  );
}

class _DigestSink implements Sink<Digest> {
  Digest? value;

  @override
  void add(Digest data) => value = data;

  @override
  void close() {}
}
