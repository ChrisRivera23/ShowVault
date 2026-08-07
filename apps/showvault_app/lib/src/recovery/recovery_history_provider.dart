import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/recovery/recovery_run.dart';

final recoveryHistoryProvider = Provider<List<RecoveryRun>>((ref) {
  // Authenticated API loading replaces this explicit preview after client sign-in.
  const completed = RecoveryStageStatus.completed;
  return [
    RecoveryRun(
      discoveryCommandId: 'preview-recovery-run',
      agentName: 'Main Control Agent',
      startedAt: DateTime.utc(2026, 8, 7, 2, 14),
      status: RecoveryRunStatus.completed,
      stages: const [
        RecoveryStage(kind: RecoveryStageKind.scan, status: completed),
        RecoveryStage(kind: RecoveryStageKind.backup, status: completed),
        RecoveryStage(kind: RecoveryStageKind.verify, status: completed),
        RecoveryStage(kind: RecoveryStageKind.restore, status: completed),
      ],
    ),
  ];
});
