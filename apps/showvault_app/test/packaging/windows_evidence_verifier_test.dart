import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/recovery/windows_evidence_verifier.dart';

void main() {
  late Directory testRoot;

  setUp(() async {
    testRoot = await Directory.systemTemp.createTemp(
      'showvault-windows-evidence-verifier-',
    );
  });

  tearDown(() async {
    if (await testRoot.exists()) await testRoot.delete(recursive: true);
  });

  test('verifies the exact checksummed package and installed proof', () async {
    final fixture = await _EvidenceFixture.create(testRoot);

    final result = await const WindowsEvidenceVerifier().verify(fixture.root);

    expect(result.package.appVersion, '0.1.0+1');
    expect(result.package.deploymentFileCount, 17);
    expect(result.proof.operatingSystemVersion, '10.0.26100.0');
    expect(result.proof.architecture, 'AMD64');
    expect(result.provenance.sourceCommitSha, 'a' * 40);
    expect(result.provenance.workflowRunId, '31423727118');
    expect(result.provenance.workflowRunAttempt, 1);
    expect(
      result.toJson()['limitations'],
      contains('headless-runner-evidence-only'),
    );
  });

  test('rejects a payload changed after SHA256SUMS was written', () async {
    final fixture = await _EvidenceFixture.create(testRoot);
    await fixture.packageInstaller.writeAsString('tampered');

    await expectLater(
      const WindowsEvidenceVerifier().verify(fixture.root),
      throwsA(isA<FormatException>()),
    );
  });

  test(
    'rejects an unlisted artifact even when checksummed files pass',
    () async {
      final fixture = await _EvidenceFixture.create(testRoot);
      await File(
        _join(fixture.packageDirectory.path, 'unexpected.txt'),
      ).writeAsString('unexpected');

      await expectLater(
        const WindowsEvidenceVerifier().verify(fixture.root),
        throwsA(isA<FormatException>()),
      );
    },
  );

  test(
    'rejects a path leak even after outer checksums are refreshed',
    () async {
      final fixture = await _EvidenceFixture.create(testRoot);
      final metadata = jsonDecode(await fixture.metadata.readAsString()) as Map;
      metadata['operatingSystemVersion'] = r'C:\Users\operator';
      await fixture.metadata.writeAsString(jsonEncode(metadata));
      await fixture.rewriteProofChecksums();

      await expectLater(
        const WindowsEvidenceVerifier().verify(fixture.root),
        throwsA(
          isA<FormatException>().having(
            (error) => error.message,
            'message',
            contains('prohibited path'),
          ),
        ),
      );
    },
  );

  test('rejects an invalid report-core digest', () async {
    final fixture = await _EvidenceFixture.create(testRoot);
    final report = jsonDecode(await fixture.report.readAsString()) as Map;
    report['evidenceSha256'] = 'f' * 64;
    await fixture.report.writeAsString(jsonEncode(report));
    await fixture.rewriteProofChecksums();

    await expectLater(
      const WindowsEvidenceVerifier().verify(fixture.root),
      throwsA(
        isA<FormatException>().having(
          (error) => error.message,
          'message',
          contains('core digest'),
        ),
      ),
    );
  });

  test('rejects provenance from a non-manual workflow event', () async {
    final fixture = await _EvidenceFixture.create(testRoot);
    final provenance =
        jsonDecode(await fixture.provenance.readAsString()) as Map;
    provenance['workflowEvent'] = 'push';
    await fixture.provenance.writeAsString(jsonEncode(provenance));
    await fixture.rewriteProofChecksums();

    await expectLater(
      const WindowsEvidenceVerifier().verify(fixture.root),
      throwsA(
        isA<FormatException>().having(
          (error) => error.message,
          'message',
          contains('workflow provenance'),
        ),
      ),
    );
  });

  test(
    'rejects a linked evidence file without following it',
    () async {
      final fixture = await _EvidenceFixture.create(testRoot);
      final outside = File(_join(testRoot.path, 'outside.json'));
      await outside.writeAsString(await fixture.metadata.readAsString());
      await fixture.metadata.delete();
      await Link(fixture.metadata.path).create(outside.path);

      await expectLater(
        const WindowsEvidenceVerifier().verify(fixture.root),
        throwsA(isA<FormatException>()),
      );
    },
    skip: Platform.isWindows
        ? 'Creating symlinks requires privileges on some Windows runners.'
        : false,
  );
}

class _EvidenceFixture {
  _EvidenceFixture({
    required this.root,
    required this.packageDirectory,
    required this.proofDirectory,
    required this.packageInstaller,
    required this.metadata,
    required this.report,
    required this.provenance,
  });

  final Directory root;
  final Directory packageDirectory;
  final Directory proofDirectory;
  final File packageInstaller;
  final File metadata;
  final File report;
  final File provenance;

  static Future<_EvidenceFixture> create(Directory parent) async {
    final root = Directory(_join(parent.path, 'artifact'));
    final packageDirectory = Directory(
      _join(root.path, 'showvault-windows-package'),
    );
    final proofDirectory = Directory(
      _join(root.path, 'showvault-windows-proof-evidence'),
    );
    await packageDirectory.create(recursive: true);
    await proofDirectory.create(recursive: true);

    const installerName = 'ShowVault-0.1.0-1-windows-x64-setup.exe';
    const archiveName = 'ShowVault-0.1.0-1-windows-x64.zip';
    final packageInstaller = File(_join(packageDirectory.path, installerName));
    await packageInstaller.writeAsBytes([1, 2, 3]);
    await File(
      _join(packageDirectory.path, archiveName),
    ).writeAsBytes([4, 5, 6]);
    await File(
      _join(packageDirectory.path, 'windows-package-manifest.json'),
    ).writeAsString(
      jsonEncode({
        'formatVersion': 'showvault.windows-package.v1',
        'appVersion': '0.1.0+1',
        'architecture': 'x64',
        'executable': 'ShowVault.exe',
        'deploymentFileCount': 17,
        'installer': installerName,
        'portableArchive': archiveName,
        'authenticationCallbackScheme': 'showvault',
        'controlPlaneProfile': 'public-https',
        'authenticodeStatus': 'NotSigned',
        'installerAuthenticodeStatus': 'NotSigned',
        'syntheticUpgradeGeneration': 'none',
        'externalVaultRemovalPolicy': 'retain-by-default',
      }),
    );
    await _writeChecksums(packageDirectory, {
      installerName,
      archiveName,
      'windows-package-manifest.json',
    });

    await File(
      _join(proofDirectory.path, 'ShowVault-before-windows-x64-setup.exe'),
    ).writeAsBytes([7, 8, 9]);
    await File(
      _join(proofDirectory.path, 'ShowVault-after-windows-x64-setup.exe'),
    ).writeAsBytes([10, 11, 12]);
    final report = File(
      _join(proofDirectory.path, 'windows-upgrade-diagnostic-report.json'),
    );
    final reportCore = <String, Object?>{
      'formatVersion': 'showvault.upgrade-diagnostic-proof.v1',
      'generatedAt': '2031-01-02T00:00:00.000Z',
      'beforeArtifact': {
        'generation': 'before',
        'executableSha256': '1' * 64,
        'diagnosticSha256': '2' * 64,
      },
      'afterArtifact': {
        'generation': 'after',
        'executableSha256': '3' * 64,
        'diagnosticSha256': '4' * 64,
      },
      'packageId': '5' * 64,
      'preservation': {
        'installedArtifactReplaced': true,
        'sourcePresentDuringRehydration': false,
        'immutableRecoveryPointVerified': true,
        'independentManifestVerified': true,
        'queueJournalSurvived': true,
        'queueAttemptCount': 2,
        'queueStateEventCount': 4,
        'cloudStatus': 'synchronized',
        'restoreEvidenceSurvived': true,
        'restoreEvidenceCount': 1,
        'rehydratedWithoutSourceScan': true,
      },
      'scope': {
        'macOS': false,
        'attendedUpgrade': true,
        'attendedUninstallDataRemoval': false,
        'hostReboot': false,
        'windows': true,
        'notarization': false,
        'productionProvider': false,
      },
    };
    final reportDigest = sha256.convert(utf8.encode(jsonEncode(reportCore)));
    await report.writeAsString(
      jsonEncode({...reportCore, 'evidenceSha256': reportDigest.toString()}),
    );
    final metadata = File(
      _join(proofDirectory.path, 'windows-execution-metadata.json'),
    );
    await metadata.writeAsString(
      jsonEncode({
        'formatVersion': 'showvault.windows-installed-proof.v1',
        'operatingSystem': 'Windows',
        'operatingSystemVersion': '10.0.26100.0',
        'architecture': 'AMD64',
        'beforeInstallerAuthenticodeStatus': 'NotSigned',
        'afterInstallerAuthenticodeStatus': 'NotSigned',
        'installedExecutableAuthenticodeStatus': 'NotSigned',
        'callbackSchemeRegisteredForCurrentUser': true,
        'externalVaultRetainedByInstaller': true,
        'hostReboot': false,
        'productionProvider': false,
        'personalData': false,
      }),
    );
    final provenance = File(
      _join(proofDirectory.path, 'windows-workflow-provenance.json'),
    );
    await provenance.writeAsString(
      jsonEncode({
        'formatVersion': 'showvault.windows-workflow-provenance.v1',
        'sourceCommitSha': 'a' * 40,
        'workflowRunId': '31423727118',
        'workflowRunAttempt': 1,
        'workflowEvent': 'workflow_dispatch',
        'workflowJob': 'package-and-prove',
        'runnerOs': 'Windows',
        'runnerArchitecture': 'X64',
        'artifactName': 'showvault-controlled-windows-evidence',
      }),
    );
    final fixture = _EvidenceFixture(
      root: root,
      packageDirectory: packageDirectory,
      proofDirectory: proofDirectory,
      packageInstaller: packageInstaller,
      metadata: metadata,
      report: report,
      provenance: provenance,
    );
    await fixture.rewriteProofChecksums();
    return fixture;
  }

  Future<void> rewriteProofChecksums() => _writeChecksums(proofDirectory, {
    'ShowVault-before-windows-x64-setup.exe',
    'ShowVault-after-windows-x64-setup.exe',
    'windows-upgrade-diagnostic-report.json',
    'windows-execution-metadata.json',
    'windows-workflow-provenance.json',
  });
}

Future<void> _writeChecksums(Directory directory, Set<String> names) async {
  final lines = <String>[];
  for (final name in names) {
    final file = File(_join(directory.path, name));
    final digest = await sha256.bind(file.openRead()).first;
    lines.add('$digest  $name');
  }
  await File(
    _join(directory.path, 'SHA256SUMS'),
  ).writeAsString('${lines.join('\r\n')}\r\n');
}

String _join(String left, String right) =>
    '$left${left.endsWith(Platform.pathSeparator) ? '' : Platform.pathSeparator}$right';
