import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/app.dart';
import 'package:showvault_app/src/config/app_config.dart';
import 'package:showvault_app/src/recovery/resilience_harness.dart';
import 'package:showvault_app/src/recovery/upgrade_diagnostic_harness.dart';

Future<void> main(List<String> arguments) async {
  if (AppConfig.upgradeHarnessEnabled &&
      await UpgradeDiagnosticHarness.tryRun(arguments)) {
    await _flushAndExit();
  }
  if (AppConfig.resilienceHarnessEnabled &&
      await ResilienceHarness.tryRun(arguments)) {
    await _flushAndExit();
  }
  runApp(const ProviderScope(child: ShowVaultApp()));
}

Future<Never> _flushAndExit() async {
  await stdout.flush();
  await stderr.flush();
  exit(exitCode);
}
