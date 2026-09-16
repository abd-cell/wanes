import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/places.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/features/driver/driver_home_screen.dart';
import 'package:wanes_app/models/models.dart';

/// The driver dashboard — screen 08.
///
/// The test binding answers every HTTP call with a failure, which is exactly
/// the case the banner group guards: the dashboard must not narrate a server
/// state it was never given.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() => AppLocalizations.current = const AppLocalizations(Locale('en')));

  Widget host(Widget child) => MaterialApp(
        theme: WanesTheme.light(),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: Scaffold(body: child),
      );

  group('the online banner', () {
    testWidgets('does not claim the driver is online until the server takes it',
        (tester) async {
      await tester.pumpWidget(host(const DriverDashboard()));
      // Let the presence call the dashboard opens with land and be refused.
      await tester.pump();
      await tester.pump(const Duration(seconds: 1));

      expect(find.text("You're offline"), findsOneWidget);
      expect(find.text("You're online"), findsNothing);
      // And it counts no session either, rather than clocking up hours the
      // driver never spent available.
      expect(find.text('0h'), findsOneWidget);
    });
  });

  group('a rider-posted trip card', () {
    RiderTrip request() => RiderTrip(
          id: 3,
          originAddress: 'Abdoun Circle',
          destinationAddress: 'Sweifieh',
          seatsWanted: 2,
          departAt: DateTime.now().add(const Duration(hours: 2)),
          riderId: 9,
          originLat: 31.9539,
          originLng: 35.9106,
          destinationLat: 31.9800,
          destinationLng: 35.8600,
        );

    const from = Place('Abdoun', 31.9539, 35.9106);

    /// The Accept / Decline pair, in that order — the two buttons the card
    /// builds below the rider row.
    Iterable<InkWell> buttons(WidgetTester tester) => tester
        .widgetList<InkWell>(find.descendant(
            of: find.byType(Row).last, matching: find.byType(InkWell)));

    testWidgets('arms both buttons while an accept is still possible',
        (tester) async {
      await tester.pumpWidget(host(HailCard(
        request: request(),
        from: from,
        onAccept: () {},
        onDecline: () {},
      )));
      await tester.pump();

      expect(buttons(tester), isNotEmpty);
      for (final b in buttons(tester)) {
        expect(b.onTap, isNotNull);
      }
    });

    testWidgets('takes no second accept while one is already in flight',
        (tester) async {
      // One driver drives one car: with an accept in flight the dashboard hands
      // the card null callbacks, and neither button may still be armed.
      await tester.pumpWidget(host(HailCard(
        request: request(),
        from: from,
        onAccept: null,
        onDecline: null,
      )));
      await tester.pump();

      expect(buttons(tester), isNotEmpty);
      for (final b in buttons(tester)) {
        expect(b.onTap, isNull);
      }
    });
  });
}
