import 'package:flutter/material.dart';
import 'package:showvault_app/src/dashboard/dashboard_screen.dart';
import 'package:showvault_app/src/theme/app_theme.dart';

class ShowVaultApp extends StatelessWidget {
  const ShowVaultApp({super.key});

  @override
  Widget build(BuildContext context) => MaterialApp(
        title: 'ShowVault',
        debugShowCheckedModeBanner: false,
        theme: AppTheme.light,
        darkTheme: AppTheme.dark,
        themeMode: ThemeMode.system,
        home: const DashboardScreen(),
      );
}
