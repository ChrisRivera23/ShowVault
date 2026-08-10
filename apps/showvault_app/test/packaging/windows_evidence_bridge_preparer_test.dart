import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/recovery/windows_evidence_bridge_preparer.dart';

const _sourceCommit = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';

void main() {
  late Directory testRoot;
  late File sourceWorkflow;
  late String sourceText;

  setUp(() async {
    testRoot = await Directory.systemTemp.createTemp(
      'showvault-windows-bridge-preparer-',
    );
    sourceWorkflow = File(
      '${Directory.current.path}/../../.github/workflows/windows-evidence.yml',
    );
    sourceText = await sourceWorkflow.readAsString();
  });

  tearDown(() async {
    if (await testRoot.exists()) await testRoot.delete(recursive: true);
  });

  test(
    'creates one deterministic workflow with an immutable checkout',
    () async {
      final output = File('${testRoot.path}/windows-evidence.yml');

      final result = await const WindowsEvidenceBridgePreparer().prepare(
        sourceWorkflow: sourceWorkflow,
        sourceCommitSha: _sourceCommit,
        outputWorkflow: output,
      );
      final text = await output.readAsString();

      expect(text, contains('ref: $_sourceCommit'));
      expect(text, contains('persist-credentials: false'));
      expect(
        RegExp(r'^\s+ref:', multiLine: true).allMatches(text),
        hasLength(1),
      );
      expect(
        RegExp(r'^\s+persist-credentials:', multiLine: true).allMatches(text),
        hasLength(1),
      );
      expect(result.workflowSha256, hasLength(64));
      expect(result.toJson()['changedFileCount'], 1);
    },
  );

  test('renders identical bytes for the same source and commit', () {
    const preparer = WindowsEvidenceBridgePreparer();

    final first = preparer.render(
      sourceWorkflowText: sourceText,
      sourceCommitSha: _sourceCommit,
    );
    final second = preparer.render(
      sourceWorkflowText: sourceText,
      sourceCommitSha: _sourceCommit,
    );

    expect(second, first);
  });

  test('rejects a mutable or abbreviated source revision', () {
    expect(
      () => const WindowsEvidenceBridgePreparer().render(
        sourceWorkflowText: sourceText,
        sourceCommitSha: 'main',
      ),
      throwsA(isA<FormatException>()),
    );
    expect(
      () => const WindowsEvidenceBridgePreparer().render(
        sourceWorkflowText: sourceText,
        sourceCommitSha: 'a' * 12,
      ),
      throwsA(isA<FormatException>()),
    );
  });

  test('rejects automatic triggers and extra third-party actions', () {
    final automatic = sourceText.replaceFirst(
      '  workflow_dispatch:',
      '  workflow_dispatch:\n  push:',
    );
    final extraAction = sourceText.replaceFirst(
      '    steps:',
      '    steps:\n      - uses: example/unsafe@${'b' * 40}',
    );

    expect(
      () => const WindowsEvidenceBridgePreparer().render(
        sourceWorkflowText: automatic,
        sourceCommitSha: _sourceCommit,
      ),
      throwsA(isA<FormatException>()),
    );
    expect(
      () => const WindowsEvidenceBridgePreparer().render(
        sourceWorkflowText: extraAction,
        sourceCommitSha: _sourceCommit,
      ),
      throwsA(isA<FormatException>()),
    );
  });

  test('rejects a source workflow that already chooses a ref', () {
    final prePinned = sourceText.replaceFirst(
      '        uses: actions/checkout@',
      '        with:\n          ref: $_sourceCommit\n'
          '        uses: actions/checkout@',
    );

    expect(
      () => const WindowsEvidenceBridgePreparer().render(
        sourceWorkflowText: prePinned,
        sourceCommitSha: _sourceCommit,
      ),
      throwsA(isA<FormatException>()),
    );
  });

  test('refuses to overwrite an existing output workflow', () async {
    final output = File('${testRoot.path}/windows-evidence.yml');
    await output.writeAsString('preserve me');

    await expectLater(
      const WindowsEvidenceBridgePreparer().prepare(
        sourceWorkflow: sourceWorkflow,
        sourceCommitSha: _sourceCommit,
        outputWorkflow: output,
      ),
      throwsA(isA<FormatException>()),
    );
    expect(await output.readAsString(), 'preserve me');
  });

  test(
    'rejects a linked source workflow without following it',
    () async {
      final link = Link('${testRoot.path}/source.yml');
      await link.create(sourceWorkflow.path);
      final output = File('${testRoot.path}/windows-evidence.yml');

      await expectLater(
        const WindowsEvidenceBridgePreparer().prepare(
          sourceWorkflow: File(link.path),
          sourceCommitSha: _sourceCommit,
          outputWorkflow: output,
        ),
        throwsA(isA<FormatException>()),
      );
      expect(await output.exists(), isFalse);
    },
    skip: Platform.isWindows
        ? 'Creating symlinks requires privileges on some Windows runners.'
        : false,
  );
}
