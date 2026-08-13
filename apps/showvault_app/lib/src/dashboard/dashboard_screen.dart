import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/api/showvault_api.dart';
import 'package:showvault_app/src/auth/auth_provider.dart';
import 'package:showvault_app/src/config/app_config.dart';
import 'package:showvault_app/src/recovery/recovery_history_provider.dart';
import 'package:showvault_app/src/recovery/recovery_run.dart';
import 'package:showvault_app/src/scanning/local_catalog_scanner.dart';

class DashboardScreen extends ConsumerWidget {
  const DashboardScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authSessionProvider);
    final session = auth.valueOrNull;
    final history = session == null
        ? null
        : ref.watch(recoveryHistoryProvider).valueOrNull;
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(24, 24, 24, 0),
          child: _DesktopScanCard(
            accessToken: session?.accessToken,
            history: history,
          ),
        ),
        Expanded(
          child: !AppConfig.hasAuth0Client && !AppConfig.personalBetaBypassAuth
              ? const _ConfigurationRequired()
              : auth.when(
                  loading: () =>
                      const Center(child: CircularProgressIndicator()),
                  error: (_, _) => const SingleChildScrollView(
                    child: _AuthPrompt(hasError: true),
                  ),
                  data: (currentSession) => currentSession == null
                      ? const SingleChildScrollView(child: _AuthPrompt())
                      : ref
                            .watch(recoveryHistoryProvider)
                            .when(
                              loading: () => const Center(
                                child: CircularProgressIndicator(),
                              ),
                              error: (_, _) => const _LoadError(),
                              data: (currentHistory) => RecoveryHistoryView(
                                history: currentHistory,
                                onSignOut: AppConfig.personalBetaBypassAuth
                                    ? null
                                    : () => ref
                                          .read(authSessionProvider.notifier)
                                          .logout(),
                              ),
                            ),
                ),
        ),
      ],
    );
  }
}

class _DesktopScanCard extends ConsumerStatefulWidget {
  const _DesktopScanCard({this.accessToken, this.history});

  final String? accessToken;
  final RecoveryHistory? history;

  @override
  ConsumerState<_DesktopScanCard> createState() => _DesktopScanCardState();
}

class _DesktopScanCardState extends ConsumerState<_DesktopScanCard> {
  List<String>? _findings;
  bool _scanning = false;
  String? _cloudError;

  Future<void> _scan() async {
    setState(() {
      _scanning = true;
      _cloudError = null;
    });
    final findings = await ref.read(localCatalogScannerProvider).scan();
    if (!mounted) return;
    setState(() {
      _findings = findings;
      _scanning = false;
    });

    final token = widget.accessToken;
    final history = widget.history;
    if (token == null || history == null || history.venueId.isEmpty) return;
    try {
      await ref
          .read(showVaultApiProvider)
          .submitComputerScan(
            accessToken: token,
            history: history,
            candidateKeys: findings,
          );
      ref.invalidate(recoveryHistoryProvider);
    } catch (error) {
      if (!mounted) return;
      setState(() => _cloudError = '$error');
    }
  }

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(20),
      child: Row(
        children: [
          const Icon(Icons.computer_outlined),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Scan this computer',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const Text(
                  'Checks only exact ShowVault catalog locations. Paths stay in memory on this computer.',
                ),
                if (_findings != null)
                  Text(
                    '${_findings!.length} recognized candidate(s) detected.',
                  ),
                if (_cloudError != null)
                  Text(
                    'Cloud submission failed; local findings were kept.',
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
              ],
            ),
          ),
          const SizedBox(width: 16),
          FilledButton.icon(
            onPressed: _scanning ? null : _scan,
            icon: const Icon(Icons.search),
            label: Text(_scanning ? 'Scanning…' : 'Scan'),
          ),
        ],
      ),
    ),
  );
}

class RecoveryHistoryView extends StatelessWidget {
  const RecoveryHistoryView({required this.history, this.onSignOut, super.key});

  final RecoveryHistory history;
  final VoidCallback? onSignOut;

  @override
  Widget build(BuildContext context) {
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
              runSpacing: 12,
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
            _HistoryContent(runs: history.runs),
            if (onSignOut != null) ...[
              const SizedBox(height: 24),
              Align(
                alignment: Alignment.centerRight,
                child: TextButton.icon(
                  onPressed: onSignOut,
                  icon: const Icon(Icons.logout),
                  label: const Text('Sign out'),
                ),
              ),
            ],
          ],
        ),
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
        'Configure the public native Client ID before connecting this app. See the Flutter app README for the complete command.',
  );
}

class _AuthPrompt extends ConsumerWidget {
  const _AuthPrompt({this.hasError = false});

  final bool hasError;

  @override
  Widget build(BuildContext context, WidgetRef ref) => _CenteredCard(
    icon: Icons.lock_outline,
    title: 'Sign in to ShowVault',
    message: hasError
        ? 'Sign-in did not complete. Try again or contact your ShowVault administrator.'
        : 'Use your operator identity to load live, tenant-scoped recovery history.',
    action: FilledButton.icon(
      onPressed: () => ref.read(authSessionProvider.notifier).login(),
      icon: const Icon(Icons.login),
      label: const Text('Sign in with Auth0'),
    ),
  );
}

class _LoadError extends ConsumerWidget {
  const _LoadError();

  @override
  Widget build(BuildContext context, WidgetRef ref) => _CenteredCard(
    icon: Icons.cloud_off_outlined,
    title: 'Live history unavailable',
    message:
        'ShowVault could not load recovery history. Check the connection and try again.',
    action: FilledButton.icon(
      onPressed: () => ref.invalidate(recoveryHistoryProvider),
      icon: const Icon(Icons.refresh),
      label: const Text('Retry'),
    ),
  );
}

class _HistoryContent extends StatelessWidget {
  const _HistoryContent({required this.runs});

  static const _emptyStages = [
    RecoveryStage(
      kind: RecoveryStageKind.scan,
      status: RecoveryStageStatus.notStarted,
    ),
    RecoveryStage(
      kind: RecoveryStageKind.backup,
      status: RecoveryStageStatus.notStarted,
    ),
    RecoveryStage(
      kind: RecoveryStageKind.verify,
      status: RecoveryStageStatus.notStarted,
    ),
    RecoveryStage(
      kind: RecoveryStageKind.restore,
      status: RecoveryStageStatus.notStarted,
    ),
  ];

  final List<RecoveryRun> runs;

  @override
  Widget build(BuildContext context) {
    final latest = runs.firstOrNull;
    final stages = latest?.stages ?? _emptyStages;
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
                for (final stage in stages)
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
        if (runs.isEmpty)
          const Card(
            child: Padding(
              padding: EdgeInsets.all(20),
              child: Text('No recovery history yet.'),
            ),
          )
        else
          for (final run in runs)
            Card(
              child: ListTile(
                leading: const Icon(Icons.dns_outlined),
                title: Text(run.agentName),
                subtitle: Text(run.startedAt.toUtc().toIso8601String()),
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
          padding: const EdgeInsets.all(24),
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

  final RecoveryRun? run;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final content = switch (run?.status) {
      null => (
        Icons.shield_outlined,
        'No recovery evidence yet',
        'Run a scan to begin a recorded recovery history.',
      ),
      RecoveryRunStatus.pending => (
        Icons.schedule_rounded,
        'Recovery run queued',
        'The latest scan command is waiting to start.',
      ),
      RecoveryRunStatus.inProgress => (
        Icons.sync_rounded,
        'Recovery run in progress',
        'The latest run has started but is not complete.',
      ),
      RecoveryRunStatus.completed => (
        Icons.task_alt_rounded,
        'Recovery loop completed',
        'The latest Restore command reports a completed outcome.',
      ),
      RecoveryRunStatus.failed => (
        Icons.error_outline_rounded,
        'Recovery action failed',
        'Review the failed stage before relying on this recovery run.',
      ),
      RecoveryRunStatus.expired => (
        Icons.timer_off_outlined,
        'Recovery action expired',
        'The command expired before the recovery run could continue.',
      ),
    };
    final isFailure = run?.status == RecoveryRunStatus.failed;
    return Card(
      color: isFailure ? colors.errorContainer : colors.primaryContainer,
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Row(
          children: [
            CircleAvatar(
              radius: 30,
              backgroundColor: isFailure ? colors.error : colors.primary,
              child: Icon(
                content.$1,
                color: isFailure ? colors.onError : colors.onPrimary,
                size: 30,
              ),
            ),
            const SizedBox(width: 18),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    content.$2,
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                  const SizedBox(height: 5),
                  Text(content.$3),
                ],
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

  static const _content = {
    RecoveryStageKind.scan: (Icons.radar_rounded, 'Scan', 'Inventory captured'),
    RecoveryStageKind.backup: (
      Icons.inventory_2_rounded,
      'Backup',
      'Immutable package',
    ),
    RecoveryStageKind.verify: (
      Icons.verified_rounded,
      'Verify',
      'SHA-256 evidence',
    ),
    RecoveryStageKind.restore: (
      Icons.restore_rounded,
      'Restore',
      'Controlled target',
    ),
  };

  @override
  Widget build(BuildContext context) {
    final content = _content[stage.kind]!;
    final colors = Theme.of(context).colorScheme;
    final status = switch (stage.status) {
      RecoveryStageStatus.notStarted => (
        Icons.radio_button_unchecked,
        'Not started',
        colors.outline,
      ),
      RecoveryStageStatus.pending => (
        Icons.schedule_outlined,
        'Pending',
        colors.secondary,
      ),
      RecoveryStageStatus.inProgress => (
        Icons.sync_rounded,
        'In progress',
        colors.primary,
      ),
      RecoveryStageStatus.completed => (
        Icons.check_circle,
        'Completed',
        colors.primary,
      ),
      RecoveryStageStatus.failed => (
        Icons.error_outline,
        'Failed',
        colors.error,
      ),
      RecoveryStageStatus.expired => (
        Icons.timer_off_outlined,
        'Expired',
        colors.tertiary,
      ),
    };
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(18),
        child: Row(
          children: [
            Icon(content.$1, color: colors.primary, size: 30),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    content.$2,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 3),
                  Text(
                    content.$3,
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                  const SizedBox(height: 4),
                  Text(status.$2, style: TextStyle(color: status.$3)),
                ],
              ),
            ),
            Icon(status.$1, color: status.$3),
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
  Widget build(BuildContext context) {
    final content = switch (status) {
      RecoveryRunStatus.pending => (Icons.schedule_outlined, 'Pending'),
      RecoveryRunStatus.inProgress => (Icons.sync_rounded, 'In progress'),
      RecoveryRunStatus.completed => (Icons.check_circle, 'Completed'),
      RecoveryRunStatus.failed => (Icons.error_outline, 'Failed'),
      RecoveryRunStatus.expired => (Icons.timer_off_outlined, 'Expired'),
    };
    return Chip(avatar: Icon(content.$1, size: 17), label: Text(content.$2));
  }
}
