import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/app.dart';
import 'package:showvault_app/src/config/app_config.dart';
import 'package:showvault_app/src/recovery/resilience_harness.dart';

Future<void> main(List<String> arguments) async {
  if (AppConfig.resilienceHarnessEnabled &&
      await ResilienceHarness.tryRun(arguments)) {
    exit(exitCode);
  }
  runApp(const ProviderScope(child: ShowVaultApp()));
}
