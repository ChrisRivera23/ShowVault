import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:showvault_app/src/navigation/app_destination.dart';

class AppShell extends StatelessWidget {
  const AppShell({required this.currentPath, required this.child, super.key});

  final String currentPath;
  final Widget child;

  int get _selectedIndex {
    final index = AppDestination.values.indexWhere(
      (destination) => destination.path == currentPath,
    );
    return index < 0 ? 0 : index;
  }

  void _navigate(BuildContext context, int index) =>
      context.go(AppDestination.values[index].path);

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, constraints) {
      final useRail = constraints.maxWidth >= 760;
      final content = Scaffold(
        appBar: AppBar(
          title: const Text('ShowVault'),
          actions: [
            IconButton(
              onPressed: () {},
              icon: const Icon(Icons.search),
              tooltip: 'Search',
            ),
            IconButton(
              onPressed: () {},
              icon: const Icon(Icons.notifications_outlined),
              tooltip: 'Notifications',
            ),
            const SizedBox(width: 8),
          ],
        ),
        body: child,
        bottomNavigationBar: useRail
            ? null
            : NavigationBar(
                selectedIndex: _selectedIndex.clamp(0, 4),
                onDestinationSelected: (index) => _navigate(context, index),
                destinations: [
                  for (final destination in AppDestination.values.take(5))
                    NavigationDestination(
                      icon: Icon(destination.icon),
                      label: destination.label,
                    ),
                ],
              ),
      );

      if (!useRail) return content;

      return Scaffold(
        body: Row(
          children: [
            SizedBox(
              width: 280,
              child: NavigationDrawer(
                selectedIndex: _selectedIndex,
                onDestinationSelected: (index) => _navigate(context, index),
                children: [
                  const Padding(
                    padding: EdgeInsets.fromLTRB(28, 20, 16, 12),
                    child: Row(
                      children: [
                        Icon(Icons.shield_outlined),
                        SizedBox(width: 12),
                        Text('ShowVault'),
                      ],
                    ),
                  ),
                  for (final destination in AppDestination.values)
                    NavigationDrawerDestination(
                      icon: Icon(destination.icon),
                      label: Text(destination.label),
                    ),
                ],
              ),
            ),
            const VerticalDivider(width: 1),
            Expanded(child: content),
          ],
        ),
      );
    },
  );
}
