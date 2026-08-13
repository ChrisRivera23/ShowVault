import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/api/showvault_api.dart';
import 'package:showvault_app/src/auth/auth_provider.dart';
import 'package:showvault_app/src/recovery/recovery_history_provider.dart';

final organizationPlanProvider = FutureProvider<OrganizationPlan?>((ref) async {
  final session = ref.watch(authSessionProvider).valueOrNull;
  if (session == null) return null;
  final history = await ref.watch(recoveryHistoryProvider.future);
  if (history.organizationRole != 'owner' || history.organizationId.isEmpty) {
    return null;
  }
  return ref
      .watch(showVaultApiProvider)
      .loadOrganizationPlan(
        accessToken: session.accessToken,
        organizationId: history.organizationId,
      );
});

class PlanStorageScreen extends ConsumerWidget {
  const PlanStorageScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(authSessionProvider).valueOrNull;
    if (session == null) {
      return const _Message(
        icon: Icons.lock_outline,
        title: 'Plan and storage',
        message: 'Sign in to view organization plan information.',
      );
    }
    return ref
        .watch(recoveryHistoryProvider)
        .when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, _) => const _Message(
            icon: Icons.error_outline,
            title: 'Plan and storage',
            message: 'Plan information could not be loaded safely.',
          ),
          data: (history) {
            if (history.organizationRole != 'owner') {
              return const _Message(
                icon: Icons.admin_panel_settings_outlined,
                title: 'Plan and storage',
                message: 'Organization Owner access is required.',
              );
            }
            return ref
                .watch(organizationPlanProvider)
                .when(
                  loading: () =>
                      const Center(child: CircularProgressIndicator()),
                  error: (_, _) => const _Message(
                    icon: Icons.error_outline,
                    title: 'Plan and storage',
                    message: 'Plan information could not be loaded safely.',
                  ),
                  data: (plan) => plan == null
                      ? const _Message(
                          icon: Icons.info_outline,
                          title: 'Plan and storage',
                          message: 'No organization plan is available.',
                        )
                      : _PlanView(
                          organizationName: history.organizationName,
                          plan: plan,
                        ),
                );
          },
        );
  }
}

class _PlanView extends StatelessWidget {
  const _PlanView({required this.organizationName, required this.plan});

  final String organizationName;
  final OrganizationPlan plan;

  @override
  Widget build(BuildContext context) => SingleChildScrollView(
    padding: const EdgeInsets.all(24),
    child: Align(
      alignment: Alignment.topLeft,
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 720),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Plan and storage',
              style: Theme.of(context).textTheme.headlineMedium,
            ),
            const SizedBox(height: 4),
            Text(organizationName),
            const SizedBox(height: 20),
            Card(
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _Value(
                      label: 'Plan',
                      value: plan.planCode ?? 'Not assigned',
                    ),
                    _Value(label: 'License', value: _words(plan.licenseStatus)),
                    _Value(
                      label: 'Subscription',
                      value: _words(plan.subscriptionStatus),
                    ),
                    if (plan.graceEndsAt != null)
                      _Value(
                        label: 'Grace ends',
                        value: _date(plan.graceEndsAt!),
                      )
                    else if (plan.currentPeriodEndsAt != null)
                      _Value(
                        label: 'Current period ends',
                        value: _date(plan.currentPeriodEndsAt!),
                      ),
                    const Divider(height: 28),
                    _Value(
                      label: 'Committed storage',
                      value: _bytes(plan.committedBytes),
                    ),
                    _Value(
                      label: 'Reserved uploads',
                      value: _bytes(plan.reservedBytes),
                    ),
                    _Value(
                      label: 'Storage limit',
                      value: _bytes(plan.logicalStorageLimitBytes),
                    ),
                    const SizedBox(height: 12),
                    Semantics(
                      label: plan.eligible
                          ? 'Hosted synchronization eligible'
                          : 'Hosted synchronization needs attention',
                      child: Chip(
                        avatar: Icon(
                          plan.eligible
                              ? Icons.check_circle_outline
                              : Icons.info_outline,
                        ),
                        label: Text(
                          plan.eligible
                              ? 'Hosted synchronization eligible'
                              : 'Plan attention required',
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    ),
  );

  static String _words(String value) => value.replaceAll('_', ' ');
  static String _date(DateTime value) =>
      value.toLocal().toIso8601String().split('T').first;
  static String _bytes(int value) {
    const units = ['B', 'KiB', 'MiB', 'GiB', 'TiB'];
    var amount = value.toDouble();
    var unit = 0;
    while (amount >= 1024 && unit < units.length - 1) {
      amount /= 1024;
      unit++;
    }
    return unit == 0
        ? '$value B'
        : '${amount.toStringAsFixed(1)} ${units[unit]}';
  }
}

class _Value extends StatelessWidget {
  const _Value({required this.label, required this.value});
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 4),
    child: Row(
      children: [
        Expanded(child: Text(label)),
        const SizedBox(width: 16),
        Text(value, style: const TextStyle(fontWeight: FontWeight.w600)),
      ],
    ),
  );
}

class _Message extends StatelessWidget {
  const _Message({
    required this.icon,
    required this.title,
    required this.message,
  });
  final IconData icon;
  final String title;
  final String message;

  @override
  Widget build(BuildContext context) => Center(
    child: Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 56),
          const SizedBox(height: 16),
          Text(title, style: Theme.of(context).textTheme.headlineMedium),
          const SizedBox(height: 8),
          Text(message, textAlign: TextAlign.center),
        ],
      ),
    ),
  );
}
