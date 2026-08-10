import 'dart:convert';
import 'dart:io';

import 'src/authorization_readiness_preflight.dart';

Future<void> main(List<String> arguments) async {
  if (arguments.isNotEmpty) {
    stderr.writeln(
      'Usage: dart run tool/verify_local_authorization_readiness.dart',
    );
    exitCode = 64;
    return;
  }
  try {
    final report = await AuthorizationReadinessPreflight().verify(
      File('../../docs/LOCAL_AUTHORIZATION_READINESS_MATRIX.md'),
    );
    stdout.writeln(const JsonEncoder.withIndent('  ').convert(report.toJson()));
  } on FormatException catch (error) {
    stderr.writeln(
      'Local authorization readiness preflight failed: ${error.message}',
    );
    exitCode = 1;
  } on FileSystemException {
    stderr.writeln(
      'Local authorization readiness preflight failed: local file boundary.',
    );
    exitCode = 1;
  }
}
