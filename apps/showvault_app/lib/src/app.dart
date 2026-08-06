import 'package:flutter/material.dart';
import 'package:showvault_app/src/navigation/app_router.dart';
import 'package:showvault_app/src/theme/app_theme.dart';

class ShowVaultApp extends StatelessWidget {
  const ShowVaultApp({super.key});

  @override
  Widget build(BuildContext context) => MaterialApp.router(
    title: 'ShowVault',
    debugShowCheckedModeBanner: false,
    theme: AppTheme.light,
    darkTheme: AppTheme.dark,
    themeMode: ThemeMode.system,
    routerConfig: appRouter,
  );
}
