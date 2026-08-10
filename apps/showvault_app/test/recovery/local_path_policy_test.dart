import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/recovery/local_path_policy.dart';

void main() {
  group('WindowsLocalPathPolicy', () {
    test('accepts bounded local drive paths', () {
      expect(
        WindowsLocalPathPolicy.isSafeLocalAbsolute(
          r'C:\Users\Operator\Documents\ShowVault Pro',
        ),
        isTrue,
      );
      expect(
        WindowsLocalPathPolicy.isSafeLocalAbsolute(r'd:/Vault/Backups'),
        isTrue,
      );
    });

    test('rejects roots, relative paths, UNC, devices, and traversal', () {
      for (final path in [
        r'C:\',
        r'C:relative',
        r'\root-relative',
        r'\\server\share\vault',
        r'\\?\C:\Vault',
        r'\\.\PhysicalDrive0',
        r'C:\Vault\..\Outside',
        r'C:\Vault\name:stream',
        r'C:\Vault\trailing.',
      ]) {
        expect(
          WindowsLocalPathPolicy.isSafeLocalAbsolute(path),
          isFalse,
          reason: path,
        );
      }
    });

    test('compares case-insensitively with separator normalization', () {
      expect(
        WindowsLocalPathPolicy.sameCanonicalPath(
          r'C:\Users\Operator\Vault',
          r'c:/users/operator/vault/',
        ),
        isTrue,
      );
      expect(
        WindowsLocalPathPolicy.sameCanonicalPath(
          r'C:\Users\Operator\Vault',
          r'C:\Users\Operator\Other',
        ),
        isFalse,
      );
    });

    test('extracts the final segment from either Windows separator', () {
      expect(
        WindowsLocalPathPolicy.finalSegment(
          r'C:\Users\runneradmin/AppData/Local/Temp/restored',
        ),
        'restored',
      );
      expect(
        WindowsLocalPathPolicy.finalSegment(r'D:/Vault\empty-target'),
        'empty-target',
      );
    });

    test('contains only exact path segments on the same drive', () {
      expect(
        WindowsLocalPathPolicy.isWithin(
          r'C:\Vault\Backups\package',
          r'c:\vault',
        ),
        isTrue,
      );
      expect(
        WindowsLocalPathPolicy.isWithin(r'C:\VaultSibling', r'C:\Vault'),
        isFalse,
      );
      expect(
        WindowsLocalPathPolicy.isWithin(r'D:\Vault\child', r'C:\Vault'),
        isFalse,
      );
    });
  });

  group('LocalDiagnosticPrivacy', () {
    test('rejects standalone and embedded Windows, UNC, and Unix paths', () {
      for (final value in [
        r'C:\Users\Operator\Vault',
        r'failed near D:/private/source',
        r'\\server\share\vault',
        r'error at \\server\share\vault',
        '/Users/operator/vault',
        'error at /private/tmp/vault',
        'file://local/source',
      ]) {
        expect(
          LocalDiagnosticPrivacy.containsLocalPath(value),
          isTrue,
          reason: value,
        );
      }
    });

    test('allows path-free identities and workflow labels', () {
      for (final value in [
        'showvault.support-diagnostic.v1',
        'windows.serato-dj-pro.user-data',
        'operator-authorized-showvault-vault',
        'synchronized',
        '2031-01-02T00:00:00.000Z',
      ]) {
        expect(
          LocalDiagnosticPrivacy.containsLocalPath(value),
          isFalse,
          reason: value,
        );
      }
    });
  });
}
