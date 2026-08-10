import 'dart:convert';
import 'dart:io';

import 'package:showvault_app/src/recovery/windows_evidence_bridge_verifier.dart';

Future<void> main(List<String> arguments) async {
  if (arguments.length != 3) {
    stderr.writeln(
      'Usage: dart run tool/verify_windows_evidence_bridge.dart '
      '<source-commit-sha> <source-workflow> <bridge-workflow>',
    );
    exitCode = 64;
    return;
  }
  try {
    final result = await const WindowsEvidenceBridgeVerifier().verify(
      sourceCommitSha: arguments[0],
      sourceWorkflow: File(arguments[1]),
      bridgeWorkflow: File(arguments[2]),
    );
    stdout.writeln(const JsonEncoder.withIndent('  ').convert(result.toJson()));
  } on FileSystemException {
    stderr.writeln(
      'Windows evidence bridge verification failed: filesystem boundary.',
    );
    exitCode = 1;
  } on FormatException catch (error) {
    stderr.writeln(
      'Windows evidence bridge verification failed: ${error.message}',
    );
    exitCode = 1;
  }
}
