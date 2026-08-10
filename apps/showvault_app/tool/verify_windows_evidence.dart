import 'dart:convert';
import 'dart:io';

import 'package:showvault_app/src/recovery/windows_evidence_verifier.dart';

Future<void> main(List<String> arguments) async {
  if (arguments.length != 1) {
    stderr.writeln(
      'Usage: dart run tool/verify_windows_evidence.dart <extracted-artifact-directory>',
    );
    exitCode = 64;
    return;
  }
  try {
    final result = await const WindowsEvidenceVerifier().verify(
      Directory(arguments.single),
    );
    stdout.writeln(const JsonEncoder.withIndent('  ').convert(result.toJson()));
  } on FileSystemException {
    stderr.writeln(
      'Windows evidence verification failed: filesystem boundary.',
    );
    exitCode = 1;
  } on FormatException catch (error) {
    stderr.writeln('Windows evidence verification failed: ${error.message}');
    exitCode = 1;
  }
}
