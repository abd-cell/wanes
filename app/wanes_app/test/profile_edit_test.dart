import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:wanes_app/features/complete_profile_screen.dart';
import 'package:wanes_app/features/edit_profile_screen.dart';
import 'package:wanes_app/features/profile_form.dart';
import 'package:wanes_app/models/models.dart';

Profile _profile({String first = '', String last = ''}) =>
    Profile(id: 1, phone: '+962790000000', firstName: first, lastName: last);

void main() {
  // main() does this before runApp; DateFormat with an explicit locale throws
  // without it, and the date-of-birth row formats through `intl`.
  setUpAll(initializeDateFormatting);

  group('profile completeness gate', () {
    test('a fresh OTP account has no name, so it is incomplete', () {
      // What POST /Accounts/verify-otp returns for a brand-new number.
      final p = Profile.fromJson(const {
        'id': 16,
        'phone': '+962795550123',
        'firstName': '',
        'lastName': '',
        'displayName': null,
        'email': null,
        'gender': 0,
        'dateOfBirth': null,
      });
      expect(p.isComplete, isFalse);
    });

    test('one name alone is still incomplete', () {
      expect(_profile(first: 'Layla').isComplete, isFalse);
      expect(_profile(last: 'Haddad').isComplete, isFalse);
      expect(_profile(first: '   ', last: '  ').isComplete, isFalse);
    });

    test('first and last name together let the account through', () {
      expect(_profile(first: 'Layla', last: 'Haddad').isComplete, isTrue);
    });
  });

  group('date of birth round-trip', () {
    test('the server timestamp keeps its calendar day', () {
      // The API sends midnight with no zone; reading it as UTC would move the
      // day for anyone west of Greenwich.
      final d = parseDateOnly('1996-04-11T00:00:00');
      expect(d, isNotNull);
      expect([d!.year, d.month, d.day], [1996, 4, 11]);
      expect(formatDateOnly(d), '1996-04-11');
    });

    test('a missing or short value is null, not a crash', () {
      expect(parseDateOnly(null), isNull);
      expect(parseDateOnly(''), isNull);
      expect(parseDateOnly('1996'), isNull);
    });
  });

  group('form validation', () {
    test('both names are required', () {
      final form = ProfileFormState(_profile());
      expect(form.validate(), 'Enter your first name.');
      form.firstName.text = 'Layla';
      expect(form.validate(), 'Enter your last name.');
      form.lastName.text = 'Haddad';
      expect(form.validate(), isNull);
      form.dispose();
    });

    test('email is optional but must look like one when filled', () {
      final form = ProfileFormState(_profile(first: 'Layla', last: 'Haddad'));
      expect(form.validate(), isNull, reason: 'blank email is fine');
      form.email.text = 'not-an-email';
      expect(form.validate(), isNotNull);
      form.email.text = 'layla@example.com';
      expect(form.validate(), isNull);
      form.dispose();
    });

    test('under-16 dates of birth are rejected', () {
      final form = ProfileFormState(_profile(first: 'Layla', last: 'Haddad'));
      final now = DateTime.now();
      form.dateOfBirth = DateTime(now.year - 15, now.month, now.day);
      expect(form.validate(), contains('at least 16'));
      form.dateOfBirth = DateTime(now.year - 30, now.month, now.day);
      expect(form.validate(), isNull);
      form.dispose();
    });
  });

  group('the form widget', () {
    testWidgets('compact mode hides display name and bio, full mode shows them',
        (tester) async {
      final form = ProfileFormState(_profile(first: 'Layla', last: 'Haddad'));
      addTearDown(form.dispose);

      Widget host({required bool compact}) => MaterialApp(
            home: Scaffold(
              body: SingleChildScrollView(
                child: ProfileFields(
                  state: form,
                  compact: compact,
                  phone: '+962790000000',
                  onChanged: () {},
                ),
              ),
            ),
          );

      await tester.pumpWidget(host(compact: true));
      expect(find.text('FIRST NAME'), findsOneWidget);
      expect(find.text('DISPLAY NAME'), findsNothing);
      expect(find.text('ABOUT YOU'), findsNothing);
      // The verified phone is shown but not editable.
      expect(find.text('+962790000000'), findsOneWidget);

      await tester.pumpWidget(host(compact: false));
      expect(find.text('DISPLAY NAME'), findsOneWidget);
      expect(find.text('ABOUT YOU'), findsOneWidget);
    });

    testWidgets('tapping a gender chip updates the form state', (tester) async {
      final form = ProfileFormState(_profile(first: 'Layla', last: 'Haddad'));
      addTearDown(form.dispose);
      expect(form.gender, Gender.unspecified);

      await tester.pumpWidget(MaterialApp(
        home: Scaffold(
          body: SingleChildScrollView(
            child: ProfileFields(state: form, onChanged: () {}),
          ),
        ),
      ));
      await tester.tap(find.text('Female'));
      await tester.pump();
      expect(form.gender, Gender.female);
      expect(form.gender.value, 2, reason: 'matches the backend enum');
    });
  });

  group('the screens', () {
    testWidgets('the first-login gate asks for a name and cannot be backed out of',
        (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: CompleteProfileScreen(profile: _profile()),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Almost there'), findsOneWidget);
      expect(find.text('Continue'), findsOneWidget);
      expect(find.text('Use a different number'), findsOneWidget);
      // No AppBar, and the route refuses a pop — the only ways on are
      // saving a name or signing out.
      expect(find.byType(BackButton), findsNothing);
      expect(tester.widget<PopScope>(find.byType(PopScope).first).canPop, isFalse);
    });

    testWidgets('the editor seeds every field from the existing profile',
        (tester) async {
      final existing = Profile(
        id: 3,
        phone: '+962790000000',
        firstName: 'Noor',
        lastName: 'Khalil',
        displayName: 'Nono',
        email: 'layla@example.com',
        gender: Gender.female,
        dateOfBirth: DateTime(1996, 4, 11),
        bio: 'Coffee runs to Irbid.',
      );

      // The editor is a lazy ListView — on a phone-sized viewport the lower
      // fields are never built, so give the test room to lay the form out.
      tester.view.physicalSize = const Size(500, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.reset);

      await tester.pumpWidget(MaterialApp(home: EditProfileScreen(profile: existing)));
      await tester.pumpAndSettle();

      expect(find.text('Edit profile'), findsOneWidget);
      expect(find.text('Save changes'), findsOneWidget);
      expect(find.widgetWithText(TextField, 'Noor'), findsOneWidget);
      expect(find.widgetWithText(TextField, 'Khalil'), findsOneWidget);
      expect(find.widgetWithText(TextField, 'Nono'), findsOneWidget);
      expect(find.widgetWithText(TextField, 'layla@example.com'), findsOneWidget);
      expect(find.widgetWithText(TextField, 'Coffee runs to Irbid.'), findsOneWidget);
      expect(find.text('11 Apr 1996'), findsOneWidget);
    });
  });
}
