import 'dart:convert';
import 'dart:io';

import 'src/pr_dependency_ledger_preflight.dart';

Future<void> main(List<String> arguments) async {
  if (arguments.isNotEmpty) {
    stderr.writeln('Usage: dart run tool/verify_pr_dependency_ledger.dart');
    exitCode = 64;
    return;
  }
  try {
    final report = await PrDependencyLedgerPreflight().verify();
    stdout.writeln(const JsonEncoder.withIndent('  ').convert(report.toJson()));
  } on FormatException catch (error) {
    stderr.writeln('PR dependency ledger preflight failed: ${error.message}');
    exitCode = 1;
  } on StateError {
    stderr.writeln(
      'PR dependency ledger preflight failed: local Git boundary.',
    );
    exitCode = 1;
  } on ProcessException {
    stderr.writeln(
      'PR dependency ledger preflight failed: Git is unavailable.',
    );
    exitCode = 1;
  }
}
