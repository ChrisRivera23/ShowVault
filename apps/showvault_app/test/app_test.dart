import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/app.dart';

void main() {
  testWidgets('requires an Auth0 client before showing tenant data', (
    tester,
  ) async {
    await tester.pumpWidget(const ProviderScope(child: ShowVaultApp()));
    expect(find.text('Auth0 client configuration required'), findsOneWidget);
    expect(find.text('Foundation preview'), findsNothing);
  });
}
