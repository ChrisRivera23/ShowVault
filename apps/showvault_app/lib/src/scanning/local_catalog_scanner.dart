import 'dart:io';

import 'package:flutter_riverpod/flutter_riverpod.dart';

final localCatalogScannerProvider = Provider<LocalCatalogScanner>(
  (ref) => LocalCatalogScanner(),
);

final localCatalogFindingsProvider = StateProvider<List<LocalCatalogFinding>>(
  (ref) => const [],
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
    final findings = await scanFindings();
    return findings
        .map((finding) => finding.candidateKey)
        .toList(growable: false);
  }

  Future<List<LocalCatalogFinding>> scanFindings() async {
    final candidates = _candidates();
    final detected = <LocalCatalogFinding>[];
    for (final candidate in candidates) {
      if (await _readType(candidate.path) != FileSystemEntityType.notFound) {
        detected.add(
          LocalCatalogFinding(
            candidateKey: candidate.key,
            pluginId: candidate.pluginId,
            productName: candidate.productName,
            candidateType: candidate.userDataRoot
                ? 'UserDataRoot'
                : 'InstalledApplication',
          ),
        );
      }
    }
    return detected;
  }

  LocalBackupSource? resolveBackupSource(String candidateKey) {
    for (final candidate in _candidates()) {
      if (candidate.key == candidateKey && candidate.userDataRoot) {
        return LocalBackupSource(
          candidateKey: candidate.key,
          pluginId: candidate.pluginId,
          productName: candidate.productName,
          rootPath: candidate.path,
        );
      }
    }
    return null;
  }

  List<_CatalogCandidate> _candidates() {
    final home = _environment['HOME'];
    final programFiles = _environment['ProgramFiles'];
    final userProfile = _environment['USERPROFILE'];
    return <_CatalogCandidate>[
      if (_platform == DesktopPlatform.macOs) ...[
        const _CatalogCandidate(
          key: 'macos.resolume-arena.application',
          pluginId: 'showvault.resolume',
          productName: 'Resolume Arena',
          path: '/Applications/Resolume Arena/Arena.app',
        ),
        if (home != null)
          _CatalogCandidate(
            key: 'macos.resolume-arena.user-data',
            pluginId: 'showvault.resolume',
            productName: 'Resolume Arena',
            path: '$home/Documents/Resolume Arena',
            userDataRoot: true,
          ),
        const _CatalogCandidate(
          key: 'macos.serato-dj-pro.application',
          pluginId: 'showvault.serato-dj-pro',
          productName: 'Serato DJ Pro',
          path: '/Applications/Serato DJ Pro.app',
        ),
        if (home != null)
          _CatalogCandidate(
            key: 'macos.serato-dj-pro.user-data',
            pluginId: 'showvault.serato-dj-pro',
            productName: 'Serato DJ Pro',
            path: '$home/Music/_Serato_',
            userDataRoot: true,
          ),
      ],
      if (_platform == DesktopPlatform.windows && programFiles != null) ...[
        _CatalogCandidate(
          key: 'windows.resolume-arena.application',
          pluginId: 'showvault.resolume',
          productName: 'Resolume Arena',
          path: '$programFiles\\Resolume Arena',
        ),
        _CatalogCandidate(
          key: 'windows.serato-dj-pro.application',
          pluginId: 'showvault.serato-dj-pro',
          productName: 'Serato DJ Pro',
          path: '$programFiles\\Serato\\Serato DJ Pro\\Serato DJ Pro.exe',
        ),
      ],
      if (_platform == DesktopPlatform.windows && userProfile != null) ...[
        _CatalogCandidate(
          key: 'windows.resolume-arena.user-data',
          pluginId: 'showvault.resolume',
          productName: 'Resolume Arena',
          path: '$userProfile\\Documents\\Resolume Arena',
          userDataRoot: true,
        ),
        _CatalogCandidate(
          key: 'windows.serato-dj-pro.user-data',
          pluginId: 'showvault.serato-dj-pro',
          productName: 'Serato DJ Pro',
          path: '$userProfile\\Music\\_Serato_',
          userDataRoot: true,
        ),
      ],
    ];
  }
}

class LocalBackupSource {
  const LocalBackupSource({
    required this.candidateKey,
    required this.pluginId,
    required this.productName,
    required this.rootPath,
  });

  final String candidateKey;
  final String pluginId;
  final String productName;
  final String rootPath;
}

class LocalCatalogFinding {
  const LocalCatalogFinding({
    required this.candidateKey,
    required this.pluginId,
    required this.productName,
    required this.candidateType,
  });

  final String candidateKey;
  final String pluginId;
  final String productName;
  final String candidateType;
}

class _CatalogCandidate {
  const _CatalogCandidate({
    required this.key,
    required this.pluginId,
    required this.productName,
    required this.path,
    this.userDataRoot = false,
  });

  final String key;
  final String pluginId;
  final String productName;
  final String path;
  final bool userDataRoot;
}

enum DesktopPlatform { macOs, windows, unsupported }
