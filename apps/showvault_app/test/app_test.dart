import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/app.dart';
import 'package:showvault_app/src/api/showvault_api.dart';
import 'package:showvault_app/src/auth/auth_provider.dart';
import 'package:showvault_app/src/auth/auth_service.dart';
import 'package:showvault_app/src/auth/auth_session.dart';
import 'package:showvault_app/src/recovery/recovery_history_provider.dart';

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
  Future<String> scanComputer({
    required String accessToken,
    required RecoveryHistory history,
    required String agentId,
  }) async {
    scanQueued = true;
    return 'inventory-command';
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
        ],
        child: const ShowVaultApp(),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Scan this computer'), findsOneWidget);
    expect(
      find.textContaining('Scan only catalog-defined standard locations'),
      findsOneWidget,
    );

    await tester.tap(find.text('Scan this computer'));
    await tester.pump();

    expect(api.scanQueued, isTrue);
    expect(find.text('Computer scan queued for Personal Mac.'), findsOneWidget);
    await tester.pump(const Duration(seconds: 6));
    await tester.pump();
  });
}
