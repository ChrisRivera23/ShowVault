import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/app.dart';
import 'package:showvault_app/src/api/showvault_api.dart';
import 'package:showvault_app/src/auth/auth_provider.dart';
import 'package:showvault_app/src/auth/auth_service.dart';
import 'package:showvault_app/src/auth/auth_session.dart';
import 'package:showvault_app/src/recovery/recovery_history_provider.dart';
import 'package:showvault_app/src/recovery/local_access_coordinator.dart';
import 'package:showvault_app/src/recovery/local_recovery_service.dart';
import 'package:showvault_app/src/recovery/local_restore_service.dart';
import 'package:showvault_app/src/recovery/local_sync_object_store.dart';
import 'package:showvault_app/src/recovery/local_sync_service.dart';
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
  bool synchronized = false;

  @override
  Future<LocalBackupResult> save(
    LocalBackupSource source, {
    LocalBackupCancellation? cancellation,
    String? authorizedVaultRoot,
  }) async {
    saved = true;
    expect(source.rootPath, '/synthetic/serato');
    return const LocalBackupResult(
      recoveryPointId: 'recovery-point-id',
      recoveryPointPath: '/synthetic/vault/recovery-point-id',
      vaultRoot: '/synthetic/vault',
      fileCount: 2,
      totalBytes: 12,
      localStatus: LocalProtectionStatus.verified,
      cloudStatus: LocalCloudSyncStatus.queued,
    );
  }

  @override
  Future<LocalVaultSnapshot> inspectVault(String authorizedVaultRoot) async =>
      LocalVaultSnapshot(
        vaultRoot: authorizedVaultRoot,
        records: [
          LocalRecoveryRecord(
            recoveryPointId: 'recovery-point-id',
            recoveryPointPath: '$authorizedVaultRoot/recovery-point-id',
            candidateKey: 'macos.serato-dj-pro.user-data',
            productName: 'Serato DJ Pro',
            createdAt: DateTime.utc(2026, 8, 10),
            fileCount: 2,
            totalBytes: 12,
            localStatus: LocalProtectionStatus.verified,
            cloudStatus: synchronized
                ? LocalCloudSyncStatus.synchronized
                : LocalCloudSyncStatus.queued,
          ),
        ],
      );
}

class _FakeLocalSyncService extends LocalSyncService {
  _FakeLocalSyncService(this.recovery)
    : super(objectStore: const _UnusedObjectStore());

  final _FakeLocalRecoveryService recovery;
  bool called = false;

  @override
  Future<LocalSyncRunResult> syncPending(
    String authorizedVaultRoot, {
    int maxJobs = 25,
    LocalSyncCancellation? cancellation,
  }) async {
    called = true;
    recovery.synchronized = true;
    return const LocalSyncRunResult(
      synchronized: 1,
      retriedLater: 0,
      failed: 0,
      skipped: 0,
    );
  }
}

class _UnusedObjectStore implements LocalSyncObjectStore {
  const _UnusedObjectStore();

  Never _unused() => throw UnimplementedError();

  @override
  Future<void> appendChunk(
    String packageId,
    String relativePath,
    int offset,
    List<int> bytes,
  ) async => _unused();

  @override
  Future<LocalSyncReceipt?> committedReceipt(String packageId) async =>
      _unused();

  @override
  Future<int> uploadedLength(String packageId, String relativePath) async =>
      _unused();

  @override
  Future<LocalSyncReceipt> verifyAndCommit(
    String packageId,
    List<int> remoteManifestBytes,
    List<LocalSyncFileDescriptor> files,
  ) async => _unused();
}

class _FakeLocalAccessCoordinator extends LocalAccessCoordinator {
  bool sourceAuthorized = false;
  bool vaultAuthorized = false;
  bool restoreTargetAuthorized = false;

  @override
  Future<LocalBackupSource> authorizeSource(LocalBackupSource expected) async {
    sourceAuthorized = true;
    return expected;
  }

  @override
  Future<String> authorizeVault() async {
    vaultAuthorized = true;
    return '/synthetic/vault';
  }

  @override
  Future<String> authorizeEmptyRestoreTarget({String? initialDirectory}) async {
    restoreTargetAuthorized = true;
    return '/synthetic/restore-target';
  }
}

class _FakeLocalRestoreService extends LocalRestoreService {
  bool restored = false;

  @override
  Future<LocalRestoreResult> restore({
    required String authorizedVaultRoot,
    required String recoveryPointId,
    required String targetPath,
    LocalRestoreCancellation? cancellation,
  }) async {
    restored = true;
    expect(authorizedVaultRoot, '/synthetic/vault');
    expect(recoveryPointId, 'recovery-point-id');
    expect(targetPath, '/synthetic/restore-target');
    return LocalRestoreResult(
      recoveryPointId: recoveryPointId,
      restoredFileCount: 2,
      restoredBytes: 12,
      completedAt: DateTime.utc(2026, 8, 10),
      evidencePath: '/synthetic/vault/Reports/restore.json',
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
    expect(find.text('Synchronize pending'), findsNothing);
    await tester.drag(find.byType(ListView).last, const Offset(0, -1000));
    await tester.pumpAndSettle();
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
      final access = _FakeLocalAccessCoordinator();
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
            localAccessCoordinatorProvider.overrideWithValue(access),
          ],
          child: const ShowVaultApp(),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Serato DJ Pro'), findsOneWidget);
      expect(find.text('Save'), findsOneWidget);
      final saveButton = find.widgetWithText(FilledButton, 'Save');
      await tester.ensureVisible(saveButton);
      await tester.pumpAndSettle();
      await tester.tap(saveButton);
      await tester.pumpAndSettle();

      expect(find.text('Save Serato DJ Pro?'), findsOneWidget);
      expect(recovery.saved, isFalse);
      await tester.tap(find.widgetWithText(FilledButton, 'Save').last);
      await tester.pumpAndSettle();

      expect(recovery.saved, isTrue);
      expect(access.sourceAuthorized, isTrue);
      expect(access.vaultAuthorized, isTrue);
      expect(find.text('Verified locally'), findsOneWidget);
      expect(find.text('Cloud queued'), findsOneWidget);
      expect(
        find.text('Verified locally • cloud synchronization queued.'),
        findsOneWidget,
      );
    },
  );

  testWidgets('opening an authorized vault rehydrates status after restart', (
    tester,
  ) async {
    final recovery = _FakeLocalRecoveryService();
    final access = _FakeLocalAccessCoordinator();
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authServiceProvider.overrideWithValue(_SignedOutAuthService()),
          localAccessCoordinatorProvider.overrideWithValue(access),
          localRecoveryServiceProvider.overrideWithValue(recovery),
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
        ],
        child: const ShowVaultApp(),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Open local vault'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Choose vault'));
    await tester.pumpAndSettle();

    expect(access.vaultAuthorized, isTrue);
    expect(
      find.text('1 verified • 0 cloud synchronized • 1 pending'),
      findsOneWidget,
    );
    expect(find.text('Verified locally'), findsOneWidget);
    expect(find.text('Cloud queued'), findsOneWidget);
  });

  testWidgets('synthetic build can synchronize durable pending work', (
    tester,
  ) async {
    final recovery = _FakeLocalRecoveryService();
    final sync = _FakeLocalSyncService(recovery);
    final access = _FakeLocalAccessCoordinator();
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authServiceProvider.overrideWithValue(_SignedOutAuthService()),
          localAccessCoordinatorProvider.overrideWithValue(access),
          localRecoveryServiceProvider.overrideWithValue(recovery),
          localSyncServiceProvider.overrideWithValue(sync),
        ],
        child: const ShowVaultApp(),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Open local vault'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Choose vault'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Synchronize pending'));
    await tester.pumpAndSettle();

    expect(sync.called, isTrue);
    expect(
      find.text('1 verified • 1 cloud synchronized • 0 pending'),
      findsOneWidget,
    );
  });

  testWidgets('verified local recovery point restores without cloud', (
    tester,
  ) async {
    final recovery = _FakeLocalRecoveryService();
    final restore = _FakeLocalRestoreService();
    final access = _FakeLocalAccessCoordinator();
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authServiceProvider.overrideWithValue(_SignedOutAuthService()),
          localAccessCoordinatorProvider.overrideWithValue(access),
          localRecoveryServiceProvider.overrideWithValue(recovery),
          localRestoreServiceProvider.overrideWithValue(restore),
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
        ],
        child: const ShowVaultApp(),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Open local vault'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Choose vault'));
    await tester.pumpAndSettle();
    final restoreButton = find.widgetWithText(OutlinedButton, 'Restore');
    tester.widget<OutlinedButton>(restoreButton).onPressed!.call();
    await tester.pumpAndSettle();
    expect(find.text('Restore Serato DJ Pro?'), findsOneWidget);
    await tester.tap(
      find.widgetWithText(FilledButton, 'Choose restore folder'),
    );
    await tester.pumpAndSettle();

    expect(access.restoreTargetAuthorized, isTrue);
    expect(restore.restored, isTrue);
  });
}
