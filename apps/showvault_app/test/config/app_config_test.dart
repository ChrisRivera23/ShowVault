import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/config/app_config.dart';

void main() {
  test('normal builds keep synthetic command modes disabled', () {
    expect(AppConfig.resilienceHarnessEnabled, isFalse);
    expect(AppConfig.canRunResilienceHarness, isFalse);
    expect(AppConfig.upgradeHarnessEnabled, isFalse);
    expect(AppConfig.canRunUpgradeHarness, isFalse);
  });
}
