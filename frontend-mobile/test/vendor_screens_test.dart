import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:procura_mobile/screens/create_vendor_screen.dart';

void main() {
  group('CreateVendorScreen', () {
    Widget buildCreateVendorScreen() {
      return const MaterialApp(
        home: CreateVendorScreen(),
      );
    }

    testWidgets('renders all required form fields and action buttons', (tester) async {
      await tester.pumpWidget(buildCreateVendorScreen());
      await tester.pump();

      expect(find.text('New Vendor'), findsOneWidget);
      expect(find.text('Vendor Name *'), findsOneWidget);
      expect(find.text('Category *'), findsOneWidget);
      expect(find.text('Contact Person *'), findsOneWidget);
      expect(find.text('Contact Email *'), findsOneWidget);
      expect(find.text('Contact Phone *'), findsOneWidget);
      expect(find.text('Rating (0 - 5)'), findsOneWidget);
      expect(find.text('Address (Optional)'), findsOneWidget);

      await tester.scrollUntilVisible(
        find.text('Create Vendor'),
        200,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.text('Create Vendor'), findsOneWidget);
      expect(find.text('Cancel'), findsOneWidget);
    });

    testWidgets('validates required fields on empty submit', (tester) async {
      await tester.pumpWidget(buildCreateVendorScreen());
      await tester.pump();

      await tester.scrollUntilVisible(
        find.text('Create Vendor'),
        200,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('Create Vendor'));
      await tester.pumpAndSettle();

      expect(find.text('Vendor name is required'), findsOneWidget);
      expect(find.text('Category is required'), findsOneWidget);
      expect(find.text('Contact person is required'), findsOneWidget);
      expect(find.text('Contact email is required'), findsOneWidget);
      expect(find.text('Contact phone is required'), findsOneWidget);
    });

    testWidgets('validates email format', (tester) async {
      await tester.pumpWidget(buildCreateVendorScreen());
      await tester.pump();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Contact Email *'),
        'invalid-email-format',
      );

      await tester.scrollUntilVisible(
        find.text('Create Vendor'),
        200,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('Create Vendor'));
      await tester.pumpAndSettle();

      expect(find.text('Please enter a valid email address'), findsOneWidget);
    });
  });
}
