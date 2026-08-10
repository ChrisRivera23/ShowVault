import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/recovery/windows_evidence_run_verifier.dart';
import 'package:showvault_app/src/recovery/windows_evidence_verifier.dart';

const _runId = '31423727118';
const _workflowCommit = 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb';
const _sourceCommit = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';

void main() {
  late Directory testRoot;

  setUp(() async {
    testRoot = await Directory.systemTemp.createTemp(
      'showvault-windows-run-verifier-',
    );
  });

  tearDown(() async {
    if (await testRoot.exists()) await testRoot.delete(recursive: true);
  });

  test(
    'attests the run, immutable workflow, and downloaded artifact',
    () async {
      final runner = _FakeGhRunner(
        outputDirectory: Directory('${testRoot.path}/download'),
      );
      final verifier = WindowsEvidenceRunVerifier(
        commandRunner: runner.call,
        artifactVerifier: (_) async => _artifactVerification(),
      );

      final result = await verifier.verify(
        runId: _runId,
        outputDirectory: runner.outputDirectory,
      );

      expect(result.workflowRunId, _runId);
      expect(result.workflowRunAttempt, 2);
      expect(result.workflowCommitSha, _workflowCommit);
      expect(result.sourceCommitSha, _sourceCommit);
      expect(runner.downloadCalls, 1);
      expect(result.toJson()['verified'], isTrue);
    },
  );

  test('rejects a non-manual or unsuccessful run before download', () async {
    final runner = _FakeGhRunner(
      outputDirectory: Directory('${testRoot.path}/download'),
      runOverrides: {'event': 'push', 'conclusion': 'failure'},
    );
    final verifier = WindowsEvidenceRunVerifier(
      commandRunner: runner.call,
      artifactVerifier: (_) async => _artifactVerification(),
    );

    await expectLater(
      verifier.verify(runId: _runId, outputDirectory: runner.outputDirectory),
      throwsA(isA<FormatException>()),
    );
    expect(runner.downloadCalls, 0);
  });

  test('rejects artifact provenance from a different source pin', () async {
    final runner = _FakeGhRunner(
      outputDirectory: Directory('${testRoot.path}/download'),
    );
    final verifier = WindowsEvidenceRunVerifier(
      commandRunner: runner.call,
      artifactVerifier: (_) async => _artifactVerification(
        sourceCommitSha: 'cccccccccccccccccccccccccccccccccccccccc',
      ),
    );

    await expectLater(
      verifier.verify(runId: _runId, outputDirectory: runner.outputDirectory),
      throwsA(
        isA<FormatException>().having(
          (error) => error.message,
          'message',
          contains('does not match'),
        ),
      ),
    );
  });

  test('rejects artifact provenance from a different run attempt', () async {
    final runner = _FakeGhRunner(
      outputDirectory: Directory('${testRoot.path}/download'),
    );
    final verifier = WindowsEvidenceRunVerifier(
      commandRunner: runner.call,
      artifactVerifier: (_) async => _artifactVerification(attempt: 1),
    );

    await expectLater(
      verifier.verify(runId: _runId, outputDirectory: runner.outputDirectory),
      throwsA(isA<FormatException>()),
    );
  });

  test('rejects a workflow revision with a mutable source ref', () async {
    final runner = _FakeGhRunner(
      outputDirectory: Directory('${testRoot.path}/download'),
      workflowText: _workflowText.replaceFirst(
        'ref: $_sourceCommit',
        'ref: main',
      ),
    );
    final verifier = WindowsEvidenceRunVerifier(
      commandRunner: runner.call,
      artifactVerifier: (_) async => _artifactVerification(),
    );

    await expectLater(
      verifier.verify(runId: _runId, outputDirectory: runner.outputDirectory),
      throwsA(isA<FormatException>()),
    );
    expect(runner.downloadCalls, 0);
  });

  test('rejects an existing output directory before invoking GitHub', () async {
    final output = Directory('${testRoot.path}/download');
    await output.create();
    final runner = _FakeGhRunner(outputDirectory: output);
    final verifier = WindowsEvidenceRunVerifier(
      commandRunner: runner.call,
      artifactVerifier: (_) async => _artifactVerification(),
    );

    await expectLater(
      verifier.verify(runId: _runId, outputDirectory: output),
      throwsA(isA<FormatException>()),
    );
    expect(runner.callCount, 0);
  });
}

class _FakeGhRunner {
  _FakeGhRunner({
    required this.outputDirectory,
    this.runOverrides = const {},
    this.workflowText = _workflowText,
  });

  final Directory outputDirectory;
  final Map<String, Object?> runOverrides;
  final String workflowText;
  int callCount = 0;
  int downloadCalls = 0;

  Future<ProcessResult> call(String executable, List<String> arguments) async {
    callCount++;
    expect(executable, 'gh');
    if (arguments.length >= 2 &&
        arguments[0] == 'run' &&
        arguments[1] == 'view') {
      expect(arguments[2], _runId);
      return _success(
        jsonEncode({
          'attempt': 2,
          'conclusion': 'success',
          'databaseId': int.parse(_runId),
          'event': 'workflow_dispatch',
          'headSha': _workflowCommit,
          'status': 'completed',
          'workflowName': 'Controlled Windows evidence',
          ...runOverrides,
        }),
      );
    }
    if (arguments.isNotEmpty && arguments.first == 'api') {
      expect(arguments, contains('ref=$_workflowCommit'));
      return _success(
        jsonEncode({
          'encoding': 'base64',
          'content': base64Encode(utf8.encode(workflowText)),
        }),
      );
    }
    if (arguments.length >= 2 &&
        arguments[0] == 'run' &&
        arguments[1] == 'download') {
      downloadCalls++;
      expect(arguments, contains(_runId));
      expect(arguments, contains('showvault-controlled-windows-evidence'));
      expect(arguments, contains(outputDirectory.path));
      await outputDirectory.create();
      return _success('');
    }
    return ProcessResult(1, 1, '', 'unexpected command');
  }
}

ProcessResult _success(String stdout) => ProcessResult(1, 0, stdout, '');

WindowsEvidenceVerification _artifactVerification({
  String sourceCommitSha = _sourceCommit,
  int attempt = 2,
}) => WindowsEvidenceVerification(
  package: const WindowsPackageEvidence(
    appVersion: '0.1.0+1',
    deploymentFileCount: 17,
    installerSha256:
        '1111111111111111111111111111111111111111111111111111111111111111',
    archiveSha256:
        '2222222222222222222222222222222222222222222222222222222222222222',
    executableAuthenticodeStatus: 'NotSigned',
    installerAuthenticodeStatus: 'NotSigned',
  ),
  proof: const WindowsInstalledProofEvidence(
    operatingSystemVersion: '10.0.26100.0',
    architecture: 'AMD64',
    beforeInstallerSha256:
        '3333333333333333333333333333333333333333333333333333333333333333',
    afterInstallerSha256:
        '4444444444444444444444444444444444444444444444444444444444444444',
    reportEvidenceSha256:
        '5555555555555555555555555555555555555555555555555555555555555555',
    beforeInstallerAuthenticodeStatus: 'NotSigned',
    afterInstallerAuthenticodeStatus: 'NotSigned',
    installedExecutableAuthenticodeStatus: 'NotSigned',
  ),
  provenance: WindowsWorkflowProvenance(
    sourceCommitSha: sourceCommitSha,
    workflowRunId: _runId,
    workflowRunAttempt: attempt,
  ),
);

const _workflowText =
    '''
name: Controlled Windows evidence

on:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  package-and-prove:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
        with:
          ref: $_sourceCommit
          persist-credentials: false
      - name: Record workflow provenance
        run: windows-workflow-provenance.json
''';
