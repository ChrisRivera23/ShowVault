import 'dart:convert';
import 'dart:io';

import 'package:showvault_app/src/recovery/windows_evidence_run_verifier.dart';

Future<void> main(List<String> arguments) async {
  if (arguments.length != 2) {
    stderr.writeln(
      'Usage: dart run tool/verify_windows_run.dart <workflow-run-id> <absent-output-directory>',
    );
    exitCode = 64;
    return;
  }
  try {
    final result = await WindowsEvidenceRunVerifier().verify(
      runId: arguments[0],
      outputDirectory: Directory(arguments[1]),
    );
    stdout.writeln(const JsonEncoder.withIndent('  ').convert(result.toJson()));
  } on FileSystemException {
    stderr.writeln('Windows run verification failed: filesystem boundary.');
    exitCode = 1;
  } on FormatException catch (error) {
    stderr.writeln('Windows run verification failed: ${error.message}');
    exitCode = 1;
  }
}
