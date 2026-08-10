import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/recovery/local_recovery_service.dart';
import 'package:showvault_app/src/scanning/local_catalog_scanner.dart';

void main() {
  late Directory testRoot;
  late Directory sourceRoot;
  late String vaultRoot;
  const source = LocalBackupSource(
    candidateKey: 'macos.serato-dj-pro.user-data',
    pluginId: 'showvault.serato-dj-pro',
    productName: 'Serato DJ Pro',
    rootPath: '',
  );

  setUp(() async {
    testRoot = await Directory.systemTemp.createTemp('showvault-local-save-');
    sourceRoot = await Directory('${testRoot.path}/source').create();
    vaultRoot = '${testRoot.path}/vault';
  });

  tearDown(() async {
    if (await testRoot.exists()) await testRoot.delete(recursive: true);
  });

  LocalBackupSource fixtureSource() => LocalBackupSource(
    candidateKey: source.candidateKey,
    pluginId: source.pluginId,
    productName: source.productName,
    rootPath: sourceRoot.path,
  );

  test(
    'Save creates, verifies, and durably queues an immutable recovery point',
    () async {
      await Directory('${sourceRoot.path}/Subcrates').create();
      await File('${sourceRoot.path}/database V2').writeAsString('library');
      await File(
        '${sourceRoot.path}/Subcrates/venue.crate',
      ).writeAsString('crate');
      final now = DateTime.utc(2026, 8, 10, 21, 15);
      final service = LocalRecoveryService(
        vaultRoot: vaultRoot,
        now: () => now,
      );

      final result = await service.save(fixtureSource());

      expect(result.localStatus, LocalProtectionStatus.verified);
      expect(result.cloudStatus, LocalCloudSyncStatus.queued);
      expect(result.fileCount, 2);
      expect(result.recoveryPointPath, contains('Serato DJ Pro'));
      expect(result.recoveryPointPath, contains('2026-08-10T21-15-00Z__'));
      expect(
        await File(
          '${result.recoveryPointPath}/content/database V2',
        ).readAsString(),
        'library',
      );
      expect(
        await File(
          '${result.recoveryPointPath}/content/Subcrates/venue.crate',
        ).readAsString(),
        'crate',
      );
      expect(
        await File('${result.recoveryPointPath}/summary.txt').exists(),
        isTrue,
      );

      final manifestFile = File(
        '$vaultRoot/Manifests/${result.recoveryPointId}.json',
      );
      final manifest =
          jsonDecode(await manifestFile.readAsString()) as Map<String, Object?>;
      expect(manifest['localProtectionStatus'], 'verified');
      expect(manifest['cloudSyncStatus'], 'pending');
      expect(
        ((manifest['source']! as Map<String, Object?>)['identity']! as String),
        await sourceRoot.resolveSymbolicLinks(),
      );
      final files = manifest['files']! as List<Object?>;
      expect(
        files.map((entry) => (entry! as Map<String, Object?>)['relativePath']),
        ['Subcrates/venue.crate', 'database V2'],
      );

      final queueFile = File(
        '$vaultRoot/Upload Queue/${result.recoveryPointId}.json',
      );
      final queue =
          jsonDecode(await queueFile.readAsString()) as Map<String, Object?>;
      expect(queue['status'], 'queued');
      expect(queue['attemptCount'], 0);
      expect(queue.containsKey('sourcePath'), isFalse);
      expect(
        Directory(
          '$vaultRoot/Backups/Serato DJ Pro',
        ).listSync().where((entry) => entry.path.contains('.staging-')),
        isEmpty,
      );
    },
  );

  test(
    'Save rejects links and publishes no recovery point',
    () async {
      final outside = File('${testRoot.path}/outside.txt');
      await outside.writeAsString('outside');
      await Link('${sourceRoot.path}/escape').create(outside.path);

      await expectLater(
        LocalRecoveryService(vaultRoot: vaultRoot).save(fixtureSource()),
        throwsA(isA<LocalRecoveryException>()),
      );

      expect(await Directory('$vaultRoot/Backups').exists(), isFalse);
    },
    skip: Platform.isWindows
        ? 'Creating a test symlink requires optional Windows developer privileges.'
        : false,
  );

  test('Save detects a source mutation and removes staging', () async {
    final mutable = File('${sourceRoot.path}/mutable.txt');
    await mutable.writeAsString('original');
    final service = LocalRecoveryService(
      vaultRoot: vaultRoot,
      onFileCopied: (path) async =>
          File(path).writeAsString('changed-and-longer'),
    );

    await expectLater(
      service.save(fixtureSource()),
      throwsA(
        isA<LocalRecoveryException>().having(
          (error) => error.message,
          'message',
          contains('changed during backup'),
        ),
      ),
    );

    expect(Directory('$vaultRoot/Backups/Serato DJ Pro').listSync(), isEmpty);
    expect(Directory('$vaultRoot/Upload Queue').listSync(), isEmpty);
  });

  test('Save rejects an empty source without reporting protection', () async {
    await expectLater(
      LocalRecoveryService(vaultRoot: vaultRoot).save(fixtureSource()),
      throwsA(
        isA<LocalRecoveryException>().having(
          (error) => error.message,
          'message',
          contains('no regular files'),
        ),
      ),
    );
    expect(await Directory(vaultRoot).exists(), isFalse);
  });

  test('Save honors cancellation before reading file contents', () async {
    await File('${sourceRoot.path}/library').writeAsString('content');
    final cancellation = LocalBackupCancellation()..cancel();

    await expectLater(
      LocalRecoveryService(
        vaultRoot: vaultRoot,
      ).save(fixtureSource(), cancellation: cancellation),
      throwsA(isA<LocalRecoveryException>()),
    );
    expect(await Directory(vaultRoot).exists(), isFalse);
  });

  test('Save enforces file-count and byte limits before publication', () async {
    await File('${sourceRoot.path}/one').writeAsString('1234');
    await File('${sourceRoot.path}/two').writeAsString('5678');

    await expectLater(
      LocalRecoveryService(
        vaultRoot: vaultRoot,
        maxFiles: 1,
      ).save(fixtureSource()),
      throwsA(isA<LocalRecoveryException>()),
    );
    expect(await Directory(vaultRoot).exists(), isFalse);

    await expectLater(
      LocalRecoveryService(
        vaultRoot: vaultRoot,
        maxFiles: 10,
        maxFileBytes: 3,
      ).save(fixtureSource()),
      throwsA(isA<LocalRecoveryException>()),
    );
    expect(await Directory(vaultRoot).exists(), isFalse);
  });

  test('Save enforces its deadline before reading file contents', () async {
    await File('${sourceRoot.path}/library').writeAsString('content');
    var clockReads = 0;
    final base = DateTime.utc(2026, 8, 10);
    DateTime advancingClock() => base.add(Duration(minutes: clockReads++));

    await expectLater(
      LocalRecoveryService(
        vaultRoot: vaultRoot,
        now: advancingClock,
        timeout: const Duration(seconds: 30),
      ).save(fixtureSource()),
      throwsA(
        isA<LocalRecoveryException>().having(
          (error) => error.message,
          'message',
          contains('timed out'),
        ),
      ),
    );
    expect(await Directory(vaultRoot).exists(), isFalse);
  });
}
