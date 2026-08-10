import 'dart:convert';
import 'dart:io';

import 'src/local_integration_plan_preflight.dart';

Future<void> main(List<String> arguments) async {
  if (arguments.isNotEmpty) {
    stderr.writeln('Usage: dart run tool/verify_local_integration_plan.dart');
    exitCode = 64;
    return;
  }
  try {
    final report = await LocalIntegrationPlanPreflight().verify();
    stdout.writeln(const JsonEncoder.withIndent('  ').convert(report.toJson()));
  } on FormatException catch (error) {
    stderr.writeln('Local integration plan preflight failed: ${error.message}');
    exitCode = 1;
  } on StateError {
    stderr.writeln(
      'Local integration plan preflight failed: local Git boundary.',
    );
    exitCode = 1;
  } on ProcessException {
    stderr.writeln('Local integration plan preflight failed: Git unavailable.');
    exitCode = 1;
  }
}
