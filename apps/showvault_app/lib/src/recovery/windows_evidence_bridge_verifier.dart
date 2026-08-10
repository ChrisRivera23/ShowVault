import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';

import 'windows_evidence_bridge_preparer.dart';

class WindowsEvidenceBridgeVerifier {
  const WindowsEvidenceBridgeVerifier({
    this.preparer = const WindowsEvidenceBridgePreparer(),
  });

  final WindowsEvidenceBridgePreparer preparer;

  Future<WindowsEvidenceBridgeVerification> verify({
    required File sourceWorkflow,
    required String sourceCommitSha,
    required File bridgeWorkflow,
  }) async {
    await _requireRegularBoundedWorkflow(
      sourceWorkflow,
      'The source workflow must be a bounded regular file.',
    );
    await _requireRegularBoundedWorkflow(
      bridgeWorkflow,
      'The bridge workflow must be a bounded regular file.',
    );
    _require(
      bridgeWorkflow.uri.pathSegments.last == 'windows-evidence.yml',
      'The bridge filename must be windows-evidence.yml.',
    );

    final sourceText = await sourceWorkflow.readAsString();
    final expected = preparer.render(
      sourceWorkflowText: sourceText,
      sourceCommitSha: sourceCommitSha,
    );
    final actual = await bridgeWorkflow.readAsString();
    _require(
      actual == expected,
      'The bridge workflow does not exactly match the deterministic output.',
    );

    return WindowsEvidenceBridgeVerification(
      sourceCommitSha: sourceCommitSha,
      workflowSha256: sha256.convert(utf8.encode(actual)).toString(),
    );
  }
}

class WindowsEvidenceBridgeVerification {
  const WindowsEvidenceBridgeVerification({
    required this.sourceCommitSha,
    required this.workflowSha256,
  });

  final String sourceCommitSha;
  final String workflowSha256;

  Map<String, Object?> toJson() => {
    'formatVersion': 'showvault.windows-evidence-bridge-verification.v1',
    'verified': true,
    'sourceCommitSha': sourceCommitSha,
    'workflowSha256': workflowSha256,
    'exactDeterministicMatch': true,
    'changedFileCount': 1,
    'workflowPath': '.github/workflows/windows-evidence.yml',
  };
}

Future<void> _requireRegularBoundedWorkflow(File file, String message) async {
  _require(
    await FileSystemEntity.type(file.path, followLinks: false) ==
            FileSystemEntityType.file &&
        await file.length() <= 1024 * 1024,
    message,
  );
}

void _require(bool condition, String message) {
  if (!condition) throw FormatException(message);
}
