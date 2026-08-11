import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

void main() {
  final harness = File(
    '${Directory.current.path}${Platform.pathSeparator}lib'
    '${Platform.pathSeparator}src${Platform.pathSeparator}recovery'
    '${Platform.pathSeparator}upgrade_diagnostic_harness.dart',
  );
  final entrypoint = File(
    '${Directory.current.path}${Platform.pathSeparator}lib'
    '${Platform.pathSeparator}main.dart',
  );

  test('upgrade harness emits only bounded status categories', () {
    final text = harness.readAsStringSync();

    expect(text, contains("'SHOWVAULT_UPGRADE_STATUS:'"));
    expect(text, contains("'\${_statusPrefix}unavailable-configuration'"));
    expect(text, contains("'\${_statusPrefix}unsupported-phase'"));
    expect(text, contains("'\$_statusPrefix\$phase-passed'"));
    expect(text, contains("'\$_statusPrefix\$phase-harness-failed'"));
    expect(text, contains('catch (_)'));
    expect(text, isNot(contains('catch (error)')));
    expect(text, isNot(contains('catch (exception)')));
    expect(text, isNot(contains('stderr.writeln(error')));
    expect(text, isNot(contains('stderr.writeln(exception')));
  });

  test('harness termination flushes bounded output before process exit', () {
    final text = entrypoint.readAsStringSync();
    final helperStart = text.indexOf('Future<Never> _flushAndExit() async {');
    final stdoutFlush = text.indexOf('await stdout.flush();', helperStart);
    final stderrFlush = text.indexOf('await stderr.flush();', stdoutFlush);
    final processExit = text.indexOf('exit(exitCode);', stderrFlush);

    expect(helperStart, greaterThanOrEqualTo(0));
    expect(stdoutFlush, greaterThan(helperStart));
    expect(stderrFlush, greaterThan(stdoutFlush));
    expect(processExit, greaterThan(stderrFlush));
    expect(RegExp(r'await _flushAndExit\(\);').allMatches(text), hasLength(2));
    expect(text, isNot(contains('    exit(exitCode);')));
  });
}
