import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/recovery/local_access_coordinator.dart';
import 'package:showvault_app/src/recovery/local_recovery_service.dart';
import 'package:showvault_app/src/scanning/local_catalog_scanner.dart';

void main() {
  late Directory testRoot;
  late Directory expectedRoot;

  setUp(() async {
    testRoot = await Directory.systemTemp.createTemp('showvault-access-');
    expectedRoot = await Directory('${testRoot.path}/approved').create();
  });

  tearDown(() async {
    if (await testRoot.exists()) await testRoot.delete(recursive: true);
  });

  LocalBackupSource source() => LocalBackupSource(
    candidateKey: 'macos.serato-dj-pro.user-data',
    pluginId: 'showvault.serato-dj-pro',
    productName: 'Serato DJ Pro',
    rootPath: expectedRoot.path,
  );

  test(
    'source permission accepts only the exact canonical catalog root',
    () async {
      String? pickedInitialDirectory;
      String? pickedButtonText;
      bool? pickedCanCreate;
      final coordinator = LocalAccessCoordinator(
        directoryPicker:
            ({
              String? initialDirectory,
              String? confirmButtonText,
              bool? canCreateDirectories,
            }) async {
              pickedInitialDirectory = initialDirectory;
              pickedButtonText = confirmButtonText;
              pickedCanCreate = canCreateDirectories;
              return expectedRoot.path;
            },
      );

      final authorized = await coordinator.authorizeSource(source());

      expect(authorized.rootPath, await expectedRoot.resolveSymbolicLinks());
      expect(pickedInitialDirectory, expectedRoot.path);
      expect(pickedButtonText, 'Allow this source');
      expect(pickedCanCreate, isFalse);
    },
  );

  test('source permission rejects a substituted directory', () async {
    final other = await Directory('${testRoot.path}/other').create();
    final coordinator = LocalAccessCoordinator(
      directoryPicker:
          ({initialDirectory, confirmButtonText, canCreateDirectories}) async =>
              other.path,
    );

    await expectLater(
      coordinator.authorizeSource(source()),
      throwsA(
        isA<LocalRecoveryException>().having(
          (error) => error.message,
          'message',
          contains('does not match'),
        ),
      ),
    );
  });

  test('vault permission returns the selected canonical directory', () async {
    final vault = await Directory('${testRoot.path}/ShowVault Pro').create();
    final coordinator = LocalAccessCoordinator(
      directoryPicker:
          ({initialDirectory, confirmButtonText, canCreateDirectories}) async {
            expect(confirmButtonText, 'Use this vault');
            expect(canCreateDirectories, isTrue);
            return vault.path;
          },
      environment: {'HOME': testRoot.path},
      windows: false,
    );

    expect(
      await coordinator.authorizeVault(),
      await vault.resolveSymbolicLinks(),
    );
    expect(
      coordinator.defaultVaultInitialDirectory,
      '${testRoot.path}${Platform.pathSeparator}Documents',
    );
  });

  test('cancelled native selection reads no source', () async {
    var calls = 0;
    final coordinator = LocalAccessCoordinator(
      directoryPicker:
          ({initialDirectory, confirmButtonText, canCreateDirectories}) async {
            calls++;
            return null;
          },
    );

    await expectLater(
      coordinator.authorizeSource(source()),
      throwsA(isA<LocalAccessCancelledException>()),
    );
    expect(calls, 1);
  });

  test('restore permission accepts only an empty selected directory', () async {
    final target = await Directory('${testRoot.path}/restore-target').create();
    final coordinator = LocalAccessCoordinator(
      directoryPicker:
          ({initialDirectory, confirmButtonText, canCreateDirectories}) async {
            expect(confirmButtonText, 'Use empty restore folder');
            expect(canCreateDirectories, isTrue);
            return target.path;
          },
    );

    expect(
      await coordinator.authorizeEmptyRestoreTarget(),
      await target.resolveSymbolicLinks(),
    );
  });

  test('restore permission rejects a non-empty directory', () async {
    final target = await Directory('${testRoot.path}/restore-target').create();
    await File('${target.path}/existing').writeAsString('occupied');
    final coordinator = LocalAccessCoordinator(
      directoryPicker:
          ({initialDirectory, confirmButtonText, canCreateDirectories}) async =>
              target.path,
    );

    await expectLater(
      coordinator.authorizeEmptyRestoreTarget(),
      throwsA(isA<LocalRecoveryException>()),
    );
  });

  test(
    'restore permission permits only an interrupted staging directory',
    () async {
      final target = await Directory(
        '${testRoot.path}/restore-target',
      ).create();
      await Directory(
        '${target.path}/.showvault-restore-0123456789abcdef-01234567',
      ).create();
      final coordinator = LocalAccessCoordinator(
        directoryPicker:
            ({
              initialDirectory,
              confirmButtonText,
              canCreateDirectories,
            }) async => target.path,
      );

      expect(
        await coordinator.authorizeEmptyRestoreTarget(),
        await target.resolveSymbolicLinks(),
      );
    },
  );
}
