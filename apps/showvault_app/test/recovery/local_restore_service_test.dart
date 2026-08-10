import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/recovery/local_recovery_service.dart';
import 'package:showvault_app/src/recovery/local_restore_service.dart';
import 'package:showvault_app/src/scanning/local_catalog_scanner.dart';

void main() {
  late Directory testRoot;
  late Directory sourceRoot;
  late String vaultRoot;
  late DateTime clock;

  setUp(() async {
    testRoot = await Directory.systemTemp.createTemp('showvault-restore-');
    sourceRoot = await Directory('${testRoot.path}/source').create();
    vaultRoot = '${testRoot.path}/vault';
    clock = DateTime.utc(2026, 8, 10, 20);
  });

  tearDown(() async {
    if (await testRoot.exists()) await testRoot.delete(recursive: true);
  });

  Future<LocalBackupResult> saveFixture() async {
    await Directory('${sourceRoot.path}/Subcrates').create();
    await File('${sourceRoot.path}/database V2').writeAsString('library');
    await File(
      '${sourceRoot.path}/Subcrates/test.crate',
    ).writeAsString('crate');
    return LocalRecoveryService(vaultRoot: vaultRoot, now: () => clock).save(
      LocalBackupSource(
        candidateKey: 'macos.serato-dj-pro.user-data',
        pluginId: 'showvault.serato-dj-pro',
        productName: 'Serato DJ Pro',
        rootPath: sourceRoot.path,
      ),
    );
  }

  test('restores verified bytes atomically into an absent target', () async {
    final saved = await saveFixture();
    final target = '${testRoot.path}/restored';
    final result = await LocalRestoreService(now: () => clock).restore(
      authorizedVaultRoot: vaultRoot,
      recoveryPointId: saved.recoveryPointId,
      targetPath: target,
    );

    expect(result.restoredFileCount, 2);
    expect(result.restoredBytes, 12);
    expect(await File('$target/database V2').readAsString(), 'library');
    expect(await File('$target/Subcrates/test.crate').readAsString(), 'crate');
    final evidence = await File(result.evidencePath).readAsString();
    expect(evidence, isNot(contains(target)));
    expect(evidence, isNot(contains(sourceRoot.path)));
    expect(evidence, contains(saved.recoveryPointId));
    expect(await Directory(saved.recoveryPointPath).exists(), isTrue);
    expect(
      testRoot.listSync().where(
        (entry) => entry.path.contains('.showvault-restore-'),
      ),
      isEmpty,
    );
  });

  test('replaces only an existing empty selected directory', () async {
    final saved = await saveFixture();
    final target = await Directory('${testRoot.path}/empty-target').create();

    await LocalRestoreService(now: () => clock).restore(
      authorizedVaultRoot: vaultRoot,
      recoveryPointId: saved.recoveryPointId,
      targetPath: target.path,
    );

    expect(await File('${target.path}/database V2').readAsString(), 'library');
  });

  test('rejects a non-empty target before creating staging', () async {
    final saved = await saveFixture();
    final target = await Directory('${testRoot.path}/occupied').create();
    await File('${target.path}/keep').writeAsString('keep');

    await expectLater(
      LocalRestoreService(now: () => clock).restore(
        authorizedVaultRoot: vaultRoot,
        recoveryPointId: saved.recoveryPointId,
        targetPath: target.path,
      ),
      throwsA(
        isA<LocalRestoreException>().having(
          (error) => error.message,
          'message',
          contains('must be empty'),
        ),
      ),
    );

    expect(await File('${target.path}/keep').readAsString(), 'keep');
    expect(
      testRoot.listSync().where(
        (entry) => entry.path.contains('.showvault-restore-'),
      ),
      isEmpty,
    );
  });

  test('tampered package is rejected before target mutation', () async {
    final saved = await saveFixture();
    await File(
      '${saved.recoveryPointPath}/content/database V2',
    ).writeAsString('tampered');
    final target = await Directory('${testRoot.path}/empty-target').create();

    await expectLater(
      LocalRestoreService(now: () => clock).restore(
        authorizedVaultRoot: vaultRoot,
        recoveryPointId: saved.recoveryPointId,
        targetPath: target.path,
      ),
      throwsA(isA<LocalRestoreException>()),
    );

    expect(await target.list().isEmpty, isTrue);
  });

  test('cancellation cleans staging and preserves the empty target', () async {
    final saved = await saveFixture();
    final target = await Directory('${testRoot.path}/empty-target').create();
    final cancellation = LocalRestoreCancellation();
    final service = LocalRestoreService(
      now: () => clock,
      onFileCopied: (_) async => cancellation.cancel(),
    );

    await expectLater(
      service.restore(
        authorizedVaultRoot: vaultRoot,
        recoveryPointId: saved.recoveryPointId,
        targetPath: target.path,
        cancellation: cancellation,
      ),
      throwsA(
        isA<LocalRestoreException>().having(
          (error) => error.message,
          'message',
          contains('cancelled'),
        ),
      ),
    );

    expect(await target.exists(), isTrue);
    expect(await target.list().isEmpty, isTrue);
    expect(
      testRoot.listSync().where(
        (entry) => entry.path.contains('.showvault-restore-'),
      ),
      isEmpty,
    );
  });

  test('package mutation during copying publishes no target', () async {
    final saved = await saveFixture();
    final target = '${testRoot.path}/restored';
    final service = LocalRestoreService(
      now: () => clock,
      onFileCopied: (relativePath) async {
        if (relativePath == 'Subcrates/test.crate') {
          await File(
            '${saved.recoveryPointPath}/content/Subcrates/test.crate',
          ).writeAsString('mutated-after-copy');
        }
      },
    );

    await expectLater(
      service.restore(
        authorizedVaultRoot: vaultRoot,
        recoveryPointId: saved.recoveryPointId,
        targetPath: target,
      ),
      throwsA(isA<LocalRestoreException>()),
    );

    expect(await Directory(target).exists(), isFalse);
  });

  test('owned interrupted staging is cleaned before a new restore', () async {
    final saved = await saveFixture();
    const targetName = 'restored';
    final suffix = sha256
        .convert(utf8.encode(targetName))
        .toString()
        .substring(0, 8);
    final stage = await Directory(
      '${testRoot.path}/.showvault-restore-'
      '${saved.recoveryPointId.substring(0, 16)}-$suffix',
    ).create();
    await File('${stage.path}/intent.json').writeAsString(
      jsonEncode({
        'formatVersion': 'showvault.restore-intent.v1',
        'packageId': saved.recoveryPointId,
        'targetName': targetName,
      }),
    );
    await File('${stage.path}/partial').writeAsString('partial');

    await LocalRestoreService(now: () => clock).restore(
      authorizedVaultRoot: vaultRoot,
      recoveryPointId: saved.recoveryPointId,
      targetPath: '${testRoot.path}/$targetName',
    );

    expect(
      await File('${testRoot.path}/$targetName/database V2').exists(),
      isTrue,
    );
    expect(await stage.exists(), isFalse);
  });

  test('unowned interrupted staging is refused and preserved', () async {
    final saved = await saveFixture();
    const targetName = 'restored';
    final suffix = sha256
        .convert(utf8.encode(targetName))
        .toString()
        .substring(0, 8);
    final stage = await Directory(
      '${testRoot.path}/.showvault-restore-'
      '${saved.recoveryPointId.substring(0, 16)}-$suffix',
    ).create();
    final keep = await File('${stage.path}/keep').writeAsString('keep');

    await expectLater(
      LocalRestoreService(now: () => clock).restore(
        authorizedVaultRoot: vaultRoot,
        recoveryPointId: saved.recoveryPointId,
        targetPath: '${testRoot.path}/$targetName',
      ),
      throwsA(
        isA<LocalRestoreException>().having(
          (error) => error.message,
          'message',
          contains('ownership marker'),
        ),
      ),
    );

    expect(await keep.readAsString(), 'keep');
    expect(await Directory('${testRoot.path}/$targetName').exists(), isFalse);
  });

  test('unsafe evidence location is rejected before target mutation', () async {
    final saved = await saveFixture();
    await File('$vaultRoot/Reports/Restores').writeAsString('unsafe');
    final target = await Directory('${testRoot.path}/empty-target').create();

    await expectLater(
      LocalRestoreService(now: () => clock).restore(
        authorizedVaultRoot: vaultRoot,
        recoveryPointId: saved.recoveryPointId,
        targetPath: target.path,
      ),
      throwsA(
        isA<LocalRestoreException>().having(
          (error) => error.message,
          'message',
          contains('evidence location is unsafe'),
        ),
      ),
    );

    expect(await target.exists(), isTrue);
    expect(await target.list().isEmpty, isTrue);
  });

  test(
    'linked target is rejected without following it',
    () async {
      final saved = await saveFixture();
      final outside = await Directory('${testRoot.path}/outside').create();
      final target = Link('${testRoot.path}/linked-target');
      await target.create(outside.path);

      await expectLater(
        LocalRestoreService(now: () => clock).restore(
          authorizedVaultRoot: vaultRoot,
          recoveryPointId: saved.recoveryPointId,
          targetPath: target.path,
        ),
        throwsA(isA<LocalRestoreException>()),
      );
      expect(await outside.list().isEmpty, isTrue);
    },
    skip: Platform.isWindows
        ? 'Creating a test symlink requires optional Windows developer privileges.'
        : false,
  );

  test('target inside the vault is rejected', () async {
    final saved = await saveFixture();
    final target = await Directory('$vaultRoot/restore-target').create();

    await expectLater(
      LocalRestoreService(now: () => clock).restore(
        authorizedVaultRoot: vaultRoot,
        recoveryPointId: saved.recoveryPointId,
        targetPath: target.path,
      ),
      throwsA(isA<LocalRestoreException>()),
    );
    expect(await target.list().isEmpty, isTrue);
  });
}
