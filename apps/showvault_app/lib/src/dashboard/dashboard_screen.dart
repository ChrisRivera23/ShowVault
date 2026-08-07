import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/api/showvault_api.dart';
import 'package:showvault_app/src/auth/auth_provider.dart';
import 'package:showvault_app/src/config/app_config.dart';
import 'package:showvault_app/src/recovery/recovery_history_provider.dart';
import 'package:showvault_app/src/recovery/recovery_run.dart';

class DashboardScreen extends ConsumerWidget {
  const DashboardScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (!AppConfig.hasAuth0Client) return const _ConfigurationRequired();
    return ref
        .watch(authSessionProvider)
        .when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => _AuthPrompt(error: error),
          data: (session) => session == null
              ? const _AuthPrompt()
              : ref
                    .watch(recoveryHistoryProvider)
                    .when(
                      loading: () =>
                          const Center(child: CircularProgressIndicator()),
                      error: (error, _) => _LoadError(error: error),
                      data: (history) => _LiveDashboard(history: history),
                    ),
        );
  }
}

class _ConfigurationRequired extends StatelessWidget {
  const _ConfigurationRequired();

  @override
  Widget build(BuildContext context) => const _CenteredCard(
    icon: Icons.settings_suggest_outlined,
    title: 'Auth0 client configuration required',
    message:
        'Run with --dart-define=AUTH0_CLIENT_ID=… to connect this native client. See the Flutter app README for the complete command.',
  );
}

class _AuthPrompt extends ConsumerWidget {
  const _AuthPrompt({this.error});
  final Object? error;

  @override
  Widget build(BuildContext context, WidgetRef ref) => _CenteredCard(
    icon: Icons.lock_outline,
    title: 'Sign in to ShowVault',
    message: error == null
        ? 'Use your operator identity to load live, tenant-scoped recovery history.'
        : 'Sign-in did not complete. $error',
    action: FilledButton.icon(
      onPressed: () => ref.read(authSessionProvider.notifier).login(),
      icon: const Icon(Icons.login),
      label: const Text('Sign in with Auth0'),
    ),
  );
}

class _LoadError extends ConsumerWidget {
  const _LoadError({required this.error});
  final Object error;

  @override
  Widget build(BuildContext context, WidgetRef ref) => _CenteredCard(
    icon: Icons.cloud_off_outlined,
    title: 'Live history unavailable',
    message: '$error',
    action: FilledButton.icon(
      onPressed: () => ref.invalidate(recoveryHistoryProvider),
      icon: const Icon(Icons.refresh),
      label: const Text('Retry'),
    ),
  );
}

class _LiveDashboard extends ConsumerWidget {
  const _LiveDashboard({required this.history});
  final RecoveryHistory history;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final colors = Theme.of(context).colorScheme;
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 1180),
        child: ListView(
          padding: const EdgeInsets.fromLTRB(24, 24, 24, 48),
          children: [
            Wrap(
              alignment: WrapAlignment.spaceBetween,
              crossAxisAlignment: WrapCrossAlignment.center,
              spacing: 16,
              children: [
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Recovery overview',
                      style: Theme.of(context).textTheme.headlineMedium,
                    ),
                    const SizedBox(height: 6),
                    Text(
                      '${history.organizationName} • ${history.venueName}',
                      style: TextStyle(color: colors.onSurfaceVariant),
                    ),
                  ],
                ),
                const Chip(
                  avatar: Icon(Icons.cloud_done_outlined, size: 18),
                  label: Text('Live data'),
                ),
              ],
            ),
            const SizedBox(height: 24),
            if (history.runs.isEmpty)
              const _CenteredCard(
                icon: Icons.history_toggle_off_outlined,
                title: 'No recovery runs yet',
                message:
                    'Live tenant access is working. A completed Agent workflow will appear here.',
              )
            else
              _HistoryContent(runs: history.runs),
            const SizedBox(height: 24),
            Align(
              alignment: Alignment.centerRight,
              child: TextButton.icon(
                onPressed: () =>
                    ref.read(authSessionProvider.notifier).logout(),
                icon: const Icon(Icons.logout),
                label: const Text('Sign out'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _HistoryContent extends StatelessWidget {
  const _HistoryContent({required this.runs});
  final List<RecoveryRun> runs;

  @override
  Widget build(BuildContext context) {
    final latest = runs.first;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _ReadinessCard(run: latest),
        const SizedBox(height: 24),
        Text('Recovery loop', style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 12),
        LayoutBuilder(
          builder: (context, constraints) {
            final columns = constraints.maxWidth >= 900
                ? 4
                : constraints.maxWidth >= 520
                ? 2
                : 1;
            final width = (constraints.maxWidth - (columns - 1) * 12) / columns;
            return Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                for (final stage in latest.stages)
                  SizedBox(
                    width: width,
                    child: _StageCard(stage: stage),
                  ),
              ],
            );
          },
        ),
        const SizedBox(height: 28),
        Text(
          'Recent recovery runs',
          style: Theme.of(context).textTheme.titleLarge,
        ),
        const SizedBox(height: 12),
        for (final run in runs)
          Card(
            child: ListTile(
              leading: const Icon(Icons.dns_outlined),
              title: Text(run.agentName),
              subtitle: Text(run.startedAt.toLocal().toString()),
              trailing: _StatusBadge(status: run.status),
            ),
          ),
      ],
    );
  }
}

class _CenteredCard extends StatelessWidget {
  const _CenteredCard({
    required this.icon,
    required this.title,
    required this.message,
    this.action,
  });
  final IconData icon;
  final String title;
  final String message;
  final Widget? action;

  @override
  Widget build(BuildContext context) => Center(
    child: ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 560),
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(icon, size: 46),
              const SizedBox(height: 18),
              Text(
                title,
                style: Theme.of(context).textTheme.headlineSmall,
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 10),
              Text(message, textAlign: TextAlign.center),
              if (action != null) ...[const SizedBox(height: 22), action!],
            ],
          ),
        ),
      ),
    ),
  );
}

class _ReadinessCard extends StatelessWidget {
  const _ReadinessCard({required this.run});
  final RecoveryRun run;

  @override
  Widget build(BuildContext context) {
    final completed = run.status == RecoveryRunStatus.completed;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Row(
          children: [
            Icon(
              completed ? Icons.shield_rounded : Icons.pending_actions_rounded,
              size: 42,
            ),
            const SizedBox(width: 18),
            Expanded(
              child: Text(
                completed
                    ? 'Recovery loop proven'
                    : 'Recovery loop in progress',
                style: Theme.of(context).textTheme.titleLarge,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _StageCard extends StatelessWidget {
  const _StageCard({required this.stage});
  final RecoveryStage stage;
  static const labels = {
    RecoveryStageKind.scan: (Icons.radar_rounded, 'Scan'),
    RecoveryStageKind.backup: (Icons.inventory_2_rounded, 'Backup'),
    RecoveryStageKind.verify: (Icons.verified_rounded, 'Verify'),
    RecoveryStageKind.restore: (Icons.restore_rounded, 'Restore'),
  };

  @override
  Widget build(BuildContext context) {
    final label = labels[stage.kind]!;
    final completed = stage.status == RecoveryStageStatus.completed;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(18),
        child: Row(
          children: [
            Icon(label.$1, size: 30),
            const SizedBox(width: 14),
            Expanded(
              child: Text(
                label.$2,
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ),
            Icon(completed ? Icons.check_circle : Icons.radio_button_unchecked),
          ],
        ),
      ),
    );
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.status});
  final RecoveryRunStatus status;

  @override
  Widget build(BuildContext context) => Chip(
    label: Text(switch (status) {
      RecoveryRunStatus.pending => 'Pending',
      RecoveryRunStatus.inProgress => 'In progress',
      RecoveryRunStatus.completed => 'Completed',
      RecoveryRunStatus.failed => 'Failed',
    }),
  );
}
