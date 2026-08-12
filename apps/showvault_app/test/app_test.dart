import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/app.dart';
import 'package:showvault_app/src/recovery/recovery_history_provider.dart';
import 'package:showvault_app/src/recovery/recovery_run.dart';

void main() {
  testWidgets('shows an honest empty recovery history', (tester) async {
    await tester.pumpWidget(const ProviderScope(child: ShowVaultApp()));
    for (final label in ['Scan', 'Backup', 'Verify', 'Restore']) {
      expect(find.text(label), findsOneWidget);
    }
    expect(find.text('No recovery evidence yet'), findsOneWidget);
    expect(find.text('Recovery loop proven'), findsNothing);
  });

  testWidgets('presents failed recovery evidence truthfully', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          recoveryHistoryProvider.overrideWithValue([
            RecoveryRun(
              discoveryCommandId: 'failed-run',
              agentName: 'Test Agent',
              startedAt: DateTime.utc(2026),
              status: RecoveryRunStatus.failed,
              stages: const [
                RecoveryStage(
                  kind: RecoveryStageKind.scan,
                  status: RecoveryStageStatus.failed,
                ),
              ],
            ),
          ]),
        ],
        child: const ShowVaultApp(),
      ),
    );

    expect(find.text('Recovery action failed'), findsOneWidget);
    expect(find.text('Failed'), findsOneWidget);
    expect(find.text('Recovery loop proven'), findsNothing);
  });

  testWidgets('presents expired recovery evidence truthfully', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          recoveryHistoryProvider.overrideWithValue([
            RecoveryRun(
              discoveryCommandId: 'expired-run',
              agentName: 'Test Agent',
              startedAt: DateTime.utc(2026),
              status: RecoveryRunStatus.expired,
              stages: const [
                RecoveryStage(
                  kind: RecoveryStageKind.scan,
                  status: RecoveryStageStatus.expired,
                ),
              ],
            ),
          ]),
        ],
        child: const ShowVaultApp(),
      ),
    );

    expect(find.text('Recovery action expired'), findsOneWidget);
    expect(find.text('Expired'), findsOneWidget);
  });
}
