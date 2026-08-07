import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/api/showvault_api.dart';
import 'package:showvault_app/src/app.dart';
import 'package:showvault_app/src/dashboard/dashboard_screen.dart';
import 'package:showvault_app/src/recovery/recovery_run.dart';

void main() {
  testWidgets('requires an Auth0 client before showing tenant data', (
    tester,
  ) async {
    await tester.pumpWidget(const ProviderScope(child: ShowVaultApp()));
    expect(find.text('Auth0 client configuration required'), findsOneWidget);
    expect(find.text('Recovery loop proven'), findsNothing);
  });

  testWidgets('shows an honest empty live recovery history', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: RecoveryHistoryView(
          history: RecoveryHistory(
            organizationName: 'Test Organization',
            venueName: 'Test Venue',
            runs: [],
          ),
        ),
      ),
    );

    expect(find.text('Live data'), findsOneWidget);
    expect(find.text('No recovery evidence yet'), findsOneWidget);
    expect(find.text('Recovery loop proven'), findsNothing);
  });

  testWidgets('presents failed recovery evidence truthfully', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: RecoveryHistoryView(
          history: RecoveryHistory(
            organizationName: 'Test Organization',
            venueName: 'Test Venue',
            runs: [
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
            ],
          ),
        ),
      ),
    );

    expect(find.text('Recovery action failed'), findsOneWidget);
    expect(find.text('Failed'), findsOneWidget);
    expect(find.text('Recovery loop proven'), findsNothing);
  });

  testWidgets('presents expired recovery evidence truthfully', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: RecoveryHistoryView(
          history: RecoveryHistory(
            organizationName: 'Test Organization',
            venueName: 'Test Venue',
            runs: [
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
            ],
          ),
        ),
      ),
    );

    expect(find.text('Recovery action expired'), findsOneWidget);
    expect(find.text('Expired'), findsOneWidget);
  });
}
