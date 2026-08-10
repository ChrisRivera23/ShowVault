import 'package:flutter_test/flutter_test.dart';

import '../../tool/src/local_first_integration_preflight.dart';

void main() {
  test('accepts the exact bounded integration metrics', () {
    final metrics = Map<String, Object>.from(
      LocalFirstIntegrationPreflight.expectedMetrics,
    );

    expect(
      () => LocalFirstIntegrationPreflight.validateMetrics(metrics),
      returnsNormally,
    );
    final report = IntegrationPreflightReport(metrics).toJson();
    expect(report['verified'], isTrue);
    expect(report['selectedCommitCount'], 52);
    expect(report['selectedPathCount'], 136);
    expect(report['legacyOverlapPathCount'], 29);
    expect(report['externalStateRead'], isFalse);
    expect(report['repositoryMutation'], isFalse);
  });

  test('rejects changed topology counts', () {
    final metrics = Map<String, Object>.from(
      LocalFirstIntegrationPreflight.expectedMetrics,
    );
    metrics['milestone3.netFiles'] = 32;

    expect(
      () => LocalFirstIntegrationPreflight.validateMetrics(metrics),
      throwsA(
        isA<FormatException>().having(
          (error) => error.message,
          'message',
          contains('milestone3.netFiles'),
        ),
      ),
    );
  });

  test('rejects changed interleaved exclusions', () {
    final metrics = Map<String, Object>.from(
      LocalFirstIntegrationPreflight.expectedMetrics,
    );
    metrics['windows.excludedCommits'] = <String>['626e88d'];

    expect(
      () => LocalFirstIntegrationPreflight.validateMetrics(metrics),
      throwsA(isA<FormatException>()),
    );
  });

  test('rejects unexpected metrics', () {
    final metrics = Map<String, Object>.from(
      LocalFirstIntegrationPreflight.expectedMetrics,
    );
    metrics['unbounded.detail'] = 1;

    expect(
      () => LocalFirstIntegrationPreflight.validateMetrics(metrics),
      throwsA(isA<FormatException>()),
    );
  });
}
