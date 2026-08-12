import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/recovery/recovery_run.dart';

void main() {
  test('parses control-plane recovery history', () {
    final run = RecoveryRun.fromJson({
      'discoveryCommandId': 'discovery-id',
      'agentName': 'Main Agent',
      'startedAt': '2026-08-07T02:14:00Z',
      'status': 'completed',
      'stages': [
        {
          'stage': 'scan',
          'status': 'completed',
          'occurredAt': '2026-08-07T02:15:00Z',
        },
        {'stage': 'backup', 'status': 'completed', 'occurredAt': null},
        {'stage': 'verify', 'status': 'completed', 'occurredAt': null},
        {'stage': 'restore', 'status': 'completed', 'occurredAt': null},
      ],
    });

    expect(run.agentName, 'Main Agent');
    expect(run.status, RecoveryRunStatus.completed);
    expect(run.stages, hasLength(4));
    expect(run.stages.first.kind, RecoveryStageKind.scan);
    expect(run.stages.first.status, RecoveryStageStatus.completed);
    expect(run.stages.first.occurredAt, DateTime.utc(2026, 8, 7, 2, 15));
  });

  test('rejects unknown server statuses', () {
    expect(
      () => RecoveryRun.fromJson({
        'discoveryCommandId': 'discovery-id',
        'agentName': 'Agent',
        'startedAt': '2026-08-07T02:14:00Z',
        'status': 'unknown',
        'stages': <Object?>[],
      }),
      throwsFormatException,
    );
  });

  test('parses expired run and stage statuses', () {
    final run = RecoveryRun.fromJson({
      'discoveryCommandId': 'expired-run',
      'agentName': 'Agent',
      'startedAt': '2026-08-07T02:14:00Z',
      'status': 'expired',
      'stages': [
        {'stage': 'scan', 'status': 'expired', 'occurredAt': null},
      ],
    });

    expect(run.status, RecoveryRunStatus.expired);
    expect(run.stages.single.status, RecoveryStageStatus.expired);
  });
}
