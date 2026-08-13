import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/api/showvault_api.dart';
import 'package:showvault_app/src/app.dart';
import 'package:showvault_app/src/auth/auth_provider.dart';
import 'package:showvault_app/src/auth/auth_service.dart';
import 'package:showvault_app/src/auth/auth_session.dart';
import 'package:showvault_app/src/dashboard/dashboard_screen.dart';
import 'package:showvault_app/src/local_recovery/local_directory_consent.dart';
import 'package:showvault_app/src/local_recovery/local_engine_client.dart';
import 'package:showvault_app/src/recovery/recovery_run.dart';
import 'package:showvault_app/src/recovery/recovery_history_provider.dart';
import 'package:showvault_app/src/scanning/local_catalog_scanner.dart';

class _SignedOutAuthService extends AuthService {
  @override
  Future<AuthSession?> restore() async => null;
}

class _SignedInAuthService extends AuthService {
  @override
  Future<AuthSession?> restore() async => const AuthSession(
    accessToken: 'synthetic-token',
    displayName: 'Synthetic Owner',
  );
}

class _SyntheticApi extends ShowVaultApi {
  _SyntheticApi() : super(baseUrl: 'https://synthetic.invalid');

  @override
  Future<RecoveryHistory> loadRecoveryHistory(String accessToken) async =>
      const RecoveryHistory(
        organizationId: '11111111-1111-1111-1111-111111111111',
        organizationName: 'Synthetic Organization',
        venueId: '22222222-2222-2222-2222-222222222222',
        venueName: 'Synthetic Venue',
        runs: [],
      );
}

class _SyntheticScanner extends LocalCatalogScanner {
  @override
  Future<List<LocalCatalogFinding>> scanFindings() async => const [
    LocalCatalogFinding(
      candidateKey: 'macos.resolume-arena.application',
      expectedPath: '/synthetic/application',
      type: LocalCatalogFindingType.installedApplication,
    ),
  ];
}

class _UserDataScanner extends LocalCatalogScanner {
  @override
  Future<List<LocalCatalogFinding>> scanFindings() async => const [
    LocalCatalogFinding(
      candidateKey: 'macos.serato-dj-pro.user-data',
      expectedPath: '/synthetic/source',
      type: LocalCatalogFindingType.userDataRoot,
    ),
  ];
}

class _SyntheticConsent extends LocalDirectoryConsent {
  const _SyntheticConsent();

  @override
  Future<String?> selectExactSource() async => '/synthetic/source';

  @override
  Future<String?> selectVault() async => '/synthetic/vault';

  @override
  Future<String?> selectRestoreTarget() async => '/synthetic/restore-target';
}

class _SuccessfulLocalEngine extends LocalEngineClient {
  @override
  LocalSaveOperation startSave({
    required String candidateKey,
    required String selectedSource,
    required String selectedVault,
    required void Function(LocalSaveProgress progress) onProgress,
  }) {
    expect(candidateKey, 'macos.serato-dj-pro.user-data');
    expect(selectedSource, '/synthetic/source');
    expect(selectedVault, '/synthetic/vault');
    onProgress(const LocalSaveProgress('verifying', 1, 1));
    return LocalSaveOperation(
      result: Future.value(
        const LocalSaveResult(
          recoveryPointId: 'opaque-id',
          productName: 'Serato DJ Pro',
          fileCount: 2,
          totalBytes: 20,
          localStatus: 'verified',
          cloudStatus: 'queued',
        ),
      ),
      cancel: () async {},
    );
  }

  @override
  Future<LocalVaultInspection> inspectVault(String selectedVault) async =>
      const LocalVaultInspection(
        recoveryPoints: [],
        queueAttentionCount: 1,
        restoreAttentionCount: 0,
      );

  @override
  LocalRestoreOperation startRestore({
    required String recoveryPointId,
    required String selectedVault,
    required String selectedTarget,
    required void Function(LocalSaveProgress progress) onProgress,
  }) => throw UnimplementedError();
}

class _SuccessfulRestoreEngine extends _SuccessfulLocalEngine {
  @override
  Future<LocalVaultInspection> inspectVault(String selectedVault) async =>
      LocalVaultInspection(
        recoveryPoints: [
          LocalRecoveryPointSummary(
            recoveryPointId: 'a' * 64,
            candidateKey: 'macos.serato-dj-pro.user-data',
            productName: 'Serato DJ Pro',
            fileCount: 2,
            totalBytes: 20,
            createdAt: DateTime.utc(2026, 8, 13),
            localStatus: 'verified',
            cloudStatus: 'queued',
          ),
        ],
        queueAttentionCount: 0,
        restoreAttentionCount: 0,
      );

  @override
  LocalRestoreOperation startRestore({
    required String recoveryPointId,
    required String selectedVault,
    required String selectedTarget,
    required void Function(LocalSaveProgress progress) onProgress,
  }) {
    expect(recoveryPointId, 'a' * 64);
    expect(selectedVault, '/synthetic/vault');
    expect(selectedTarget, '/synthetic/restore-target');
    onProgress(const LocalSaveProgress('completed', 1, 1));
    return LocalRestoreOperation(
      result: Future.value(
        LocalRestoreResult(
          recoveryPointId: recoveryPointId,
          restoreEvidenceId: 'b' * 64,
          fileCount: 2,
          totalBytes: 20,
          completedAt: DateTime.utc(2026, 8, 13),
          localStatus: 'restored',
        ),
      ),
      cancel: () async {},
    );
  }
}

class _SuccessfulSyncEngine extends _SuccessfulRestoreEngine {
  bool synchronized = false;

  @override
  Future<LocalVaultInspection> inspectVault(String selectedVault) async =>
      LocalVaultInspection(
        recoveryPoints: [
          LocalRecoveryPointSummary(
            recoveryPointId: 'a' * 64,
            candidateKey: 'macos.serato-dj-pro.user-data',
            productName: 'Serato DJ Pro',
            fileCount: 2,
            totalBytes: 20,
            createdAt: DateTime.utc(2026, 8, 13),
            localStatus: 'verified',
            cloudStatus: synchronized ? 'synchronized' : 'queued',
          ),
        ],
        queueAttentionCount: 0,
        restoreAttentionCount: 0,
      );

  @override
  LocalSyncOperation startSync({
    required String selectedVault,
    required String organizationId,
    required String venueId,
    required String accessToken,
    required void Function(LocalSaveProgress progress) onProgress,
  }) {
    expect(selectedVault, '/synthetic/vault');
    expect(organizationId, '11111111-1111-1111-1111-111111111111');
    expect(venueId, '22222222-2222-2222-2222-222222222222');
    expect(accessToken, 'synthetic-token');
    onProgress(const LocalSaveProgress('uploading', 1, 2));
    synchronized = true;
    return LocalSyncOperation(
      result: Future.value(
        const LocalSyncResult(
          synchronizedCount: 1,
          retryScheduledCount: 0,
          attentionCount: 0,
          synchronizedBytes: 20,
          cloudStatus: 'synchronized',
        ),
      ),
      cancel: () async {},
    );
  }
}

class _CancellableLocalEngine extends _SuccessfulLocalEngine {
  final Completer<LocalSaveResult> _result = Completer<LocalSaveResult>();

  @override
  LocalSaveOperation startSave({
    required String candidateKey,
    required String selectedSource,
    required String selectedVault,
    required void Function(LocalSaveProgress progress) onProgress,
  }) {
    onProgress(const LocalSaveProgress('copying', 1, 2));
    return LocalSaveOperation(
      result: _result.future,
      cancel: () async {
        _result.completeError(const LocalEngineClientException('cancelled'));
      },
    );
  }
}

void main() {
  testWidgets('shows Auth0 sign in before tenant data', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authServiceProvider.overrideWithValue(_SignedOutAuthService()),
        ],
        child: const ShowVaultApp(),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Sign in to ShowVault'), findsOneWidget);
    expect(find.text('Sign in with Auth0'), findsOneWidget);
    expect(find.text('Scan this computer'), findsOneWidget);
    expect(find.text('Recovery loop proven'), findsNothing);
  });

  testWidgets('keeps direct scan available while signed out', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authServiceProvider.overrideWithValue(_SignedOutAuthService()),
          localCatalogScannerProvider.overrideWithValue(_SyntheticScanner()),
        ],
        child: const ShowVaultApp(),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Scan'));
    await tester.pumpAndSettle();

    expect(find.text('1 recognized candidate(s) detected.'), findsOneWidget);
    expect(find.textContaining('Agent'), findsNothing);
    expect(find.textContaining('enrollment'), findsNothing);
  });

  testWidgets('saves and verifies user data while signed out', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authServiceProvider.overrideWithValue(_SignedOutAuthService()),
          localCatalogScannerProvider.overrideWithValue(_UserDataScanner()),
          localDirectoryConsentProvider.overrideWithValue(
            const _SyntheticConsent(),
          ),
          localEngineClientProvider.overrideWithValue(_SuccessfulLocalEngine()),
        ],
        child: const ShowVaultApp(),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Scan'));
    await tester.pumpAndSettle();
    expect(find.text('Save'), findsOneWidget);
    await tester.ensureVisible(find.text('Save'));
    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();
    expect(find.text('Choose folders'), findsOneWidget);
    expect(find.textContaining('/synthetic/'), findsNothing);
    await tester.tap(find.text('Choose folders'));
    await tester.pumpAndSettle();

    expect(find.text('Verified locally'), findsOneWidget);
    expect(find.text('Cloud queued'), findsOneWidget);
    expect(find.textContaining('/synthetic/'), findsNothing);
  });

  testWidgets('reopens a local vault and reports queue attention', (
    tester,
  ) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authServiceProvider.overrideWithValue(_SignedOutAuthService()),
          localDirectoryConsentProvider.overrideWithValue(
            const _SyntheticConsent(),
          ),
          localEngineClientProvider.overrideWithValue(_SuccessfulLocalEngine()),
        ],
        child: const ShowVaultApp(),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Open local vault'));
    await tester.pumpAndSettle();

    expect(find.text('Queue attention: 1'), findsOneWidget);
    expect(find.textContaining('/synthetic/'), findsNothing);
  });

  testWidgets('restores a freshly verified point while signed out', (
    tester,
  ) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authServiceProvider.overrideWithValue(_SignedOutAuthService()),
          localDirectoryConsentProvider.overrideWithValue(
            const _SyntheticConsent(),
          ),
          localEngineClientProvider.overrideWithValue(
            _SuccessfulRestoreEngine(),
          ),
        ],
        child: const ShowVaultApp(),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Open local vault'));
    await tester.pumpAndSettle();
    expect(find.text('Synchronize'), findsNothing);
    await tester.ensureVisible(find.text('Restore'));
    await tester.tap(find.text('Restore'));
    await tester.pumpAndSettle();

    expect(find.textContaining('will not load'), findsOneWidget);
    expect(find.text('Choose sandbox'), findsOneWidget);
    expect(find.textContaining('/synthetic/'), findsNothing);
    await tester.tap(find.text('Choose sandbox'));
    await tester.pumpAndSettle();

    expect(find.text('Restored locally'), findsOneWidget);
    expect(find.textContaining('/synthetic/'), findsNothing);
  });

  testWidgets('requires signed-in consent then refreshes synchronized status', (
    tester,
  ) async {
    final engine = _SuccessfulSyncEngine();
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authServiceProvider.overrideWithValue(_SignedInAuthService()),
          showVaultApiProvider.overrideWithValue(_SyntheticApi()),
          localDirectoryConsentProvider.overrideWithValue(
            const _SyntheticConsent(),
          ),
          localEngineClientProvider.overrideWithValue(engine),
        ],
        child: const ShowVaultApp(),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Open local vault'));
    await tester.pumpAndSettle();
    expect(find.textContaining('• Cloud queued'), findsOneWidget);
    await tester.ensureVisible(find.text('Synchronize'));
    await tester.tap(find.text('Synchronize'));
    await tester.pumpAndSettle();

    expect(
      find.textContaining('backup content and relative filenames'),
      findsOneWidget,
    );
    expect(find.textContaining('/synthetic/'), findsNothing);
    await tester.tap(
      find.descendant(
        of: find.byType(AlertDialog),
        matching: find.widgetWithText(FilledButton, 'Synchronize'),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Synchronized: 1'), findsOneWidget);
    expect(find.textContaining('• Synchronized'), findsOneWidget);
    expect(find.textContaining('/synthetic/'), findsNothing);
  });

  testWidgets('cancels a running local Save without publishing success', (
    tester,
  ) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authServiceProvider.overrideWithValue(_SignedOutAuthService()),
          localCatalogScannerProvider.overrideWithValue(_UserDataScanner()),
          localDirectoryConsentProvider.overrideWithValue(
            const _SyntheticConsent(),
          ),
          localEngineClientProvider.overrideWithValue(
            _CancellableLocalEngine(),
          ),
        ],
        child: const ShowVaultApp(),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Scan'));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Save'));
    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Choose folders'));
    await tester.pump();
    await tester.ensureVisible(find.text('Cancel'));

    await tester.tap(find.text('Cancel'));
    await tester.pumpAndSettle();

    expect(
      find.text('Save cancelled. No recovery point was published.'),
      findsOneWidget,
    );
    expect(find.text('Verified locally'), findsNothing);
  });

  testWidgets('shows an honest empty live recovery history', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: RecoveryHistoryView(
            history: RecoveryHistory(
              organizationName: 'Test Organization',
              venueName: 'Test Venue',
              runs: [],
            ),
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
        home: Scaffold(
          body: RecoveryHistoryView(
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
      ),
    );

    expect(find.text('Recovery action failed'), findsOneWidget);
    expect(find.text('Failed'), findsWidgets);
    expect(find.text('Recovery loop proven'), findsNothing);
  });

  testWidgets('presents expired recovery evidence truthfully', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: RecoveryHistoryView(
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
      ),
    );

    expect(find.text('Recovery action expired'), findsOneWidget);
    expect(find.text('Expired'), findsWidgets);
  });
}
