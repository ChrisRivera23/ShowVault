import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/recovery/local_recovery_service.dart';
import 'package:showvault_app/src/recovery/local_sync_object_store.dart';
import 'package:showvault_app/src/recovery/local_sync_service.dart';
import 'package:showvault_app/src/scanning/local_catalog_scanner.dart';

void main() {
  late Directory testRoot;
  late Directory sourceRoot;
  late String vaultRoot;
  late String remoteRoot;
  late DateTime clock;

  setUp(() async {
    testRoot = await Directory.systemTemp.createTemp('showvault-sync-');
    sourceRoot = await Directory('${testRoot.path}/source').create();
    vaultRoot = '${testRoot.path}/vault';
    remoteRoot = '${testRoot.path}/object-store';
    clock = DateTime.utc(2026, 8, 10, 18);
  });

  tearDown(() async {
    if (await testRoot.exists()) await testRoot.delete(recursive: true);
  });

  LocalBackupSource source() => LocalBackupSource(
    candidateKey: 'macos.serato-dj-pro.user-data',
    pluginId: 'showvault.serato-dj-pro',
    productName: 'Serato DJ Pro',
    rootPath: sourceRoot.path,
  );

  Future<LocalBackupResult> saveFixture() async {
    await Directory('${sourceRoot.path}/Subcrates').create();
    await File(
      '${sourceRoot.path}/database V2',
    ).writeAsString('synthetic-library-content');
    await File(
      '${sourceRoot.path}/Subcrates/test.crate',
    ).writeAsString('synthetic-crate-content');
    return LocalRecoveryService(
      vaultRoot: vaultRoot,
      now: () => clock,
    ).save(source());
  }

  Future<Map<String, Object?>> latestState(String packageId) async {
    final root = Directory('$vaultRoot/Upload Queue/State/$packageId');
    final files = root.listSync().whereType<File>().toList()
      ..sort((left, right) => left.path.compareTo(right.path));
    return jsonDecode(await files.last.readAsString()) as Map<String, Object?>;
  }

  test(
    'sync reverifies locally, uploads, remotely verifies, and filters private path',
    () async {
      final saved = await saveFixture();
      final service = LocalSyncService(
        objectStore: LocalFolderObjectStore(remoteRoot),
        now: () => clock,
        chunkBytes: 5,
      );

      final run = await service.syncPending(vaultRoot);

      expect(run.synchronized, 1);
      expect(run.failed, 0);
      final remoteManifest = File(
        '$remoteRoot/packages/${saved.recoveryPointId}/manifest.json',
      );
      final text = await remoteManifest.readAsString();
      expect(text, isNot(contains(sourceRoot.path)));
      expect(text, isNot(contains('"identity"')));
      final decoded = jsonDecode(text) as Map<String, Object?>;
      expect(decoded['packageId'], saved.recoveryPointId);
      expect(decoded['localManifestSha256'], saved.recoveryPointId);
      expect(
        await File(
          '$remoteRoot/packages/${saved.recoveryPointId}/content/database V2',
        ).readAsString(),
        'synthetic-library-content',
      );
      expect(
        (await latestState(saved.recoveryPointId))['status'],
        'synchronized',
      );
      final snapshot = await LocalRecoveryService().inspectVault(vaultRoot);
      expect(
        snapshot.records.single.cloudStatus,
        LocalCloudSyncStatus.synchronized,
      );
    },
  );

  test('interrupted upload resumes from remote length after restart', () async {
    final saved = await saveFixture();
    final delegate = LocalFolderObjectStore(remoteRoot);
    final interrupted = _InterruptAfterFirstChunkStore(delegate);
    final first = LocalSyncService(
      objectStore: interrupted,
      now: () => clock,
      chunkBytes: 4,
    );

    final firstRun = await first.syncPending(vaultRoot);

    expect(firstRun.retriedLater, 1);
    expect(interrupted.appendCalls, 1);
    expect((await latestState(saved.recoveryPointId))['status'], 'retry');

    clock = clock.add(const Duration(seconds: 31));
    final second = LocalSyncService(
      objectStore: delegate,
      now: () => clock,
      chunkBytes: 4,
    );
    final secondRun = await second.syncPending(vaultRoot);

    expect(secondRun.synchronized, 1);
    final state = await latestState(saved.recoveryPointId);
    expect(state['status'], 'synchronized');
    expect(state['attemptCount'], 2);
    expect(
      await File(
        '$remoteRoot/packages/${saved.recoveryPointId}/content/database V2',
      ).readAsString(),
      'synthetic-library-content',
    );
  });

  test('completed synchronization is idempotent', () async {
    final saved = await saveFixture();
    final store = _CountingStore(LocalFolderObjectStore(remoteRoot));
    final service = LocalSyncService(
      objectStore: store,
      now: () => clock,
      chunkBytes: 4,
    );

    expect((await service.syncPending(vaultRoot)).synchronized, 1);
    final appendCalls = store.appendCalls;
    final stateEvents = Directory(
      '$vaultRoot/Upload Queue/State/${saved.recoveryPointId}',
    ).listSync().length;

    expect((await service.syncPending(vaultRoot)).skipped, 1);
    expect(store.appendCalls, appendCalls);
    expect(
      Directory(
        '$vaultRoot/Upload Queue/State/${saved.recoveryPointId}',
      ).listSync().length,
      stateEvents,
    );
  });

  test(
    'offline failure schedules bounded retry and preserves local verification',
    () async {
      final saved = await saveFixture();
      final service = LocalSyncService(
        objectStore: const _UnavailableStore(),
        now: () => clock,
      );

      final run = await service.syncPending(vaultRoot);

      expect(run.retriedLater, 1);
      final state = await latestState(saved.recoveryPointId);
      expect(state['status'], 'retry');
      expect(state['attemptCount'], 1);
      expect(
        state['lastError'],
        'Synchronization is unavailable and will retry.',
      );
      expect(await Directory(saved.recoveryPointPath).exists(), isTrue);
      final snapshot = await LocalRecoveryService().inspectVault(vaultRoot);
      expect(
        snapshot.records.single.localStatus,
        LocalProtectionStatus.verified,
      );
      expect(
        snapshot.records.single.cloudStatus,
        LocalCloudSyncStatus.retryScheduled,
      );
    },
  );

  test('offline retries stop at the durable attempt limit', () async {
    final saved = await saveFixture();
    final service = LocalSyncService(
      objectStore: const _UnavailableStore(),
      now: () => clock,
      maxAttempts: 3,
      baseRetryDelay: const Duration(seconds: 1),
    );

    expect((await service.syncPending(vaultRoot)).retriedLater, 1);
    clock = clock.add(const Duration(seconds: 2));
    expect((await service.syncPending(vaultRoot)).retriedLater, 1);
    clock = clock.add(const Duration(seconds: 3));
    expect((await service.syncPending(vaultRoot)).failed, 1);

    final state = await latestState(saved.recoveryPointId);
    expect(state['status'], 'failed');
    expect(state['attemptCount'], 3);
    expect((await service.syncPending(vaultRoot)).skipped, 1);
  });

  test('tampered local package is rejected without upload', () async {
    final saved = await saveFixture();
    await File(
      '${saved.recoveryPointPath}/content/database V2',
    ).writeAsString('tampered');
    final service = LocalSyncService(
      objectStore: LocalFolderObjectStore(remoteRoot),
      now: () => clock,
    );

    final run = await service.syncPending(vaultRoot);

    expect(run.failed, 1);
    expect((await latestState(saved.recoveryPointId))['status'], 'failed');
    expect(await Directory('$remoteRoot/packages').exists(), isFalse);
    expect(await Directory(saved.recoveryPointPath).exists(), isTrue);
  });

  test('unlisted local package content is rejected without upload', () async {
    final saved = await saveFixture();
    await File(
      '${saved.recoveryPointPath}/content/unlisted',
    ).writeAsString('unexpected');

    final run = await LocalSyncService(
      objectStore: LocalFolderObjectStore(remoteRoot),
      now: () => clock,
    ).syncPending(vaultRoot);

    expect(run.failed, 1);
    expect((await latestState(saved.recoveryPointId))['status'], 'failed');
    expect(await Directory('$remoteRoot/packages').exists(), isFalse);
  });

  test(
    'remote checksum corruption never marks synchronization complete',
    () async {
      final saved = await saveFixture();
      final delegate = LocalFolderObjectStore(remoteRoot);
      final service = LocalSyncService(
        objectStore: _CorruptBeforeCommitStore(delegate, remoteRoot),
        now: () => clock,
        chunkBytes: 4,
      );

      final run = await service.syncPending(vaultRoot);

      expect(run.failed, 1);
      expect((await latestState(saved.recoveryPointId))['status'], 'failed');
      expect(
        await Directory(
          '$remoteRoot/packages/${saved.recoveryPointId}',
        ).exists(),
        isFalse,
      );
    },
  );

  test(
    'linked package content is rejected before upload',
    () async {
      final saved = await saveFixture();
      final content = File('${saved.recoveryPointPath}/content/database V2');
      final outside = File('${testRoot.path}/outside')..writeAsStringSync('x');
      await content.delete();
      await Link(content.path).create(outside.path);

      final run = await LocalSyncService(
        objectStore: LocalFolderObjectStore(remoteRoot),
        now: () => clock,
      ).syncPending(vaultRoot);

      expect(run.failed, 1);
      expect(await Directory('$remoteRoot/packages').exists(), isFalse);
    },
    skip: Platform.isWindows
        ? 'Creating a test symlink requires optional Windows developer privileges.'
        : false,
  );

  test(
    'linked queue state directory is rejected before upload',
    () async {
      await saveFixture();
      final outside = await Directory(
        '${testRoot.path}/outside-state',
      ).create();
      await Link('$vaultRoot/Upload Queue/State').create(outside.path);

      await expectLater(
        LocalSyncService(
          objectStore: LocalFolderObjectStore(remoteRoot),
          now: () => clock,
        ).syncPending(vaultRoot),
        throwsA(isA<LocalRecoveryException>()),
      );
      expect(await Directory('$remoteRoot/packages').exists(), isFalse);
    },
    skip: Platform.isWindows
        ? 'Creating a test symlink requires optional Windows developer privileges.'
        : false,
  );

  test(
    'linked object-store root is rejected without writing outside it',
    () async {
      await saveFixture();
      final outside = await Directory(
        '${testRoot.path}/outside-object-store',
      ).create();
      await Link(remoteRoot).create(outside.path);

      final run = await LocalSyncService(
        objectStore: LocalFolderObjectStore(remoteRoot),
        now: () => clock,
      ).syncPending(vaultRoot);

      expect(run.failed, 1);
      expect(await Directory('${outside.path}/packages').exists(), isFalse);
      expect(await Directory('${outside.path}/.partial').exists(), isFalse);
    },
    skip: Platform.isWindows
        ? 'Creating a test symlink requires optional Windows developer privileges.'
        : false,
  );
}

class _CountingStore implements LocalSyncObjectStore {
  _CountingStore(this.delegate);

  final LocalSyncObjectStore delegate;
  int appendCalls = 0;

  @override
  Future<void> appendChunk(
    String packageId,
    String relativePath,
    int offset,
    List<int> bytes,
  ) {
    appendCalls++;
    return delegate.appendChunk(packageId, relativePath, offset, bytes);
  }

  @override
  Future<LocalSyncReceipt?> committedReceipt(String packageId) =>
      delegate.committedReceipt(packageId);

  @override
  Future<int> uploadedLength(String packageId, String relativePath) =>
      delegate.uploadedLength(packageId, relativePath);

  @override
  Future<LocalSyncReceipt> verifyAndCommit(
    String packageId,
    List<int> remoteManifestBytes,
    List<LocalSyncFileDescriptor> files,
  ) => delegate.verifyAndCommit(packageId, remoteManifestBytes, files);
}

class _InterruptAfterFirstChunkStore extends _CountingStore {
  _InterruptAfterFirstChunkStore(super.delegate);

  @override
  Future<void> appendChunk(
    String packageId,
    String relativePath,
    int offset,
    List<int> bytes,
  ) async {
    await super.appendChunk(packageId, relativePath, offset, bytes);
    if (appendCalls == 1) {
      throw const LocalObjectStoreUnavailableException(
        'Synthetic interruption.',
      );
    }
  }
}

class _UnavailableStore implements LocalSyncObjectStore {
  const _UnavailableStore();

  Never _offline() =>
      throw const LocalObjectStoreUnavailableException('Synthetic offline.');

  @override
  Future<void> appendChunk(
    String packageId,
    String relativePath,
    int offset,
    List<int> bytes,
  ) async => _offline();

  @override
  Future<LocalSyncReceipt?> committedReceipt(String packageId) async =>
      _offline();

  @override
  Future<int> uploadedLength(String packageId, String relativePath) async =>
      _offline();

  @override
  Future<LocalSyncReceipt> verifyAndCommit(
    String packageId,
    List<int> remoteManifestBytes,
    List<LocalSyncFileDescriptor> files,
  ) async => _offline();
}

class _CorruptBeforeCommitStore extends _CountingStore {
  _CorruptBeforeCommitStore(super.delegate, this.rootPath);

  final String rootPath;

  @override
  Future<LocalSyncReceipt> verifyAndCommit(
    String packageId,
    List<int> remoteManifestBytes,
    List<LocalSyncFileDescriptor> files,
  ) async {
    final target = File(
      '$rootPath/.partial/$packageId/content/${files.first.relativePath}',
    );
    await target.writeAsString('remote-corruption');
    return super.verifyAndCommit(packageId, remoteManifestBytes, files);
  }
}
