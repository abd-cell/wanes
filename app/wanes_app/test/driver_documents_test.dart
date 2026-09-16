import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/features/driver/driver_apply_screen.dart';
import 'package:wanes_app/models/models.dart';
import 'package:wanes_app/widgets/wanes_ui.dart';

/// Driver verification from the driver's side: what the API tells the screen
/// about their application, and what the screen does with it.
///
/// The uploads used to be two dashed boxes that toasted "coming soon", so this
/// is mostly about the shape of the thing that replaced them — a slot per
/// document type, with the required set and the reviewer's verdict both coming
/// from the server rather than being guessed here.
///
/// The test binding answers every request with 400 offline, so the widget tests
/// exercise the empty screen; the payload reading is covered directly, which
/// needs no server.
void main() {
  setUp(() => AppLocalizations.current = const AppLocalizations(Locale('en')));

  Widget host() => MaterialApp(
        theme: WanesTheme.light(),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: const DriverApplyScreen(),
      );

  group('the verification payload', () {
    test('reads the documents and what is still missing', () {
      final state = DriverVerification.fromJson(const {
        'status': 0,
        'licenseNumber': 'JO-99',
        'documents': [
          {
            'id': 4,
            'type': 1,
            'fileName': 'front.jpg',
            'contentType': 'image/jpeg',
            'sizeBytes': 240000,
            'isPdf': false,
          },
        ],
        'missingTypes': [2, 3],
        'canSubmit': false,
      });

      expect(state.documents.single.type, DriverDocumentType.licenseFront);
      expect(state.documentOf(DriverDocumentType.licenseFront), isNotNull);
      expect(state.documentOf(DriverDocumentType.idDocument), isNull);
      expect(state.missingTypes,
          [DriverDocumentType.licenseBack, DriverDocumentType.idDocument]);
      expect(state.canSubmit, isFalse);
    });

    test('takes canSubmit from the server rather than recomputing it', () {
      // The required set is a platform rule. A client that decided for itself
      // would eventually offer a submit the API refuses.
      final state = DriverVerification.fromJson(const {
        'status': 1,
        'documents': [],
        'missingTypes': [],
        'canSubmit': true,
      });

      expect(state.canSubmit, isTrue);
      expect(state.isPending, isTrue);
    });

    test('carries the reviewer note on a rejection', () {
      final state = DriverVerification.fromJson(const {
        'status': 3,
        'reviewNote': 'The back of the licence is out of focus.',
        'documents': [],
        'missingTypes': [],
        'canSubmit': true,
      });

      expect(state.isRejected, isTrue);
      expect(state.reviewNote, 'The back of the licence is out of focus.');
    });

    test('an unknown document type does not blow up the list', () {
      // A client one release behind a new type still has to render the rest.
      final document = DriverDocument.fromJson(const {'id': 1, 'type': 99});
      expect(document.type, DriverDocumentType.idDocument);
    });
  });

  group('the required set', () {
    test('is licence front, licence back and an id', () {
      final required =
          DriverDocumentType.values.where((t) => t.required).toList();
      expect(required, [
        DriverDocumentType.licenseFront,
        DriverDocumentType.licenseBack,
        DriverDocumentType.idDocument,
      ]);
    });

    test('registration and insurance are optional', () {
      expect(DriverDocumentType.vehicleRegistration.required, isFalse);
      expect(DriverDocumentType.insurance.required, isFalse);
    });
  });

  group('the apply screen', () {
    /// Offline is the default in tests, and the screen says so before anything
    /// else — there is nothing to review and nothing that could be uploaded.
    testWidgets('says so when it cannot reach the server', (tester) async {
      await tester.pumpWidget(host());
      await tester.pumpAndSettle();

      expect(find.text('Connection lost'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });

    testWidgets('offers a slot per document, required ones marked',
        (tester) async {
      await tester.pumpWidget(host());
      await tester.pumpAndSettle();
      await tester.tap(find.text('Dismiss'));
      await tester.pumpAndSettle();

      // The required three are above the fold; the optional two are further
      // down the list.
      for (final type in DriverDocumentType.values.where((t) => t.required)) {
        expect(find.text(type.label), findsOneWidget,
            reason: 'no slot for ${type.name}');
      }
      expect(find.text('*'), findsNWidgets(3));

      await tester.scrollUntilVisible(
          find.text(DriverDocumentType.insurance.label), 200,
          scrollable: find.byType(Scrollable).first);
      expect(find.text(DriverDocumentType.insurance.label), findsOneWidget);
    });

    testWidgets('cannot submit an application with nothing attached',
        (tester) async {
      await tester.pumpWidget(host());
      await tester.pumpAndSettle();
      await tester.tap(find.text('Dismiss'));
      await tester.pumpAndSettle();

      await tester.scrollUntilVisible(find.byType(PrimaryButton), 200,
          scrollable: find.byType(Scrollable).first);

      // Submitting here could only ever be refused by the API, so the button
      // does not pretend otherwise.
      final button = tester.widget<PrimaryButton>(find.byType(PrimaryButton));
      expect(button.label, 'Submit for verification');
      expect(button.onPressed, isNull);
    });
  });
}
