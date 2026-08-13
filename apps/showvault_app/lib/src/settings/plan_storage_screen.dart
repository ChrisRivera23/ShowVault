import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/api/showvault_api.dart';
import 'package:showvault_app/src/auth/auth_provider.dart';
import 'package:showvault_app/src/recovery/recovery_history_provider.dart';
import 'package:url_launcher/url_launcher.dart';

typedef HostedBillingUrlOpener = Future<bool> Function(Uri url);

final hostedBillingUrlOpenerProvider = Provider<HostedBillingUrlOpener>(
  (_) =>
      (url) => launchUrl(url, mode: LaunchMode.externalApplication),
);

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

final billingOfferingProvider = FutureProvider<BillingOffering?>((ref) async {
  final session = ref.watch(authSessionProvider).valueOrNull;
  if (session == null) return null;
  final history = await ref.watch(recoveryHistoryProvider.future);
  if (history.organizationRole != 'owner' || history.organizationId.isEmpty) {
    return null;
  }
  return ref
      .watch(showVaultApiProvider)
      .loadBillingOffering(
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
                          organizationId: history.organizationId,
                          accessToken: session.accessToken,
                          plan: plan,
                        ),
                );
          },
        );
  }
}

class _PlanView extends ConsumerStatefulWidget {
  const _PlanView({
    required this.organizationName,
    required this.organizationId,
    required this.accessToken,
    required this.plan,
  });

  final String organizationName;
  final String organizationId;
  final String accessToken;
  final OrganizationPlan plan;

  @override
  ConsumerState<_PlanView> createState() => _PlanViewState();
}

class _PlanViewState extends ConsumerState<_PlanView> {
  bool _busy = false;
  String? _message;

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
            Text(widget.organizationName),
            const SizedBox(height: 20),
            Card(
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _Value(
                      label: 'Plan',
                      value: widget.plan.planCode ?? 'Not assigned',
                    ),
                    _Value(
                      label: 'License',
                      value: _words(widget.plan.licenseStatus),
                    ),
                    _Value(
                      label: 'Subscription',
                      value: _words(widget.plan.subscriptionStatus),
                    ),
                    if (widget.plan.graceEndsAt != null)
                      _Value(
                        label: 'Grace ends',
                        value: _date(widget.plan.graceEndsAt!),
                      )
                    else if (widget.plan.currentPeriodEndsAt != null)
                      _Value(
                        label: 'Current period ends',
                        value: _date(widget.plan.currentPeriodEndsAt!),
                      ),
                    const Divider(height: 28),
                    _Value(
                      label: 'Committed storage',
                      value: _bytes(widget.plan.committedBytes),
                    ),
                    _Value(
                      label: 'Reserved uploads',
                      value: _bytes(widget.plan.reservedBytes),
                    ),
                    _Value(
                      label: 'Storage limit',
                      value: _bytes(widget.plan.logicalStorageLimitBytes),
                    ),
                    const SizedBox(height: 12),
                    Semantics(
                      label: widget.plan.eligible
                          ? 'Hosted synchronization eligible'
                          : 'Hosted synchronization needs attention',
                      child: Chip(
                        avatar: Icon(
                          widget.plan.eligible
                              ? Icons.check_circle_outline
                              : Icons.info_outline,
                        ),
                        label: Text(
                          widget.plan.eligible
                              ? 'Hosted synchronization eligible'
                              : 'Plan attention required',
                        ),
                      ),
                    ),
                    const SizedBox(height: 20),
                    ref
                        .watch(billingOfferingProvider)
                        .when(
                          loading: () => const LinearProgressIndicator(),
                          error: (_, _) => const Text(
                            'Secure billing actions are unavailable.',
                          ),
                          data: (offering) => offering == null
                              ? const Text(
                                  'Secure billing actions are unavailable.',
                                )
                              : Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      offering.hasBillingAccount
                                          ? 'Billing is managed securely by Stripe.'
                                          : '${offering.displayName}. Price and payment details are shown in secure checkout.',
                                    ),
                                    const SizedBox(height: 12),
                                    FilledButton.icon(
                                      onPressed: _busy
                                          ? null
                                          : () => _openBilling(offering),
                                      icon: Icon(
                                        offering.hasBillingAccount
                                            ? Icons.open_in_new
                                            : Icons.lock_outline,
                                      ),
                                      label: Text(
                                        offering.hasBillingAccount
                                            ? 'Manage billing'
                                            : 'Continue to secure checkout',
                                      ),
                                    ),
                                    if (_message != null) ...[
                                      const SizedBox(height: 12),
                                      Text(_message!),
                                    ],
                                  ],
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

  Future<void> _openBilling(BillingOffering offering) async {
    setState(() {
      _busy = true;
      _message = null;
    });
    try {
      final api = ref.read(showVaultApiProvider);
      final session = offering.hasBillingAccount
          ? await api.createPortalSession(
              accessToken: widget.accessToken,
              organizationId: widget.organizationId,
            )
          : await api.createCheckoutSession(
              accessToken: widget.accessToken,
              organizationId: widget.organizationId,
              offeringCode: offering.code,
            );
      if (session.url.scheme != 'https' ||
          !await ref.read(hostedBillingUrlOpenerProvider)(session.url)) {
        throw StateError('The secure billing page could not be opened.');
      }
      if (!offering.hasBillingAccount) {
        _message = 'Payment processing—refresh plan status after checkout.';
      }
    } catch (_) {
      _message = 'The secure billing page could not be opened safely.';
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

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
