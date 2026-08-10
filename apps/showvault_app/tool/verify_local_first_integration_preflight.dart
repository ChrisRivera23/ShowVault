import 'dart:convert';
import 'dart:io';

import 'src/local_first_integration_preflight.dart';

Future<void> main(List<String> arguments) async {
  if (arguments.isNotEmpty) {
    stderr.writeln(
      'Usage: dart run tool/verify_local_first_integration_preflight.dart',
    );
    exitCode = 64;
    return;
  }
  try {
    final report = await LocalFirstIntegrationPreflight().verify();
    stdout.writeln(const JsonEncoder.withIndent('  ').convert(report.toJson()));
  } on FormatException catch (error) {
    stderr.writeln(
      'Local-first integration preflight failed: ${error.message}',
    );
    exitCode = 1;
  } on StateError {
    stderr.writeln(
      'Local-first integration preflight failed: local Git boundary.',
    );
    exitCode = 1;
  } on ProcessException {
    stderr.writeln(
      'Local-first integration preflight failed: Git is unavailable.',
    );
    exitCode = 1;
  }
}
