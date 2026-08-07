import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/recovery/recovery_history_provider.dart';
import 'package:showvault_app/src/recovery/recovery_run.dart';

class DashboardScreen extends ConsumerWidget {
  const DashboardScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final runs = ref.watch(recoveryHistoryProvider);
    final latest = runs.first;
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
                      'One clear history from discovery to proven recovery.',
                      style: TextStyle(color: colors.onSurfaceVariant),
                    ),
                  ],
                ),
                const Chip(
                  avatar: Icon(Icons.visibility_outlined, size: 18),
                  label: Text('Foundation preview'),
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
            Card(
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        CircleAvatar(
                          backgroundColor: colors.primaryContainer,
                          child: Icon(
                            Icons.dns_outlined,
                            color: colors.onPrimaryContainer,
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                latest.agentName,
                                style: Theme.of(context).textTheme.titleMedium,
                              ),
                              const Text('Complete recovery rehearsal'),
                            ],
                          ),
                        ),
                        _StatusBadge(status: latest.status),
                      ],
                    ),
                    const SizedBox(height: 18),
                    Text(
                      'Preview data • Live venue history follows Auth0 client sign-in.',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: colors.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ReadinessCard extends StatelessWidget {
  const _ReadinessCard({required this.run});

  final RecoveryRun run;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    return Card(
      color: colors.primaryContainer,
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Row(
          children: [
            CircleAvatar(
              radius: 30,
              backgroundColor: colors.primary,
              child: Icon(
                Icons.shield_rounded,
                color: colors.onPrimary,
                size: 30,
              ),
            ),
            const SizedBox(width: 18),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Recovery loop proven',
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                  const SizedBox(height: 5),
                  const Text(
                    'Scan, package integrity, and controlled restore all have evidence.',
                  ),
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
    final completed = stage.status == RecoveryStageStatus.completed;
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
                ],
              ),
            ),
            Icon(
              completed ? Icons.check_circle : Icons.radio_button_unchecked,
              color: completed ? colors.primary : colors.outline,
            ),
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
    avatar: const Icon(Icons.check_circle, size: 17),
    label: Text(
      status == RecoveryRunStatus.completed ? 'Completed' : 'In progress',
    ),
  );
}
