import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/features/login_screen.dart';

void main() {
  testWidgets('Login screen asks for a number and offers Continue', (tester) async {
    await tester.pumpWidget(const MaterialApp(home: LoginScreen()));
    expect(find.text('Enter your number'), findsOneWidget);
    expect(find.text('Continue'), findsOneWidget);
  });
}
