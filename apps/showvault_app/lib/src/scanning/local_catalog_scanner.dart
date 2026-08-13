import 'dart:io';

import 'package:flutter_riverpod/flutter_riverpod.dart';

final localCatalogScannerProvider = Provider<LocalCatalogScanner>(
  (ref) => LocalCatalogScanner(),
);

class LocalCatalogScanner {
  LocalCatalogScanner({
    DesktopPlatform? platform,
    Map<String, String>? environment,
    String? syntheticHome,
    Future<FileSystemEntityType> Function(String)? readType,
  }) : _platform = platform ?? _hostPlatform,
       _environment = environment ?? Platform.environment,
       _syntheticHome = syntheticHome,
       _readType = readType ?? FileSystemEntity.type;

  final DesktopPlatform _platform;
  final Map<String, String> _environment;
  final String? _syntheticHome;
  final Future<FileSystemEntityType> Function(String) _readType;

  static DesktopPlatform get _hostPlatform => Platform.isMacOS
      ? DesktopPlatform.macOs
      : Platform.isWindows
      ? DesktopPlatform.windows
      : DesktopPlatform.unsupported;

  Future<List<String>> scan() async {
    final synthetic = _syntheticHome != null;
    final home = _syntheticHome ?? _environment['HOME'];
    final userProfile = _syntheticHome ?? _environment['USERPROFILE'];
    final programFiles = synthetic ? null : _environment['ProgramFiles'];
    final candidates = <(String, String)>[
      if (_platform == DesktopPlatform.macOs && !synthetic) ...[
        (
          'macos.resolume-arena.application',
          '/Applications/Resolume Arena/Arena.app',
        ),
        ('macos.serato-dj-pro.application', '/Applications/Serato DJ Pro.app'),
      ],
      if (_platform == DesktopPlatform.macOs && home != null) ...[
        ('macos.resolume-arena.user-data', '$home/Documents/Resolume Arena'),
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
    return List.unmodifiable(detected);
  }
}

enum DesktopPlatform { macOs, windows, unsupported }
