import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/app.dart';
import 'package:showvault_app/src/api/showvault_api.dart';
import 'package:showvault_app/src/auth/auth_provider.dart';
import 'package:showvault_app/src/auth/auth_service.dart';
import 'package:showvault_app/src/auth/auth_session.dart';
import 'package:showvault_app/src/recovery/recovery_history_provider.dart';
import 'package:showvault_app/src/recovery/local_recovery_service.dart';
import 'package:showvault_app/src/scanning/local_catalog_scanner.dart';

class _SignedOutAuthService extends AuthService {
  @override
  Future<AuthSession?> restore() async => null;
}

class _SignedInAuthService extends AuthService {
  @override
  Future<AuthSession?> restore() async => const AuthSession(
    accessToken: 'access-token',
    displayName: 'Personal tester',
  );
}

class _ScanningApi extends ShowVaultApi {
  bool scanQueued = false;

  @override
  Future<RecoveryHistory> loadRecoveryHistory(String accessToken) async =>
      const RecoveryHistory(
        organizationId: 'org-id',
        organizationName: 'ShowVault',
        venueId: 'venue-id',
        venueName: 'Personal Test',
        agents: [VenueAgent(id: 'agent-id', name: 'Personal Mac')],
        candidates: [],
        runs: [],
      );

  @override
  Future<int> submitComputerScan({
    required String accessToken,
    required RecoveryHistory history,
    required List<String> candidateKeys,
  }) async {
    scanQueued = true;
    expect(candidateKeys, ['macos.resolume-arena.application']);
    return 1;
  }
}

class _ScanningLocalCatalog extends LocalCatalogScanner {
  @override
  Future<List<String>> scan() async => ['macos.resolume-arena.application'];

  @override
  Future<List<LocalCatalogFinding>> scanFindings() async => const [
    LocalCatalogFinding(
      candidateKey: 'macos.resolume-arena.application',
      pluginId: 'showvault.resolume',
      productName: 'Resolume Arena',
      candidateType: 'InstalledApplication',
    ),
  ];
}

class _SavingLocalCatalog extends LocalCatalogScanner {
  @override
  LocalBackupSource? resolveBackupSource(String candidateKey) =>
      const LocalBackupSource(
        candidateKey: 'macos.serato-dj-pro.user-data',
        pluginId: 'showvault.serato-dj-pro',
        productName: 'Serato DJ Pro',
        rootPath: '/synthetic/serato',
      );
}

class _FakeLocalRecoveryService extends LocalRecoveryService {
  bool saved = false;

  @override
  Future<LocalBackupResult> save(
    LocalBackupSource source, {
    LocalBackupCancellation? cancellation,
  }) async {
    saved = true;
    expect(source.rootPath, '/synthetic/serato');
    return const LocalBackupResult(
      recoveryPointId: 'recovery-point-id',
      recoveryPointPath: '/synthetic/vault/recovery-point-id',
      fileCount: 2,
      totalBytes: 12,
      localStatus: LocalProtectionStatus.verified,
      cloudStatus: LocalCloudSyncStatus.queued,
    );
  }
}

void main() {
  testWidgets('keeps local Scan available before cloud sign in', (
    tester,
  ) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authServiceProvider.overrideWithValue(_SignedOutAuthService()),
        ],
        child: const ShowVaultApp(),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Scan this computer'), findsOneWidget);
    expect(find.text('Cloud not connected'), findsOneWidget);
    expect(find.text('Connect cloud service'), findsOneWidget);
    expect(find.text('Foundation preview'), findsNothing);
  });

  testWidgets('local scan succeeds while cloud is not connected', (
    tester,
  ) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authServiceProvider.overrideWithValue(_SignedOutAuthService()),
          localCatalogScannerProvider.overrideWithValue(
            _ScanningLocalCatalog(),
          ),
        ],
        child: const ShowVaultApp(),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Scan this computer'));
    await tester.pumpAndSettle();

    expect(find.text('Resolume Arena'), findsOneWidget);
    expect(find.text('Detected'), findsOneWidget);
    expect(
      find.textContaining('Local scan complete • 1 candidates found'),
      findsOneWidget,
    );
  });

  testWidgets('queues a catalog-only scan from Detected systems', (
    tester,
  ) async {
    final api = _ScanningApi();
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authServiceProvider.overrideWithValue(_SignedInAuthService()),
          showVaultApiProvider.overrideWithValue(api),
          localCatalogScannerProvider.overrideWithValue(
            _ScanningLocalCatalog(),
          ),
        ],
        child: const ShowVaultApp(),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Scan this computer'), findsOneWidget);
    expect(
      find.textContaining('Scan only exact catalog-defined locations'),
      findsOneWidget,
    );
    expect(find.textContaining('Venue Agent'), findsNothing);

    await tester.ensureVisible(find.text('Scan this computer'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Scan this computer'));
    await tester.pump();

    expect(api.scanQueued, isTrue);
    expect(
      find.text('Computer scan complete • 1 candidates found.'),
      findsOneWidget,
    );
    expect(find.text('Install this Mac Agent'), findsNothing);
    expect(find.textContaining('enrollment'), findsNothing);
  });

  testWidgets(
    'explicit Save verifies locally and shows independent cloud status',
    (tester) async {
      final recovery = _FakeLocalRecoveryService();
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            authServiceProvider.overrideWithValue(_SignedOutAuthService()),
            localCatalogScannerProvider.overrideWithValue(
              _SavingLocalCatalog(),
            ),
            localCatalogFindingsProvider.overrideWith(
              (ref) => const [
                LocalCatalogFinding(
                  candidateKey: 'macos.serato-dj-pro.user-data',
                  pluginId: 'showvault.serato-dj-pro',
                  productName: 'Serato DJ Pro',
                  candidateType: 'UserDataRoot',
                ),
              ],
            ),
            localRecoveryServiceProvider.overrideWithValue(recovery),
          ],
          child: const ShowVaultApp(),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Serato DJ Pro'), findsOneWidget);
      expect(find.text('Save'), findsOneWidget);
      await tester.ensureVisible(find.text('Save'));
      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();

      expect(find.text('Save Serato DJ Pro?'), findsOneWidget);
      expect(recovery.saved, isFalse);
      await tester.tap(find.widgetWithText(FilledButton, 'Save').last);
      await tester.pumpAndSettle();

      expect(recovery.saved, isTrue);
      expect(find.text('Verified locally'), findsOneWidget);
      expect(find.text('Cloud queued'), findsOneWidget);
      expect(
        find.text('Verified locally • cloud synchronization queued.'),
        findsOneWidget,
      );
    },
  );
}
