import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:procura_mobile/providers/auth_provider.dart';
import 'package:procura_mobile/screens/login_screen.dart';

void main() {
  group('LoginScreen', () {
    Widget buildLoginScreen() {
      return ChangeNotifierProvider(
        create: (_) => AuthProvider(),
        child: const MaterialApp(
          home: LoginScreen(),
        ),
      );
    }

    testWidgets('renders email and password fields', (tester) async {
      await tester.pumpWidget(buildLoginScreen());
      await tester.pump(); // Wait for async init

      expect(find.byType(TextFormField), findsAtLeastNWidgets(2));
      expect(find.widgetWithText(TextFormField, 'Email'), findsOneWidget);
      expect(find.widgetWithText(TextFormField, 'Password'), findsOneWidget);
    });

    testWidgets('shows sign in button', (tester) async {
      await tester.pumpWidget(buildLoginScreen());
      await tester.pump();

      expect(find.widgetWithText(ElevatedButton, 'Sign in'), findsOneWidget);
    });

    testWidgets('shows register link', (tester) async {
      await tester.pumpWidget(buildLoginScreen());
      await tester.pump();

      expect(find.text('Register'), findsOneWidget);
    });

    testWidgets('shows validation errors when form is submitted empty', (tester) async {
      await tester.pumpWidget(buildLoginScreen());
      await tester.pump();

      await tester.tap(find.widgetWithText(ElevatedButton, 'Sign in'));
      await tester.pump();

      expect(find.text('Required'), findsAtLeastNWidgets(1));
    });
  });
}
