import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:http/http.dart' as http;
import 'package:showvault_app/src/config/app_config.dart';
import 'package:showvault_app/src/recovery/hosted_sync_object_store.dart';
import 'package:showvault_app/src/recovery/local_recovery_service.dart';
import 'package:showvault_app/src/recovery/local_restore_service.dart';
import 'package:showvault_app/src/recovery/local_sync_object_store.dart';
import 'package:showvault_app/src/recovery/local_sync_service.dart';
import 'package:showvault_app/src/scanning/local_catalog_scanner.dart';

class ResilienceHarness {
  const ResilienceHarness._();

  static const _command = '--showvault-resilience-phase';
  static const _version = 'showvault.resilience-matrix.v1';
  static const _artifactVersion = '0.1.0+1';
  static const _accessToken = 'showvault-personal-beta-loopback';

  static Future<bool> tryRun(List<String> arguments) async {
    if (!arguments.contains(_command)) return false;
    if (!AppConfig.canRunResilienceHarness || arguments.length != 2) {
      stderr.writeln('ShowVault resilience harness is unavailable.');
      exitCode = 64;
      return true;
    }
    final phaseIndex = arguments.indexOf(_command);
    final phase = phaseIndex == 0 ? arguments[1] : arguments[0];
    if (!_phases.contains(phase)) {
      stderr.writeln('ShowVault resilience phase is unsupported.');
      exitCode = 64;
      return true;
    }

    final fixtureName = AppConfig.syntheticFixtureHome;
    if (!RegExp(
      r'^showvault-resilience-[a-z0-9-]{1,80}$',
    ).hasMatch(fixtureName)) {
      stderr.writeln('ShowVault resilience fixture identity is unsafe.');
      exitCode = 64;
      return true;
    }
    final harness = _Harness(
      _Harness.join(Directory.systemTemp.path, fixtureName),
    );
    if (phase == 'cleanup') {
      await harness.cleanup();
      stdout.writeln('ShowVault resilience cleanup passed.');
      return true;
    }
    final watch = Stopwatch()..start();
    try {
      final details = await harness.run(phase);
      watch.stop();
      await harness.record(phase, 'passed', watch.elapsedMilliseconds, details);
      stdout.writeln('ShowVault resilience phase passed: $phase');
      if (phase == 'finalize') {
        stdout.writeln(
          'SHOWVAULT_RESILIENCE_REPORT:${await harness.encodedReport()}',
        );
      }
    } catch (_) {
      watch.stop();
      await harness.record(
        phase,
        'failed',
        watch.elapsedMilliseconds,
        const {},
      );
      stderr.writeln('ShowVault resilience phase failed: $phase');
      exitCode = 1;
    }
    return true;
  }

  static const _phases = {
    'prepare',
    'api-unavailable',
    'interrupt-upload',
    'resume-upload',
    'storage-unavailable',
    'storage-resume',
    'failure-matrix',
    'finalize',
    'cleanup',
  };
}

class _Harness {
  _Harness(this.root)
    : stateFile = File(_join(root, 'state.json')),
      eventsFile = File(_join(_join(root, 'evidence'), 'events.jsonl'));

  final String root;
  final File stateFile;
  final File eventsFile;
  File get _ownershipMarker => File(_join(root, '.showvault-resilience-owned'));

  String get _primarySource => _join(root, 'primary-source');
  String get _primaryVault => _join(root, 'primary-vault');
  String get _storageSource => _join(root, 'storage-source');
  String get _storageVault => _join(root, 'storage-vault');

  Future<Map<String, Object?>> run(String phase) => switch (phase) {
    'prepare' => _prepare(),
    'api-unavailable' => _apiUnavailable(),
    'interrupt-upload' => _interruptUpload(),
    'resume-upload' => _resumeUpload(),
    'storage-unavailable' => _storageUnavailable(),
    'storage-resume' => _storageResume(),
    'failure-matrix' => _failureMatrix(),
    'finalize' => _finalize(),
    _ => throw const FormatException(),
  };

  Future<String> encodedReport() async => base64Encode(
    await File(_join(_join(root, 'evidence'), 'report.json')).readAsBytes(),
  );

  Future<void> cleanup() async {
    final name = root.split(Platform.pathSeparator).last;
    if (!name.startsWith('showvault-resilience-')) {
      throw const FormatException();
    }
    final type = await FileSystemEntity.type(root, followLinks: false);
    if (type == FileSystemEntityType.notFound) return;
    if (type != FileSystemEntityType.directory ||
        !await _ownershipMarker.exists()) {
      throw const FormatException();
    }
    await Directory(root).delete(recursive: true);
  }

  Future<Map<String, Object?>> _prepare() async {
    await _requireSafeRoot();
    if (await stateFile.exists()) throw const FormatException();
    final tenant = await _createTenant();
    final primary = await _saveFixture(
      _primarySource,
      _primaryVault,
      'primary',
      DateTime.utc(2030, 1, 1),
    );
    final storage = await _saveFixture(
      _storageSource,
      _storageVault,
      'storage',
      DateTime.utc(2030, 1, 1, 0, 1),
    );
    await stateFile.writeAsString(
      jsonEncode({
        'organizationId': tenant.$1,
        'venueId': tenant.$2,
        'primaryPackageId': primary.recoveryPointId,
        'storagePackageId': storage.recoveryPointId,
      }),
      flush: true,
    );
    return {
      'localPackages': 2,
      'verifiedFiles': primary.fileCount + storage.fileCount,
      'verifiedBytes': primary.totalBytes + storage.totalBytes,
      'health': await _readiness(),
    };
  }

  Future<Map<String, Object?>> _apiUnavailable() async {
    final state = await _state();
    final result = await LocalSyncService(
      objectStore: _hosted(state),
      now: () => DateTime.utc(2030, 2, 1),
      chunkBytes: 8,
    ).syncPending(_primaryVault);
    _require(result.retriedLater == 1 && result.synchronized == 0);
    final queue = await _latestQueue(_primaryVault, state.primaryPackageId);
    final snapshot = await LocalRecoveryService().inspectVault(_primaryVault);
    _require(
      snapshot.records.single.localStatus == LocalProtectionStatus.verified,
    );
    return {
      'health': await _readiness(),
      'queueStatus': queue['status'],
      'attemptCount': queue['attemptCount'],
      'localPackagePreserved': true,
      'receiptPublished': false,
    };
  }

  Future<Map<String, Object?>> _interruptUpload() async {
    final state = await _state();
    final cancellation = LocalSyncCancellation();
    final store = _CancelAfterFirstChunkStore(_hosted(state), cancellation);
    final result = await LocalSyncService(
      objectStore: store,
      now: () => DateTime.utc(2030, 3, 1),
      chunkBytes: 8,
    ).syncPending(_primaryVault, cancellation: cancellation);
    _require(result.retriedLater == 1 && store.appendCalls == 1);
    final partial = await store.delegate.uploadedLength(
      state.primaryPackageId,
      'Subcrates/synthetic.crate',
    );
    _require(partial == 8);
    _require(
      await store.delegate.committedReceipt(state.primaryPackageId) == null,
    );
    final queue = await _latestQueue(_primaryVault, state.primaryPackageId);
    return {
      'health': await _readiness(),
      'queueStatus': queue['status'],
      'attemptCount': queue['attemptCount'],
      'durableRemoteBytes': partial,
      'receiptPublished': false,
    };
  }

  Future<Map<String, Object?>> _resumeUpload() async {
    final state = await _state();
    final store = _hosted(state);
    final service = LocalSyncService(
      objectStore: store,
      now: () => DateTime.utc(2030, 4, 1),
      chunkBytes: 8,
    );
    final first = await service.syncPending(_primaryVault);
    _require(first.synchronized == 1);
    final receipt = await store.committedReceipt(state.primaryPackageId);
    _require(receipt != null);
    final second = await LocalSyncService(
      objectStore: store,
      now: () => DateTime.utc(2030, 4, 1, 1),
      chunkBytes: 8,
    ).syncPending(_primaryVault);
    _require(second.skipped == 1);

    final target = Directory(_join(root, 'successful-restore'));
    await target.create();
    final restored =
        await LocalRestoreService(
          now: () => DateTime.utc(2030, 4, 1, 2),
        ).restore(
          authorizedVaultRoot: _primaryVault,
          recoveryPointId: state.primaryPackageId,
          targetPath: target.path,
        );
    final evidence = await File(restored.evidencePath).readAsString();
    _require(!evidence.contains(root));
    _require(
      await File(
            _join(
              _join(target.path, 'ShowVault Restored Files'),
              'database V2',
            ),
          ).readAsString() ==
          'synthetic-primary-library-content',
    );
    final queue = await _latestQueue(_primaryVault, state.primaryPackageId);
    return {
      'health': await _readiness(),
      'queueStatus': queue['status'],
      'attemptCount': queue['attemptCount'],
      'receiptPublished': true,
      'duplicateCompletion': 'idempotent',
      'restoredFiles': restored.restoredFileCount,
      'restoredBytes': restored.restoredBytes,
      'restoreEvidencePathFree': true,
    };
  }

  Future<Map<String, Object?>> _storageUnavailable() async {
    final state = await _state();
    final result = await LocalSyncService(
      objectStore: _hosted(state),
      now: () => DateTime.utc(2030, 5, 1),
      chunkBytes: 8,
    ).syncPending(_storageVault);
    _require(result.retriedLater == 1 && result.synchronized == 0);
    final queue = await _latestQueue(_storageVault, state.storagePackageId);
    return {
      'health': await _readiness(),
      'queueStatus': queue['status'],
      'attemptCount': queue['attemptCount'],
      'localPackagePreserved': true,
      'receiptPublished': false,
    };
  }

  Future<Map<String, Object?>> _storageResume() async {
    final state = await _state();
    final store = _hosted(state);
    final result = await LocalSyncService(
      objectStore: store,
      now: () => DateTime.utc(2030, 6, 1),
      chunkBytes: 8,
    ).syncPending(_storageVault);
    _require(result.synchronized == 1);
    _require(await store.committedReceipt(state.storagePackageId) != null);
    final queue = await _latestQueue(_storageVault, state.storagePackageId);
    return {
      'health': await _readiness(),
      'queueStatus': queue['status'],
      'attemptCount': queue['attemptCount'],
      'receiptPublished': true,
    };
  }

  Future<Map<String, Object?>> _failureMatrix() async {
    final state = await _state();
    final store = _hosted(state);
    var safeFailures = 0;

    final mutationSource = _join(root, 'mutation-source');
    final mutationVault = _join(root, 'mutation-vault');
    await _writeSource(mutationSource, 'mutation');
    var mutated = false;
    try {
      await LocalRecoveryService(
        vaultRoot: mutationVault,
        now: () => DateTime.utc(2030, 7, 1),
        onFileCopied: (path) async {
          if (!mutated) {
            mutated = true;
            await File(path).writeAsString('changed-during-save');
          }
        },
      ).save(_source(mutationSource));
      throw const FormatException();
    } on LocalRecoveryException {
      _require(!await _hasPublishedPackage(mutationVault));
      safeFailures++;
    }

    final tamper = await _saveFixture(
      _join(root, 'tamper-source'),
      _join(root, 'tamper-vault'),
      'tamper',
      DateTime.utc(2030, 7, 2),
    );
    final tamperVault = _join(root, 'tamper-vault');
    await File(
      _join(_join(tamper.recoveryPointPath, 'content'), 'database V2'),
    ).writeAsString('tampered');
    final tamperRun = await LocalSyncService(
      objectStore: store,
      now: () => DateTime.utc(2030, 7, 3),
      chunkBytes: 8,
    ).syncPending(tamperVault);
    _require(tamperRun.failed == 1);
    _require(await store.committedReceipt(tamper.recoveryPointId) == null);
    safeFailures++;

    safeFailures += await _remoteFailure(
      state,
      'corrupt',
      _CorruptingStore(store),
      DateTime.utc(2030, 7, 4),
    );
    safeFailures += await _remoteFailure(
      state,
      'incomplete',
      _DropFirstChunkStore(store),
      DateTime.utc(2030, 7, 5),
    );
    safeFailures += await _remoteFailure(
      state,
      'conflict',
      _ConflictingDuplicateStore(store),
      DateTime.utc(2030, 7, 6),
    );

    final occupied = await Directory(_join(root, 'occupied-target')).create();
    await File(_join(occupied.path, 'keep')).writeAsString('keep');
    try {
      await LocalRestoreService().restore(
        authorizedVaultRoot: _primaryVault,
        recoveryPointId: state.primaryPackageId,
        targetPath: occupied.path,
      );
      throw const FormatException();
    } on LocalRestoreException {
      _require(
        await File(_join(occupied.path, 'keep')).readAsString() == 'keep',
      );
      safeFailures++;
    }

    final interruptedTarget = await Directory(
      _join(root, 'interrupted-target'),
    ).create();
    final cancellation = LocalRestoreCancellation();
    try {
      await LocalRestoreService(
        onFileCopied: (_) async => cancellation.cancel(),
      ).restore(
        authorizedVaultRoot: _primaryVault,
        recoveryPointId: state.primaryPackageId,
        targetPath: interruptedTarget.path,
        cancellation: cancellation,
      );
      throw const FormatException();
    } on LocalRestoreException {
      _require(await interruptedTarget.list().isEmpty);
      safeFailures++;
    }

    _require(safeFailures == 7);
    return {
      'health': await _readiness(),
      'safeFailureCases': safeFailures,
      'sourceMutationPublishedPackage': false,
      'tamperPublishedReceipt': false,
      'corruptRemotePublishedReceipt': false,
      'incompleteRemotePublishedReceipt': false,
      'conflictingChunkPublishedReceipt': false,
      'nonEmptyTargetChanged': false,
      'interruptedRestorePublished': false,
    };
  }

  Future<int> _remoteFailure(
    _State state,
    String label,
    LocalSyncObjectStore faultingStore,
    DateTime time,
  ) async {
    final vault = _join(root, '$label-vault');
    final saved = await _saveFixture(
      _join(root, '$label-source'),
      vault,
      label,
      time,
    );
    final run = await LocalSyncService(
      objectStore: faultingStore,
      now: () => time.add(const Duration(hours: 1)),
      chunkBytes: 8,
    ).syncPending(vault);
    _require(run.failed == 1);
    _require(
      await _hosted(state).committedReceipt(saved.recoveryPointId) == null,
    );
    _require(await Directory(saved.recoveryPointPath).exists());
    return 1;
  }

  Future<Map<String, Object?>> _finalize() async {
    final events = await _events();
    _require(events.length == 7);
    _require(events.every((event) => event['outcome'] == 'passed'));
    final executableDigest = await sha256
        .bind(File(Platform.resolvedExecutable).openRead())
        .first;
    final core = <String, Object?>{
      'formatVersion': ResilienceHarness._version,
      'artifactVersion': ResilienceHarness._artifactVersion,
      'platform': 'macos',
      'installedArtifactExecution': true,
      'hostRebootExecuted': false,
      'productionProviderExecuted': false,
      'executableSha256': executableDigest.toString(),
      'events': events,
    };
    _require(!jsonEncode(core).contains(root));
    final evidenceSha256 = sha256
        .convert(utf8.encode(jsonEncode(core)))
        .toString();
    final report = {...core, 'evidenceSha256': evidenceSha256};
    final reportFile = File(_join(_join(root, 'evidence'), 'report.json'));
    await reportFile.writeAsString(jsonEncode(report), flush: true);
    return {
      'eventCount': events.length,
      'reportSha256': evidenceSha256,
      'pathFree': true,
    };
  }

  Future<(String, String)> _createTenant() async {
    final suffix = DateTime.now().microsecondsSinceEpoch.toRadixString(36);
    final organization = await _post('/api/v1/organizations/', {
      'name': 'Synthetic resilience',
      'slug': 'resilience-$suffix',
    });
    final organizationId = _id(organization);
    final venue = await _post('/api/v1/organizations/$organizationId/venues', {
      'name': 'Synthetic venue',
      'timeZoneId': 'UTC',
    });
    return (organizationId, _id(venue));
  }

  Future<Map<String, Object?>> _post(
    String path,
    Map<String, Object?> body,
  ) async {
    final response = await http
        .post(
          Uri.parse('${AppConfig.apiBaseUrl}$path'),
          headers: {
            'Authorization': 'Bearer ${ResilienceHarness._accessToken}',
            'Content-Type': 'application/json',
          },
          body: jsonEncode(body),
        )
        .timeout(const Duration(seconds: 10));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw const HttpException('Synthetic tenant request failed.');
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! Map<String, Object?> ||
        decoded['payload'] is! Map<String, Object?>) {
      throw const FormatException();
    }
    return decoded['payload']! as Map<String, Object?>;
  }

  static String _id(Map<String, Object?> payload) {
    final id = payload['id'];
    if (id is! String || id.length != 36) throw const FormatException();
    return id;
  }

  HostedSyncObjectStore _hosted(_State state) => HostedSyncObjectStore(
    apiBaseUrl: AppConfig.apiBaseUrl,
    accessToken: ResilienceHarness._accessToken,
    organizationId: state.organizationId,
    venueId: state.venueId,
  );

  Future<LocalBackupResult> _saveFixture(
    String sourceRoot,
    String vaultRoot,
    String label,
    DateTime time,
  ) async {
    await _writeSource(sourceRoot, label);
    return LocalRecoveryService(
      vaultRoot: vaultRoot,
      now: () => time,
    ).save(_source(sourceRoot));
  }

  static LocalBackupSource _source(String sourceRoot) => LocalBackupSource(
    candidateKey: 'macos.serato-dj-pro.user-data',
    pluginId: 'showvault.serato-dj-pro',
    productName: 'Serato DJ Pro',
    rootPath: sourceRoot,
  );

  static Future<void> _writeSource(String sourceRoot, String label) async {
    final root = await Directory(sourceRoot).create(recursive: true);
    await Directory(_join(root.path, 'Subcrates')).create();
    await File(
      _join(root.path, 'database V2'),
    ).writeAsString('synthetic-$label-library-content', flush: true);
    await File(
      _join(_join(root.path, 'Subcrates'), 'synthetic.crate'),
    ).writeAsString('synthetic-$label-crate-content', flush: true);
  }

  Future<_State> _state() async {
    if (await stateFile.length() > 16 * 1024) throw const FormatException();
    final decoded = jsonDecode(await stateFile.readAsString());
    if (decoded is! Map<String, Object?>) throw const FormatException();
    return _State(
      _required(decoded, 'organizationId'),
      _required(decoded, 'venueId'),
      _required(decoded, 'primaryPackageId'),
      _required(decoded, 'storagePackageId'),
    );
  }

  static String _required(Map<String, Object?> value, String key) {
    final result = value[key];
    if (result is! String || result.isEmpty || result.length > 128) {
      throw const FormatException();
    }
    return result;
  }

  Future<Map<String, Object?>> _latestQueue(
    String vault,
    String packageId,
  ) async {
    final directory = Directory(
      _join(_join(_join(vault, 'Upload Queue'), 'State'), packageId),
    );
    final files = directory.listSync().whereType<File>().toList()
      ..sort((left, right) => left.path.compareTo(right.path));
    if (files.isEmpty) throw const FormatException();
    final decoded = jsonDecode(await files.last.readAsString());
    if (decoded is! Map<String, Object?>) throw const FormatException();
    return decoded;
  }

  Future<String> _readiness() async {
    try {
      final response = await http
          .get(Uri.parse('${AppConfig.apiBaseUrl}/health/ready'))
          .timeout(const Duration(seconds: 2));
      return response.statusCode == 200 ? 'ready' : 'unavailable';
    } catch (_) {
      return 'unavailable';
    }
  }

  Future<void> record(
    String phase,
    String outcome,
    int elapsedMilliseconds,
    Map<String, Object?> details,
  ) async {
    await eventsFile.parent.create(recursive: true);
    final event = <String, Object?>{
      'formatVersion': ResilienceHarness._version,
      'phase': phase,
      'outcome': outcome,
      'elapsedMilliseconds': elapsedMilliseconds,
      'artifactVersion': ResilienceHarness._artifactVersion,
      'platform': 'macos',
      ...details,
    };
    final encoded = jsonEncode(event);
    _require(!encoded.contains(root));
    await eventsFile.writeAsString(
      '$encoded\n',
      mode: FileMode.append,
      flush: true,
    );
  }

  Future<List<Map<String, Object?>>> _events() async {
    final lines = await eventsFile.readAsLines();
    return lines
        .map((line) {
          final decoded = jsonDecode(line);
          if (decoded is! Map<String, Object?>) throw const FormatException();
          return decoded;
        })
        .toList(growable: false);
  }

  Future<void> _requireSafeRoot() async {
    final type = await FileSystemEntity.type(root, followLinks: false);
    if (type == FileSystemEntityType.notFound) {
      await Directory(root).create(recursive: true);
    } else if (type != FileSystemEntityType.directory) {
      throw const FormatException();
    }
    if (await Directory(root).resolveSymbolicLinks() !=
        Directory(root).absolute.path) {
      throw const FormatException();
    }
    if (await _ownershipMarker.exists()) {
      if (await _ownershipMarker.readAsString() != ResilienceHarness._version) {
        throw const FormatException();
      }
    } else {
      await _ownershipMarker.writeAsString(
        ResilienceHarness._version,
        flush: true,
      );
    }
  }

  static Future<bool> _hasPublishedPackage(String vault) async {
    final backups = Directory(_join(vault, 'Backups'));
    if (!await backups.exists()) return false;
    await for (final entity in backups.list(
      recursive: true,
      followLinks: false,
    )) {
      if (entity is Directory &&
          !entity.path.split(Platform.pathSeparator).last.startsWith('.')) {
        final manifest = File(_join(entity.path, 'manifest.json'));
        if (await manifest.exists()) return true;
      }
    }
    return false;
  }

  static String join(String left, String right) =>
      '$left${left.endsWith(Platform.pathSeparator) ? '' : Platform.pathSeparator}$right';

  static String _join(String left, String right) => join(left, right);

  static void _require(bool condition) {
    if (!condition) throw const FormatException();
  }
}

class _State {
  const _State(
    this.organizationId,
    this.venueId,
    this.primaryPackageId,
    this.storagePackageId,
  );

  final String organizationId;
  final String venueId;
  final String primaryPackageId;
  final String storagePackageId;
}

class _CancelAfterFirstChunkStore
    implements LocalSyncObjectStore, LocalSyncSessionObjectStore {
  _CancelAfterFirstChunkStore(this.delegate, this.cancellation);

  final HostedSyncObjectStore delegate;
  final LocalSyncCancellation cancellation;
  int appendCalls = 0;

  @override
  Future<LocalSyncReceipt?> beginUpload(
    String packageId,
    List<int> manifest,
    List<LocalSyncFileDescriptor> files,
  ) => delegate.beginUpload(packageId, manifest, files);

  @override
  Future<LocalSyncReceipt?> committedReceipt(String packageId) =>
      delegate.committedReceipt(packageId);

  @override
  Future<int> uploadedLength(String packageId, String relativePath) =>
      delegate.uploadedLength(packageId, relativePath);

  @override
  Future<void> appendChunk(
    String packageId,
    String relativePath,
    int offset,
    List<int> bytes,
  ) async {
    await delegate.appendChunk(packageId, relativePath, offset, bytes);
    appendCalls++;
    if (appendCalls == 1) cancellation.cancel();
  }

  @override
  Future<LocalSyncReceipt> verifyAndCommit(
    String packageId,
    List<int> manifest,
    List<LocalSyncFileDescriptor> files,
  ) => delegate.verifyAndCommit(packageId, manifest, files);
}

abstract class _DelegatingFaultStore
    implements LocalSyncObjectStore, LocalSyncSessionObjectStore {
  _DelegatingFaultStore(this.delegate);
  final HostedSyncObjectStore delegate;

  @override
  Future<LocalSyncReceipt?> beginUpload(
    String packageId,
    List<int> manifest,
    List<LocalSyncFileDescriptor> files,
  ) => delegate.beginUpload(packageId, manifest, files);
  @override
  Future<LocalSyncReceipt?> committedReceipt(String packageId) =>
      delegate.committedReceipt(packageId);
  @override
  Future<int> uploadedLength(String packageId, String relativePath) =>
      delegate.uploadedLength(packageId, relativePath);
  @override
  Future<LocalSyncReceipt> verifyAndCommit(
    String packageId,
    List<int> manifest,
    List<LocalSyncFileDescriptor> files,
  ) => delegate.verifyAndCommit(packageId, manifest, files);
}

class _CorruptingStore extends _DelegatingFaultStore {
  _CorruptingStore(super.delegate);
  bool corrupted = false;
  @override
  Future<void> appendChunk(
    String packageId,
    String relativePath,
    int offset,
    List<int> bytes,
  ) async {
    final payload = List<int>.from(bytes);
    if (!corrupted) {
      payload[0] ^= 0xff;
      corrupted = true;
    }
    await delegate.appendChunk(packageId, relativePath, offset, payload);
  }
}

class _DropFirstChunkStore extends _DelegatingFaultStore {
  _DropFirstChunkStore(super.delegate);
  bool dropped = false;
  @override
  Future<void> appendChunk(
    String packageId,
    String relativePath,
    int offset,
    List<int> bytes,
  ) async {
    if (!dropped) {
      dropped = true;
      return;
    }
    await delegate.appendChunk(packageId, relativePath, offset, bytes);
  }
}

class _ConflictingDuplicateStore extends _DelegatingFaultStore {
  _ConflictingDuplicateStore(super.delegate);
  bool conflicted = false;
  @override
  Future<void> appendChunk(
    String packageId,
    String relativePath,
    int offset,
    List<int> bytes,
  ) async {
    await delegate.appendChunk(packageId, relativePath, offset, bytes);
    if (!conflicted) {
      conflicted = true;
      final conflict = List<int>.from(bytes)..[0] ^= 0xff;
      await delegate.appendChunk(packageId, relativePath, offset, conflict);
    }
  }
}
