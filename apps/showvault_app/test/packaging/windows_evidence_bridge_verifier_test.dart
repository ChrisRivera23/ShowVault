import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/recovery/windows_evidence_bridge_preparer.dart';
import 'package:showvault_app/src/recovery/windows_evidence_bridge_verifier.dart';

const _sourceCommit = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';

void main() {
  late Directory testRoot;
  late File sourceWorkflow;
  late File bridgeWorkflow;
  late String expectedBridge;

  setUp(() async {
    testRoot = await Directory.systemTemp.createTemp(
      'showvault-windows-bridge-verifier-',
    );
    sourceWorkflow = File(
      '${Directory.current.path}/../../.github/workflows/windows-evidence.yml',
    );
    expectedBridge = const WindowsEvidenceBridgePreparer().render(
      sourceWorkflowText: await sourceWorkflow.readAsString(),
      sourceCommitSha: _sourceCommit,
    );
    bridgeWorkflow = File('${testRoot.path}/windows-evidence.yml');
    await bridgeWorkflow.writeAsString(expectedBridge);
  });

  tearDown(() async {
    if (await testRoot.exists()) await testRoot.delete(recursive: true);
  });

  test('verifies an exact deterministic bridge', () async {
    final result = await const WindowsEvidenceBridgeVerifier().verify(
      sourceWorkflow: sourceWorkflow,
      sourceCommitSha: _sourceCommit,
      bridgeWorkflow: bridgeWorkflow,
    );

    expect(result.sourceCommitSha, _sourceCommit);
    expect(result.workflowSha256, hasLength(64));
    expect(result.toJson()['exactDeterministicMatch'], isTrue);
    expect(result.toJson()['changedFileCount'], 1);
  });

  test('rejects a changed source pin', () async {
    await bridgeWorkflow.writeAsString(
      expectedBridge.replaceFirst(_sourceCommit, 'b' * 40),
    );

    await expectLater(
      const WindowsEvidenceBridgeVerifier().verify(
        sourceWorkflow: sourceWorkflow,
        sourceCommitSha: _sourceCommit,
        bridgeWorkflow: bridgeWorkflow,
      ),
      throwsA(isA<FormatException>()),
    );
  });

  test('rejects any additional workflow content', () async {
    await bridgeWorkflow.writeAsString(
      '$expectedBridge\n# unreviewed change\n',
    );

    await expectLater(
      const WindowsEvidenceBridgeVerifier().verify(
        sourceWorkflow: sourceWorkflow,
        sourceCommitSha: _sourceCommit,
        bridgeWorkflow: bridgeWorkflow,
      ),
      throwsA(isA<FormatException>()),
    );
  });

  test('rejects line-ending substitution', () async {
    await bridgeWorkflow.writeAsString(expectedBridge.replaceAll('\n', '\r\n'));

    await expectLater(
      const WindowsEvidenceBridgeVerifier().verify(
        sourceWorkflow: sourceWorkflow,
        sourceCommitSha: _sourceCommit,
        bridgeWorkflow: bridgeWorkflow,
      ),
      throwsA(isA<FormatException>()),
    );
  });

  test('rejects a bridge with a substituted filename', () async {
    final substituted = File('${testRoot.path}/other.yml');
    await substituted.writeAsString(expectedBridge);

    await expectLater(
      const WindowsEvidenceBridgeVerifier().verify(
        sourceWorkflow: sourceWorkflow,
        sourceCommitSha: _sourceCommit,
        bridgeWorkflow: substituted,
      ),
      throwsA(isA<FormatException>()),
    );
  });

  test(
    'rejects a linked bridge without following it',
    () async {
      final target = File('${testRoot.path}/target.yml');
      await target.writeAsString(expectedBridge);
      await bridgeWorkflow.delete();
      final link = Link(bridgeWorkflow.path);
      await link.create(target.path);

      await expectLater(
        const WindowsEvidenceBridgeVerifier().verify(
          sourceWorkflow: sourceWorkflow,
          sourceCommitSha: _sourceCommit,
          bridgeWorkflow: File(link.path),
        ),
        throwsA(isA<FormatException>()),
      );
    },
    skip: Platform.isWindows
        ? 'Creating symlinks requires privileges on some Windows runners.'
        : false,
  );
}
