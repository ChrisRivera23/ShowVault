import 'package:go_router/go_router.dart';
import 'package:showvault_app/src/dashboard/dashboard_screen.dart';
import 'package:showvault_app/src/navigation/app_destination.dart';
import 'package:showvault_app/src/navigation/app_shell.dart';
import 'package:showvault_app/src/navigation/section_screen.dart';
import 'package:showvault_app/src/settings/plan_storage_screen.dart';

final GoRouter appRouter = GoRouter(
  routes: [
    ShellRoute(
      builder: (context, state, child) =>
          AppShell(currentPath: state.uri.path, child: child),
      routes: [
        GoRoute(
          path: AppDestination.dashboard.path,
          builder: (context, state) => const DashboardScreen(),
        ),
        GoRoute(
          path: AppDestination.settings.path,
          builder: (context, state) => const PlanStorageScreen(),
        ),
        for (final destination in AppDestination.values.skip(1))
          if (destination != AppDestination.settings)
            GoRoute(
              path: destination.path,
              builder: (context, state) =>
                  SectionScreen(destination: destination),
            ),
      ],
    ),
  ],
);
