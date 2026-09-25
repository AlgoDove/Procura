import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:procura_mobile/providers/auth_provider.dart';
import 'package:procura_mobile/screens/login_screen.dart';
import 'package:procura_mobile/screens/create_request_screen.dart';

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
      expect(find.text('Email'), findsWidgets);
      expect(find.text('Password'), findsWidgets);
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

  group('CreateRequestScreen', () {
    Widget buildCreateRequestScreen() {
      return const MaterialApp(
        home: CreateRequestScreen(),
      );
    }

    testWidgets('displays both AI Assistant and Manual Request choices', (tester) async {
      await tester.pumpWidget(buildCreateRequestScreen());
      await tester.pump();

      expect(find.text('AI Assistant'), findsOneWidget);
      expect(find.text('Manual Request'), findsOneWidget);
      // Defaults to AI view
      expect(find.text('Create Draft with AI'), findsOneWidget);
    });

    testWidgets('switching to Manual Request shows manual form fields', (tester) async {
      await tester.pumpWidget(buildCreateRequestScreen());
      await tester.pump();

      // Tap Manual Request segment
      await tester.tap(find.text('Manual Request'));
      await tester.pumpAndSettle();

      expect(find.text('Title *'), findsOneWidget);
      expect(find.text('Description *'), findsOneWidget);
      expect(find.text('Justification *'), findsOneWidget);

      await tester.scrollUntilVisible(find.text('Save Draft'), 200, scrollable: find.byType(Scrollable).first);
      expect(find.text('Save Draft'), findsOneWidget);
    });
  });
}
