import 'dart:io';

import 'package:flutter_riverpod/flutter_riverpod.dart';

final localCatalogScannerProvider = Provider<LocalCatalogScanner>(
  (ref) => LocalCatalogScanner(),
);

class LocalCatalogScanner {
  LocalCatalogScanner({
    DesktopPlatform? platform,
    Map<String, String>? environment,
    Future<FileSystemEntityType> Function(String)? readType,
  }) : _platform = platform ?? _hostPlatform,
       _environment = environment ?? Platform.environment,
       _readType = readType ?? FileSystemEntity.type;

  final DesktopPlatform _platform;
  final Map<String, String> _environment;
  final Future<FileSystemEntityType> Function(String) _readType;

  static DesktopPlatform get _hostPlatform => Platform.isMacOS
      ? DesktopPlatform.macOs
      : Platform.isWindows
      ? DesktopPlatform.windows
      : DesktopPlatform.unsupported;

  Future<List<String>> scan() async {
    final home = _environment['HOME'];
    final programFiles = _environment['ProgramFiles'];
    final userProfile = _environment['USERPROFILE'];
    final candidates = <(String, String)>[
      if (_platform == DesktopPlatform.macOs) ...[
        (
          'macos.resolume-arena.application',
          '/Applications/Resolume Arena/Arena.app',
        ),
        if (home != null)
          ('macos.resolume-arena.user-data', '$home/Documents/Resolume Arena'),
        ('macos.serato-dj-pro.application', '/Applications/Serato DJ Pro.app'),
        if (home != null)
          ('macos.serato-dj-pro.user-data', '$home/Music/_Serato_'),
      ],
      if (_platform == DesktopPlatform.windows && programFiles != null) ...[
        ('windows.resolume-arena.application', '$programFiles\\Resolume Arena'),
        (
          'windows.serato-dj-pro.application',
          '$programFiles\\Serato\\Serato DJ Pro\\Serato DJ Pro.exe',
        ),
      ],
      if (_platform == DesktopPlatform.windows && userProfile != null) ...[
        (
          'windows.resolume-arena.user-data',
          '$userProfile\\Documents\\Resolume Arena',
        ),
        ('windows.serato-dj-pro.user-data', '$userProfile\\Music\\_Serato_'),
      ],
    ];

    final detected = <String>[];
    for (final candidate in candidates) {
      if (await _readType(candidate.$2) != FileSystemEntityType.notFound) {
        detected.add(candidate.$1);
      }
    }
    return detected;
  }
}

enum DesktopPlatform { macOs, windows, unsupported }
