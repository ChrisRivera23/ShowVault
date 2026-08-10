import 'package:flutter_test/flutter_test.dart';

import '../../tool/src/pr_dependency_ledger_preflight.dart';

void main() {
  test('accepts the exact bounded PR dependency metrics', () {
    final metrics = Map<String, Object>.from(
      PrDependencyLedgerPreflight.expectedMetrics,
    );

    expect(
      () => PrDependencyLedgerPreflight.validateMetrics(metrics),
      returnsNormally,
    );
    final report = PrDependencyLedgerReport(metrics).toJson();
    expect(report['verified'], isTrue);
    expect(report['branchCount'], 22);
    expect(report['commitCount'], 32);
    expect(report['pathCount'], 237);
    expect(report['additionCount'], 16079);
    expect(report['deletionCount'], 106);
    expect(report['binaryPathCount'], 31);
    expect(report['externalStateRead'], isFalse);
    expect(report['repositoryMutation'], isFalse);
  });

  test('rejects a changed row head', () {
    final metrics = Map<String, Object>.from(
      PrDependencyLedgerPreflight.expectedMetrics,
    );
    metrics['pr12.head'] = '0000000';

    expect(
      () => PrDependencyLedgerPreflight.validateMetrics(metrics),
      throwsA(
        isA<FormatException>().having(
          (error) => error.message,
          'message',
          contains('pr12.head'),
        ),
      ),
    );
  });

  test('rejects broken ancestry', () {
    final metrics = Map<String, Object>.from(
      PrDependencyLedgerPreflight.expectedMetrics,
    );
    metrics['pr18.ancestor'] = false;

    expect(
      () => PrDependencyLedgerPreflight.validateMetrics(metrics),
      throwsA(isA<FormatException>()),
    );
  });

  test('rejects unexpected metrics', () {
    final metrics = Map<String, Object>.from(
      PrDependencyLedgerPreflight.expectedMetrics,
    );
    metrics['remote.mergeability'] = 'unknown';

    expect(
      () => PrDependencyLedgerPreflight.validateMetrics(metrics),
      throwsA(isA<FormatException>()),
    );
  });
}
