import 'package:flutter_test/flutter_test.dart';

import '../../tool/src/local_integration_plan_preflight.dart';

void main() {
  test('accepts the exact two local Git reports', () {
    expect(
      () => LocalIntegrationPlanPreflight.validateReports(
        reconstruction: Map<String, Object>.from(
          LocalIntegrationPlanPreflight.expectedReconstruction,
        ),
        foundation: Map<String, Object>.from(
          LocalIntegrationPlanPreflight.expectedFoundation,
        ),
      ),
      returnsNormally,
    );

    final report = const LocalIntegrationPlanReport().toJson();
    expect(report['localPlanVerified'], isTrue);
    expect(report['foundationBranchCount'], 22);
    expect(report['reconstructionCommitCount'], 52);
    expect(report['externalAuthorizationEvaluated'], isFalse);
    expect(report['externalStateRead'], isFalse);
    expect(report['repositoryMutation'], isFalse);
  });

  test('rejects a changed reconstruction report', () {
    final reconstruction = Map<String, Object>.from(
      LocalIntegrationPlanPreflight.expectedReconstruction,
    );
    reconstruction['selectedPathCount'] = 137;

    expect(
      () => LocalIntegrationPlanPreflight.validateReports(
        reconstruction: reconstruction,
        foundation: Map<String, Object>.from(
          LocalIntegrationPlanPreflight.expectedFoundation,
        ),
      ),
      throwsA(
        isA<FormatException>().having(
          (error) => error.message,
          'message',
          contains('reconstruction.selectedPathCount'),
        ),
      ),
    );
  });

  test('rejects a changed foundation report', () {
    final foundation = Map<String, Object>.from(
      LocalIntegrationPlanPreflight.expectedFoundation,
    );
    foundation['binaryPathCount'] = 30;

    expect(
      () => LocalIntegrationPlanPreflight.validateReports(
        reconstruction: Map<String, Object>.from(
          LocalIntegrationPlanPreflight.expectedReconstruction,
        ),
        foundation: foundation,
      ),
      throwsA(isA<FormatException>()),
    );
  });

  test('rejects fields that could imply external authorization', () {
    final foundation = Map<String, Object>.from(
      LocalIntegrationPlanPreflight.expectedFoundation,
    );
    foundation['pushAuthorized'] = true;

    expect(
      () => LocalIntegrationPlanPreflight.validateReports(
        reconstruction: Map<String, Object>.from(
          LocalIntegrationPlanPreflight.expectedReconstruction,
        ),
        foundation: foundation,
      ),
      throwsA(isA<FormatException>()),
    );
  });
}
