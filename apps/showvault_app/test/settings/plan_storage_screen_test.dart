import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/api/showvault_api.dart';
import 'package:showvault_app/src/auth/auth_provider.dart';
import 'package:showvault_app/src/auth/auth_service.dart';
import 'package:showvault_app/src/auth/auth_session.dart';
import 'package:showvault_app/src/recovery/recovery_history_provider.dart';
import 'package:showvault_app/src/settings/plan_storage_screen.dart';

class _SignedInOwner extends AuthService {
  @override
  Future<AuthSession?> restore() async => const AuthSession(
    accessToken: 'synthetic-token',
    displayName: 'Synthetic Owner',
  );
}

class _OwnerApi extends ShowVaultApi {
  _OwnerApi() : super(baseUrl: 'https://synthetic.invalid');

  @override
  Future<RecoveryHistory> loadRecoveryHistory(String accessToken) async =>
      const RecoveryHistory(
        organizationId: 'organization-id',
        organizationName: 'Synthetic Organization',
        organizationRole: 'owner',
        venueId: 'venue-id',
        venueName: 'Synthetic Venue',
        runs: [],
      );

  @override
  Future<OrganizationPlan> loadOrganizationPlan({
    required String accessToken,
    required String organizationId,
  }) async => OrganizationPlan(
    planCode: 'synthetic.standard',
    licenseStatus: 'active',
    subscriptionStatus: 'past_due',
    currentPeriodEndsAt: null,
    graceEndsAt: DateTime.utc(2026, 8, 20),
    logicalStorageLimitBytes: 100 * 1024 * 1024,
    committedBytes: 1024,
    reservedBytes: 512,
    eligible: true,
    reasonCode: 'eligible',
  );
}

class _ManagerApi extends _OwnerApi {
  bool planRequested = false;

  @override
  Future<RecoveryHistory> loadRecoveryHistory(String accessToken) async =>
      const RecoveryHistory(
        organizationId: 'organization-id',
        organizationName: 'Synthetic Organization',
        organizationRole: 'manager',
        venueId: 'venue-id',
        venueName: 'Synthetic Venue',
        runs: [],
      );

  @override
  Future<OrganizationPlan> loadOrganizationPlan({
    required String accessToken,
    required String organizationId,
  }) async {
    planRequested = true;
    return super.loadOrganizationPlan(
      accessToken: accessToken,
      organizationId: organizationId,
    );
  }
}

void main() {
  testWidgets('owner sees read-only normalized plan and logical storage', (
    tester,
  ) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authServiceProvider.overrideWithValue(_SignedInOwner()),
          showVaultApiProvider.overrideWithValue(_OwnerApi()),
        ],
        child: const MaterialApp(home: Scaffold(body: PlanStorageScreen())),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Plan and storage'), findsOneWidget);
    expect(find.text('Synthetic Organization'), findsOneWidget);
    expect(find.text('synthetic.standard'), findsOneWidget);
    expect(find.text('past due'), findsOneWidget);
    expect(find.text('1.0 KiB'), findsOneWidget);
    expect(find.text('512 B'), findsOneWidget);
    expect(find.text('100.0 MiB'), findsOneWidget);
    expect(find.text('Hosted synchronization eligible'), findsOneWidget);
    expect(find.textContaining('payment'), findsNothing);
    expect(find.textContaining('invoice'), findsNothing);
  });

  testWidgets('manager does not request or see commercial details', (
    tester,
  ) async {
    final api = _ManagerApi();
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authServiceProvider.overrideWithValue(_SignedInOwner()),
          showVaultApiProvider.overrideWithValue(api),
        ],
        child: const MaterialApp(home: Scaffold(body: PlanStorageScreen())),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Organization Owner access is required.'), findsOneWidget);
    expect(find.text('synthetic.standard'), findsNothing);
    expect(api.planRequested, isFalse);
  });
}
