import 'dart:convert';
import 'dart:io';

import 'windows_evidence_verifier.dart';

const _repository = 'ChrisRivera23/ShowVault';
const _workflowPath = '.github/workflows/windows-evidence.yml';
const _workflowName = 'Controlled Windows evidence';
const _artifactName = 'showvault-controlled-windows-evidence';

final _runIdPattern = RegExp(r'^[1-9][0-9]{0,19}$');
final _commitPattern = RegExp(r'^[0-9a-f]{40}$');

typedef WindowsEvidenceCommandRunner =
    Future<ProcessResult> Function(String executable, List<String> arguments);
typedef WindowsArtifactVerificationRunner =
    Future<WindowsEvidenceVerification> Function(Directory artifactRoot);

class WindowsEvidenceRunVerifier {
  WindowsEvidenceRunVerifier({
    WindowsEvidenceCommandRunner? commandRunner,
    WindowsArtifactVerificationRunner? artifactVerifier,
  }) : _commandRunner = commandRunner ?? _runCommand,
       _artifactVerifier =
           artifactVerifier ?? const WindowsEvidenceVerifier().verify;

  final WindowsEvidenceCommandRunner _commandRunner;
  final WindowsArtifactVerificationRunner _artifactVerifier;

  Future<WindowsEvidenceRunVerification> verify({
    required String runId,
    required Directory outputDirectory,
  }) async {
    _require(_runIdPattern.hasMatch(runId), 'The workflow run ID is invalid.');
    _require(
      await FileSystemEntity.type(outputDirectory.path, followLinks: false) ==
          FileSystemEntityType.notFound,
      'The artifact output directory must not already exist.',
    );

    final runText = await _runGh([
      'run',
      'view',
      runId,
      '--repo',
      _repository,
      '--json',
      'attempt,conclusion,databaseId,event,headSha,status,workflowName',
    ], 'run inspection');
    final run = _decodeObject(runText);
    _requireExactKeys(run, {
      'attempt',
      'conclusion',
      'databaseId',
      'event',
      'headSha',
      'status',
      'workflowName',
    });
    final attempt = run['attempt'];
    final databaseId = run['databaseId'];
    final workflowCommitSha = run['headSha'];
    _require(
      attempt is int &&
          attempt > 0 &&
          attempt <= 100 &&
          databaseId is int &&
          databaseId.toString() == runId &&
          run['conclusion'] == 'success' &&
          run['status'] == 'completed' &&
          run['event'] == 'workflow_dispatch' &&
          workflowCommitSha is String &&
          _commitPattern.hasMatch(workflowCommitSha) &&
          run['workflowName'] == _workflowName,
      'The GitHub Actions run is not a completed successful manual Windows evidence run.',
    );

    final workflowResponseText = await _runGh([
      'api',
      '-X',
      'GET',
      'repos/$_repository/contents/$_workflowPath',
      '-f',
      'ref=$workflowCommitSha',
    ], 'workflow revision inspection');
    final workflowResponse = _decodeObject(workflowResponseText);
    final encoding = workflowResponse['encoding'];
    final encodedContent = workflowResponse['content'];
    _require(
      encoding == 'base64' &&
          encodedContent is String &&
          encodedContent.length <= 1024 * 1024,
      'The workflow revision response is invalid.',
    );
    final workflowText = _decodeBase64Utf8(encodedContent as String);
    final sourceCommitSha = _verifyWorkflowRevision(workflowText);

    await _runGh([
      'run',
      'download',
      runId,
      '--repo',
      _repository,
      '--name',
      _artifactName,
      '--dir',
      outputDirectory.path,
    ], 'artifact download');
    _require(
      await FileSystemEntity.type(outputDirectory.path, followLinks: false) ==
          FileSystemEntityType.directory,
      'The downloaded artifact directory is missing or substituted.',
    );
    final artifact = await _artifactVerifier(outputDirectory);
    _require(
      artifact.provenance.workflowRunId == runId &&
          artifact.provenance.workflowRunAttempt == attempt &&
          artifact.provenance.sourceCommitSha == sourceCommitSha,
      'The downloaded artifact provenance does not match the GitHub run and workflow revision.',
    );

    return WindowsEvidenceRunVerification(
      workflowRunId: runId,
      workflowRunAttempt: attempt as int,
      workflowCommitSha: workflowCommitSha as String,
      sourceCommitSha: sourceCommitSha,
      artifact: artifact,
    );
  }

  Future<String> _runGh(List<String> arguments, String operation) async {
    final result = await _commandRunner('gh', arguments);
    _require(
      result.exitCode == 0 && result.stdout is String,
      'The GitHub $operation command failed.',
    );
    return result.stdout as String;
  }
}

class WindowsEvidenceRunVerification {
  const WindowsEvidenceRunVerification({
    required this.workflowRunId,
    required this.workflowRunAttempt,
    required this.workflowCommitSha,
    required this.sourceCommitSha,
    required this.artifact,
  });

  final String workflowRunId;
  final int workflowRunAttempt;
  final String workflowCommitSha;
  final String sourceCommitSha;
  final WindowsEvidenceVerification artifact;

  Map<String, Object?> toJson() => {
    'formatVersion': 'showvault.windows-run-verification.v1',
    'verified': true,
    'workflowRunId': workflowRunId,
    'workflowRunAttempt': workflowRunAttempt,
    'workflowCommitSha': workflowCommitSha,
    'sourceCommitSha': sourceCommitSha,
    'artifact': artifact.toJson(),
  };
}

String _verifyWorkflowRevision(String workflowText) {
  _require(
    workflowText.length <= 1024 * 1024 &&
        workflowText.contains('name: $_workflowName') &&
        workflowText.contains('workflow_dispatch:') &&
        !workflowText.contains('pull_request:') &&
        !workflowText.contains('push:') &&
        workflowText.contains('permissions:\n  contents: read') &&
        workflowText.contains('Record workflow provenance') &&
        workflowText.contains('windows-workflow-provenance.json') &&
        workflowText.contains('persist-credentials: false'),
    'The workflow revision does not preserve the manual provenance boundary.',
  );
  final refMatches = RegExp(
    r'^\s+ref: ([0-9a-f]{40})\s*$',
    multiLine: true,
  ).allMatches(workflowText).toList(growable: false);
  _require(
    refMatches.length == 1,
    'The workflow revision must pin exactly one source commit.',
  );
  return refMatches.single.group(1)!;
}

String _decodeBase64Utf8(String content) {
  try {
    return utf8.decode(base64Decode(content.replaceAll(RegExp(r'\s'), '')));
  } on FormatException {
    throw const FormatException('The workflow revision content is invalid.');
  }
}

Map<String, Object?> _decodeObject(String text) {
  final value = jsonDecode(text);
  _require(value is Map<String, Object?>, 'A GitHub response is invalid.');
  return value as Map<String, Object?>;
}

void _requireExactKeys(Map<String, Object?> value, Set<String> expected) {
  _require(
    value.keys.toSet().containsAll(expected) && value.length == expected.length,
    'A GitHub response has an unexpected schema.',
  );
}

Future<ProcessResult> _runCommand(String executable, List<String> arguments) =>
    Process.run(executable, arguments);

void _require(bool condition, String message) {
  if (!condition) throw FormatException(message);
}
