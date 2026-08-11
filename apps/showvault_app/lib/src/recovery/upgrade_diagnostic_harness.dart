import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:showvault_app/src/config/app_config.dart';
import 'package:showvault_app/src/recovery/local_recovery_service.dart';
import 'package:showvault_app/src/recovery/local_restore_service.dart';
import 'package:showvault_app/src/recovery/local_support_diagnostic_service.dart';
import 'package:showvault_app/src/recovery/local_sync_object_store.dart';
import 'package:showvault_app/src/recovery/local_sync_service.dart';
import 'package:showvault_app/src/scanning/local_catalog_scanner.dart';

class UpgradeDiagnosticHarness {
  const UpgradeDiagnosticHarness._();

  static const _command = '--showvault-upgrade-phase';
  static const _resultFileCommand = '--showvault-upgrade-result-file';
  static const _statusPrefix = 'SHOWVAULT_UPGRADE_STATUS:';

  static Future<bool> tryRun(List<String> arguments) async {
    if (!arguments.contains(_command)) return false;
    final result = await _ResultChannel.tryOpen(arguments);
    if (!AppConfig.canRunUpgradeHarness || result == null) {
      stderr.writeln('ShowVault upgrade harness is unavailable.');
      if (result != null) {
        await result.write('${_statusPrefix}unavailable-configuration');
      }
      exitCode = 64;
      return true;
    }
    final commandIndex = arguments.indexOf(_command);
    final phase = commandIndex == 0 ? arguments[1] : arguments[0];
    if (!const {'prepare', 'verify', 'cleanup'}.contains(phase)) {
      stderr.writeln('ShowVault upgrade phase is unsupported.');
      await result.write('${_statusPrefix}unsupported-phase');
      exitCode = 64;
      return true;
    }
    final harness = _UpgradeHarness(
      _join(Directory.systemTemp.path, AppConfig.syntheticFixtureHome),
    );
    try {
      if (phase == 'cleanup') {
        await harness.cleanup();
      } else if (phase == 'prepare' &&
          AppConfig.upgradeGeneration == 'before') {
        await harness.prepare();
      } else if (phase == 'verify' && AppConfig.upgradeGeneration == 'after') {
        final report =
            'SHOWVAULT_UPGRADE_REPORT:${await harness.verifyAndEncode()}';
        stdout.writeln(report);
        await result.write(report);
      } else {
        throw const FormatException();
      }
      stdout.writeln('ShowVault upgrade phase passed: $phase');
      await result.write('$_statusPrefix$phase-passed');
    } catch (_) {
      stderr.writeln('ShowVault upgrade phase failed: $phase');
      await result.write('$_statusPrefix$phase-harness-failed');
      exitCode = 1;
    }
    return true;
  }
}

class _ResultChannel {
  const _ResultChannel(this.file);

  final File file;

  static Future<_ResultChannel?> tryOpen(List<String> arguments) async {
    if (arguments.length != 4) return null;
    final commandIndex = arguments.indexOf(
      UpgradeDiagnosticHarness._resultFileCommand,
    );
    if (commandIndex < 0 || commandIndex == arguments.length - 1) return null;
    final file = File(arguments[commandIndex + 1]);
    final parent = file.parent;
    final parentName = parent.path.split(Platform.pathSeparator).last;
    final fileName = file.path.split(Platform.pathSeparator).last;
    if (!file.isAbsolute ||
        !RegExp(
          r'^showvault-windows-proof-[0-9a-f]{32}$',
        ).hasMatch(parentName) ||
        !RegExp(
          r'^showvault-upgrade-result-[0-9a-f]{32}\.txt$',
        ).hasMatch(fileName) ||
        await FileSystemEntity.type(file.path, followLinks: false) !=
            FileSystemEntityType.notFound ||
        await FileSystemEntity.type(parent.path, followLinks: false) !=
            FileSystemEntityType.directory) {
      return null;
    }
    final marker = File(
      '${parent.path}${Platform.pathSeparator}.showvault-windows-proof-owned',
    );
    if (await FileSystemEntity.type(marker.path, followLinks: false) !=
            FileSystemEntityType.file ||
        (await marker.readAsString()).trim() != 'showvault.windows-proof.v1') {
      return null;
    }
    return _ResultChannel(file);
  }

  Future<void> write(String line) async {
    await file.writeAsString('$line\n', mode: FileMode.append, flush: true);
  }
}

class _UpgradeHarness {
  _UpgradeHarness(this.root);

  final String root;
  String get _source => _join(root, 'source');
  String get _vault => _join(root, 'vault');
  String get _objectStore => _join(root, 'object-store');
  String get _restoreTarget => _join(root, 'restore-target');
  File get _stateFile => File(_join(root, 'state.json'));
  File get _ownershipMarker => File(_join(root, '.showvault-upgrade-owned'));

  Future<void> prepare() async {
    await _initializeRoot();
    if (await _stateFile.exists()) throw const FormatException();
    final source = await Directory(_source).create();
    await Directory(_join(source.path, 'Subcrates')).create();
    await File(
      _join(source.path, 'database V2'),
    ).writeAsString('synthetic-upgrade-library', flush: true);
    await File(
      _join(_join(source.path, 'Subcrates'), 'upgrade.crate'),
    ).writeAsString('synthetic-upgrade-crate', flush: true);
    var clock = DateTime.utc(2031, 1, 1);
    final saved =
        await LocalRecoveryService(vaultRoot: _vault, now: () => clock).save(
          LocalBackupSource(
            candidateKey: 'macos.serato-dj-pro.user-data',
            pluginId: 'showvault.serato-dj-pro',
            productName: 'Serato DJ Pro',
            rootPath: source.path,
          ),
        );
    final unavailable = await LocalSyncService(
      objectStore: const _UnavailableStore(),
      now: () => clock,
    ).syncPending(_vault);
    _require(unavailable.retriedLater == 1);
    clock = clock.add(const Duration(seconds: 31));
    final synchronized = await LocalSyncService(
      objectStore: LocalFolderObjectStore(_objectStore),
      now: () => clock,
      chunkBytes: 8,
    ).syncPending(_vault);
    _require(synchronized.synchronized == 1);
    await LocalRestoreService(now: () => clock).restore(
      authorizedVaultRoot: _vault,
      recoveryPointId: saved.recoveryPointId,
      targetPath: _restoreTarget,
    );
    final diagnostic = await LocalSupportDiagnosticService(
      now: () => clock,
      appVersion: 'upgrade-before',
    ).generate(_vault);
    final snapshot = await LocalRecoveryService().inspectVault(_vault);
    final record = snapshot.records.single;
    _require(
      record.recoveryPointId == saved.recoveryPointId &&
          record.cloudStatus == LocalCloudSyncStatus.synchronized &&
          record.queueAttemptCount == 2 &&
          record.queueStateEventCount == 4,
    );
    await _stateFile.writeAsString(
      jsonEncode({
        'formatVersion': 'showvault.upgrade-state.v1',
        'packageId': saved.recoveryPointId,
        'fileCount': saved.fileCount,
        'totalBytes': saved.totalBytes,
        'beforeExecutableSha256': await _fileSha256(
          File(Platform.resolvedExecutable),
        ),
        'beforeDiagnosticSha256': diagnostic.evidenceSha256,
        'restoreEvidenceCount': diagnostic.restoreEvidenceCount,
        'queueAttemptCount': record.queueAttemptCount,
        'queueStateEventCount': record.queueStateEventCount,
      }),
      flush: true,
    );
    await source.delete(recursive: true);
  }

  Future<String> verifyAndEncode() async {
    await _requireOwnedRoot();
    final state = _decodeObject(await _stateFile.readAsString());
    _require(state['formatVersion'] == 'showvault.upgrade-state.v1');
    _require(!await Directory(_source).exists());
    final snapshot = await LocalRecoveryService().inspectVault(_vault);
    final record = snapshot.records.single;
    _require(
      record.recoveryPointId == state['packageId'] &&
          record.fileCount == state['fileCount'] &&
          record.totalBytes == state['totalBytes'] &&
          record.cloudStatus == LocalCloudSyncStatus.synchronized &&
          record.queueAttemptCount == state['queueAttemptCount'] &&
          record.queueStateEventCount == state['queueStateEventCount'],
    );
    final beforeExecutableSha = state['beforeExecutableSha256'];
    final afterExecutableSha = await _fileSha256(
      File(Platform.resolvedExecutable),
    );
    _require(
      beforeExecutableSha is String &&
          beforeExecutableSha.length == 64 &&
          beforeExecutableSha != afterExecutableSha,
    );
    final diagnostic = await LocalSupportDiagnosticService(
      now: () => DateTime.utc(2031, 1, 2),
      appVersion: 'upgrade-after',
    ).generate(_vault);
    _require(
      diagnostic.restoreEvidenceCount == state['restoreEvidenceCount'] &&
          diagnostic.recoveryPointCount == 1,
    );
    final report = <String, Object?>{
      'formatVersion': 'showvault.upgrade-diagnostic-proof.v1',
      'generatedAt': DateTime.utc(2031, 1, 2).toIso8601String(),
      'beforeArtifact': {
        'generation': 'before',
        'executableSha256': beforeExecutableSha,
        'diagnosticSha256': state['beforeDiagnosticSha256'],
      },
      'afterArtifact': {
        'generation': 'after',
        'executableSha256': afterExecutableSha,
        'diagnosticSha256': diagnostic.evidenceSha256,
      },
      'packageId': record.recoveryPointId,
      'preservation': {
        'installedArtifactReplaced': true,
        'sourcePresentDuringRehydration': false,
        'immutableRecoveryPointVerified': true,
        'independentManifestVerified': true,
        'queueJournalSurvived': true,
        'queueAttemptCount': record.queueAttemptCount,
        'queueStateEventCount': record.queueStateEventCount,
        'cloudStatus': 'synchronized',
        'restoreEvidenceSurvived': true,
        'restoreEvidenceCount': diagnostic.restoreEvidenceCount,
        'rehydratedWithoutSourceScan': true,
      },
      'scope': {
        'macOS': Platform.isMacOS,
        'attendedUpgrade': true,
        'attendedUninstallDataRemoval': false,
        'hostReboot': false,
        'windows': Platform.isWindows,
        'notarization': false,
        'productionProvider': false,
      },
    };
    final coreSha = sha256.convert(utf8.encode(jsonEncode(report))).toString();
    return base64Encode(
      utf8.encode(jsonEncode({...report, 'evidenceSha256': coreSha})),
    );
  }

  Future<void> cleanup() async {
    final type = await FileSystemEntity.type(root, followLinks: false);
    if (type == FileSystemEntityType.notFound) return;
    await _requireOwnedRoot();
    await Directory(root).delete(recursive: true);
  }

  Future<void> _initializeRoot() async {
    final type = await FileSystemEntity.type(root, followLinks: false);
    if (type != FileSystemEntityType.notFound) throw const FormatException();
    await Directory(root).create();
    await _ownershipMarker.writeAsString(
      'showvault.upgrade-harness.v1\n',
      flush: true,
    );
  }

  Future<void> _requireOwnedRoot() async {
    final name = root.split(Platform.pathSeparator).last;
    if (!RegExp(r'^showvault-upgrade-[a-z0-9-]{1,80}$').hasMatch(name) ||
        await FileSystemEntity.type(root, followLinks: false) !=
            FileSystemEntityType.directory ||
        await FileSystemEntity.type(
              _ownershipMarker.path,
              followLinks: false,
            ) !=
            FileSystemEntityType.file ||
        await _ownershipMarker.readAsString() !=
            'showvault.upgrade-harness.v1\n') {
      throw const FormatException();
    }
  }

  static Map<String, Object?> _decodeObject(String text) {
    final value = jsonDecode(text);
    if (value is! Map<String, Object?>) throw const FormatException();
    return value;
  }

  static void _require(bool condition) {
    if (!condition) throw const FormatException();
  }
}

class _UnavailableStore implements LocalSyncObjectStore {
  const _UnavailableStore();
  Never _offline() => throw const LocalObjectStoreUnavailableException(
    'Synthetic upgrade interruption.',
  );

  @override
  Future<void> appendChunk(
    String packageId,
    String relativePath,
    int offset,
    List<int> bytes,
  ) async => _offline();

  @override
  Future<LocalSyncReceipt?> committedReceipt(String packageId) async =>
      _offline();

  @override
  Future<int> uploadedLength(String packageId, String relativePath) async =>
      _offline();

  @override
  Future<LocalSyncReceipt> verifyAndCommit(
    String packageId,
    List<int> remoteManifestBytes,
    List<LocalSyncFileDescriptor> files,
  ) async => _offline();
}

Future<String> _fileSha256(File file) async =>
    (await sha256.bind(file.openRead()).first).toString();

String _join(String left, String right) =>
    '$left${left.endsWith(Platform.pathSeparator) ? '' : Platform.pathSeparator}$right';
