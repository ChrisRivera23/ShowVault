import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/app.dart';
import 'package:showvault_app/src/api/showvault_api.dart';
import 'package:showvault_app/src/auth/auth_provider.dart';
import 'package:showvault_app/src/auth/auth_service.dart';
import 'package:showvault_app/src/auth/auth_session.dart';
import 'package:showvault_app/src/recovery/recovery_history_provider.dart';
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
    expect(find.text('Foundation preview'), findsNothing);
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
}
