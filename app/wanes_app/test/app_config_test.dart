import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/core/app_config.dart';
import 'package:wanes_app/core/fare.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/models/models.dart';

/// The admin-controlled configuration: how a brand colour turns into the theme,
/// and how the currency settings turn into the strings shown next to a price.
void main() {
  tearDown(() => AppConfigController.config.value = AppConfig.fallback);

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
