import 'dart:convert';
import 'dart:io';

import 'package:showvault_app/src/recovery/windows_evidence_bridge_preparer.dart';

Future<void> main(List<String> arguments) async {
  if (arguments.length != 3) {
    stderr.writeln(
      'Usage: dart run tool/prepare_windows_evidence_bridge.dart '
      '<source-commit-sha> <source-workflow> <absent-output-workflow>',
    );
    exitCode = 64;
    return;
  }
  try {
    final result = await const WindowsEvidenceBridgePreparer().prepare(
      sourceCommitSha: arguments[0],
      sourceWorkflow: File(arguments[1]),
      outputWorkflow: File(arguments[2]),
    );
    stdout.writeln(const JsonEncoder.withIndent('  ').convert(result.toJson()));
  } on FileSystemException {
    stderr.writeln(
      'Windows evidence bridge preparation failed: filesystem boundary.',
    );
    exitCode = 1;
  } on FormatException catch (error) {
    stderr.writeln(
      'Windows evidence bridge preparation failed: ${error.message}',
    );
    exitCode = 1;
  }
}
