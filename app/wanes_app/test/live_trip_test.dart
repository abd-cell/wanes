import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/features/live_trip_screen.dart';
import 'package:wanes_app/models/models.dart';
import 'package:wanes_app/widgets/wanes_ui.dart';

/// The rider's live-trip screen — prototype screen 06: the TRIP STATUS
/// headline with its ETA card, the vertical stage rail, and the driver sheet.
///
/// The screen reaches for the network in `initState` (a status re-read and the
/// SSE stream); both fail closed in a test binding, which is the same path a
/// phone with no signal takes. What is asserted here is what it paints from the
/// trip and booking it was handed, with no events having arrived.
void main() {
  setUp(() => AppLocalizations.current = const AppLocalizations(Locale('en')));

  Widget host(Widget child) => MaterialApp(
        theme: WanesTheme.light(),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: child,
      );

  // 1 Posted · 2 Full · 3 Active · 4 Completed · 5 Cancelled · 6 Arrived
  Trip trip({int status = 1, DateTime? startedAt}) => Trip(
        id: 7,
        driverName: 'Omar Haddad',
        driverRating: 4.8,
        originAddress: 'Abdoun Circle',
        destinationAddress: 'Sweifieh',
        departAt: DateTime.now().add(const Duration(minutes: 6, seconds: 30)),
        seatsLeft: 2,
        seatsTotal: 3,
        vehicleLabel: 'Toyota Prius',
        vehiclePlate: 'WNS-4021',
        status: status,
        startedAt: startedAt,
        // Real coordinates: a zero-length route has no ride to estimate, so the
        // "In trip" row would have nothing to say.
        originLat: 31.9539,
        originLng: 35.9106,
        destinationLat: 31.9800,
        destinationLng: 35.8600,
      );

  // 1 Pending · 2 Confirmed · 3 InProgress · 4 Completed · 5 Cancelled
  Booking booking({int status = 2, String? phone = '+962791112222'}) => Booking(
        id: 23,
        tripId: 7,
        seats: 1,
        status: status,
        originAddress: 'Abdoun Circle',
        destinationAddress: 'Sweifieh',
        departAt: DateTime.now().add(const Duration(minutes: 6)),
        driverPhone: phone,
      );

  Future<void> pump(WidgetTester tester, {required Trip t, required Booking b}) async {
    await tester.pumpWidget(host(LiveTripScreen(trip: t, booking: b)));
    await tester.pump();
  }

  TripTimeline rail(WidgetTester tester) =>
      tester.widget<TripTimeline>(find.byType(TripTimeline));

  group('the driver row', () {
    testWidgets('offers a call button and no messaging', (tester) async {
      await pump(tester, t: trip(), b: booking());

      expect(find.byIcon(Icons.call_outlined), findsOneWidget);
      // Messaging was removed — neither icon the old row used may come back.
      expect(find.byIcon(Icons.chat_bubble_outline_rounded), findsNothing);
      expect(find.byIcon(Icons.message_outlined), findsNothing);
      expect(find.text('Omar Haddad'), findsOneWidget);
      expect(find.text('Toyota Prius · WNS-4021'), findsOneWidget);
    });

    testWidgets('drops the call button once the seat is no longer live',
        (tester) async {
      // Completed booking — the server withholds the number, so there is
      // nothing to dial and the button must not be offered.
      await pump(tester, t: trip(status: 4), b: booking(status: 4, phone: null));

      expect(find.byIcon(Icons.call_outlined), findsNothing);
    });
  });

  group('the stage rail', () {
    /// The rail opens with the booking, so the trip's own four stages sit one
    /// row further down than the screen's internal step index.
    testWidgets('always opens on the confirmed booking', (tester) async {
      await pump(tester, t: trip(status: 1), b: booking());

      expect(find.text('Booking confirmed'), findsOneWidget);
      expect(find.text('Seat with Omar Haddad'), findsOneWidget);
      // Every stage is drawn, whether reached or not — it is a rail, not a log.
      expect(find.text('On the way to pickup'), findsOneWidget);
      expect(find.text('Driver at pickup'), findsOneWidget);
      expect(find.text('In trip'), findsOneWidget);
      expect(find.text('Completed'), findsOneWidget);
      expect(rail(tester).stages, hasLength(5));
    });

    testWidgets('a posted trip sits on "on the way to pickup"', (tester) async {
      await pump(tester, t: trip(status: 1), b: booking());
      expect(rail(tester).current, 1);
      expect(find.text('Driver on the way'), findsOneWidget);
    });

    testWidgets('a full trip is still only "on the way"', (tester) async {
      await pump(tester, t: trip(status: 2), b: booking());
      expect(rail(tester).current, 1);
    });

    testWidgets('an arrived trip moves the rail to the pickup', (tester) async {
      await pump(tester, t: trip(status: 6), b: booking(status: 6));

      expect(rail(tester).current, 2);
      expect(find.text('Driver is here'), findsOneWidget);
      expect(find.text('Waiting for you'), findsOneWidget);
      // Nothing left to count down to — the driver is standing there.
      expect(find.text('ETA'), findsNothing);
    });

    testWidgets('an active trip moves the rail to "in trip"', (tester) async {
      await pump(tester, t: trip(status: 3), b: booking(status: 3));

      expect(rail(tester).current, 3);
      expect(find.text('On your way'), findsOneWidget);
    });

    testWidgets('a completed trip reaches the end and offers the rating',
        (tester) async {
      await pump(tester, t: trip(status: 4), b: booking(status: 4, phone: null));

      expect(rail(tester).current, 4);
      expect(find.text('Trip complete'), findsOneWidget);
      expect(find.text('Rate your trip'), findsOneWidget);
      expect(find.text('Cancel trip'), findsNothing);
    });

    testWidgets('an unfinished trip offers Cancel trip, not the rating',
        (tester) async {
      await pump(tester, t: trip(status: 6), b: booking());

      expect(find.text('Cancel trip'), findsOneWidget);
      expect(find.text('Rate your trip'), findsNothing);
    });

    testWidgets('a cancelled trip stops the rail and says so', (tester) async {
      await pump(tester, t: trip(status: 5), b: booking(status: 5, phone: null));

      // Nothing on the rail is live any more, so no node may beat.
      expect(rail(tester).muted, isTrue);
      expect(find.byType(PulseDot), findsNothing);
      expect(find.text('Trip cancelled'), findsOneWidget);
    });

    testWidgets('a no-show ends the rail in its own words', (tester) async {
      await pump(tester, t: trip(status: 3), b: booking(status: 7, phone: null));

      expect(rail(tester).muted, isTrue);
      expect(find.text('Marked as a no-show'), findsOneWidget);
    });
  });

  group('the ETA card', () {
    testWidgets('counts down to pickup before the trip starts', (tester) async {
      await pump(tester, t: trip(status: 1), b: booking());

      // Departure is 6 minutes out; the countdown is real, not a fake timer.
      expect(find.text('6 min'), findsOneWidget);
      expect(find.text('ETA'), findsOneWidget);
    });

    testWidgets('switches to the drop-off once under way', (tester) async {
      await pump(
        tester,
        t: trip(
            status: 3,
            startedAt: DateTime.now().toUtc().subtract(const Duration(minutes: 2))),
        b: booking(status: 3),
      );

      // Still an ETA, but now the one that matters — and the rail's own
      // estimate for the ride is on the "In trip" row.
      expect(find.text('ETA'), findsOneWidget);
      expect(find.textContaining('arrive'), findsOneWidget);
    });

    testWidgets('goes away once there is nothing left to count', (tester) async {
      await pump(tester, t: trip(status: 4), b: booking(status: 4, phone: null));
      expect(find.text('ETA'), findsNothing);
    });

    testWidgets('goes away when the trip is called off', (tester) async {
      await pump(tester, t: trip(status: 5), b: booking(status: 5, phone: null));
      expect(find.text('ETA'), findsNothing);
    });
  });

  group('the panel', () {
    testWidgets('is the prototype panel, with no map on it', (tester) async {
      await pump(tester, t: trip(), b: booking());

      expect(find.text('TRIP STATUS'), findsOneWidget);
      expect(find.byType(TripTimeline), findsOneWidget);
      expect(find.byType(MapSheet), findsOneWidget);
    });

    testWidgets('pulls to refresh, for when the SSE stream has dropped',
        (tester) async {
      await pump(tester, t: trip(status: 6), b: booking(status: 6));

      expect(find.byType(RefreshIndicator), findsOneWidget);

      // Asking again with no connection is harmless, and leaves the rail where
      // the trip's own status put it.
      // Pumped by hand rather than settled: the live node beats forever, so
      // nothing on this screen ever comes to rest.
      await tester.drag(find.byType(ListView), const Offset(0, 300));
      await tester.pump();
      await tester.pump(const Duration(seconds: 1));
      await tester.pump(const Duration(seconds: 1));

      expect(rail(tester).current, 2);
    });
  });
}
