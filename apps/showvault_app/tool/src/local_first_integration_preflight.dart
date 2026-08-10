import 'dart:io';

typedef PreflightMetrics = Map<String, Object>;

class LocalFirstIntegrationPreflight {
  LocalFirstIntegrationPreflight({GitProcessRunner? gitRunner})
    : _gitRunner = gitRunner ?? const GitProcessRunner();

  final GitProcessRunner _gitRunner;

  static const _milestones = <_MilestoneRange>[
    _MilestoneRange('milestone1', '310190c', 'ce5be25'),
    _MilestoneRange('milestone2', 'ce5be25', 'c172e49'),
    _MilestoneRange('milestone3', 'c172e49', 'fff4434'),
    _MilestoneRange('milestone4', 'fff4434', '69b83ab'),
    _MilestoneRange('milestone5', '69b83ab', '3a5e715'),
  ];

  static const windowsSelectedCommits = <String>[
    '58ad46a',
    '5c7ade7',
    'e503ca1',
    '6fdccca',
    '70fe056',
    'ddfcaa6',
    'd5e441e',
    '1dd2d23',
    'a1a69eb',
    'a66f744',
    '0644cb1',
    'b231d4c',
    '7592fbe',
    'a375e40',
    'a927c20',
    '7b6093d',
    '1ce2efc',
    '2e107a8',
  ];

  static const expectedMetrics = <String, Object>{
    'milestone1.commits': 9,
    'milestone1.netFiles': 41,
    'milestone1.transientFiles': 2,
    'milestone1.legacyOverlap': 23,
    'milestone1.rangeOnly': 18,
    'milestone2.commits': 6,
    'milestone2.netFiles': 36,
    'milestone2.addedFiles': 8,
    'milestone2.transientFiles': 0,
    'milestone2.legacyOverlap': 14,
    'milestone2.rangeOnly': 22,
    'milestone2.milestone1Overlap': 14,
    'milestone3.commits': 10,
    'milestone3.netFiles': 31,
    'milestone3.addedFiles': 18,
    'milestone3.transientFiles': 0,
    'milestone3.legacyOverlap': 4,
    'milestone3.milestone1Overlap': 7,
    'milestone3.milestone2Overlap': 10,
    'milestone4.commits': 2,
    'milestone4.netFiles': 28,
    'milestone4.addedFiles': 17,
    'milestone4.transientFiles': 0,
    'milestone4.legacyOverlap': 3,
    'milestone4.milestone1Overlap': 3,
    'milestone4.milestone2Overlap': 2,
    'milestone4.milestone3Overlap': 9,
    'milestone5.commits': 7,
    'milestone5.netFiles': 19,
    'milestone5.addedFiles': 9,
    'milestone5.transientFiles': 0,
    'milestone5.legacyOverlap': 3,
    'milestone5.milestone1Overlap': 7,
    'milestone5.milestone2Overlap': 7,
    'milestone5.milestone3Overlap': 7,
    'milestone5.milestone4Overlap': 3,
    'windows.selectedCommits': 18,
    'windows.unionFiles': 35,
    'windows.addedFiles': 21,
    'windows.legacyOverlap': 2,
    'windows.milestone1Overlap': 5,
    'windows.milestone2Overlap': 7,
    'windows.milestone3Overlap': 6,
    'windows.milestone4Overlap': 2,
    'windows.milestone5Overlap': 6,
    'windows.spanCommits': 22,
    'windows.excludedCommits': <String>[
      '0c174ba',
      '626e88d',
      '65c50be',
      'a1c3c83',
    ],
    'combined.productCommits': 34,
    'combined.productNetFiles': 112,
    'combined.selectedCommits': 52,
    'combined.unionFiles': 136,
    'combined.productWindowsOverlap': 11,
    'combined.legacyOverlap': 29,
  };

  Future<IntegrationPreflightReport> verify() async {
    await _gitRunner.requireSuccess(['rev-parse', '--show-toplevel']);
    final legacyFiles = await _fileSet([
      'diff',
      '--name-only',
      '254cbbf..310190c',
    ]);
    final milestoneFiles = <Set<String>>[];
    final metrics = <String, Object>{};

    for (var index = 0; index < _milestones.length; index += 1) {
      final milestone = _milestones[index];
      final range = '${milestone.from}..${milestone.to}';
      final files = await _fileSet(['diff', '--name-only', range]);
      milestoneFiles.add(files);
      final commitCount = await _integer(['rev-list', '--count', range]);
      final logFiles = await _fileSet([
        'log',
        '--format=',
        '--name-only',
        range,
      ]);
      metrics['${milestone.name}.commits'] = commitCount;
      metrics['${milestone.name}.netFiles'] = files.length;
      metrics['${milestone.name}.transientFiles'] = logFiles
          .difference(files)
          .length;
      metrics['${milestone.name}.legacyOverlap'] = _overlap(legacyFiles, files);
      if (index < 2) {
        metrics['${milestone.name}.rangeOnly'] = files
            .difference(legacyFiles)
            .length;
      }
      if (index > 0) {
        metrics['${milestone.name}.addedFiles'] = (await _fileSet([
          'diff',
          '--diff-filter=A',
          '--name-only',
          range,
        ])).length;
      }
      for (var prior = 0; prior < index; prior += 1) {
        metrics['${milestone.name}.milestone${prior + 1}Overlap'] = _overlap(
          milestoneFiles[prior],
          files,
        );
      }
    }

    final windowsFiles = <String>{};
    final selectedFullShas = <String>{};
    for (final commit in windowsSelectedCommits) {
      selectedFullShas.add(
        (await _gitRunner.requireSuccess(['rev-parse', commit])).trim(),
      );
      windowsFiles.addAll(
        await _fileSet([
          'diff-tree',
          '--no-commit-id',
          '--name-only',
          '-r',
          commit,
        ]),
      );
    }
    var windowsAdded = 0;
    for (final filePath in windowsFiles) {
      final result = await _gitRunner.run([
        'cat-file',
        '-e',
        '3a5e715:$filePath',
      ]);
      if (result.exitCode != 0) windowsAdded += 1;
    }
    final windowsSpan = await _fileSet([
      'rev-list',
      '--reverse',
      '--first-parent',
      '3a5e715..2e107a8',
    ]);
    final excluded =
        windowsSpan
            .difference(selectedFullShas)
            .map((sha) => sha.substring(0, 7))
            .toList()
          ..sort();
    metrics['windows.selectedCommits'] = windowsSelectedCommits.length;
    metrics['windows.unionFiles'] = windowsFiles.length;
    metrics['windows.addedFiles'] = windowsAdded;
    metrics['windows.legacyOverlap'] = _overlap(legacyFiles, windowsFiles);
    metrics['windows.spanCommits'] = windowsSpan.length;
    metrics['windows.excludedCommits'] = excluded;
    for (var index = 0; index < milestoneFiles.length; index += 1) {
      metrics['windows.milestone${index + 1}Overlap'] = _overlap(
        milestoneFiles[index],
        windowsFiles,
      );
    }

    final productFiles = await _fileSet([
      'diff',
      '--name-only',
      '310190c..3a5e715',
    ]);
    final combinedFiles = {...productFiles, ...windowsFiles};
    final productCommits = await _integer([
      'rev-list',
      '--count',
      '310190c..3a5e715',
    ]);
    metrics['combined.productCommits'] = productCommits;
    metrics['combined.productNetFiles'] = productFiles.length;
    metrics['combined.selectedCommits'] =
        productCommits + windowsSelectedCommits.length;
    metrics['combined.unionFiles'] = combinedFiles.length;
    metrics['combined.productWindowsOverlap'] = _overlap(
      productFiles,
      windowsFiles,
    );
    metrics['combined.legacyOverlap'] = _overlap(legacyFiles, combinedFiles);

    validateMetrics(metrics);
    return IntegrationPreflightReport(metrics);
  }

  static void validateMetrics(PreflightMetrics actual) {
    for (final entry in expectedMetrics.entries) {
      final observed = actual[entry.key];
      if (!_equal(observed, entry.value)) {
        throw FormatException(
          'Integration preflight mismatch for ${entry.key}: '
          'expected ${entry.value}, observed $observed.',
        );
      }
    }
    final unexpected = actual.keys.toSet().difference(
      expectedMetrics.keys.toSet(),
    );
    if (unexpected.isNotEmpty) {
      throw const FormatException(
        'Integration preflight produced unexpected metrics.',
      );
    }
  }

  Future<Set<String>> _fileSet(List<String> arguments) async =>
      _lines(await _gitRunner.requireSuccess(arguments)).toSet();

  Future<int> _integer(List<String> arguments) async =>
      int.parse((await _gitRunner.requireSuccess(arguments)).trim());
}

class IntegrationPreflightReport {
  const IntegrationPreflightReport(this.metrics);

  final PreflightMetrics metrics;

  Map<String, Object> toJson() => {
    'formatVersion': 'showvault.local-first-integration-preflight.v1',
    'verified': true,
    'selectedCommitCount': metrics['combined.selectedCommits']!,
    'selectedPathCount': metrics['combined.unionFiles']!,
    'legacyOverlapPathCount': metrics['combined.legacyOverlap']!,
    'milestoneCount': 6,
    'excludedInterleavedCommitCount': 4,
    'externalStateRead': false,
    'repositoryMutation': false,
  };
}

class GitProcessRunner {
  const GitProcessRunner({this.workingDirectory});

  final String? workingDirectory;

  Future<ProcessResult> run(List<String> arguments) =>
      Process.run('git', arguments, workingDirectory: workingDirectory);

  Future<String> requireSuccess(List<String> arguments) async {
    final result = await run(arguments);
    if (result.exitCode != 0) {
      throw StateError('Git integration preflight command failed.');
    }
    return result.stdout as String;
  }
}

class _MilestoneRange {
  const _MilestoneRange(this.name, this.from, this.to);

  final String name;
  final String from;
  final String to;
}

int _overlap(Set<String> first, Set<String> second) =>
    first.intersection(second).length;

List<String> _lines(String value) => value
    .split(RegExp(r'\r?\n'))
    .map((line) => line.trim())
    .where((line) => line.isNotEmpty)
    .toList(growable: false);

bool _equal(Object? first, Object? second) {
  if (first is List && second is List) {
    if (first.length != second.length) return false;
    for (var index = 0; index < first.length; index += 1) {
      if (first[index] != second[index]) return false;
    }
    return true;
  }
  return first == second;
}
