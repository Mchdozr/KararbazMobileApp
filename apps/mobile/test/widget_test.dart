import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nereye/app.dart';
import 'package:nereye/intents.dart';

void main() {
  testWidgets('splash shows brand', (tester) async {
    await tester.pumpWidget(const ProviderScope(child: NereyeApp()));
    expect(find.text('Nereye'), findsWidgets);
  });

  test('intent labels cover taxonomy', () {
    expect(intentLabel('Kultur'), 'Kültür');
    expect(intents.length, 6);
    expect(vibes, contains('Laptop'));
  });
}
