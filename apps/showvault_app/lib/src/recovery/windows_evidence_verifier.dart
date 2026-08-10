import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';

const _packageDirectoryName = 'showvault-windows-package';
const _proofDirectoryName = 'showvault-windows-proof-evidence';
const _checksumFileName = 'SHA256SUMS';
const _packageManifestName = 'windows-package-manifest.json';
const _proofReportName = 'windows-upgrade-diagnostic-report.json';
const _proofMetadataName = 'windows-execution-metadata.json';
const _maxJsonBytes = 256 * 1024;
const _maxChecksumBytes = 16 * 1024;

final _sha256Pattern = RegExp(r'^[0-9a-f]{64}$');
final _safeFileNamePattern = RegExp(r'^[A-Za-z0-9][A-Za-z0-9._-]{0,199}$');
final _packageNamePattern = RegExp(
  r'^ShowVault-[0-9A-Za-z.-]{1,80}-windows-x64-setup\.exe$',
);
final _archiveNamePattern = RegExp(
  r'^ShowVault-[0-9A-Za-z.-]{1,80}-windows-x64\.zip$',
);
final _versionPattern = RegExp(r'^[0-9]+(?:\.[0-9]+){1,3}$');
final _prohibitedTextPattern = RegExp(
  r'([A-Z]:\\|\\\\|/Users/|/private/|/tmp/|file://|Bearer |accessToken|refreshToken|password|secret)',
  caseSensitive: false,
);
const _authenticodeStatuses = {
  'Valid',
  'NotSigned',
  'HashMismatch',
  'NotTrusted',
  'UnknownError',
  'Incompatible',
};

class WindowsEvidenceVerifier {
  const WindowsEvidenceVerifier();

  Future<WindowsEvidenceVerification> verify(Directory artifactRoot) async {
    await _requireDirectory(artifactRoot.path);
    final rootEntries = await _regularEntries(artifactRoot);
    _require(
      rootEntries.keys.toSet().containsAll({
            _packageDirectoryName,
            _proofDirectoryName,
          }) &&
          rootEntries.length == 2,
      'The artifact root must contain only the package and installed-proof directories.',
    );

    final packageDirectory = Directory(
      _join(artifactRoot.path, _packageDirectoryName),
    );
    final proofDirectory = Directory(
      _join(artifactRoot.path, _proofDirectoryName),
    );
    _require(
      rootEntries[_packageDirectoryName] == FileSystemEntityType.directory &&
          rootEntries[_proofDirectoryName] == FileSystemEntityType.directory,
      'The expected artifact entries must be regular directories.',
    );

    final packageChecksums = await _verifyChecksumDomain(packageDirectory);
    final packageManifestText = await _readBoundedUtf8(
      File(_join(packageDirectory.path, _packageManifestName)),
      _maxJsonBytes,
    );
    _require(
      !_prohibitedTextPattern.hasMatch(packageManifestText),
      'The package manifest contains a prohibited path or sensitive value.',
    );
    final packageManifest = _decodeObject(packageManifestText);
    final package = _verifyPackageManifest(packageManifest, packageChecksums);

    final proofChecksums = await _verifyChecksumDomain(proofDirectory);
    final proofMetadataText = await _readBoundedUtf8(
      File(_join(proofDirectory.path, _proofMetadataName)),
      _maxJsonBytes,
    );
    final proofMetadata = _decodeObject(proofMetadataText);
    final reportText = await _readBoundedUtf8(
      File(_join(proofDirectory.path, _proofReportName)),
      _maxJsonBytes,
    );
    _require(
      !_prohibitedTextPattern.hasMatch(proofMetadataText) &&
          !_prohibitedTextPattern.hasMatch(reportText),
      'The installed evidence contains a prohibited path or sensitive value.',
    );
    final proof = _verifyInstalledProof(
      proofMetadata,
      reportText,
      proofChecksums,
    );

    return WindowsEvidenceVerification(package: package, proof: proof);
  }

  Future<Map<String, String>> _verifyChecksumDomain(Directory directory) async {
    await _requireDirectory(directory.path);
    final entries = await _regularEntries(directory);
    _require(
      entries[_checksumFileName] == FileSystemEntityType.file,
      'A checksum domain is missing SHA256SUMS.',
    );
    _require(
      entries.values.every((type) => type == FileSystemEntityType.file),
      'Checksum domains may contain only regular files.',
    );
    final checksumText = await _readBoundedUtf8(
      File(_join(directory.path, _checksumFileName)),
      _maxChecksumBytes,
    );
    _require(
      !checksumText.contains('\r') && checksumText.endsWith('\n'),
      'SHA256SUMS must use bounded LF-terminated ASCII lines.',
    );
    final checksums = <String, String>{};
    for (final line in checksumText.trimRight().split('\n')) {
      final match = RegExp(
        r'^([0-9a-f]{64})  ([A-Za-z0-9][A-Za-z0-9._-]{0,199})$',
      ).firstMatch(line);
      _require(match != null, 'A checksum line is malformed.');
      final digest = match!.group(1)!;
      final name = match.group(2)!;
      _require(name != _checksumFileName, 'A checksum filename is invalid.');
      _require(
        !checksums.containsKey(name),
        'A checksum filename is duplicated.',
      );
      checksums[name] = digest;
    }
    _require(checksums.isNotEmpty, 'SHA256SUMS is empty.');
    _require(
      entries.keys.toSet().difference({_checksumFileName}).length ==
              checksums.length &&
          checksums.keys.every(entries.containsKey),
      'The checksum set does not match the exact artifact file set.',
    );
    for (final entry in checksums.entries) {
      final file = File(_join(directory.path, entry.key));
      _require(
        await _hashFile(file) == entry.value,
        'An artifact checksum does not match.',
      );
    }
    return Map.unmodifiable(checksums);
  }

  WindowsPackageEvidence _verifyPackageManifest(
    Map<String, Object?> manifest,
    Map<String, String> checksums,
  ) {
    final installer = manifest['installer'];
    final archive = manifest['portableArchive'];
    final appVersion = manifest['appVersion'];
    final deploymentFileCount = manifest['deploymentFileCount'];
    final executableStatus = manifest['authenticodeStatus'];
    final installerStatus = manifest['installerAuthenticodeStatus'];
    _requireExactKeys(manifest, {
      'formatVersion',
      'appVersion',
      'architecture',
      'executable',
      'deploymentFileCount',
      'installer',
      'portableArchive',
      'authenticationCallbackScheme',
      'controlPlaneProfile',
      'authenticodeStatus',
      'installerAuthenticodeStatus',
      'syntheticUpgradeGeneration',
      'externalVaultRemovalPolicy',
    });
    _require(
      manifest['formatVersion'] == 'showvault.windows-package.v1' &&
          appVersion is String &&
          appVersion.isNotEmpty &&
          appVersion.length <= 80 &&
          manifest['architecture'] == 'x64' &&
          manifest['executable'] == 'ShowVault.exe' &&
          deploymentFileCount is int &&
          deploymentFileCount > 0 &&
          deploymentFileCount <= 100000 &&
          installer is String &&
          _packageNamePattern.hasMatch(installer) &&
          archive is String &&
          _archiveNamePattern.hasMatch(archive) &&
          manifest['authenticationCallbackScheme'] == 'showvault' &&
          manifest['controlPlaneProfile'] == 'public-https' &&
          executableStatus is String &&
          _authenticodeStatuses.contains(executableStatus) &&
          installerStatus is String &&
          _authenticodeStatuses.contains(installerStatus) &&
          manifest['syntheticUpgradeGeneration'] == 'none' &&
          manifest['externalVaultRemovalPolicy'] == 'retain-by-default',
      'The Windows package manifest is incomplete or unsafe.',
    );
    final installerName = installer as String;
    final archiveName = archive as String;
    final expectedNames = {installerName, archiveName, _packageManifestName};
    _require(
      checksums.keys.toSet().containsAll(expectedNames) &&
          checksums.length == expectedNames.length,
      'The package checksum set is not exact.',
    );
    return WindowsPackageEvidence(
      appVersion: appVersion as String,
      deploymentFileCount: deploymentFileCount as int,
      installerSha256: checksums[installerName]!,
      archiveSha256: checksums[archiveName]!,
      executableAuthenticodeStatus: executableStatus as String,
      installerAuthenticodeStatus: installerStatus as String,
    );
  }

  WindowsInstalledProofEvidence _verifyInstalledProof(
    Map<String, Object?> metadata,
    String reportText,
    Map<String, String> checksums,
  ) {
    const expectedNames = {
      'ShowVault-before-windows-x64-setup.exe',
      'ShowVault-after-windows-x64-setup.exe',
      _proofReportName,
      _proofMetadataName,
    };
    _require(
      checksums.keys.toSet().containsAll(expectedNames) &&
          checksums.length == expectedNames.length,
      'The installed-proof checksum set is not exact.',
    );
    final osVersion = metadata['operatingSystemVersion'];
    final architecture = metadata['architecture'];
    final beforeStatus = metadata['beforeInstallerAuthenticodeStatus'];
    final afterStatus = metadata['afterInstallerAuthenticodeStatus'];
    final executableStatus = metadata['installedExecutableAuthenticodeStatus'];
    _requireExactKeys(metadata, {
      'formatVersion',
      'operatingSystem',
      'operatingSystemVersion',
      'architecture',
      'beforeInstallerAuthenticodeStatus',
      'afterInstallerAuthenticodeStatus',
      'installedExecutableAuthenticodeStatus',
      'callbackSchemeRegisteredForCurrentUser',
      'externalVaultRetainedByInstaller',
      'hostReboot',
      'productionProvider',
      'personalData',
    });
    _require(
      metadata['formatVersion'] == 'showvault.windows-installed-proof.v1' &&
          metadata['operatingSystem'] == 'Windows' &&
          osVersion is String &&
          _versionPattern.hasMatch(osVersion) &&
          architecture is String &&
          {'AMD64', 'x64'}.contains(architecture) &&
          beforeStatus is String &&
          _authenticodeStatuses.contains(beforeStatus) &&
          afterStatus is String &&
          _authenticodeStatuses.contains(afterStatus) &&
          executableStatus is String &&
          _authenticodeStatuses.contains(executableStatus) &&
          metadata['callbackSchemeRegisteredForCurrentUser'] == true &&
          metadata['externalVaultRetainedByInstaller'] == true &&
          metadata['hostReboot'] == false &&
          metadata['productionProvider'] == false &&
          metadata['personalData'] == false,
      'The Windows execution metadata is incomplete or unsafe.',
    );

    final digestMatch = RegExp(
      r',"evidenceSha256":"([0-9a-f]{64})"}$',
    ).firstMatch(reportText);
    _require(
      digestMatch != null,
      'The installed report has no bounded digest.',
    );
    final coreText = reportText.replaceRange(
      digestMatch!.start,
      digestMatch.end,
      '}',
    );
    final evidenceSha256 = digestMatch.group(1)!;
    _require(
      sha256.convert(utf8.encode(coreText)).toString() == evidenceSha256,
      'The installed report core digest does not match.',
    );
    final report = _decodeObject(reportText);
    final beforeArtifact = _object(report, 'beforeArtifact');
    final afterArtifact = _object(report, 'afterArtifact');
    final preservation = _object(report, 'preservation');
    final scope = _object(report, 'scope');
    _requireExactKeys(report, {
      'formatVersion',
      'generatedAt',
      'beforeArtifact',
      'afterArtifact',
      'packageId',
      'preservation',
      'scope',
      'evidenceSha256',
    });
    _requireExactKeys(beforeArtifact, {
      'generation',
      'executableSha256',
      'diagnosticSha256',
    });
    _requireExactKeys(afterArtifact, {
      'generation',
      'executableSha256',
      'diagnosticSha256',
    });
    _requireExactKeys(preservation, {
      'installedArtifactReplaced',
      'sourcePresentDuringRehydration',
      'immutableRecoveryPointVerified',
      'independentManifestVerified',
      'queueJournalSurvived',
      'queueAttemptCount',
      'queueStateEventCount',
      'cloudStatus',
      'restoreEvidenceSurvived',
      'restoreEvidenceCount',
      'rehydratedWithoutSourceScan',
    });
    _requireExactKeys(scope, {
      'macOS',
      'attendedUpgrade',
      'attendedUninstallDataRemoval',
      'hostReboot',
      'windows',
      'notarization',
      'productionProvider',
    });
    final beforeExecutableSha = beforeArtifact['executableSha256'];
    final afterExecutableSha = afterArtifact['executableSha256'];
    _require(
      report['formatVersion'] == 'showvault.upgrade-diagnostic-proof.v1' &&
          report['generatedAt'] == '2031-01-02T00:00:00.000Z' &&
          beforeArtifact['generation'] == 'before' &&
          afterArtifact['generation'] == 'after' &&
          beforeExecutableSha is String &&
          _sha256Pattern.hasMatch(beforeExecutableSha) &&
          afterExecutableSha is String &&
          _sha256Pattern.hasMatch(afterExecutableSha) &&
          beforeExecutableSha != afterExecutableSha &&
          _validDigest(beforeArtifact['diagnosticSha256']) &&
          _validDigest(afterArtifact['diagnosticSha256']) &&
          _validDigest(report['packageId']) &&
          preservation['installedArtifactReplaced'] == true &&
          preservation['sourcePresentDuringRehydration'] == false &&
          preservation['immutableRecoveryPointVerified'] == true &&
          preservation['independentManifestVerified'] == true &&
          preservation['queueJournalSurvived'] == true &&
          preservation['queueAttemptCount'] == 2 &&
          preservation['queueStateEventCount'] == 4 &&
          preservation['cloudStatus'] == 'synchronized' &&
          preservation['restoreEvidenceSurvived'] == true &&
          preservation['restoreEvidenceCount'] == 1 &&
          preservation['rehydratedWithoutSourceScan'] == true &&
          scope['windows'] == true &&
          scope['macOS'] == false &&
          scope['attendedUpgrade'] == true &&
          scope['attendedUninstallDataRemoval'] == false &&
          scope['hostReboot'] == false &&
          scope['notarization'] == false &&
          scope['productionProvider'] == false,
      'The installed preservation report is incomplete.',
    );
    return WindowsInstalledProofEvidence(
      operatingSystemVersion: osVersion as String,
      architecture: architecture as String,
      beforeInstallerSha256:
          checksums['ShowVault-before-windows-x64-setup.exe']!,
      afterInstallerSha256: checksums['ShowVault-after-windows-x64-setup.exe']!,
      reportEvidenceSha256: evidenceSha256,
      beforeInstallerAuthenticodeStatus: beforeStatus as String,
      afterInstallerAuthenticodeStatus: afterStatus as String,
      installedExecutableAuthenticodeStatus: executableStatus as String,
    );
  }
}

class WindowsEvidenceVerification {
  const WindowsEvidenceVerification({
    required this.package,
    required this.proof,
  });

  final WindowsPackageEvidence package;
  final WindowsInstalledProofEvidence proof;

  Map<String, Object?> toJson() => {
    'formatVersion': 'showvault.windows-evidence-verification.v1',
    'verified': true,
    'package': package.toJson(),
    'installedProof': proof.toJson(),
    'limitations': [
      'headless-runner-evidence-only',
      'no-attended-picker-or-auth-callback',
      'no-clean-customer-machine',
      'no-host-reboot',
      'no-distribution-signing-claim',
      'no-personal-data',
      'no-venue-readiness-claim',
    ],
  };
}

class WindowsPackageEvidence {
  const WindowsPackageEvidence({
    required this.appVersion,
    required this.deploymentFileCount,
    required this.installerSha256,
    required this.archiveSha256,
    required this.executableAuthenticodeStatus,
    required this.installerAuthenticodeStatus,
  });

  final String appVersion;
  final int deploymentFileCount;
  final String installerSha256;
  final String archiveSha256;
  final String executableAuthenticodeStatus;
  final String installerAuthenticodeStatus;

  Map<String, Object?> toJson() => {
    'appVersion': appVersion,
    'architecture': 'x64',
    'deploymentFileCount': deploymentFileCount,
    'installerSha256': installerSha256,
    'archiveSha256': archiveSha256,
    'executableAuthenticodeStatus': executableAuthenticodeStatus,
    'installerAuthenticodeStatus': installerAuthenticodeStatus,
  };
}

class WindowsInstalledProofEvidence {
  const WindowsInstalledProofEvidence({
    required this.operatingSystemVersion,
    required this.architecture,
    required this.beforeInstallerSha256,
    required this.afterInstallerSha256,
    required this.reportEvidenceSha256,
    required this.beforeInstallerAuthenticodeStatus,
    required this.afterInstallerAuthenticodeStatus,
    required this.installedExecutableAuthenticodeStatus,
  });

  final String operatingSystemVersion;
  final String architecture;
  final String beforeInstallerSha256;
  final String afterInstallerSha256;
  final String reportEvidenceSha256;
  final String beforeInstallerAuthenticodeStatus;
  final String afterInstallerAuthenticodeStatus;
  final String installedExecutableAuthenticodeStatus;

  Map<String, Object?> toJson() => {
    'operatingSystem': 'Windows',
    'operatingSystemVersion': operatingSystemVersion,
    'architecture': architecture,
    'beforeInstallerSha256': beforeInstallerSha256,
    'afterInstallerSha256': afterInstallerSha256,
    'reportEvidenceSha256': reportEvidenceSha256,
    'beforeInstallerAuthenticodeStatus': beforeInstallerAuthenticodeStatus,
    'afterInstallerAuthenticodeStatus': afterInstallerAuthenticodeStatus,
    'installedExecutableAuthenticodeStatus':
        installedExecutableAuthenticodeStatus,
    'externalVaultRetainedByInstaller': true,
    'sourceFreeRehydrationVerified': true,
  };
}

Future<Map<String, FileSystemEntityType>> _regularEntries(
  Directory directory,
) async {
  final entries = <String, FileSystemEntityType>{};
  await for (final entity in directory.list(followLinks: false)) {
    final name = entity.uri.pathSegments
        .where((segment) => segment.isNotEmpty)
        .last;
    _require(
      _safeFileNamePattern.hasMatch(name) && !entries.containsKey(name),
      'An artifact entry has an unsafe or duplicate name.',
    );
    entries[name] = await FileSystemEntity.type(
      entity.path,
      followLinks: false,
    );
  }
  return entries;
}

Future<void> _requireDirectory(String path) async {
  _require(
    await FileSystemEntity.type(path, followLinks: false) ==
        FileSystemEntityType.directory,
    'An expected evidence directory is missing or substituted.',
  );
}

Future<String> _readBoundedUtf8(File file, int maximumBytes) async {
  _require(
    await FileSystemEntity.type(file.path, followLinks: false) ==
        FileSystemEntityType.file,
    'An expected evidence file is missing or substituted.',
  );
  final length = await file.length();
  _require(
    length > 0 && length <= maximumBytes,
    'An evidence file is oversized.',
  );
  final bytes = await file.readAsBytes();
  try {
    return utf8.decode(bytes);
  } on FormatException {
    throw const FormatException('An evidence file is not valid UTF-8.');
  }
}

Map<String, Object?> _decodeObject(String text) {
  final value = jsonDecode(text);
  _require(value is Map<String, Object?>, 'An evidence JSON root is invalid.');
  return value as Map<String, Object?>;
}

Map<String, Object?> _object(Map<String, Object?> parent, String key) {
  final value = parent[key];
  _require(
    value is Map<String, Object?>,
    'An evidence JSON object is missing.',
  );
  return value as Map<String, Object?>;
}

bool _validDigest(Object? value) =>
    value is String && _sha256Pattern.hasMatch(value);

void _requireExactKeys(Map<String, Object?> value, Set<String> expected) {
  _require(
    value.keys.toSet().containsAll(expected) && value.length == expected.length,
    'An evidence JSON object has an unexpected schema.',
  );
}

Future<String> _hashFile(File file) async =>
    (await sha256.bind(file.openRead()).first).toString();

String _join(String left, String right) =>
    '$left${left.endsWith(Platform.pathSeparator) ? '' : Platform.pathSeparator}$right';

void _require(bool condition, String message) {
  if (!condition) throw FormatException(message);
}
