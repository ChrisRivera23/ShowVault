import 'dart:io';

import 'package:file_selector/file_selector.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/config/app_config.dart';
import 'package:showvault_app/src/recovery/local_recovery_service.dart';
import 'package:showvault_app/src/scanning/local_catalog_scanner.dart';

final localAccessCoordinatorProvider = Provider<LocalAccessCoordinator>(
  (ref) => LocalAccessCoordinator(),
);

typedef LocalDirectoryPicker =
    Future<String?> Function({
      String? initialDirectory,
      String? confirmButtonText,
      bool? canCreateDirectories,
    });

class LocalAccessCoordinator {
  LocalAccessCoordinator({
    LocalDirectoryPicker? directoryPicker,
    Map<String, String>? environment,
    bool? windows,
  }) : _directoryPicker = directoryPicker ?? getDirectoryPath,
       _environment = environment ?? _defaultEnvironment,
       _windows = windows ?? Platform.isWindows;

  final LocalDirectoryPicker _directoryPicker;
  final Map<String, String> _environment;
  final bool _windows;

  static Map<String, String> get _defaultEnvironment =>
      AppConfig.syntheticFixtureHome.isEmpty
      ? Platform.environment
      : {
          ...Platform.environment,
          'HOME': AppConfig.syntheticFixtureHome,
          'USERPROFILE': AppConfig.syntheticFixtureHome,
        };

  Future<LocalBackupSource> authorizeSource(LocalBackupSource expected) async {
    final selected = await _directoryPicker(
      initialDirectory: expected.rootPath,
      confirmButtonText: 'Allow this source',
      canCreateDirectories: false,
    );
    if (selected == null) {
      throw const LocalAccessCancelledException();
    }

    final expectedCanonical = await _canonicalDirectory(
      expected.rootPath,
      'The catalog-approved source is missing or is a filesystem link.',
    );
    final selectedCanonical = await _canonicalDirectory(
      selected,
      'The selected source is missing or is a filesystem link.',
    );
    if (!_samePath(expectedCanonical, selectedCanonical)) {
      throw const LocalRecoveryException(
        'The selected folder does not match the exact catalog-approved source.',
      );
    }

    return LocalBackupSource(
      candidateKey: expected.candidateKey,
      pluginId: expected.pluginId,
      productName: expected.productName,
      rootPath: selectedCanonical,
    );
  }

  Future<String> authorizeVault() async {
    final selected = await _directoryPicker(
      initialDirectory: defaultVaultInitialDirectory,
      confirmButtonText: 'Use this vault',
      canCreateDirectories: true,
    );
    if (selected == null) {
      throw const LocalAccessCancelledException();
    }
    return _canonicalDirectory(
      selected,
      'The selected vault is missing or is a filesystem link.',
    );
  }

  Future<String> authorizeEmptyRestoreTarget({String? initialDirectory}) async {
    final selected = await _directoryPicker(
      initialDirectory: initialDirectory ?? defaultVaultInitialDirectory,
      confirmButtonText: 'Use empty restore folder',
      canCreateDirectories: true,
    );
    if (selected == null) {
      throw const LocalAccessCancelledException();
    }
    final canonical = await _canonicalDirectory(
      selected,
      'The selected restore target is missing or is a filesystem link.',
    );
    await for (final _ in Directory(canonical).list(followLinks: false)) {
      throw const LocalRecoveryException(
        'The selected restore target must be empty.',
      );
    }
    return canonical;
  }

  String? get defaultVaultInitialDirectory {
    final home = _windows ? _environment['USERPROFILE'] : _environment['HOME'];
    if (home == null || home.trim().isEmpty) return null;
    final documents = _join(home, 'Documents');
    final defaultVault = _join(documents, 'ShowVault Pro');
    return Directory(defaultVault).existsSync() ? defaultVault : documents;
  }

  Future<String> _canonicalDirectory(String path, String error) async {
    final type = await FileSystemEntity.type(path, followLinks: false);
    if (type != FileSystemEntityType.directory) {
      throw LocalRecoveryException(error);
    }
    return Directory(path).resolveSymbolicLinks();
  }

  bool _samePath(String left, String right) =>
      _windows ? left.toLowerCase() == right.toLowerCase() : left == right;

  static String _join(String left, String right) =>
      '$left${left.endsWith(Platform.pathSeparator) ? '' : Platform.pathSeparator}$right';
}

class LocalAccessCancelledException implements Exception {
  const LocalAccessCancelledException();

  @override
  String toString() => 'Folder access was cancelled.';
}
