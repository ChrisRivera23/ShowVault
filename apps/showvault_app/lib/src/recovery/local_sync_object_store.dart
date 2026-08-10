import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:crypto/crypto.dart';

abstract class LocalSyncObjectStore {
  Future<LocalSyncReceipt?> committedReceipt(String packageId);

  Future<int> uploadedLength(String packageId, String relativePath);

  Future<void> appendChunk(
    String packageId,
    String relativePath,
    int offset,
    List<int> bytes,
  );

  Future<LocalSyncReceipt> verifyAndCommit(
    String packageId,
    List<int> remoteManifestBytes,
    List<LocalSyncFileDescriptor> files,
  );
}

abstract class LocalSyncSessionObjectStore {
  Future<LocalSyncReceipt?> beginUpload(
    String packageId,
    List<int> remoteManifestBytes,
    List<LocalSyncFileDescriptor> files,
  );
}

class LocalFolderObjectStore implements LocalSyncObjectStore {
  LocalFolderObjectStore(this.rootPath);

  final String rootPath;

  @override
  Future<LocalSyncReceipt?> committedReceipt(String packageId) async {
    _validatePackageId(packageId);
    final packagesRoot = _join(rootPath, 'packages');
    final packagesType = await FileSystemEntity.type(
      packagesRoot,
      followLinks: false,
    );
    if (packagesType == FileSystemEntityType.notFound) return null;
    if (packagesType != FileSystemEntityType.directory) {
      throw const LocalObjectStoreIntegrityException(
        'The committed object-store location is unsafe.',
      );
    }
    final packageRoot = _join(packagesRoot, packageId);
    final packageType = await FileSystemEntity.type(
      packageRoot,
      followLinks: false,
    );
    if (packageType == FileSystemEntityType.notFound) return null;
    if (packageType != FileSystemEntityType.directory) {
      throw const LocalObjectStoreIntegrityException(
        'The committed remote package location is unsafe.',
      );
    }
    final receiptFile = File(_join(packageRoot, 'receipt.json'));
    return _readReceipt(receiptFile, packageId);
  }

  Future<LocalSyncReceipt?> _readReceipt(
    File receiptFile,
    String packageId,
  ) async {
    final type = await FileSystemEntity.type(
      receiptFile.path,
      followLinks: false,
    );
    if (type == FileSystemEntityType.notFound) return null;
    if (type != FileSystemEntityType.file ||
        await receiptFile.length() > 64 * 1024) {
      throw const LocalObjectStoreIntegrityException(
        'The remote receipt is not a bounded regular file.',
      );
    }
    try {
      final value = jsonDecode(await receiptFile.readAsString());
      if (value is! Map<String, Object?> ||
          value['packageId'] != packageId ||
          value['remoteManifestSha256'] is! String ||
          value['completedAt'] is! String) {
        throw const LocalObjectStoreIntegrityException(
          'The remote receipt has an invalid identity.',
        );
      }
      return LocalSyncReceipt(
        packageId: packageId,
        remoteManifestSha256: value['remoteManifestSha256']! as String,
        completedAt: DateTime.parse(value['completedAt']! as String).toUtc(),
      );
    } on FormatException {
      throw const LocalObjectStoreIntegrityException(
        'The remote receipt is malformed.',
      );
    }
  }

  @override
  Future<int> uploadedLength(String packageId, String relativePath) async {
    final file = await _partialContentFile(packageId, relativePath);
    await _createSafeParents(file.parent.path);
    final type = await FileSystemEntity.type(file.path, followLinks: false);
    if (type == FileSystemEntityType.notFound) return 0;
    if (type != FileSystemEntityType.file) {
      throw const LocalObjectStoreIntegrityException(
        'A partial remote object is not a regular file.',
      );
    }
    return file.length();
  }

  @override
  Future<void> appendChunk(
    String packageId,
    String relativePath,
    int offset,
    List<int> bytes,
  ) async {
    if (offset < 0 || bytes.isEmpty) {
      throw const LocalObjectStoreIntegrityException(
        'A remote chunk has an invalid range.',
      );
    }
    final file = await _partialContentFile(packageId, relativePath);
    await _createSafeParents(file.parent.path);
    final type = await FileSystemEntity.type(file.path, followLinks: false);
    if (type != FileSystemEntityType.file &&
        type != FileSystemEntityType.notFound) {
      throw const LocalObjectStoreIntegrityException(
        'A partial remote object is not a regular file.',
      );
    }
    final handle = await file.open(mode: FileMode.append);
    try {
      final length = await handle.length();
      if (length > offset) {
        throw const LocalObjectStoreIntegrityException(
          'The remote object offset moved beyond the local checkpoint.',
        );
      }
      if (length < offset) {
        throw const LocalObjectStoreIntegrityException(
          'The remote object has an unexpected gap.',
        );
      }
      await handle.writeFrom(bytes);
      await handle.flush();
    } finally {
      await handle.close();
    }
  }

  @override
  Future<LocalSyncReceipt> verifyAndCommit(
    String packageId,
    List<int> remoteManifestBytes,
    List<LocalSyncFileDescriptor> files,
  ) async {
    _validatePackageId(packageId);
    final remoteManifestSha256 = sha256.convert(remoteManifestBytes).toString();
    final existing = await committedReceipt(packageId);
    if (existing != null) {
      if (existing.remoteManifestSha256 != remoteManifestSha256) {
        throw const LocalObjectStoreIntegrityException(
          'The committed remote package has a different manifest identity.',
        );
      }
      return existing;
    }

    for (final descriptor in files) {
      final file = await _partialContentFile(
        packageId,
        descriptor.relativePath,
      );
      if (await FileSystemEntity.type(file.path, followLinks: false) !=
              FileSystemEntityType.file ||
          await file.length() != descriptor.size ||
          await _hashFile(file) != descriptor.sha256) {
        throw const LocalObjectStoreIntegrityException(
          'Remote checksum verification failed.',
        );
      }
    }

    final partialRoot = Directory(
      _join(_join(rootPath, '.partial'), packageId),
    );
    final manifestFile = File(_join(partialRoot.path, 'manifest.json'));
    await _writeNewFile(manifestFile, remoteManifestBytes);
    final partialReceiptFile = File(_join(partialRoot.path, 'receipt.json'));
    final priorReceipt = await _readReceipt(partialReceiptFile, packageId);
    final completedAt = priorReceipt?.completedAt ?? DateTime.now().toUtc();
    if (priorReceipt != null &&
        priorReceipt.remoteManifestSha256 != remoteManifestSha256) {
      throw const LocalObjectStoreIntegrityException(
        'The partial remote receipt has a different manifest identity.',
      );
    }
    if (priorReceipt == null) {
      await _writeNewFile(
        partialReceiptFile,
        utf8.encode(
          jsonEncode({
            'packageId': packageId,
            'remoteManifestSha256': remoteManifestSha256,
            'completedAt': completedAt.toIso8601String(),
          }),
        ),
      );
    }
    final packagesRoot = Directory(_join(rootPath, 'packages'));
    await _createSafeParents(packagesRoot.path);
    final committedRoot = Directory(_join(packagesRoot.path, packageId));
    if (await committedRoot.exists()) {
      final receipt = await committedReceipt(packageId);
      if (receipt?.remoteManifestSha256 == remoteManifestSha256) {
        return receipt!;
      }
      throw const LocalObjectStoreIntegrityException(
        'The remote package identity already exists with different bytes.',
      );
    }
    await partialRoot.rename(committedRoot.path);
    return LocalSyncReceipt(
      packageId: packageId,
      remoteManifestSha256: remoteManifestSha256,
      completedAt: completedAt,
    );
  }

  Future<File> _partialContentFile(
    String packageId,
    String relativePath,
  ) async {
    _validatePackageId(packageId);
    final segments = _validateRelativePath(relativePath);
    var path = _join(_join(_join(rootPath, '.partial'), packageId), 'content');
    for (final segment in segments) {
      path = _join(path, segment);
    }
    return File(path);
  }

  Future<void> _createSafeParents(String path) async {
    final absoluteRoot = Directory(rootPath).absolute.path;
    final rootType = await FileSystemEntity.type(
      absoluteRoot,
      followLinks: false,
    );
    if (rootType == FileSystemEntityType.notFound) {
      await Directory(absoluteRoot).create(recursive: true);
    } else if (rootType != FileSystemEntityType.directory) {
      throw const LocalObjectStoreIntegrityException(
        'The object-store root is unsafe.',
      );
    }
    final relative = Directory(
      path,
    ).absolute.path.substring(absoluteRoot.length);
    var current = absoluteRoot;
    for (final segment
        in relative
            .split(Platform.pathSeparator)
            .where((value) => value.isNotEmpty)) {
      current = _join(current, segment);
      final type = await FileSystemEntity.type(current, followLinks: false);
      if (type == FileSystemEntityType.link ||
          type == FileSystemEntityType.file) {
        throw const LocalObjectStoreIntegrityException(
          'The object-store path contains an unsafe entry.',
        );
      }
      if (type == FileSystemEntityType.notFound) {
        await Directory(current).create();
      }
    }
  }

  Future<void> _writeNewFile(File file, List<int> bytes) async {
    final type = await FileSystemEntity.type(file.path, followLinks: false);
    if (type == FileSystemEntityType.file) {
      if (!_bytesEqual(await file.readAsBytes(), bytes)) {
        throw const LocalObjectStoreIntegrityException(
          'A remote metadata object already exists with different bytes.',
        );
      }
      return;
    }
    if (type != FileSystemEntityType.notFound) {
      throw const LocalObjectStoreIntegrityException(
        'A remote metadata object is not a regular file.',
      );
    }
    await _createSafeParents(file.parent.path);
    final temporary = File('${file.path}.tmp-${_randomHex(8)}');
    try {
      await temporary.writeAsBytes(bytes, flush: true);
      await temporary.rename(file.path);
    } finally {
      if (await temporary.exists()) await temporary.delete();
    }
  }

  static Future<String> _hashFile(File file) async {
    final sink = _DigestSink();
    final converter = sha256.startChunkedConversion(sink);
    await for (final chunk in file.openRead()) {
      converter.add(chunk);
    }
    converter.close();
    if (sink.value == null) {
      throw const LocalObjectStoreIntegrityException(
        'A remote object could not be hashed.',
      );
    }
    return sink.value.toString();
  }

  static List<String> _validateRelativePath(String value) {
    if (value.isEmpty || value.startsWith('/') || value.contains('\\')) {
      throw const LocalObjectStoreIntegrityException(
        'A remote object has an unsafe logical path.',
      );
    }
    final segments = value.split('/');
    if (segments.any(
      (segment) => segment.isEmpty || segment == '.' || segment == '..',
    )) {
      throw const LocalObjectStoreIntegrityException(
        'A remote object has an unsafe logical path.',
      );
    }
    return segments;
  }

  static void _validatePackageId(String value) {
    if (value.length != 64 ||
        !value
            .split('')
            .every((character) => RegExp(r'^[0-9a-f]$').hasMatch(character))) {
      throw const LocalObjectStoreIntegrityException(
        'A remote package has an invalid identity.',
      );
    }
  }

  static bool _bytesEqual(List<int> left, List<int> right) {
    if (left.length != right.length) return false;
    for (var index = 0; index < left.length; index++) {
      if (left[index] != right[index]) return false;
    }
    return true;
  }

  static String _join(String left, String right) =>
      '$left${left.endsWith(Platform.pathSeparator) ? '' : Platform.pathSeparator}$right';

  static String _randomHex(int byteCount) {
    final random = Random.secure();
    return List<int>.generate(
      byteCount,
      (_) => random.nextInt(256),
    ).map((value) => value.toRadixString(16).padLeft(2, '0')).join();
  }
}

class LocalSyncFileDescriptor {
  const LocalSyncFileDescriptor({
    required this.relativePath,
    required this.size,
    required this.sha256,
  });

  final String relativePath;
  final int size;
  final String sha256;
}

class LocalSyncReceipt {
  const LocalSyncReceipt({
    required this.packageId,
    required this.remoteManifestSha256,
    required this.completedAt,
  });

  final String packageId;
  final String remoteManifestSha256;
  final DateTime completedAt;
}

class LocalObjectStoreUnavailableException implements Exception {
  const LocalObjectStoreUnavailableException([
    this.message = 'Object store unavailable.',
  ]);

  final String message;

  @override
  String toString() => message;
}

class LocalObjectStoreIntegrityException implements Exception {
  const LocalObjectStoreIntegrityException(this.message);

  final String message;

  @override
  String toString() => message;
}

class _DigestSink implements Sink<Digest> {
  Digest? value;

  @override
  void add(Digest data) => value = data;

  @override
  void close() {}
}
