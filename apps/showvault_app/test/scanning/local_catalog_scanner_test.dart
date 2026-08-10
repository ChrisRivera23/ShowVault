import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/scanning/local_catalog_scanner.dart';

void main() {
  test('macOS scan checks only exact approved catalog candidates', () async {
    final attempted = <String>[];
    final scanner = LocalCatalogScanner(
      platform: DesktopPlatform.macOs,
      environment: const {'HOME': '/Users/tester'},
      readType: (path) async {
        attempted.add(path);
        return path == '/Applications/Resolume Arena/Arena.app' ||
                path == '/Applications/Serato DJ Pro.app'
            ? FileSystemEntityType.directory
            : FileSystemEntityType.notFound;
      },
    );

    expect(await scanner.scan(), [
      'macos.resolume-arena.application',
      'macos.serato-dj-pro.application',
    ]);
    expect(attempted, [
      '/Applications/Resolume Arena/Arena.app',
      '/Users/tester/Documents/Resolume Arena',
      '/Applications/Serato DJ Pro.app',
      '/Users/tester/Music/_Serato_',
    ]);
  });

  test('unsupported platforms do not inspect the filesystem', () async {
    var attempted = false;
    final scanner = LocalCatalogScanner(
      platform: DesktopPlatform.unsupported,
      environment: const {},
      readType: (path) async {
        attempted = true;
        return FileSystemEntityType.notFound;
      },
    );

    expect(await scanner.scan(), isEmpty);
    expect(attempted, isFalse);
  });

  test('backup path resolves only for exact UserDataRoot catalog keys', () {
    final scanner = LocalCatalogScanner(
      platform: DesktopPlatform.macOs,
      environment: const {'HOME': '/Users/tester'},
    );

    final source = scanner.resolveBackupSource('macos.serato-dj-pro.user-data');
    expect(source, isNotNull);
    expect(source!.rootPath, '/Users/tester/Music/_Serato_');
    expect(source.productName, 'Serato DJ Pro');
    expect(
      scanner.resolveBackupSource('macos.serato-dj-pro.application'),
      isNull,
    );
    expect(scanner.resolveBackupSource('macos.unknown.user-data'), isNull);
  });

  test('Windows scan checks only exact approved catalog candidates', () async {
    final attempted = <String>[];
    final scanner = LocalCatalogScanner(
      platform: DesktopPlatform.windows,
      environment: const {
        'ProgramFiles': r'C:\Program Files',
        'USERPROFILE': r'C:\Users\Operator',
      },
      readType: (path) async {
        attempted.add(path);
        return path ==
                    r'C:\Program Files\Serato\Serato DJ Pro\Serato DJ Pro.exe' ||
                path == r'C:\Users\Operator\Music\_Serato_'
            ? FileSystemEntityType.file
            : FileSystemEntityType.notFound;
      },
    );

    expect(await scanner.scan(), [
      'windows.serato-dj-pro.application',
      'windows.serato-dj-pro.user-data',
    ]);
    expect(attempted, [
      r'C:\Program Files\Resolume Arena',
      r'C:\Program Files\Serato\Serato DJ Pro\Serato DJ Pro.exe',
      r'C:\Users\Operator\Documents\Resolume Arena',
      r'C:\Users\Operator\Music\_Serato_',
    ]);
  });

  test('Windows backup resolves only an exact local user-data key', () {
    final scanner = LocalCatalogScanner(
      platform: DesktopPlatform.windows,
      environment: const {'USERPROFILE': r'C:\Users\Operator'},
    );

    final source = scanner.resolveBackupSource(
      'windows.serato-dj-pro.user-data',
    );
    expect(source, isNotNull);
    expect(source!.rootPath, r'C:\Users\Operator\Music\_Serato_');
    expect(
      scanner.resolveBackupSource('windows.serato-dj-pro.application'),
      isNull,
    );
  });
}
