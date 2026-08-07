import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/app.dart';

void main() {
  testWidgets('shows the four primary workflows', (tester) async {
    await tester.pumpWidget(const ProviderScope(child: ShowVaultApp()));
    for (final label in ['Scan', 'Backup', 'Verify', 'Restore']) {
      expect(find.text(label), findsOneWidget);
    }
    expect(find.text('Recovery loop proven'), findsOneWidget);
    expect(find.text('Foundation preview'), findsOneWidget);
  });
}
