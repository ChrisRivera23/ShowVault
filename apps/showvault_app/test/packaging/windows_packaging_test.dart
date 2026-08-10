import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

void main() {
  final appRoot = Directory.current.path;
  final installer = File(
    '$appRoot${Platform.pathSeparator}packaging${Platform.pathSeparator}'
    'windows${Platform.pathSeparator}installer.iss',
  );
  final builder = File(
    '$appRoot${Platform.pathSeparator}packaging${Platform.pathSeparator}'
    'windows${Platform.pathSeparator}build-app.ps1',
  );
  final proof = File(
    '$appRoot${Platform.pathSeparator}tool${Platform.pathSeparator}'
    'run-windows-installed-proof.ps1',
  );
  final workflow = File(
    '$appRoot${Platform.pathSeparator}..${Platform.pathSeparator}..'
    '${Platform.pathSeparator}.github${Platform.pathSeparator}workflows'
    '${Platform.pathSeparator}windows-evidence.yml',
  );

  test('installer registers only the customer callback for current user', () {
    final text = installer.readAsStringSync();
    expect(text, contains('PrivilegesRequired=lowest'));
    expect(
      text,
      contains('Root: HKCU; Subkey: "Software\\Classes\\showvault"'),
    );
    expect(text, contains('""%1""'));
    expect(text, isNot(contains('HKLM')));
    expect(text, isNot(contains('Venue Agent')));
    expect(text, isNot(contains('[UninstallDelete]')));
    expect(text, contains('[InstallDelete]'));
    expect(text, contains('Name: "{app}\\*"'));
    expect(text, isNot(contains('ShowVault Pro')));
  });

  test('package includes the complete Flutter deployment and checksums', () {
    final text = builder.readAsStringSync();
    expect(text, contains("'build',"));
    expect(text, contains("'windows',"));
    expect(text, contains(r'build\windows\x64\runner\Release'));
    expect(text, contains("'flutter_windows.dll'"));
    expect(text, contains("'data\\flutter_assets'"));
    expect(text, contains("'data\\app.so'"));
    expect(text, contains('Compress-Archive'));
    expect(text, contains('Get-AuthenticodeSignature'));
    expect(text, contains("'SHA256SUMS'"));
    expect(text, contains("externalVaultRemovalPolicy = 'retain-by-default'"));
  });

  test('installed proof is marker-scoped and refuses callback collision', () {
    final text = proof.readAsStringSync();
    expect(text, contains('HKEY_CURRENT_USER\\Software\\Classes\\showvault'));
    expect(text, contains("'showvault.windows-proof.v1'"));
    expect(text, contains('showvault-windows-proof-[0-9a-f]{32}'));
    expect(text, contains('--showvault-upgrade-phase prepare'));
    expect(text, contains('--showvault-upgrade-phase verify'));
    expect(text, contains('--showvault-upgrade-phase cleanup'));
    expect(text, contains('sourcePresentDuringRehydration'));
    expect(text, contains('Get-AuthenticodeSignature'));
    expect(text, isNot(contains(r'Remove-Item -LiteralPath $env:USERPROFILE')));
  });

  test('Windows evidence workflow is manual, pinned, and synthetic', () {
    final text = workflow.readAsStringSync();
    expect(text, contains('workflow_dispatch:'));
    expect(text, contains('runs-on: windows-2025'));
    expect(text, contains('permissions:\n  contents: read'));
    expect(text, contains('flutter-version: 3.44.8'));
    expect(text, contains('flutter test'));
    expect(text, contains('build-app.ps1'));
    expect(text, contains('run-windows-installed-proof.ps1'));
    expect(text, contains('Record workflow provenance'));
    expect(text, contains('windows-workflow-provenance.json'));
    expect(text, contains(r'git rev-parse HEAD'));
    expect(text, contains(r'$env:GITHUB_RUN_ID'));
    expect(text, contains(r'$env:GITHUB_RUN_ATTEMPT'));
    expect(text, contains('Verify checksums and cleanup'));
    expect(text, contains('retention-days: 14'));
    expect(text, isNot(contains('secrets.')));
    expect(text, isNot(contains('pull_request:')));
    expect(text, isNot(contains('push:')));
  });
}
