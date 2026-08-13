import 'package:file_selector/file_selector.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

final localDirectoryConsentProvider = Provider<LocalDirectoryConsent>(
  (ref) => const NativeLocalDirectoryConsent(),
);

abstract class LocalDirectoryConsent {
  const LocalDirectoryConsent();

  Future<String?> selectExactSource();
  Future<String?> selectVault();
}

class NativeLocalDirectoryConsent extends LocalDirectoryConsent {
  const NativeLocalDirectoryConsent();

  @override
  Future<String?> selectExactSource() =>
      getDirectoryPath(confirmButtonText: 'Use exact source');

  @override
  Future<String?> selectVault() =>
      getDirectoryPath(confirmButtonText: 'Use local vault');
}
