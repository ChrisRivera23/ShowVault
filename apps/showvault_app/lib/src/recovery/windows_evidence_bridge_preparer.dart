import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';

final _commitPattern = RegExp(r'^[0-9a-f]{40}$');
final _checkoutLinePattern = RegExp(
  r'^        uses: actions/checkout@([0-9a-f]{40}) # v4$',
  multiLine: true,
);
final _usesPattern = RegExp(
  r'^\s+(?:- )?uses: ([a-zA-Z0-9_.-]+/[a-zA-Z0-9_.-]+)@([0-9a-f]{40})(?: # .+)?$',
  multiLine: true,
);
final _anyUsesLinePattern = RegExp(
  r'^\s+(?:- )?uses:\s+\S+.*$',
  multiLine: true,
);

const _requiredActions = <String>{
  'actions/checkout',
  'actions/upload-artifact',
  'subosito/flutter-action',
};

class WindowsEvidenceBridgePreparer {
  const WindowsEvidenceBridgePreparer();

  Future<WindowsEvidenceBridgePreparation> prepare({
    required File sourceWorkflow,
    required String sourceCommitSha,
    required File outputWorkflow,
  }) async {
    _require(
      _commitPattern.hasMatch(sourceCommitSha),
      'The source commit must be a lowercase full Git SHA.',
    );
    _require(
      await FileSystemEntity.type(sourceWorkflow.path, followLinks: false) ==
          FileSystemEntityType.file,
      'The source workflow must be a regular file.',
    );
    _require(
      await sourceWorkflow.length() <= 1024 * 1024,
      'The source workflow is too large.',
    );
    _require(
      await FileSystemEntity.type(outputWorkflow.path, followLinks: false) ==
          FileSystemEntityType.notFound,
      'The output workflow must not already exist.',
    );
    _require(
      outputWorkflow.uri.pathSegments.last == 'windows-evidence.yml',
      'The output filename must be windows-evidence.yml.',
    );
    _require(
      await FileSystemEntity.type(
            outputWorkflow.parent.path,
            followLinks: false,
          ) ==
          FileSystemEntityType.directory,
      'The output parent must be an existing regular directory.',
    );

    final sourceText = await sourceWorkflow.readAsString();
    final bridgeText = render(
      sourceWorkflowText: sourceText,
      sourceCommitSha: sourceCommitSha,
    );

    await outputWorkflow.create(exclusive: true);
    await outputWorkflow.writeAsString(bridgeText, flush: true);
    _require(
      await outputWorkflow.readAsString() == bridgeText,
      'The written bridge workflow could not be verified.',
    );

    return WindowsEvidenceBridgePreparation(
      sourceCommitSha: sourceCommitSha,
      workflowSha256: sha256.convert(utf8.encode(bridgeText)).toString(),
    );
  }

  String render({
    required String sourceWorkflowText,
    required String sourceCommitSha,
  }) {
    _require(
      _commitPattern.hasMatch(sourceCommitSha),
      'The source commit must be a lowercase full Git SHA.',
    );
    final canonicalSource = _canonicalWorkflowText(sourceWorkflowText);
    _verifySourceWorkflow(canonicalSource);
    final matches = _checkoutLinePattern
        .allMatches(canonicalSource)
        .toList(growable: false);
    _require(
      matches.length == 1,
      'The source workflow must contain exactly one pinned checkout action.',
    );
    final checkoutLine = matches.single.group(0)!;
    final replacement =
        '''$checkoutLine
        with:
          ref: $sourceCommitSha
          persist-credentials: false''';
    final result = canonicalSource.replaceRange(
      matches.single.start,
      matches.single.end,
      replacement,
    );
    _verifyBridgeWorkflow(result, sourceCommitSha);
    return result;
  }
}

String _canonicalWorkflowText(String text) {
  final canonical = text.replaceAll('\r\n', '\n');
  _require(
    !canonical.contains('\r'),
    'The source workflow contains unsupported line endings.',
  );
  return canonical;
}

class WindowsEvidenceBridgePreparation {
  const WindowsEvidenceBridgePreparation({
    required this.sourceCommitSha,
    required this.workflowSha256,
  });

  final String sourceCommitSha;
  final String workflowSha256;

  Map<String, Object?> toJson() => {
    'formatVersion': 'showvault.windows-evidence-bridge.v1',
    'prepared': true,
    'sourceCommitSha': sourceCommitSha,
    'workflowSha256': workflowSha256,
    'changedFileCount': 1,
    'workflowPath': '.github/workflows/windows-evidence.yml',
  };
}

void _verifySourceWorkflow(String text) {
  _require(
    text.length <= 1024 * 1024 &&
        text.startsWith('name: Controlled Windows evidence\n') &&
        RegExp(
          r'^on:\n  workflow_dispatch:\s*$',
          multiLine: true,
        ).hasMatch(text) &&
        RegExp(
          r'^permissions:\n  contents: read\s*$',
          multiLine: true,
        ).hasMatch(text) &&
        text.contains('runs-on: windows-2025') &&
        text.contains('timeout-minutes: 90') &&
        text.contains('flutter-version: 3.44.8') &&
        text.contains('Record workflow provenance') &&
        text.contains('windows-workflow-provenance.json') &&
        text.contains('Verify checksums and cleanup') &&
        text.contains('retention-days: 14') &&
        !text.contains('secrets.') &&
        !RegExp(r'^\s+push:', multiLine: true).hasMatch(text) &&
        !RegExp(r'^\s+pull_request:', multiLine: true).hasMatch(text) &&
        !RegExp(r'^\s+ref:', multiLine: true).hasMatch(text) &&
        !RegExp(r'^\s+persist-credentials:', multiLine: true).hasMatch(text),
    'The source workflow does not preserve the controlled evidence policy.',
  );
  final actions = _usesPattern
      .allMatches(text)
      .map((match) => match.group(1)!)
      .toList(growable: false);
  _require(
    _anyUsesLinePattern.allMatches(text).length == _requiredActions.length &&
        actions.length == _requiredActions.length &&
        actions.toSet().containsAll(_requiredActions),
    'The source workflow must use only the three approved pinned actions.',
  );
}

void _verifyBridgeWorkflow(String text, String sourceCommitSha) {
  final refs = RegExp(
    r'^\s+ref: ([0-9a-f]{40})\s*$',
    multiLine: true,
  ).allMatches(text).toList(growable: false);
  final credentialControls = RegExp(
    r'^\s+persist-credentials: false\s*$',
    multiLine: true,
  ).allMatches(text).toList(growable: false);
  _require(
    refs.length == 1 &&
        refs.single.group(1) == sourceCommitSha &&
        credentialControls.length == 1,
    'The bridge workflow does not contain exactly one immutable checkout.',
  );
}

void _require(bool condition, String message) {
  if (!condition) throw FormatException(message);
}
