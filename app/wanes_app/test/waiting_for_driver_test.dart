import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/features/searching_screen.dart';
import 'package:wanes_app/widgets/live_trip_map.dart';
import 'package:wanes_app/widgets/map_backdrop.dart';

/// Waiting for a driver — the screen the rider watches after posting a trip.
///
/// It reaches for SSE on open, which fails closed in a test binding, so what is
/// asserted here is the still-waiting state: nobody has claimed it yet.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() => AppLocalizations.current = const AppLocalizations(Locale('en')));

  Widget host(Widget child) => MaterialApp(
        theme: WanesTheme.light(),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: child,
      );

  group('with the rider coordinates', () {
    Future<void> pump(WidgetTester tester) async {
      await tester.pumpWidget(host(SearchingScreen(
        riderTripId: 12,
        departAt: DateTime.now().add(const Duration(hours: 2)),
        driversNotified: 2,
        originLat: 31.9539,
        originLng: 35.9106,
        destLat: 31.9800,
        destLng: 35.8600,
      )));
      await tester.pump();
    }

    testWidgets('draws a real map on the posting, not the illustration',
        (tester) async {
      await pump(tester);

      expect(find.byType(LiveTripMap), findsOneWidget);
      expect(find.byType(MapBackdrop), findsNothing);

      final map = tester.widget<LiveTripMap>(find.byType(LiveTripMap));
      expect(map.origin.latitude, 31.9539);
      expect(map.destination.longitude, 35.8600);
    });

    testWidgets('has no car until a driver claims it', (tester) async {
      await pump(tester);
      final map = tester.widget<LiveTripMap>(find.byType(LiveTripMap));

      // Nobody has claimed it, so there is no driver to draw and no position to
      // draw them at.
      expect(map.progress.marker, isNull);
      expect(map.driverAt, isNull);
      // The ride is only a posting so far — the route reads as not-yet-happening.
      expect(map.progress.ahead, MapLegStyle.faint);
    });

    testWidgets('pings out from the rider pin while it waits', (tester) async {
      await pump(tester);
      await tester.pump();
      expect(find.byType(PingRings), findsOneWidget);
      expect(find.text('No trips on this route yet'), findsOneWidget);
    });
  });

  group('without coordinates', () {
    testWidgets('falls back to the prototype illustration', (tester) async {
      // An entry point that never carried a position must still render.
      await tester.pumpWidget(host(const SearchingScreen(riderTripId: 12)));
      await tester.pump();

      expect(find.byType(MapBackdrop), findsOneWidget);
      expect(find.byType(LiveTripMap), findsNothing);
    });
  });
}
