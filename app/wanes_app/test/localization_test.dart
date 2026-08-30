import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/l10n/strings_ar.dart';
import 'package:wanes_app/l10n/strings_en.dart';
import 'package:wanes_app/features/login_screen.dart';
import 'package:wanes_app/widgets/language_picker.dart';

/// Wraps [child] the way `WanesApp` does, so `context.tr` resolves and the
/// text direction follows the locale.
Widget _app(Widget child, {Locale locale = const Locale('en')}) => MaterialApp(
      locale: locale,
      supportedLocales: AppLocalizations.supportedLocales,
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      home: child,
    );

void main() {
  setUpAll(initializeDateFormatting);

  setUp(() {
    SharedPreferences.setMockInitialValues({});
    LocaleController.locale.value = const Locale('en');
    AppLocalizations.current = const AppLocalizations(Locale('en'));
  });

  group('the catalogues', () {
    test('Arabic covers every key English defines', () {
      // English is the fallback, so a key missing from Arabic silently shows
      // English copy — catch that here rather than on screen.
      final missing = enStrings.keys.where((k) => !arStrings.containsKey(k)).toList();
      expect(missing, isEmpty, reason: 'untranslated keys: $missing');
    });

    test('plural keys carry the full Arabic category set', () {
      const counted = [
        'results.seatsLeft',
        'booking.fareForSeats',
        'vehicle.seatCount',
        'hail.driversNotified',
        'driver.incomingCount',
      ];
      const ar = AppLocalizations(Locale('ar'));
      for (final base in counted) {
        // 0 → zero, 1 → one, 2 → two, 3 → few, 11 → many, 100 → other.
        for (final n in [0, 1, 2, 3, 11, 100]) {
          expect(ar.plural(base, n), isNot(startsWith(base)),
              reason: '$base has no form for $n');
        }
      }
    });
  });

  group('lookup', () {
    test('placeholders are substituted', () {
      const en = AppLocalizations(Locale('en'));
      expect(en.t('driver.hi', {'name': 'Layla'}), 'Hi, Layla');
      expect(en.t('common.routeSummary', {'from': 'A', 'to': 'B'}), 'A → B');
    });

    test('Arabic reverses the route arrow', () {
      const ar = AppLocalizations(Locale('ar'));
      expect(ar.t('common.routeSummary', {'from': 'أ', 'to': 'ب'}), 'أ ← ب');
    });

    test('English pluralises on one/other, Arabic on the CLDR set', () {
      const en = AppLocalizations(Locale('en'));
      expect(en.plural('vehicle.seatCount', 1), '1 seat');
      expect(en.plural('vehicle.seatCount', 4), '4 seats');

      const ar = AppLocalizations(Locale('ar'));
      expect(ar.plural('vehicle.seatCount', 1), 'مقعد واحد');
      expect(ar.plural('vehicle.seatCount', 2), 'مقعدان');
      expect(ar.plural('vehicle.seatCount', 4), '4 مقاعد');
    });

    test('an unknown key falls back to English, then to the key', () {
      const ar = AppLocalizations(Locale('ar'));
      expect(ar.t('nav.home'), 'الرئيسية');
      expect(ar.t('definitely.notAKey'), 'definitely.notAKey');
    });
  });

  group('the app in Arabic', () {
    testWidgets('the sign-in screen renders Arabic copy right-to-left',
        (tester) async {
      await tester.pumpWidget(_app(const LoginScreen(), locale: const Locale('ar')));
      await tester.pump();

      expect(find.text('أدخل رقمك'), findsOneWidget);
      expect(find.text('متابعة'), findsOneWidget);
      expect(
        Directionality.of(tester.element(find.text('متابعة'))),
        TextDirection.rtl,
      );
    });

    testWidgets('the same screen renders English left-to-right', (tester) async {
      await tester.pumpWidget(_app(const LoginScreen()));
      await tester.pump();

      expect(find.text('Enter your number'), findsOneWidget);
      expect(
        Directionality.of(tester.element(find.text('Continue'))),
        TextDirection.ltr,
      );
    });
  });

  group('the language picker', () {
    testWidgets('the profile row shows the language in use', (tester) async {
      await tester.pumpWidget(_app(const Scaffold(body: LanguageRow())));
      await tester.pump();

      expect(find.text('Language'), findsOneWidget);
      expect(find.text('English'), findsOneWidget);
    });

    testWidgets('picking Arabic persists the choice and flips the app',
        (tester) async {
      await tester.pumpWidget(
        ValueListenableBuilder<Locale>(
          valueListenable: LocaleController.locale,
          builder: (_, locale, __) =>
              _app(const Scaffold(body: LanguageRow()), locale: locale),
        ),
      );
      await tester.pump();

      await tester.tap(find.text('Language'));
      await tester.pumpAndSettle();

      // The sheet lists both languages by their own names.
      expect(find.text('العربية'), findsOneWidget);
      await tester.tap(find.text('العربية'));
      await tester.pumpAndSettle();

      expect(LocaleController.value.languageCode, 'ar');
      expect(find.text('اللغة'), findsOneWidget);

      final prefs = await SharedPreferences.getInstance();
      expect(prefs.getString('Wanes_locale'), 'ar');
    });
  });
}
