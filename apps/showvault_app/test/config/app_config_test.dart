import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/config/app_config.dart';

void main() {
  test('normal builds keep the resilience command mode disabled', () {
    expect(AppConfig.resilienceHarnessEnabled, isFalse);
    expect(AppConfig.canRunResilienceHarness, isFalse);
  });
}
