import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/core/app_config.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/features/contact_us_screen.dart';
import 'package:wanes_app/models/models.dart';
import 'package:wanes_app/widgets/wanes_ui.dart';

/// The Contact us screen and the admin-owned channels behind it.
///
/// The screen reads [AppConfigController] rather than the network, so setting
/// the notifier is enough to drive it. Its `initState` also kicks off a config
/// refresh; with no server in a test that fails fast inside `ApiClient` and is
/// swallowed, which is the same path a device offline takes.
void main() {
  setUp(() => AppLocalizations.current = const AppLocalizations(Locale('en')));
  tearDown(() => AppConfigController.config.value = AppConfig.fallback);

  Widget host() => MaterialApp(
        theme: WanesTheme.light(),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: const ContactUsScreen(),
      );

  const fullyConfigured = AppConfig(
    supportPhone: '+962 79 000 0000',
    supportWhatsApp: '+962790000000',
    supportEmail: 'help@wanes.app',
    supportWebsite: 'https://wanes.app/help',
    supportHours: 'Sun–Thu, 9:00–17:00',
  );

  group('the config payload', () {
    test('reads the support channels the API sends', () {
      final c = AppConfig.fromJson(const {
        'primaryColor': '#0FAE9E',
        'supportPhone': ' +962 79 000 0000 ',
        'supportWhatsApp': '+962790000000',
        'supportEmail': 'help@wanes.app',
        'supportWebsite': 'https://wanes.app/help',
        'supportHours': 'Sun–Thu, 9:00–17:00',
      });

      expect(c.supportPhone, '+962 79 000 0000');
      expect(c.supportEmail, 'help@wanes.app');
      expect(c.hasSupportChannel, isTrue);
    });

    test('an unset channel arrives as null and reads as empty', () {
      final c = AppConfig.fromJson(const {
        'primaryColor': '#0FAE9E',
        'supportPhone': null,
        'supportEmail': '',
      });

      expect(c.supportPhone, '');
      expect(c.supportEmail, '');
      expect(c.hasSupportChannel, isFalse);
    });

    test('opening hours alone is not a channel — there is nothing to tap', () {
      const c = AppConfig(supportHours: 'Sun–Thu, 9:00–17:00');
      expect(c.hasSupportChannel, isFalse);
    });

    test('survives the round trip through the offline cache', () {
      final restored = AppConfig.fromJson(fullyConfigured.toJson());
      expect(restored.supportPhone, fullyConfigured.supportPhone);
      expect(restored.supportWhatsApp, fullyConfigured.supportWhatsApp);
      expect(restored.supportEmail, fullyConfigured.supportEmail);
      expect(restored.supportWebsite, fullyConfigured.supportWebsite);
      expect(restored.supportHours, fullyConfigured.supportHours);
    });
  });

  group('the screen', () {
    testWidgets('offers every configured channel, with its value', (tester) async {
      AppConfigController.config.value = fullyConfigured;
      await tester.pumpWidget(host());
      await tester.pump();

      expect(find.text('Contact us'), findsOneWidget);
      expect(find.text('Call us'), findsOneWidget);
      expect(find.text('WhatsApp'), findsOneWidget);
      expect(find.text('Email us'), findsOneWidget);
      expect(find.text('Help centre'), findsOneWidget);

      expect(find.text('+962 79 000 0000'), findsOneWidget);
      expect(find.text('help@wanes.app'), findsOneWidget);
      expect(find.text('Sun–Thu, 9:00–17:00'), findsOneWidget);
    });

    testWidgets('hides the channels the admin left blank', (tester) async {
      AppConfigController.config.value = const AppConfig(supportEmail: 'help@wanes.app');
      await tester.pumpWidget(host());
      await tester.pump();

      expect(find.text('Email us'), findsOneWidget);
      expect(find.text('Call us'), findsNothing);
      expect(find.text('WhatsApp'), findsNothing);
      expect(find.text('Help centre'), findsNothing);
      // No hours configured, so no hours card.
      expect(find.byIcon(Icons.schedule_rounded), findsNothing);
    });

    testWidgets('says so plainly when nothing is configured', (tester) async {
      AppConfigController.config.value = AppConfig.fallback;
      await tester.pumpWidget(host());
      await tester.pump();

      expect(find.text('No support channel yet'), findsOneWidget);
      expect(find.byType(GroupedCard), findsNothing);
    });

    testWidgets('a channel added in the CMS appears without a rebuild', (tester) async {
      AppConfigController.config.value = AppConfig.fallback;
      await tester.pumpWidget(host());
      await tester.pump();
      expect(find.text('Call us'), findsNothing);

      AppConfigController.config.value = fullyConfigured;
      await tester.pump();
      expect(find.text('Call us'), findsOneWidget);
    });

    testWidgets('copies a value to the clipboard from the row action', (tester) async {
      final copied = <String>[];
      tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
        SystemChannels.platform,
        (call) async {
          if (call.method == 'Clipboard.setData') {
            copied.add((call.arguments as Map)['text'] as String);
          }
          return null;
        },
      );
      addTearDown(() => tester.binding.defaultBinaryMessenger
          .setMockMethodCallHandler(SystemChannels.platform, null));

      AppConfigController.config.value = fullyConfigured;
      await tester.pumpWidget(host());
      await tester.pump();

      // The copy affordance only exists on the channels worth copying — the
      // help-centre URL is a link you open, not a string you paste.
      final copies = find.byIcon(Icons.copy_rounded);
      expect(copies, findsNWidgets(3));

      await tester.tap(copies.first);
      await tester.pump();
      expect(copied, ['+962 79 000 0000']);
    });
  });
}
