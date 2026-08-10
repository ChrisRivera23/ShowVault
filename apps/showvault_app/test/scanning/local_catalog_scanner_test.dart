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
}
