import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:crypto/crypto.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/scanning/local_catalog_scanner.dart';

final localRecoveryServiceProvider = Provider<LocalRecoveryService>(
  (ref) => LocalRecoveryService(),
);

final localVaultSnapshotProvider = StateProvider<LocalVaultSnapshot?>(
  (ref) => null,
);

class LocalRecoveryService {
  LocalRecoveryService({
    String? vaultRoot,
    Map<String, String>? environment,
    DateTime Function()? now,
    this.maxFiles = 10000,
    this.maxFileBytes = 512 * 1024 * 1024,
    this.maxTotalBytes = 5 * 1024 * 1024 * 1024,
    this.timeout = const Duration(minutes: 10),
    this.onFileCopied,
  }) : _environment = environment ?? Platform.environment,
       _configuredVaultRoot = vaultRoot,
       _now = now ?? DateTime.now;

  final String? _configuredVaultRoot;
  final Map<String, String> _environment;
  final DateTime Function() _now;
  final int maxFiles;
  final int maxFileBytes;
  final int maxTotalBytes;
  final Duration timeout;
  final Future<void> Function(String sourcePath)? onFileCopied;

  Future<LocalBackupResult> save(
    LocalBackupSource source, {
    LocalBackupCancellation? cancellation,
    String? authorizedVaultRoot,
  }) async {
    final startedAt = _now().toUtc();
    final deadline = startedAt.add(timeout);
    void checkActive() {
      if (cancellation?.isCancelled ?? false) {
        throw const LocalRecoveryException('Backup cancelled.');
      }
      if (_now().toUtc().isAfter(deadline)) {
        throw const LocalRecoveryException('Backup timed out.');
      }
    }

    checkActive();
    final sourceType = await FileSystemEntity.type(
      source.rootPath,
      followLinks: false,
    );
    if (sourceType != FileSystemEntityType.directory) {
      throw const LocalRecoveryException(
        'The approved recovery source is missing or is a filesystem link.',
      );
    }
    final canonicalRoot = await Directory(
      source.rootPath,
    ).resolveSymbolicLinks();
    final files = <_SourceFile>[];
    var totalBytes = 0;
    await for (final entity in Directory(
      canonicalRoot,
    ).list(recursive: true, followLinks: false)) {
      checkActive();
      final type = await FileSystemEntity.type(entity.path, followLinks: false);
      if (type == FileSystemEntityType.link) {
        throw LocalRecoveryException(
          'Backup source contains a filesystem link: ${_relativePath(canonicalRoot, entity.path)}',
        );
      }
      if (type == FileSystemEntityType.directory) continue;
      if (type != FileSystemEntityType.file) {
        throw const LocalRecoveryException(
          'Backup source contains an unsupported filesystem entry.',
        );
      }
      final relativePath = _relativePath(canonicalRoot, entity.path);
      final stat = await FileStat.stat(entity.path);
      if (stat.size > maxFileBytes) {
        throw LocalRecoveryException(
          'File exceeds the backup size limit: $relativePath',
        );
      }
      totalBytes += stat.size;
      if (totalBytes > maxTotalBytes) {
        throw const LocalRecoveryException(
          'Backup exceeds the total size limit.',
        );
      }
      files.add(_SourceFile(entity.path, relativePath, stat));
      if (files.length > maxFiles) {
        throw const LocalRecoveryException(
          'Backup exceeds the file-count limit.',
        );
      }
    }
    if (files.isEmpty) {
      throw const LocalRecoveryException(
        'The approved recovery source contains no regular files.',
      );
    }
    files.sort(
      (left, right) => left.relativePath.compareTo(right.relativePath),
    );

    final vaultRoot = _resolveVaultRoot(authorizedVaultRoot);
    final backupsRoot = _join(vaultRoot, 'Backups');
    final parentRoot = _join(backupsRoot, _safeName(source.productName));
    final stagingPath = _join(parentRoot, '.staging-${_randomHex(16)}');
    final contentPath = _join(stagingPath, 'content');
    await _initializeVault(vaultRoot);
    await Directory(contentPath).create(recursive: true);

    var published = false;
    try {
      final manifestFiles = <Map<String, Object?>>[];
      for (final sourceFile in files) {
        checkActive();
        final destination = _joinRelative(contentPath, sourceFile.relativePath);
        await Directory(File(destination).parent.path).create(recursive: true);
        final copied = await _copyAndHash(
          sourceFile.path,
          destination,
          sourceFile.stat.size,
          checkActive,
        );
        if (onFileCopied != null) await onFileCopied!(sourceFile.path);
        final after = await FileStat.stat(sourceFile.path);
        if (after.type != FileSystemEntityType.file ||
            after.size != sourceFile.stat.size ||
            after.modified != sourceFile.stat.modified ||
            copied.bytes != sourceFile.stat.size) {
          throw LocalRecoveryException(
            'Source changed during backup: ${sourceFile.relativePath}',
          );
        }
        manifestFiles.add({
          'relativePath': sourceFile.relativePath,
          'size': copied.bytes,
          'sha256': copied.sha256,
        });
      }

      final manifest = <String, Object?>{
        'formatVersion': '1.0',
        'agentId': '00000000-0000-0000-0000-000000000000',
        'discoveryCommandId': '00000000-0000-0000-0000-000000000000',
        'source': {
          'identity': canonicalRoot,
          'candidateKey': source.candidateKey,
          'pluginId': source.pluginId,
          'pluginVersion': 'desktop-0.1.0',
          'productName': source.productName,
          'productVersion': null,
          'firmwareVersion': null,
        },
        'createdAt': startedAt.toIso8601String(),
        'files': manifestFiles,
        'dependencies': <Object?>[],
        'relationships': <Object?>[],
        'restorePrerequisites': <Object?>[],
        'compatibilityRules': <Object?>[],
        'verificationRecords': [
          {
            'level': 'local-structural-and-cryptographic',
            'verifiedAt': startedAt.toIso8601String(),
            'passed': true,
            'evidence':
                'All copied files were independently hashed before publication.',
          },
        ],
        'localProtectionStatus': 'verified',
        'cloudSyncStatus': 'pending',
      };
      final manifestBytes = utf8.encode(jsonEncode(manifest));
      final recoveryPointId = sha256.convert(manifestBytes).toString();
      await File(
        _join(stagingPath, 'manifest.json'),
      ).writeAsBytes(manifestBytes, flush: true);
      await File(_join(stagingPath, 'summary.txt')).writeAsString(
        'ShowVault Pro recovery point\n'
        'System: ${source.productName}\n'
        'Created: ${startedAt.toIso8601String()}\n'
        'Files: ${files.length}\n'
        'Bytes: $totalBytes\n'
        'Local protection: verified\n'
        'Cloud synchronization: pending\n',
        flush: true,
      );
      await _verifyStaging(contentPath, manifestFiles, checkActive);

      final recoveryPointPath = _join(
        parentRoot,
        '${_timestamp(startedAt)}__$recoveryPointId',
      );
      if (await Directory(recoveryPointPath).exists()) {
        throw const LocalRecoveryException(
          'An immutable recovery point with this identity already exists.',
        );
      }
      await Directory(stagingPath).rename(recoveryPointPath);
      published = true;

      var cloudStatus = LocalCloudSyncStatus.queued;
      String? warning;
      try {
        await _atomicWrite(
          _join(_join(vaultRoot, 'Manifests'), '$recoveryPointId.json'),
          manifestBytes,
        );
      } on FileSystemException catch (error) {
        warning =
            'Verified locally, but the independent manifest copy failed: ${error.message}';
      }
      try {
        final queueRecord = utf8.encode(
          jsonEncode({
            'packageId': recoveryPointId,
            'packagePath': recoveryPointPath,
            'status': 'queued',
            'attemptCount': 0,
            'createdAt': startedAt.toIso8601String(),
            'updatedAt': startedAt.toIso8601String(),
            'lastError': null,
          }),
        );
        await _atomicWrite(
          _join(_join(vaultRoot, 'Upload Queue'), '$recoveryPointId.json'),
          queueRecord,
        );
      } on FileSystemException catch (error) {
        cloudStatus = LocalCloudSyncStatus.queueFailed;
        final queueWarning = 'Cloud queue persistence failed: ${error.message}';
        warning = warning == null
            ? 'Verified locally, but $queueWarning'
            : '$warning $queueWarning';
      }
      return LocalBackupResult(
        recoveryPointId: recoveryPointId,
        recoveryPointPath: recoveryPointPath,
        vaultRoot: vaultRoot,
        fileCount: files.length,
        totalBytes: totalBytes,
        localStatus: LocalProtectionStatus.verified,
        cloudStatus: cloudStatus,
        warning: warning,
      );
    } finally {
      if (!published && await Directory(stagingPath).exists()) {
        await Directory(stagingPath).delete(recursive: true);
      }
    }
  }

  Future<LocalVaultSnapshot> inspectVault(String authorizedVaultRoot) async {
    final vaultType = await FileSystemEntity.type(
      authorizedVaultRoot,
      followLinks: false,
    );
    if (vaultType != FileSystemEntityType.directory) {
      throw const LocalRecoveryException(
        'The authorized vault is missing or is a filesystem link.',
      );
    }
    final vaultRoot = await Directory(
      authorizedVaultRoot,
    ).resolveSymbolicLinks();
    final manifestsRoot = Directory(_join(vaultRoot, 'Manifests'));
    if (!await manifestsRoot.exists()) {
      return LocalVaultSnapshot(vaultRoot: vaultRoot, records: const []);
    }
    if (await FileSystemEntity.type(manifestsRoot.path, followLinks: false) !=
        FileSystemEntityType.directory) {
      throw const LocalRecoveryException(
        'The vault Manifests location is not a regular directory.',
      );
    }
    final backupsRoot = Directory(_join(vaultRoot, 'Backups'));
    if (await FileSystemEntity.type(backupsRoot.path, followLinks: false) !=
        FileSystemEntityType.directory) {
      throw const LocalRecoveryException(
        'The vault Backups location is not a regular directory.',
      );
    }
    final queueRoot = Directory(_join(vaultRoot, 'Upload Queue'));
    final queueRootType = await FileSystemEntity.type(
      queueRoot.path,
      followLinks: false,
    );
    if (queueRootType != FileSystemEntityType.notFound &&
        queueRootType != FileSystemEntityType.directory) {
      throw const LocalRecoveryException(
        'The vault Upload Queue location is unsafe.',
      );
    }

    final manifestFiles = <File>[];
    await for (final entity in manifestsRoot.list(followLinks: false)) {
      final type = await FileSystemEntity.type(entity.path, followLinks: false);
      if (type == FileSystemEntityType.link) {
        throw const LocalRecoveryException(
          'The vault contains a linked manifest entry.',
        );
      }
      if (type != FileSystemEntityType.file) continue;
      final name = _fileName(entity.path);
      if (name.contains('.tmp-')) continue;
      if (!_isRecordFileName(name)) {
        throw const LocalRecoveryException(
          'The vault contains an unrecognized manifest record.',
        );
      }
      manifestFiles.add(File(entity.path));
      if (manifestFiles.length > 10000) {
        throw const LocalRecoveryException(
          'The vault exceeds the manifest-count limit.',
        );
      }
    }
    manifestFiles.sort((left, right) => left.path.compareTo(right.path));

    final records = <LocalRecoveryRecord>[];
    for (final manifestFile in manifestFiles) {
      if (await manifestFile.length() > 2 * 1024 * 1024) {
        throw const LocalRecoveryException(
          'A vault manifest exceeds the size limit.',
        );
      }
      final bytes = await manifestFile.readAsBytes();
      final packageId = _fileName(manifestFile.path).substring(0, 64);
      if (sha256.convert(bytes).toString() != packageId) {
        throw const LocalRecoveryException(
          'A vault manifest identity does not match its SHA-256 digest.',
        );
      }
      final manifest = _decodeObject(
        bytes,
        'A vault manifest is not valid JSON.',
      );
      final source = manifest['source'];
      if (source is! Map<String, Object?>) {
        throw const LocalRecoveryException(
          'A vault manifest has no valid source.',
        );
      }
      final candidateKey = _requiredString(source, 'candidateKey');
      final productName = _requiredString(source, 'productName');
      final createdAt = DateTime.tryParse(
        _requiredString(manifest, 'createdAt'),
      )?.toUtc();
      final files = manifest['files'];
      if (createdAt == null || files is! List<Object?>) {
        throw const LocalRecoveryException('A vault manifest is incomplete.');
      }
      if (files.isEmpty || files.length > maxFiles) {
        throw const LocalRecoveryException(
          'A vault manifest has an invalid file count.',
        );
      }
      var totalBytes = 0;
      for (final file in files) {
        if (file is! Map<String, Object?> ||
            file['size'] is! int ||
            (file['size']! as int) < 0 ||
            (file['size']! as int) > maxFileBytes) {
          throw const LocalRecoveryException(
            'A vault manifest contains invalid file metadata.',
          );
        }
        totalBytes += file['size']! as int;
        if (totalBytes > maxTotalBytes) {
          throw const LocalRecoveryException(
            'A vault manifest exceeds the total size limit.',
          );
        }
      }
      final productRoot = _join(backupsRoot.path, _safeName(productName));
      if (await FileSystemEntity.type(productRoot, followLinks: false) !=
          FileSystemEntityType.directory) {
        throw const LocalRecoveryException(
          'A vault product backup location is unsafe.',
        );
      }
      final recoveryPointPath = _join(
        productRoot,
        '${_timestamp(createdAt)}__$packageId',
      );
      if (await FileSystemEntity.type(recoveryPointPath, followLinks: false) !=
          FileSystemEntityType.directory) {
        throw const LocalRecoveryException(
          'A vault recovery-point location is unsafe.',
        );
      }
      final packageManifest = File(_join(recoveryPointPath, 'manifest.json'));
      if (await FileSystemEntity.type(
                packageManifest.path,
                followLinks: false,
              ) !=
              FileSystemEntityType.file ||
          !_bytesEqual(bytes, await packageManifest.readAsBytes())) {
        throw const LocalRecoveryException(
          'A vault recovery point is missing or its manifest does not match.',
        );
      }

      final queueFile = File(
        _join(_join(vaultRoot, 'Upload Queue'), '$packageId.json'),
      );
      var cloudStatus = LocalCloudSyncStatus.queueFailed;
      if (await FileSystemEntity.type(queueFile.path, followLinks: false) ==
          FileSystemEntityType.file) {
        cloudStatus = await _readCloudStatus(vaultRoot, packageId, queueFile);
      }
      records.add(
        LocalRecoveryRecord(
          recoveryPointId: packageId,
          recoveryPointPath: recoveryPointPath,
          candidateKey: candidateKey,
          productName: productName,
          createdAt: createdAt,
          fileCount: files.length,
          totalBytes: totalBytes,
          localStatus: LocalProtectionStatus.verified,
          cloudStatus: cloudStatus,
        ),
      );
    }
    records.sort((left, right) => right.createdAt.compareTo(left.createdAt));
    return LocalVaultSnapshot(vaultRoot: vaultRoot, records: records);
  }

  Future<LocalCloudSyncStatus> _readCloudStatus(
    String vaultRoot,
    String packageId,
    File queueFile,
  ) async {
    if (await queueFile.length() > 64 * 1024) {
      throw const LocalRecoveryException(
        'A vault queue record exceeds the size limit.',
      );
    }
    final queue = _decodeObject(
      await queueFile.readAsBytes(),
      'A vault queue record is not valid JSON.',
    );
    if (_requiredString(queue, 'packageId') != packageId) {
      throw const LocalRecoveryException(
        'A vault queue record has the wrong identity.',
      );
    }
    var status = _cloudStatus(_requiredString(queue, 'status'));
    final stateParent = Directory(
      _join(_join(vaultRoot, 'Upload Queue'), 'State'),
    );
    final stateParentType = await FileSystemEntity.type(
      stateParent.path,
      followLinks: false,
    );
    if (stateParentType == FileSystemEntityType.notFound) return status;
    if (stateParentType != FileSystemEntityType.directory) {
      throw const LocalRecoveryException(
        'The vault queue state location is unsafe.',
      );
    }
    final stateRoot = Directory(_join(stateParent.path, packageId));
    final stateType = await FileSystemEntity.type(
      stateRoot.path,
      followLinks: false,
    );
    if (stateType == FileSystemEntityType.notFound) return status;
    if (stateType != FileSystemEntityType.directory) {
      throw const LocalRecoveryException(
        'A vault queue state location is unsafe.',
      );
    }
    final events = <File>[];
    await for (final entity in stateRoot.list(followLinks: false)) {
      if (await FileSystemEntity.type(entity.path, followLinks: false) !=
          FileSystemEntityType.file) {
        throw const LocalRecoveryException(
          'A vault queue state entry is unsafe.',
        );
      }
      final name = _fileName(entity.path);
      if (!RegExp(r'^\d{8}\.json$').hasMatch(name)) {
        throw const LocalRecoveryException(
          'A vault queue state entry has an invalid name.',
        );
      }
      events.add(File(entity.path));
      if (events.length > 1000) {
        throw const LocalRecoveryException(
          'A vault queue job exceeds the state-event limit.',
        );
      }
    }
    if (events.isEmpty) return status;
    events.sort((left, right) => left.path.compareTo(right.path));
    final latest = events.last;
    if (await latest.length() > 64 * 1024) {
      throw const LocalRecoveryException(
        'A vault queue state entry exceeds the size limit.',
      );
    }
    final event = _decodeObject(
      await latest.readAsBytes(),
      'A vault queue state entry is not valid JSON.',
    );
    if (_requiredString(event, 'packageId') != packageId) {
      throw const LocalRecoveryException(
        'A vault queue state entry has the wrong identity.',
      );
    }
    status = _cloudStatus(_requiredString(event, 'status'));
    return status;
  }

  static LocalCloudSyncStatus _cloudStatus(String value) => switch (value) {
    'queued' => LocalCloudSyncStatus.queued,
    'syncing' => LocalCloudSyncStatus.syncing,
    'retry' => LocalCloudSyncStatus.retryScheduled,
    'synchronized' => LocalCloudSyncStatus.synchronized,
    'failed' => LocalCloudSyncStatus.queueFailed,
    _ => throw const LocalRecoveryException(
      'A vault queue record has an invalid status.',
    ),
  };

  Future<_CopiedFile> _copyAndHash(
    String sourcePath,
    String destinationPath,
    int expectedBytes,
    void Function() checkActive,
  ) async {
    final digestSink = _DigestSink();
    final hashSink = sha256.startChunkedConversion(digestSink);
    final output = File(destinationPath).openWrite(mode: FileMode.writeOnly);
    var copiedBytes = 0;
    var hashClosed = false;
    var outputClosed = false;
    try {
      await for (final chunk in File(sourcePath).openRead()) {
        checkActive();
        copiedBytes += chunk.length;
        if (copiedBytes > expectedBytes || copiedBytes > maxFileBytes) {
          throw const LocalRecoveryException('Source grew during backup.');
        }
        hashSink.add(chunk);
        output.add(chunk);
      }
      hashSink.close();
      hashClosed = true;
      await output.flush();
      await output.close();
      outputClosed = true;
      final digest = digestSink.value;
      if (digest == null) {
        throw const LocalRecoveryException('Could not hash the backup source.');
      }
      return _CopiedFile(copiedBytes, digest.toString());
    } catch (_) {
      if (!hashClosed) hashSink.close();
      if (!outputClosed) await output.close();
      rethrow;
    }
  }

  Future<void> _verifyStaging(
    String contentPath,
    List<Map<String, Object?>> files,
    void Function() checkActive,
  ) async {
    for (final entry in files) {
      checkActive();
      final relativePath = entry['relativePath']! as String;
      final file = File(_joinRelative(contentPath, relativePath));
      if (!await file.exists() || await file.length() != entry['size']) {
        throw LocalRecoveryException(
          'Local verification failed: $relativePath',
        );
      }
      final digest = await _hashFile(file, entry['size']! as int, checkActive);
      if (digest != entry['sha256']) {
        throw LocalRecoveryException(
          'Local verification failed: $relativePath',
        );
      }
    }
  }

  Future<String> _hashFile(
    File file,
    int expectedBytes,
    void Function() checkActive,
  ) async {
    final digestSink = _DigestSink();
    final hashSink = sha256.startChunkedConversion(digestSink);
    var bytes = 0;
    await for (final chunk in file.openRead()) {
      checkActive();
      bytes += chunk.length;
      if (bytes > expectedBytes) {
        throw const LocalRecoveryException(
          'Local verification found unexpected file growth.',
        );
      }
      hashSink.add(chunk);
    }
    hashSink.close();
    if (bytes != expectedBytes || digestSink.value == null) {
      throw const LocalRecoveryException(
        'Local verification could not read the exact file.',
      );
    }
    return digestSink.value.toString();
  }

  Future<void> _initializeVault(String vaultRoot) async {
    for (final name in const [
      'Backups',
      'Manifests',
      'Device Exports',
      'Upload Queue',
      'Reports',
      'Logs',
      'Quarantine',
    ]) {
      await Directory(_join(vaultRoot, name)).create(recursive: true);
    }
  }

  Future<void> _atomicWrite(String path, List<int> bytes) async {
    final temporaryPath = '$path.tmp-${_randomHex(8)}';
    final temporary = File(temporaryPath);
    try {
      await temporary.writeAsBytes(bytes, flush: true);
      await temporary.rename(path);
    } finally {
      if (await temporary.exists()) await temporary.delete();
    }
  }

  String _resolveVaultRoot(String? authorizedVaultRoot) {
    if (authorizedVaultRoot case final authorized?) {
      return Directory(authorized).absolute.path;
    }
    if (_configuredVaultRoot case final configured?) {
      return Directory(configured).absolute.path;
    }
    final home = Platform.isWindows
        ? _environment['USERPROFILE']
        : _environment['HOME'];
    if (home == null || home.trim().isEmpty) {
      throw const LocalRecoveryException(
        'The Documents directory is unavailable. Configure a local vault location.',
      );
    }
    return _join(_join(home, 'Documents'), 'ShowVault Pro');
  }

  static String _relativePath(String root, String path) {
    final prefix = root.endsWith(Platform.pathSeparator)
        ? root
        : '$root${Platform.pathSeparator}';
    if (!path.startsWith(prefix)) {
      throw const LocalRecoveryException(
        'Backup path escaped the approved source.',
      );
    }
    final relative = path.substring(prefix.length);
    final segments = relative.split(Platform.pathSeparator);
    if (segments.isEmpty ||
        segments.any((part) => part.isEmpty || part == '.' || part == '..')) {
      throw const LocalRecoveryException(
        'Backup contains an unsafe relative path.',
      );
    }
    return segments.join('/');
  }

  static String _join(String left, String right) =>
      '$left${left.endsWith(Platform.pathSeparator) ? '' : Platform.pathSeparator}$right';

  static String _joinRelative(String root, String relative) => relative
      .split('/')
      .fold(root, (current, segment) => _join(current, segment));

  static String _safeName(String value) {
    final sanitized = value
        .replaceAll(RegExp(r'[<>:"/\\|?*\x00-\x1F]'), '-')
        .trim();
    return sanitized.isEmpty || sanitized == '.' || sanitized == '..'
        ? 'Unknown System'
        : sanitized;
  }

  static String _timestamp(DateTime value) =>
      '${value.year.toString().padLeft(4, '0')}-'
      '${value.month.toString().padLeft(2, '0')}-'
      '${value.day.toString().padLeft(2, '0')}T'
      '${value.hour.toString().padLeft(2, '0')}-'
      '${value.minute.toString().padLeft(2, '0')}-'
      '${value.second.toString().padLeft(2, '0')}Z';

  static Map<String, Object?> _decodeObject(List<int> bytes, String error) {
    try {
      final decoded = jsonDecode(utf8.decode(bytes));
      if (decoded is Map<String, Object?>) return decoded;
    } on FormatException {
      // Replaced with the bounded product error below.
    }
    throw LocalRecoveryException(error);
  }

  static String _requiredString(Map<String, Object?> value, String key) {
    final candidate = value[key];
    if (candidate is String && candidate.isNotEmpty) return candidate;
    throw LocalRecoveryException('A vault record has no valid $key.');
  }

  static bool _isRecordFileName(String name) =>
      name.length == 69 &&
      name.endsWith('.json') &&
      name.substring(0, 64).split('').every(_isHexCharacter);

  static bool _isHexCharacter(String value) =>
      (value.codeUnitAt(0) >= 48 && value.codeUnitAt(0) <= 57) ||
      (value.codeUnitAt(0) >= 97 && value.codeUnitAt(0) <= 102);

  static String _fileName(String path) =>
      path.split(Platform.pathSeparator).last;

  static bool _bytesEqual(List<int> left, List<int> right) {
    if (left.length != right.length) return false;
    for (var index = 0; index < left.length; index++) {
      if (left[index] != right[index]) return false;
    }
    return true;
  }

  static String _randomHex(int byteCount) {
    final random = Random.secure();
    return List<int>.generate(
      byteCount,
      (_) => random.nextInt(256),
    ).map((value) => value.toRadixString(16).padLeft(2, '0')).join();
  }
}

class LocalBackupCancellation {
  bool _cancelled = false;
  bool get isCancelled => _cancelled;
  void cancel() => _cancelled = true;
}

class LocalBackupResult {
  const LocalBackupResult({
    required this.recoveryPointId,
    required this.recoveryPointPath,
    required this.vaultRoot,
    required this.fileCount,
    required this.totalBytes,
    required this.localStatus,
    required this.cloudStatus,
    this.warning,
  });

  final String recoveryPointId;
  final String recoveryPointPath;
  final String vaultRoot;
  final int fileCount;
  final int totalBytes;
  final LocalProtectionStatus localStatus;
  final LocalCloudSyncStatus cloudStatus;
  final String? warning;
}

class LocalVaultSnapshot {
  const LocalVaultSnapshot({required this.vaultRoot, required this.records});

  final String vaultRoot;
  final List<LocalRecoveryRecord> records;
}

class LocalRecoveryRecord {
  const LocalRecoveryRecord({
    required this.recoveryPointId,
    required this.recoveryPointPath,
    required this.candidateKey,
    required this.productName,
    required this.createdAt,
    required this.fileCount,
    required this.totalBytes,
    required this.localStatus,
    required this.cloudStatus,
  });

  final String recoveryPointId;
  final String recoveryPointPath;
  final String candidateKey;
  final String productName;
  final DateTime createdAt;
  final int fileCount;
  final int totalBytes;
  final LocalProtectionStatus localStatus;
  final LocalCloudSyncStatus cloudStatus;
}

enum LocalProtectionStatus { verified }

enum LocalCloudSyncStatus {
  queued,
  syncing,
  retryScheduled,
  synchronized,
  queueFailed,
}

class LocalRecoveryException implements Exception {
  const LocalRecoveryException(this.message);
  final String message;
  @override
  String toString() => message;
}

class _SourceFile {
  const _SourceFile(this.path, this.relativePath, this.stat);
  final String path;
  final String relativePath;
  final FileStat stat;
}

class _CopiedFile {
  const _CopiedFile(this.bytes, this.sha256);
  final int bytes;
  final String sha256;
}

class _DigestSink implements Sink<Digest> {
  Digest? value;

  @override
  void add(Digest data) => value = data;

  @override
  void close() {}
}
