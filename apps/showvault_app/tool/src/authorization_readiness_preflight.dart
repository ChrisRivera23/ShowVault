import 'dart:io';

class AuthorizationReadinessPreflight {
  static const maximumMatrixBytes = 64 * 1024;

  static const expectedApprovals = <String, String>{
    'L1': 'None',
    'L2': 'No additional approval within the requested scope',
    'L3': 'Product Owner request for that implementation slice',
    'X1':
        'Remote-state review authorization for the named repository and PR range',
    'X2': 'Push authorization naming that branch/head',
    'X3': 'PR mutation authorization naming the PR/branch and operation',
    'X4': 'Separate ready/merge authorization for that PR revision',
    'X5': 'Separate authorization for exactly one manual run',
    'X6': 'Authorization covering run-artifact retrieval/verification',
    'W1': 'Equipment authorization and scoped execution approval',
    'W2': 'Separate attended-equipment authorization',
    'M1': 'Scoped local execution approval',
    'C1': 'External-resource/deployment authorization',
    'V1': 'New explicit personal/venue authorization',
    'D1': 'Separate destructive approval naming the exact target',
  };

  Future<AuthorizationReadinessReport> verify(File matrixFile) async {
    final type = await FileSystemEntity.type(
      matrixFile.path,
      followLinks: false,
    );
    if (type != FileSystemEntityType.file) {
      throw const FormatException(
        'Authorization matrix must be a regular local file.',
      );
    }
    final length = await matrixFile.length();
    if (length == 0 || length > maximumMatrixBytes) {
      throw const FormatException('Authorization matrix size is invalid.');
    }
    final content = await matrixFile.readAsString();
    validateContent(content);
    return const AuthorizationReadinessReport();
  }

  static void validateContent(String content) {
    content = content.replaceAll('\r\n', '\n');
    if (content.contains('\r')) {
      throw const FormatException(
        'Authorization matrix contains unsupported line endings.',
      );
    }
    const tableStart = '## Operation-to-approval matrix';
    const tableEnd = '## Dependency sequence';
    final start = content.indexOf(tableStart);
    final end = content.indexOf(tableEnd);
    if (start < 0 || end <= start) {
      throw const FormatException(
        'Authorization matrix operation table is missing.',
      );
    }

    final observed = <String, String>{};
    for (final line in content.substring(start, end).split(RegExp(r'\r?\n'))) {
      final fields = line.split('|').map((field) => field.trim()).toList();
      if (fields.length != 7 || !expectedApprovals.containsKey(fields[1])) {
        continue;
      }
      final id = fields[1];
      if (observed.containsKey(id)) {
        throw FormatException('Authorization matrix duplicates operation $id.');
      }
      observed[id] = fields[4];
    }

    if (observed.keys.toList().join(',') !=
        expectedApprovals.keys.toList().join(',')) {
      throw const FormatException(
        'Authorization matrix operation IDs or order changed.',
      );
    }
    for (final entry in expectedApprovals.entries) {
      if (observed[entry.key] != entry.value) {
        throw FormatException(
          'Authorization matrix approval changed for ${entry.key}.',
        );
      }
    }

    const readyBoundary =
        'Only L1 and requested, bounded L2 work are currently ready.';
    const blockedBoundary =
        'No\nfetch, push, PR mutation, merge, dispatch, artifact retrieval, '
        'installed proof,\nequipment use, cloud action, venue access, or '
        'destructive cleanup is authorized\nby this matrix.';
    if (!content.contains(readyBoundary) ||
        !content.contains(blockedBoundary)) {
      throw const FormatException(
        'Authorization matrix current decision is not fail-closed.',
      );
    }
  }
}

class AuthorizationReadinessReport {
  const AuthorizationReadinessReport();

  Map<String, Object> toJson() => {
    'formatVersion': 'showvault.authorization-readiness-preflight.v1',
    'verified': true,
    'operationCount': 15,
    'locallyReadyOperationCount': 2,
    'blockedOperationCount': 13,
    'externalActionAuthorized': false,
    'externalStateRead': false,
    'repositoryMutation': false,
  };
}
