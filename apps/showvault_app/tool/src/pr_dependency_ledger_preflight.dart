import 'local_first_integration_preflight.dart' show GitProcessRunner;

typedef PrLedgerMetrics = Map<String, Object>;

class PrDependencyLedgerPreflight {
  PrDependencyLedgerPreflight({GitProcessRunner? gitRunner})
    : _gitRunner = gitRunner ?? const GitProcessRunner();

  final GitProcessRunner _gitRunner;

  static const _rows = <_PrLedgerRow>[
    _PrLedgerRow(
      3,
      'origin/main',
      'origin/codex/auth-tenancy-foundation',
      '236fa22',
      4,
      26,
      1176,
      9,
    ),
    _PrLedgerRow(
      4,
      'origin/codex/auth-tenancy-foundation',
      'origin/codex/agent-enrollment-identity',
      '6749d99',
      2,
      30,
      1783,
      12,
    ),
    _PrLedgerRow(
      5,
      'origin/codex/agent-enrollment-identity',
      'origin/codex/agent-outbound-queue',
      '7180241',
      1,
      20,
      1044,
      7,
    ),
    _PrLedgerRow(
      6,
      'origin/codex/agent-outbound-queue',
      'origin/codex/agent-command-delivery',
      '755859b',
      1,
      18,
      1146,
      13,
    ),
    _PrLedgerRow(
      7,
      'origin/codex/agent-command-delivery',
      'origin/codex/file-discovery-plugin',
      '8e79f7f',
      1,
      13,
      671,
      11,
    ),
    _PrLedgerRow(
      8,
      'origin/codex/file-discovery-plugin',
      'origin/codex/immutable-recovery-package',
      'dc8fd2c',
      1,
      13,
      749,
      42,
    ),
    _PrLedgerRow(
      9,
      'origin/codex/immutable-recovery-package',
      'origin/codex/package-verification',
      '4a1ab0f',
      1,
      10,
      746,
      10,
    ),
    _PrLedgerRow(
      10,
      'origin/codex/package-verification',
      'origin/codex/controlled-local-restore',
      'a354beb',
      1,
      11,
      958,
      12,
    ),
    _PrLedgerRow(
      11,
      'origin/codex/controlled-local-restore',
      'origin/codex/recovery-history-read-model',
      '9f8d4a3',
      1,
      13,
      789,
      83,
    ),
    _PrLedgerRow(
      12,
      'origin/codex/recovery-history-read-model',
      'origin/codex/flutter-auth0-live-history',
      'b42ef9b',
      2,
      121,
      4651,
      185,
    ),
    _PrLedgerRow(
      13,
      'origin/codex/flutter-auth0-live-history',
      'origin/codex/system-inventory-plugin',
      '36dc6c5',
      1,
      9,
      219,
      8,
    ),
    _PrLedgerRow(
      14,
      'origin/codex/system-inventory-plugin',
      'origin/codex/network-device-discovery',
      '3615d63',
      1,
      11,
      465,
      8,
    ),
    _PrLedgerRow(
      15,
      'origin/codex/network-device-discovery',
      'origin/codex/resolume-portable-bundle',
      '667767b',
      3,
      11,
      457,
      10,
    ),
    _PrLedgerRow(
      16,
      'origin/codex/resolume-portable-bundle',
      'origin/codex/resolume-user-data',
      '652df64',
      1,
      7,
      133,
      15,
    ),
    _PrLedgerRow(
      17,
      'origin/codex/resolume-user-data',
      'origin/codex/grandma-show-backups',
      'bf0d543',
      1,
      9,
      372,
      5,
    ),
    _PrLedgerRow(
      18,
      'origin/codex/grandma-show-backups',
      'origin/codex/yamaha-console-exports',
      '92dc57a',
      1,
      10,
      267,
      8,
    ),
    _PrLedgerRow(
      19,
      'origin/codex/yamaha-console-exports',
      'origin/codex/yamaha-clql-tf-exports',
      '893057a',
      1,
      10,
      173,
      23,
    ),
    _PrLedgerRow(
      20,
      'origin/codex/yamaha-clql-tf-exports',
      'origin/codex/yamaha-dm3-exports',
      '15f0b4f',
      1,
      9,
      99,
      8,
    ),
    _PrLedgerRow(
      21,
      'origin/codex/yamaha-dm3-exports',
      'origin/codex/yamaha-dsp-projects',
      'e5a1a08',
      2,
      10,
      224,
      6,
    ),
    _PrLedgerRow(
      22,
      'origin/codex/yamaha-dsp-projects',
      'origin/codex/yamaha-pc-amplifiers',
      'b25a6c5',
      1,
      7,
      87,
      4,
    ),
    _PrLedgerRow(
      23,
      'origin/codex/yamaha-pc-amplifiers',
      'origin/codex/yamaha-provisionaire-control',
      '725d9f0',
      2,
      9,
      164,
      5,
    ),
    _PrLedgerRow(
      24,
      'origin/codex/yamaha-provisionaire-control',
      'origin/codex/yamaha-dme5-dme3',
      '254cbbf',
      2,
      9,
      93,
      9,
    ),
  ];

  static final expectedMetrics = <String, Object>{
    for (final row in _rows) ...{
      'pr${row.number}.head': row.head,
      'pr${row.number}.commits': row.commits,
      'pr${row.number}.files': row.files,
      'pr${row.number}.additions': row.additions,
      'pr${row.number}.deletions': row.deletions,
      'pr${row.number}.binaryFiles': row.number == 12 ? 31 : 0,
      'pr${row.number}.ancestor': true,
    },
    'combined.branches': 22,
    'combined.commits': 32,
    'combined.files': 237,
    'combined.additions': 16079,
    'combined.deletions': 106,
    'combined.binaryFiles': 31,
  };

  Future<PrDependencyLedgerReport> verify() async {
    await _gitRunner.requireSuccess(['rev-parse', '--show-toplevel']);
    final metrics = <String, Object>{};

    for (final row in _rows) {
      final prefix = 'pr${row.number}';
      final fullHead = (await _gitRunner.requireSuccess([
        'rev-parse',
        row.targetRef,
      ])).trim();
      final ancestor = await _gitRunner.run([
        'merge-base',
        '--is-ancestor',
        row.baseRef,
        row.targetRef,
      ]);
      metrics['$prefix.head'] = fullHead.substring(0, 7);
      metrics['$prefix.ancestor'] = ancestor.exitCode == 0;
      metrics['$prefix.commits'] = await _integer([
        'rev-list',
        '--count',
        '${row.baseRef}..${row.targetRef}',
      ]);
      metrics['$prefix.files'] = (await _lines([
        'diff',
        '--name-only',
        '${row.baseRef}..${row.targetRef}',
      ])).length;
      final rowChanges = await _changes(row.baseRef, row.targetRef);
      metrics['$prefix.additions'] = rowChanges.additions;
      metrics['$prefix.deletions'] = rowChanges.deletions;
      metrics['$prefix.binaryFiles'] = rowChanges.binaryFiles;
    }

    const first = 'origin/main';
    const last = 'origin/codex/yamaha-dme5-dme3';
    metrics['combined.branches'] = _rows.length;
    metrics['combined.commits'] = await _integer([
      'rev-list',
      '--count',
      '$first..$last',
    ]);
    metrics['combined.files'] = (await _lines([
      'diff',
      '--name-only',
      '$first..$last',
    ])).length;
    final combinedChanges = await _changes(first, last);
    metrics['combined.additions'] = combinedChanges.additions;
    metrics['combined.deletions'] = combinedChanges.deletions;
    metrics['combined.binaryFiles'] = combinedChanges.binaryFiles;

    validateMetrics(metrics);
    return PrDependencyLedgerReport(metrics);
  }

  static void validateMetrics(PrLedgerMetrics actual) {
    for (final expected in expectedMetrics.entries) {
      final observed = actual[expected.key];
      if (observed != expected.value) {
        throw FormatException(
          'PR dependency ledger mismatch for ${expected.key}: '
          'expected ${expected.value}, observed $observed.',
        );
      }
    }
    final unexpected = actual.keys.toSet().difference(
      expectedMetrics.keys.toSet(),
    );
    if (unexpected.isNotEmpty) {
      throw const FormatException(
        'PR dependency ledger produced unexpected metrics.',
      );
    }
  }

  Future<int> _integer(List<String> arguments) async =>
      int.parse((await _gitRunner.requireSuccess(arguments)).trim());

  Future<List<String>> _lines(List<String> arguments) async =>
      (await _gitRunner.requireSuccess(arguments))
          .split(RegExp(r'\r?\n'))
          .where((line) => line.isNotEmpty)
          .toList(growable: false);

  Future<_Changes> _changes(String base, String target) async {
    var additions = 0;
    var deletions = 0;
    var binaryFiles = 0;
    for (final line in await _lines(['diff', '--numstat', '$base..$target'])) {
      final fields = line.split('\t');
      if (fields.length < 3) {
        throw const FormatException('PR dependency ledger numstat is invalid.');
      }
      if (fields[0] == '-' && fields[1] == '-') {
        binaryFiles += 1;
        continue;
      }
      additions += int.parse(fields[0]);
      deletions += int.parse(fields[1]);
    }
    return _Changes(additions, deletions, binaryFiles);
  }
}

class PrDependencyLedgerReport {
  const PrDependencyLedgerReport(this.metrics);

  final PrLedgerMetrics metrics;

  Map<String, Object> toJson() => {
    'formatVersion': 'showvault.pr-dependency-ledger-preflight.v1',
    'verified': true,
    'branchCount': metrics['combined.branches']!,
    'commitCount': metrics['combined.commits']!,
    'pathCount': metrics['combined.files']!,
    'additionCount': metrics['combined.additions']!,
    'deletionCount': metrics['combined.deletions']!,
    'binaryPathCount': metrics['combined.binaryFiles']!,
    'externalStateRead': false,
    'repositoryMutation': false,
  };
}

class _PrLedgerRow {
  const _PrLedgerRow(
    this.number,
    this.baseRef,
    this.targetRef,
    this.head,
    this.commits,
    this.files,
    this.additions,
    this.deletions,
  );

  final int number;
  final String baseRef;
  final String targetRef;
  final String head;
  final int commits;
  final int files;
  final int additions;
  final int deletions;
}

class _Changes {
  const _Changes(this.additions, this.deletions, this.binaryFiles);

  final int additions;
  final int deletions;
  final int binaryFiles;
}
