import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/recovery/recovery_history_provider.dart';
import 'package:showvault_app/src/recovery/recovery_run.dart';

class DashboardScreen extends ConsumerWidget {
  const DashboardScreen({super.key});

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

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final runs = ref.watch(recoveryHistoryProvider);
    final latest = runs.firstOrNull;
    final stages = latest?.stages ?? _emptyStages;
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
                      'Recovery history from Scan through Restore.',
                      style: TextStyle(color: colors.onSurfaceVariant),
                    ),
                  ],
                ),
                Chip(
                  avatar: Icon(
                    latest == null
                        ? Icons.history_toggle_off
                        : Icons.history_rounded,
                    size: 18,
                  ),
                  label: Text(
                    latest == null
                        ? 'No recorded runs'
                        : '${runs.length} recorded ${runs.length == 1 ? 'run' : 'runs'}',
                  ),
                ),
              ],
            ),
            const SizedBox(height: 24),
            _ReadinessCard(run: latest),
            const SizedBox(height: 24),
            Text(
              'Recovery loop',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 12),
            LayoutBuilder(
              builder: (context, constraints) {
                final columns = constraints.maxWidth >= 900
                    ? 4
                    : constraints.maxWidth >= 520
                    ? 2
                    : 1;
                final width =
                    (constraints.maxWidth - (columns - 1) * 12) / columns;
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
            _RecentRunCard(run: latest),
          ],
        ),
      ),
    );
  }
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

class _RecentRunCard extends StatelessWidget {
  const _RecentRunCard({required this.run});

  final RecoveryRun? run;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    if (run == null) {
      return Card(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Text(
            'No recovery history yet.',
            style: TextStyle(color: colors.onSurfaceVariant),
          ),
        ),
      );
    }

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Row(
          children: [
            CircleAvatar(
              backgroundColor: colors.primaryContainer,
              child: Icon(Icons.dns_outlined, color: colors.onPrimaryContainer),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    run!.agentName,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  Text('Started ${run!.startedAt.toUtc().toIso8601String()}'),
                ],
              ),
            ),
            _StatusBadge(status: run!.status),
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
