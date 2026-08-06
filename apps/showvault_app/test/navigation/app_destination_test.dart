import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/navigation/app_destination.dart';

void main() {
  test('all navigation paths and labels are unique', () {
    final paths = AppDestination.values.map((item) => item.path).toSet();
    final labels = AppDestination.values.map((item) => item.label).toSet();

    expect(paths.length, AppDestination.values.length);
    expect(labels.length, AppDestination.values.length);
  });
}
