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
        return path == '/Applications/Resolume Arena/Arena.app'
            ? FileSystemEntityType.directory
            : FileSystemEntityType.notFound;
      },
    );

    expect(await scanner.scan(), ['macos.resolume-arena.application']);
    expect(attempted, [
      '/Applications/Resolume Arena/Arena.app',
      '/Applications/Serato DJ Pro.app',
      '/Users/tester/Documents/Resolume Arena',
      '/Users/tester/Music/_Serato_',
    ]);
  });

  test('synthetic home suppresses real application candidates', () async {
    final attempted = <String>[];
    final scanner = LocalCatalogScanner(
      platform: DesktopPlatform.macOs,
      environment: const {'HOME': '/Users/real-person'},
      syntheticHome: '/synthetic/home',
      readType: (path) async {
        attempted.add(path);
        return FileSystemEntityType.notFound;
      },
    );

    expect(await scanner.scan(), isEmpty);
    expect(attempted, [
      '/synthetic/home/Documents/Resolume Arena',
      '/synthetic/home/Music/_Serato_',
    ]);
    expect(
      attempted,
      isNot(contains('/Applications/Resolume Arena/Arena.app')),
    );
  });

  test('marks only closed user-data candidates as saveable', () async {
    final scanner = LocalCatalogScanner(
      platform: DesktopPlatform.macOs,
      environment: const {},
      syntheticHome: '/synthetic/home',
      readType: (path) async => path.endsWith('Music/_Serato_')
          ? FileSystemEntityType.directory
          : FileSystemEntityType.notFound,
    );

    final finding = (await scanner.scanFindings()).single;

    expect(finding.candidateKey, 'macos.serato-dj-pro.user-data');
    expect(finding.type, LocalCatalogFindingType.userDataRoot);
    expect(finding.canSave, isTrue);
    expect(finding.expectedPath, '/synthetic/home/Music/_Serato_');
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
