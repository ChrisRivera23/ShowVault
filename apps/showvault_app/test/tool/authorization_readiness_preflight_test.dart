import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

import '../../tool/src/authorization_readiness_preflight.dart';

void main() {
  late String matrix;

  setUpAll(() {
    matrix = File(
      '../../docs/LOCAL_AUTHORIZATION_READINESS_MATRIX.md',
    ).readAsStringSync();
  });

  test('accepts the exact fail-closed authorization matrix', () {
    expect(
      () => AuthorizationReadinessPreflight.validateContent(matrix),
      returnsNormally,
    );
    final report = const AuthorizationReadinessReport().toJson();
    expect(report['verified'], isTrue);
    expect(report['operationCount'], 15);
    expect(report['locallyReadyOperationCount'], 2);
    expect(report['blockedOperationCount'], 13);
    expect(report['externalActionAuthorized'], isFalse);
    expect(report['externalStateRead'], isFalse);
    expect(report['repositoryMutation'], isFalse);
  });

  test('rejects a removed operation', () {
    final changed = matrix.replaceFirst('| X5 |', '| ZZ |');

    expect(
      () => AuthorizationReadinessPreflight.validateContent(changed),
      throwsA(isA<FormatException>()),
    );
  });

  test('rejects a weakened external approval', () {
    final changed = matrix.replaceFirst(
      'Separate authorization for exactly one manual run',
      'None',
    );

    expect(
      () => AuthorizationReadinessPreflight.validateContent(changed),
      throwsA(
        isA<FormatException>().having(
          (error) => error.message,
          'message',
          contains('X5'),
        ),
      ),
    );
  });

  test('rejects broadening the locally ready set', () {
    final changed = matrix.replaceFirst(
      'Only L1 and requested, bounded L2 work are currently ready.',
      'L1, L2, and X2 are currently ready.',
    );

    expect(
      () => AuthorizationReadinessPreflight.validateContent(changed),
      throwsA(isA<FormatException>()),
    );
  });
}
