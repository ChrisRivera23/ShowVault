import 'local_first_integration_preflight.dart';
import 'pr_dependency_ledger_preflight.dart';

typedef JsonReport = Map<String, Object>;

class LocalIntegrationPlanPreflight {
  LocalIntegrationPlanPreflight({
    LocalFirstIntegrationPreflight? reconstructionPreflight,
    PrDependencyLedgerPreflight? foundationPreflight,
  }) : _reconstructionPreflight =
           reconstructionPreflight ?? LocalFirstIntegrationPreflight(),
       _foundationPreflight =
           foundationPreflight ?? PrDependencyLedgerPreflight();

  final LocalFirstIntegrationPreflight _reconstructionPreflight;
  final PrDependencyLedgerPreflight _foundationPreflight;

  static const expectedReconstruction = <String, Object>{
    'formatVersion': 'showvault.local-first-integration-preflight.v1',
    'verified': true,
    'selectedCommitCount': 52,
    'selectedPathCount': 136,
    'legacyOverlapPathCount': 29,
    'milestoneCount': 6,
    'excludedInterleavedCommitCount': 4,
    'externalStateRead': false,
    'repositoryMutation': false,
  };

  static const expectedFoundation = <String, Object>{
    'formatVersion': 'showvault.pr-dependency-ledger-preflight.v1',
    'verified': true,
    'branchCount': 22,
    'commitCount': 32,
    'pathCount': 237,
    'additionCount': 16079,
    'deletionCount': 106,
    'binaryPathCount': 31,
    'externalStateRead': false,
    'repositoryMutation': false,
  };

  Future<LocalIntegrationPlanReport> verify() async {
    final reconstruction = (await _reconstructionPreflight.verify()).toJson();
    final foundation = (await _foundationPreflight.verify()).toJson();
    validateReports(reconstruction: reconstruction, foundation: foundation);
    return const LocalIntegrationPlanReport();
  }

  static void validateReports({
    required JsonReport reconstruction,
    required JsonReport foundation,
  }) {
    _validateExact(
      name: 'reconstruction',
      actual: reconstruction,
      expected: expectedReconstruction,
    );
    _validateExact(
      name: 'foundation',
      actual: foundation,
      expected: expectedFoundation,
    );
  }

  static void _validateExact({
    required String name,
    required JsonReport actual,
    required JsonReport expected,
  }) {
    for (final entry in expected.entries) {
      if (actual[entry.key] != entry.value) {
        throw FormatException(
          'Combined integration plan mismatch for $name.${entry.key}: '
          'expected ${entry.value}, observed ${actual[entry.key]}.',
        );
      }
    }
    if (actual.keys.toSet().difference(expected.keys.toSet()).isNotEmpty) {
      throw FormatException(
        'Combined integration plan produced unexpected $name fields.',
      );
    }
  }
}

class LocalIntegrationPlanReport {
  const LocalIntegrationPlanReport();

  Map<String, Object> toJson() => {
    'formatVersion': 'showvault.local-integration-plan-preflight.v1',
    'localPlanVerified': true,
    'foundationBranchCount': 22,
    'foundationCommitCount': 32,
    'foundationPathCount': 237,
    'reconstructionCommitCount': 52,
    'reconstructionPathCount': 136,
    'legacyOverlapPathCount': 29,
    'externalAuthorizationEvaluated': false,
    'externalStateRead': false,
    'repositoryMutation': false,
  };
}
