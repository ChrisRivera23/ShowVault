import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:showvault_app/src/recovery/local_recovery_service.dart';

class LocalPackageVerifier {
  const LocalPackageVerifier({
    this.maxFiles = 10000,
    this.maxFileBytes = 512 * 1024 * 1024,
    this.maxTotalBytes = 5 * 1024 * 1024 * 1024,
  });

  final int maxFiles;
  final int maxFileBytes;
  final int maxTotalBytes;

  Future<VerifiedLocalPackage> verify(LocalRecoveryRecord record) async {
    try {
      final manifestFile = File(
        _join(record.recoveryPointPath, 'manifest.json'),
      );
      if (await FileSystemEntity.type(manifestFile.path, followLinks: false) !=
              FileSystemEntityType.file ||
          await manifestFile.length() > 2 * 1024 * 1024) {
        throw const LocalPackageVerificationException(
          'The local package manifest is unavailable or oversized.',
        );
      }
      final manifestBytes = await manifestFile.readAsBytes();
      if (sha256.convert(manifestBytes).toString() != record.recoveryPointId) {
        throw const LocalPackageVerificationException(
          'The local package manifest identity changed.',
        );
      }
      final decoded = jsonDecode(utf8.decode(manifestBytes));
      if (decoded is! Map<String, Object?> ||
          decoded['source'] is! Map<String, Object?> ||
          decoded['files'] is! List<Object?>) {
        throw const LocalPackageVerificationException(
          'The local package manifest is malformed.',
        );
      }
      final source = decoded['source']! as Map<String, Object?>;
      final candidateKey = _boundedString(source, 'candidateKey');
      final pluginId = _boundedString(source, 'pluginId');
      final productName = _boundedString(source, 'productName');
      if (candidateKey != record.candidateKey ||
          productName != record.productName) {
        throw const LocalPackageVerificationException(
          'The local package metadata does not match its recovery record.',
        );
      }
      final entries = decoded['files']! as List<Object?>;
      if (entries.isEmpty || entries.length > maxFiles) {
        throw const LocalPackageVerificationException(
          'The local package has an invalid file count.',
        );
      }
      final contentRoot = _join(record.recoveryPointPath, 'content');
      if (await FileSystemEntity.type(contentRoot, followLinks: false) !=
          FileSystemEntityType.directory) {
        throw const LocalPackageVerificationException(
          'The local package content directory is unsafe.',
        );
      }
      final files = <VerifiedLocalFile>[];
      final paths = <String>{};
      var totalBytes = 0;
      for (final entry in entries) {
        if (entry is! Map<String, Object?>) {
          throw const LocalPackageVerificationException(
            'The local package file metadata is malformed.',
          );
        }
        final relativePath = _boundedString(entry, 'relativePath', max: 4096);
        final segments = safeLogicalPathSegments(relativePath);
        if (!paths.add(relativePath)) {
          throw const LocalPackageVerificationException(
            'The local package contains a duplicate logical path.',
          );
        }
        final size = entry['size'];
        final digest = entry['sha256'];
        if (size is! int ||
            size < 0 ||
            size > maxFileBytes ||
            digest is! String ||
            !isSha256(digest)) {
          throw const LocalPackageVerificationException(
            'The local package file metadata is invalid.',
          );
        }
        totalBytes += size;
        if (totalBytes > maxTotalBytes) {
          throw const LocalPackageVerificationException(
            'The local package exceeds the size limit.',
          );
        }
        var path = contentRoot;
        for (var index = 0; index < segments.length; index++) {
          path = _join(path, segments[index]);
          final type = await FileSystemEntity.type(path, followLinks: false);
          final expected = index == segments.length - 1
              ? FileSystemEntityType.file
              : FileSystemEntityType.directory;
          if (type != expected) {
            throw const LocalPackageVerificationException(
              'The local package contains a missing or linked entry.',
            );
          }
        }
        final file = File(path);
        if (await file.length() != size ||
            await hashBoundedFile(file, size) != digest) {
          throw const LocalPackageVerificationException(
            'The local package content failed checksum verification.',
          );
        }
        files.add(
          VerifiedLocalFile(
            file: file,
            relativePath: relativePath,
            size: size,
            sha256: digest,
          ),
        );
      }
      final actualPaths = <String>{};
      await for (final entity in Directory(
        contentRoot,
      ).list(recursive: true, followLinks: false)) {
        final type = await FileSystemEntity.type(
          entity.path,
          followLinks: false,
        );
        if (type == FileSystemEntityType.directory) continue;
        if (type != FileSystemEntityType.file) {
          throw const LocalPackageVerificationException(
            'The local package contains an unsupported or linked entry.',
          );
        }
        actualPaths.add(relativeLogicalPath(contentRoot, entity.path));
        if (actualPaths.length > maxFiles) {
          throw const LocalPackageVerificationException(
            'The local package exceeds the file limit.',
          );
        }
      }
      if (actualPaths.length != paths.length ||
          !actualPaths.containsAll(paths)) {
        throw const LocalPackageVerificationException(
          'The local package content set does not match its manifest.',
        );
      }
      return VerifiedLocalPackage(
        packageId: record.recoveryPointId,
        candidateKey: candidateKey,
        pluginId: pluginId,
        productName: productName,
        createdAt: record.createdAt,
        files: files,
        totalBytes: totalBytes,
      );
    } on LocalPackageVerificationException {
      rethrow;
    } catch (_) {
      throw const LocalPackageVerificationException(
        'The local package could not be safely reverified.',
      );
    }
  }

  static Future<String> hashBoundedFile(File file, int expectedBytes) async {
    final sink = _DigestSink();
    final converter = sha256.startChunkedConversion(sink);
    var bytes = 0;
    await for (final chunk in file.openRead()) {
      bytes += chunk.length;
      if (bytes > expectedBytes) {
        throw const LocalPackageVerificationException(
          'The local package content changed during verification.',
        );
      }
      converter.add(chunk);
    }
    converter.close();
    if (bytes != expectedBytes || sink.value == null) {
      throw const LocalPackageVerificationException(
        'The local package content changed during verification.',
      );
    }
    return sink.value.toString();
  }

  static List<String> safeLogicalPathSegments(String path) {
    if (path.isEmpty || path.startsWith('/') || path.contains('\\')) {
      throw const LocalPackageVerificationException(
        'The local package contains an unsafe logical path.',
      );
    }
    final segments = path.split('/');
    if (segments.any(
      (segment) => segment.isEmpty || segment == '.' || segment == '..',
    )) {
      throw const LocalPackageVerificationException(
        'The local package contains an unsafe logical path.',
      );
    }
    return segments;
  }

  static String relativeLogicalPath(String root, String path) {
    final prefix = root.endsWith(Platform.pathSeparator)
        ? root
        : '$root${Platform.pathSeparator}';
    if (!path.startsWith(prefix)) {
      throw const LocalPackageVerificationException(
        'The local package content escaped its root.',
      );
    }
    final value = path
        .substring(prefix.length)
        .split(Platform.pathSeparator)
        .join('/');
    safeLogicalPathSegments(value);
    return value;
  }

  static bool isSha256(String value) =>
      RegExp(r'^[0-9a-f]{64}$').hasMatch(value);

  static String _boundedString(
    Map<String, Object?> value,
    String key, {
    int max = 256,
  }) {
    final candidate = value[key];
    if (candidate is String &&
        candidate.isNotEmpty &&
        candidate.length <= max) {
      return candidate;
    }
    throw const LocalPackageVerificationException(
      'The local package contains invalid bounded metadata.',
    );
  }

  static String _join(String left, String right) =>
      '$left${left.endsWith(Platform.pathSeparator) ? '' : Platform.pathSeparator}$right';
}

class VerifiedLocalPackage {
  const VerifiedLocalPackage({
    required this.packageId,
    required this.candidateKey,
    required this.pluginId,
    required this.productName,
    required this.createdAt,
    required this.files,
    required this.totalBytes,
  });

  final String packageId;
  final String candidateKey;
  final String pluginId;
  final String productName;
  final DateTime createdAt;
  final List<VerifiedLocalFile> files;
  final int totalBytes;
}

class VerifiedLocalFile {
  const VerifiedLocalFile({
    required this.file,
    required this.relativePath,
    required this.size,
    required this.sha256,
  });

  final File file;
  final String relativePath;
  final int size;
  final String sha256;
}

class LocalPackageVerificationException implements Exception {
  const LocalPackageVerificationException(this.message);

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
