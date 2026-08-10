import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/recovery/local_recovery_service.dart';
import 'package:showvault_app/src/recovery/local_restore_service.dart';
import 'package:showvault_app/src/recovery/local_support_diagnostic_service.dart';
import 'package:showvault_app/src/recovery/local_sync_object_store.dart';
import 'package:showvault_app/src/recovery/local_sync_service.dart';
import 'package:showvault_app/src/scanning/local_catalog_scanner.dart';

void main() {
  late Directory testRoot;
  late Directory sourceRoot;
  late String vaultRoot;
  late DateTime clock;

  setUp(() async {
    testRoot = await Directory.systemTemp.createTemp('showvault-diagnostic-');
    sourceRoot = await Directory('${testRoot.path}/source').create();
    vaultRoot = '${testRoot.path}/vault';
    clock = DateTime.utc(2026, 8, 10, 22);
    await Directory('${sourceRoot.path}/Subcrates').create();
    await File(
      '${sourceRoot.path}/database V2',
    ).writeAsString('private-library-content');
    await File(
      '${sourceRoot.path}/Subcrates/test.crate',
    ).writeAsString('private-crate-content');
  });

  tearDown(() async {
    if (await testRoot.exists()) await testRoot.delete(recursive: true);
  });

  Future<LocalBackupResult> saveFixture() =>
      LocalRecoveryService(vaultRoot: vaultRoot, now: () => clock).save(
        LocalBackupSource(
          candidateKey: 'macos.serato-dj-pro.user-data',
          pluginId: 'showvault.serato-dj-pro',
          productName: 'Serato DJ Pro',
          rootPath: sourceRoot.path,
        ),
      );

  test('writes a checksummed path-free bounded workflow summary', () async {
    final saved = await saveFixture();
    final firstRun = await LocalSyncService(
      objectStore: const _UnavailableStore(),
      now: () => clock,
    ).syncPending(vaultRoot);
    expect(firstRun.retriedLater, 1);

    clock = clock.add(const Duration(seconds: 31));
    final secondRun = await LocalSyncService(
      objectStore: LocalFolderObjectStore('${testRoot.path}/object-store'),
      now: () => clock,
    ).syncPending(vaultRoot);
    expect(secondRun.synchronized, 1);

    final restore = await LocalRestoreService(now: () => clock).restore(
      authorizedVaultRoot: vaultRoot,
      recoveryPointId: saved.recoveryPointId,
      targetPath: '${testRoot.path}/restored',
    );
    final result = await LocalSupportDiagnosticService(
      now: () => clock,
      appVersion: 'test-upgrade-after',
    ).generate(vaultRoot);

    final text = await File(result.diagnosticPath).readAsString();
    final report = jsonDecode(text) as Map<String, Object?>;
    final packages = report['packages']! as List<Object?>;
    final package = packages.single as Map<String, Object?>;
    expect(
      report['formatVersion'],
      LocalSupportDiagnosticService.formatVersion,
    );
    expect(report['appVersion'], 'test-upgrade-after');
    expect(report['recoveryPointCount'], 1);
    expect(report['restoreEvidenceCount'], 1);
    expect(package['packageId'], saved.recoveryPointId);
    expect(package['cloudStatus'], 'synchronized');
    expect(package['queueAttemptCount'], 2);
    expect(package['queueStateEventCount'], 4);
    expect(package['errorCategory'], 'none');
    expect(report['integrity'], containsPair('packageContents', 'not-read'));
    expect(report['integrity'], containsPair('recoverySources', 'not-read'));

    final expectedHash = report.remove('evidenceSha256');
    expect(
      expectedHash,
      sha256.convert(utf8.encode(jsonEncode(report))).toString(),
    );
    expect(result.evidenceSha256, expectedHash);
    final checksum = await File(result.checksumPath).readAsString();
    expect(
      checksum,
      '$expectedHash  ${File(result.diagnosticPath).uri.pathSegments.last}\n',
    );
    for (final privateValue in [
      testRoot.path,
      sourceRoot.path,
      restore.evidencePath,
      'private-library-content',
      'private-crate-content',
      'Synthetic offline.',
    ]) {
      expect(text, isNot(contains(privateValue)));
    }
  });

  test('rejects an oversized queue record before writing a report', () async {
    final saved = await saveFixture();
    await File(
      '$vaultRoot/Upload Queue/${saved.recoveryPointId}.json',
    ).writeAsString(List.filled(64 * 1024 + 1, 'x').join());

    await expectLater(
      LocalSupportDiagnosticService(now: () => clock).generate(vaultRoot),
      throwsA(isA<LocalSupportDiagnosticException>()),
    );
    expect(await Directory('$vaultRoot/Reports/Diagnostics').exists(), isFalse);
  });

  test('rejects substituted restore evidence fields', () async {
    final saved = await saveFixture();
    final restore = await LocalRestoreService(now: () => clock).restore(
      authorizedVaultRoot: vaultRoot,
      recoveryPointId: saved.recoveryPointId,
      targetPath: '${testRoot.path}/restored',
    );
    final evidence =
        jsonDecode(await File(restore.evidencePath).readAsString())
            as Map<String, Object?>;
    evidence['localPath'] = sourceRoot.path;
    await File(restore.evidencePath).writeAsString(jsonEncode(evidence));

    await expectLater(
      LocalSupportDiagnosticService(now: () => clock).generate(vaultRoot),
      throwsA(isA<LocalSupportDiagnosticException>()),
    );
    expect(await Directory('$vaultRoot/Reports/Diagnostics').exists(), isFalse);
  });

  test('rejects a malformed earlier append-only queue event', () async {
    final saved = await saveFixture();
    await LocalSyncService(
      objectStore: const _UnavailableStore(),
      now: () => clock,
    ).syncPending(vaultRoot);
    await File(
      '$vaultRoot/Upload Queue/State/${saved.recoveryPointId}/00000001.json',
    ).writeAsString('{malformed');

    await expectLater(
      LocalSupportDiagnosticService(now: () => clock).generate(vaultRoot),
      throwsA(isA<LocalSupportDiagnosticException>()),
    );
    expect(await Directory('$vaultRoot/Reports/Diagnostics').exists(), isFalse);
  });

  test(
    'rejects a linked queue record without reading its outside target',
    () async {
      final saved = await saveFixture();
      final queue = File(
        '$vaultRoot/Upload Queue/${saved.recoveryPointId}.json',
      );
      final outside = File('${testRoot.path}/outside-queue.json');
      await outside.writeAsString('{outside-malformed');
      await queue.delete();
      await Link(queue.path).create(outside.path);

      await expectLater(
        LocalSupportDiagnosticService(now: () => clock).generate(vaultRoot),
        throwsA(isA<LocalSupportDiagnosticException>()),
      );
      expect(await outside.readAsString(), '{outside-malformed');
      expect(
        await Directory('$vaultRoot/Reports/Diagnostics').exists(),
        isFalse,
      );
    },
    skip: Platform.isWindows
        ? 'Creating a test symlink requires optional Windows developer privileges.'
        : false,
  );

  test(
    'rejects a linked diagnostics location without writing outside the vault',
    () async {
      await saveFixture();
      final outside = await Directory('${testRoot.path}/outside').create();
      await Link('$vaultRoot/Reports/Diagnostics').create(outside.path);

      await expectLater(
        LocalSupportDiagnosticService(now: () => clock).generate(vaultRoot),
        throwsA(isA<LocalSupportDiagnosticException>()),
      );
      expect(await outside.list().isEmpty, isTrue);
    },
    skip: Platform.isWindows
        ? 'Creating a test symlink requires optional Windows developer privileges.'
        : false,
  );
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
