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
  final windowsCmake = File(
    '$appRoot${Platform.pathSeparator}windows${Platform.pathSeparator}'
    'CMakeLists.txt',
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
    expect(text, contains(r'$VcpkgRoot = $env:VCPKG_ROOT'));
    expect(
      text,
      contains("Join-Path \$VcpkgRoot 'scripts\\buildsystems\\vcpkg.cmake'"),
    );
    expect(text, contains("Join-Path \$VcpkgRoot 'vcpkg.exe'"));
    expect(
      text,
      contains('VCPKG_ROOT must be an absolute local Windows directory.'),
    );
    expect(text, contains(r"'^[A-Za-z]:[\\/][^\r\n]+$'"));
    expect(text, contains(r'-PathType Container'));
    expect(text, contains(r'-PathType Leaf'));
    expect(
      text,
      contains(
        'VCPKG_ROOT does not contain the required vcpkg executable and '
        'CMake toolchain.',
      ),
    );
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

  test('Windows CMake enables validated vcpkg before the first project', () {
    final text = windowsCmake.readAsStringSync();
    final minimumIndex = text.indexOf('cmake_minimum_required(VERSION 3.14)');
    final rootIndex = text.indexOf(r'ENV{VCPKG_ROOT}');
    final toolchainIndex = text.indexOf('set(CMAKE_TOOLCHAIN_FILE');
    final projectIndex = text.indexOf('project(showvault_app LANGUAGES CXX)');

    expect(minimumIndex, greaterThanOrEqualTo(0));
    expect(rootIndex, greaterThan(minimumIndex));
    expect(toolchainIndex, greaterThan(rootIndex));
    expect(projectIndex, greaterThan(toolchainIndex));
    expect(text, contains('scripts/buildsystems/vcpkg.cmake'));
    expect(
      text,
      contains('VCPKG_ROOT is required to build ShowVault for Windows.'),
    );
    expect(text, contains('if(NOT DEFINED ENV{VCPKG_ROOT}'));
    expect(text, contains('if(NOT EXISTS "\${SHOWVAULT_VCPKG_TOOLCHAIN}")'));
    expect(
      text,
      contains('VCPKG_ROOT does not contain scripts/buildsystems/vcpkg.cmake.'),
    );
  });

  test('installed proof is marker-scoped and refuses callback collision', () {
    final text = proof.readAsStringSync();
    expect(text, contains('HKEY_CURRENT_USER\\Software\\Classes\\showvault'));
    expect(text, contains("'showvault.windows-proof.v1'"));
    expect(text, contains('showvault-windows-proof-[0-9a-f]{32}'));
    expect(text, contains("'--showvault-upgrade-phase', \$Phase"));
    expect(text, contains("[ValidateSet('prepare', 'verify', 'cleanup')]"));
    expect(text, contains("-Phase 'prepare'"));
    expect(text, contains("-Phase 'verify'"));
    expect(text, contains("-Phase 'cleanup'"));
    expect(text, contains('Start-Process -FilePath \$FilePath'));
    expect(text, contains('-RedirectStandardOutput \$StandardOutput'));
    expect(text, contains('-RedirectStandardError \$StandardError'));
    expect(text, contains('-Wait -PassThru'));
    expect(text, isNot(contains('@(& \$InstalledExecutable')));
    expect(text, contains('unavailable-configuration'));
    expect(text, contains('command-exit'));
    expect(text, contains('missing-success-marker'));
    expect(text, contains('harness-prepare-failure'));
    expect(text, isNot(contains('throw \$Result.OutputLines')));
    expect(text, contains('sourcePresentDuringRehydration'));
    expect(text, contains('Get-AuthenticodeSignature'));
    expect(text, isNot(contains(r'Remove-Item -LiteralPath $env:USERPROFILE')));
  });

  test('Windows evidence workflow is manual, pinned, and synthetic', () {
    final text = workflow.readAsStringSync().replaceAll('\r\n', '\n');
    expect(text, contains('workflow_dispatch:'));
    expect(text, contains('runs-on: windows-2025'));
    expect(text, contains('permissions:\n  contents: read'));
    expect(text, contains('flutter-version: 3.44.8'));
    expect(text, contains(r'$vcpkgRoot = $env:VCPKG_INSTALLATION_ROOT'));
    expect(text, contains(r'"SHOWVAULT_RUNNER_VCPKG_ROOT=$vcpkgRoot"'));
    expect(text, contains(r'$env:GITHUB_ENV'));
    expect(text, contains(r"'^[A-Za-z]:[\\/][^\r\n]+$'"));
    expect(text, contains("Join-Path \$vcpkgRoot 'vcpkg.exe'"));
    expect(
      text,
      contains("Join-Path \$vcpkgRoot 'scripts\\buildsystems\\vcpkg.cmake'"),
    );
    expect(
      text,
      contains(
        'The controlled runner did not provide a local '
        'VCPKG_INSTALLATION_ROOT.',
      ),
    );
    expect(
      text,
      contains('The controlled runner vcpkg installation is incomplete.'),
    );
    expect(text, contains('The controlled runner vcpkg executable failed.'));
    expect(
      text,
      contains(
        'VCPKG_INSTALLATION_ROOT was redirected after toolchain validation.',
      ),
    );
    expect(
      text,
      contains(
        "\$pinnedVcpkgCommit = "
        "'fa9a5b330aed997a68310ed56418617b87a3b83d'",
      ),
    );
    expect(text, contains('https://github.com/microsoft/vcpkg.git'));
    expect(
      text,
      contains(r'fetch --quiet --depth 1 origin $pinnedVcpkgCommit'),
    );
    expect(text, contains('checkout --quiet --detach FETCH_HEAD'));
    expect(
      text,
      contains('The pinned vcpkg checkout did not match the approved commit.'),
    );
    expect(text, contains("'bootstrap-vcpkg.bat'"));
    expect(text, contains(r'& $bootstrapVcpkg -disableMetrics'));
    expect(text, contains(r'"VCPKG_ROOT=$vcpkgRoot"'));
    expect(text, contains(r'"SHOWVAULT_PINNED_VCPKG_ROOT=$vcpkgRoot"'));
    expect(
      text,
      contains(
        'VCPKG_ROOT was redirected after pinned dependency installation.',
      ),
    );
    final nativePorts = RegExp(
      r"^            '([a-z0-9-]+)',?$",
      multiLine: true,
    ).allMatches(text).map((match) => match.group(1)).toList();
    expect(
      nativePorts,
      equals(<String>[
        'cpprestsdk',
        'openssl',
        'boost-system',
        'boost-date-time',
        'boost-regex',
      ]),
    );
    expect(
      text,
      contains('The pinned vcpkg checkout does not contain required port'),
    );
    expect(text, isNot(contains('refs/heads/master')));
    expect(text, isNot(contains('refs/heads/main')));
    final nativePackages = RegExp(
      r"'([a-z0-9-]+:x64-windows)'",
    ).allMatches(text).map((match) => match.group(1)).toList();
    expect(
      nativePackages,
      equals(<String>[
        'cpprestsdk:x64-windows',
        'openssl:x64-windows',
        'boost-system:x64-windows',
        'boost-date-time:x64-windows',
        'boost-regex:x64-windows',
      ]),
    );
    final nativeInstallIndex = text.indexOf(
      r'& $vcpkg install @requiredNativePackages',
    );
    final nativeListIndex = text.indexOf(
      r'$installedNativePackages = @(& $vcpkg list)',
    );
    final flutterVerificationIndex = text.indexOf(
      '- name: Run Windows Flutter verification',
    );
    final pinnedRootRecheckIndex = text.indexOf(
      r'$env:SHOWVAULT_PINNED_VCPKG_ROOT',
      nativeListIndex,
    );
    final packageBuildIndex = text.indexOf(
      '- name: Build normal current-user package',
    );
    expect(
      nativeInstallIndex,
      greaterThan(
        text.indexOf('- name: Install pinned Windows native dependencies'),
      ),
    );
    expect(nativeListIndex, greaterThan(nativeInstallIndex));
    expect(pinnedRootRecheckIndex, greaterThan(nativeListIndex));
    expect(flutterVerificationIndex, greaterThan(nativeListIndex));
    expect(pinnedRootRecheckIndex, greaterThan(flutterVerificationIndex));
    expect(packageBuildIndex, greaterThan(flutterVerificationIndex));
    expect(
      text,
      contains(
        'The controlled runner failed to install the pinned Windows native '
        'dependencies.',
      ),
    );
    expect(
      text,
      contains('The controlled runner did not install required package'),
    );
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
