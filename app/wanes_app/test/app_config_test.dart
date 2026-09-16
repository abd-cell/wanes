import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/core/app_config.dart';
import 'package:wanes_app/core/fare.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/models/models.dart';

/// The admin-controlled configuration: how a brand colour turns into the theme,
/// and how the currency settings turn into the strings shown next to a price.
void main() {
  // The typeface tests build real GoogleFonts styles, which reach for the asset
  // bundle to see whether a face is already on the device. Without a binding
  // that lookup prints a warning on every call.
  TestWidgetsFlutterBinding.ensureInitialized();

  tearDown(() {
    AppConfigController.config.value = AppConfig.fallback;
    // Static, so a test that switched to Arabic type would otherwise leak the
    // flag into every test that runs after it.
    WanesTheme.arabic = false;
  });

  group('parseHexColor', () {
    test('accepts the shapes the API and the CMS can produce', () {
      expect(parseHexColor('#0FAE9E'), 0xFF0FAE9E);
      expect(parseHexColor('0fae9e'), 0xFF0FAE9E);
      expect(parseHexColor('#abc'), 0xFFAABBCC);
    });

    test('rejects anything else, so the caller can fall back', () {
      expect(parseHexColor(null), isNull);
      expect(parseHexColor(''), isNull);
      expect(parseHexColor('#12345'), isNull);
      expect(parseHexColor('rebeccapurple'), isNull);
    });
  });

  group('AppConfig.fromJson', () {
    test('reads a well-formed payload', () {
      final c = AppConfig.fromJson({
        'currencyCode': 'JOD',
        'currencySymbol': 'د.أ',
        'currencyPosition': 2,
        'currencyDecimals': 3,
        'primaryColor': '#7C3AED',
      });
      expect(c.currencyCode, 'JOD');
      expect(c.currencyPosition, CurrencyPosition.after);
      expect(c.currencyDecimals, 3);
      expect(c.primaryColor, 0xFF7C3AED);
      expect(c.hexColor, '#7C3AED');
    });

    test('falls back field by field rather than failing the launch', () {
      final c = AppConfig.fromJson({
        'currencySymbol': '   ',
        'currencyDecimals': 9,
        'primaryColor': 'not-a-colour',
      });
      expect(c.currencySymbol, AppConfig.fallback.currencySymbol);
      expect(c.currencyDecimals, 3, reason: 'clamped into 0..3');
      expect(c.primaryColor, AppConfig.fallback.primaryColor);
    });
  });

  group('typeface', () {
    test('fromJson reads the configured font', () {
      expect(AppConfig.fromJson({'fontFamily': 3}).font, AppFont.rubik);
      expect(AppConfig.fromJson({'fontFamily': 6}).font, AppFont.system);
    });

    test('a font this build does not know falls back to the shipped pairing', () {
      // 99 is a newer server; 0 is a settings row written before the column
      // existed. Either way the app must still have a face to render with.
      expect(AppConfig.fromJson({'fontFamily': 99}).font, AppFont.jakarta);
      expect(AppConfig.fromJson({'fontFamily': 0}).font, AppFont.jakarta);
      expect(AppConfig.fromJson(const {}).font, AppFont.jakarta);
    });

    test('the choice survives the shared_preferences round trip', () {
      const before = AppConfig(font: AppFont.tajawal);
      expect(AppConfig.fromJson(before.toJson()).font, AppFont.tajawal);
    });

    /// GoogleFonts names the loaded variant ("Cairo_regular") and lists the
    /// bare family as the fallback, so the family is what a test can pin. It
    /// spells the family without spaces ("PlusJakartaSans").
    List<String> families(TextStyle style) => style.fontFamilyFallback ?? const [];

    test('each language draws in its own half of the configured pairing', () {
      AppConfigController.config.value = const AppConfig(font: AppFont.inter);

      WanesTheme.arabic = false;
      expect(families(WanesTheme.display()), contains('Inter'));

      WanesTheme.arabic = true;
      expect(families(WanesTheme.display()), contains('IBMPlexSansArabic'));
    });

    test('changing the setting changes the face', () {
      WanesTheme.arabic = false;

      AppConfigController.config.value = const AppConfig(font: AppFont.jakarta);
      expect(families(WanesTheme.display()), contains('PlusJakartaSans'));

      AppConfigController.config.value = const AppConfig(font: AppFont.noto);
      expect(families(WanesTheme.display()), contains('NotoSans'));
    });

    test('the system font asks for no family at all, so the device picks', () {
      AppConfigController.config.value = const AppConfig(font: AppFont.system);
      WanesTheme.arabic = false;
      expect(WanesTheme.display().fontFamily, isNull);
      WanesTheme.arabic = true;
      expect(WanesTheme.display().fontFamily, isNull);
    });

    test('every option resolves to its own pairing, not the shipped fallback', () {
      // The table in theme.dart is keyed by enum value, and a missing key falls
      // back to Jakarta silently — so an option added to the enum without a
      // pairing would look configured and paint the old face. Naming the
      // expected family per value is what catches that.
      const expected = <AppFont, (String latin, String arabic)>{
        AppFont.jakarta: ('PlusJakartaSans', 'Cairo'),
        AppFont.inter: ('Inter', 'IBMPlexSansArabic'),
        AppFont.rubik: ('Rubik', 'Rubik'),
        AppFont.noto: ('NotoSans', 'NotoSansArabic'),
        AppFont.tajawal: ('Tajawal', 'Tajawal'),
        AppFont.almarai: ('Almarai', 'Almarai'),
        AppFont.readexPro: ('ReadexPro', 'ReadexPro'),
        AppFont.alexandria: ('Alexandria', 'Alexandria'),
        AppFont.poppins: ('Poppins', 'Almarai'),
        AppFont.montserrat: ('Montserrat', 'ElMessiri'),
        AppFont.amiri: ('Amiri', 'Amiri'),
      };

      for (final font in AppFont.values.where((f) => f != AppFont.system)) {
        final faces = expected[font];
        expect(faces, isNotNull, reason: '$font has no expected pairing here');

        AppConfigController.config.value = AppConfig(font: font);
        WanesTheme.arabic = false;
        expect(families(WanesTheme.display()), contains(faces!.$1), reason: 'latin face for $font');
        WanesTheme.arabic = true;
        expect(families(WanesTheme.display()), contains(faces.$2), reason: 'arabic face for $font');
      }
    });

    test('the mono/data face is fixed — a font choice must not move figures', () {
      WanesTheme.arabic = false;
      for (final font in AppFont.values) {
        AppConfigController.config.value = AppConfig(font: font);
        expect(families(WanesTheme.mono()), contains('JetBrainsMono'),
            reason: 'mono should stay put for $font');
      }
    });

    test('displayFor draws a named language in its own face', () {
      AppConfigController.config.value = const AppConfig(font: AppFont.jakarta);
      // The language picker lists both names at once, so this must not depend
      // on which language is currently active.
      WanesTheme.arabic = false;
      expect(families(WanesTheme.displayFor('ar')), contains('Cairo'));
      expect(families(WanesTheme.displayFor('en')), contains('PlusJakartaSans'));
    });
  });

  group('theme derivation', () {
    test('the default brand reproduces the shipped palette', () {
      final t = WanesTokens.lightFor(const Color(0xFF0FAE9E));
      expect(t.teal, const Color(0xFF0FAE9E));
      // Within a shade of the hand-tuned #0A8578 the design ships.
      expect(t.tealInk.r, closeTo(const Color(0xFF0A8578).r, 0.03));
      expect(t.tealInk.g, closeTo(const Color(0xFF0A8578).g, 0.03));
      expect(t.tealInk.b, closeTo(const Color(0xFF0A8578).b, 0.03));
    });

    test('light keeps surfaces put and only moves the brand family', () {
      final t = WanesTokens.lightFor(const Color(0xFF7C3AED));
      expect(t.teal, const Color(0xFF7C3AED));
      expect(t.bg, WanesTokens.light.bg);
      expect(t.ink, WanesTokens.light.ink);
      expect(t.alert, WanesTokens.light.alert);
    });

    test('dark lifts a brand that would be lost against the dark surface', () {
      const navy = Color(0xFF10214A); // very dark blue
      final dark = WanesTokens.darkFor(navy);
      expect(HSLColor.fromColor(dark.teal).lightness, greaterThanOrEqualTo(0.46));
      // ink is text on dark here, so it goes lighter than the fill.
      expect(HSLColor.fromColor(dark.tealInk).lightness,
          greaterThan(HSLColor.fromColor(dark.teal).lightness));
    });

    test('onTeal flips to the side that actually reads on the fill', () {
      // Bright yellow needs dark text; deep indigo needs light text.
      expect(WanesTokens.lightFor(const Color(0xFFFFD400)).onTeal.computeLuminance(),
          lessThan(0.1));
      expect(WanesTokens.lightFor(const Color(0xFF2A1B7A)).onTeal.computeLuminance(),
          greaterThan(0.7));
    });
  });

  group('Fare.format', () {
    test('symbol before the amount', () {
      AppConfigController.config.value = const AppConfig(
        currencySymbol: r'$', currencyPosition: CurrencyPosition.before, currencyDecimals: 2);
      expect(Fare.format(1234.5), r'$1,234.50');
    });

    test('symbol after the amount, at the configured precision', () {
      AppConfigController.config.value = const AppConfig(
        currencySymbol: 'JD', currencyPosition: CurrencyPosition.after, currencyDecimals: 3);
      expect(Fare.format(1234.5), '1,234.500 JD');
    });

    test('a currency with no minor unit drops the decimals entirely', () {
      AppConfigController.config.value = const AppConfig(
        currencySymbol: '¥', currencyPosition: CurrencyPosition.before, currencyDecimals: 0);
      expect(Fare.format(1234.5), '¥1,235');
    });
  });
}
