import 'package:flutter/material.dart';

class DashboardScreen extends StatelessWidget {
  const DashboardScreen({super.key});

  static const _actions = [
    (Icons.radar_rounded, 'Scan', 'Discover production systems'),
    (Icons.inventory_2_rounded, 'Backup', 'Create a protected package'),
    (Icons.verified_rounded, 'Verify', 'Prove recovery readiness'),
    (Icons.restore_rounded, 'Restore', 'Recover with confidence'),
  ];

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    return Scaffold(
      appBar: AppBar(title: const Text('ShowVault'), actions: [
        IconButton(onPressed: () {}, icon: const Icon(Icons.search), tooltip: 'Search'),
        IconButton(onPressed: () {}, icon: const Icon(Icons.notifications_outlined), tooltip: 'Notifications'),
        const SizedBox(width: 8),
      ]),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 1120),
          child: ListView(
            padding: const EdgeInsets.all(24),
            children: [
              Text('Production readiness', style: Theme.of(context).textTheme.headlineMedium),
              const SizedBox(height: 8),
              Text('Protect and recover your production environment.', style: TextStyle(color: colors.onSurfaceVariant)),
              const SizedBox(height: 24),
              Wrap(
                spacing: 16,
                runSpacing: 16,
                children: _actions.map((item) => _ActionCard(icon: item.$1, title: item.$2, subtitle: item.$3)).toList(),
              ),
              const SizedBox(height: 28),
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Row(children: [
                    CircleAvatar(radius: 28, backgroundColor: colors.primaryContainer, child: Text('—', style: Theme.of(context).textTheme.headlineSmall)),
                    const SizedBox(width: 16),
                    const Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Text('Recovery Confidence'),
                      SizedBox(height: 4),
                      Text('Run your first scan to establish a baseline.'),
                    ])),
                  ]),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ActionCard extends StatelessWidget {
  const _ActionCard({required this.icon, required this.title, required this.subtitle});
  final IconData icon;
  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) => SizedBox(
        width: 250,
        child: Card(
          clipBehavior: Clip.antiAlias,
          child: InkWell(
            onTap: () {},
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Icon(icon, size: 32),
                const SizedBox(height: 18),
                Text(title, style: Theme.of(context).textTheme.titleLarge),
                const SizedBox(height: 6),
                Text(subtitle),
              ]),
            ),
          ),
        ),
      );
}
