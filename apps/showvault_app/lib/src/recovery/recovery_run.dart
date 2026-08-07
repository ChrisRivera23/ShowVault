enum RecoveryRunStatus { pending, inProgress, completed, failed }

enum RecoveryStageKind { scan, backup, verify, restore }

enum RecoveryStageStatus { notStarted, pending, inProgress, completed, failed }

class RecoveryRun {
  const RecoveryRun({
    required this.discoveryCommandId,
    required this.agentName,
    required this.startedAt,
    required this.status,
    required this.stages,
  });

  factory RecoveryRun.fromJson(Map<String, Object?> json) => RecoveryRun(
    discoveryCommandId: json['discoveryCommandId']! as String,
    agentName: json['agentName']! as String,
    startedAt: DateTime.parse(json['startedAt']! as String),
    status: _runStatus(json['status']! as String),
    stages: (json['stages']! as List<Object?>)
        .map((stage) => RecoveryStage.fromJson(stage! as Map<String, Object?>))
        .toList(growable: false),
  );

  final String discoveryCommandId;
  final String agentName;
  final DateTime startedAt;
  final RecoveryRunStatus status;
  final List<RecoveryStage> stages;

  static RecoveryRunStatus _runStatus(String value) => switch (value) {
    'pending' => RecoveryRunStatus.pending,
    'in_progress' => RecoveryRunStatus.inProgress,
    'completed' => RecoveryRunStatus.completed,
    'failed' => RecoveryRunStatus.failed,
    _ => throw FormatException('Unknown recovery run status: $value'),
  };
}

class RecoveryStage {
  const RecoveryStage({
    required this.kind,
    required this.status,
    this.occurredAt,
  });

  factory RecoveryStage.fromJson(Map<String, Object?> json) => RecoveryStage(
    kind: switch (json['stage']) {
      'scan' => RecoveryStageKind.scan,
      'backup' => RecoveryStageKind.backup,
      'verify' => RecoveryStageKind.verify,
      'restore' => RecoveryStageKind.restore,
      final value => throw FormatException('Unknown recovery stage: $value'),
    },
    status: switch (json['status']) {
      'not_started' => RecoveryStageStatus.notStarted,
      'pending' => RecoveryStageStatus.pending,
      'in_progress' => RecoveryStageStatus.inProgress,
      'completed' => RecoveryStageStatus.completed,
      'failed' => RecoveryStageStatus.failed,
      final value => throw FormatException(
        'Unknown recovery stage status: $value',
      ),
    },
    occurredAt: json['occurredAt'] == null
        ? null
        : DateTime.parse(json['occurredAt']! as String),
  );

  final RecoveryStageKind kind;
  final RecoveryStageStatus status;
  final DateTime? occurredAt;
}
