import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/features/live_trip_screen.dart';
import 'package:wanes_app/models/models.dart';
import 'package:wanes_app/widgets/live_trip_map.dart';
import 'package:wanes_app/widgets/map_backdrop.dart';
import 'package:wanes_app/widgets/wanes_ui.dart';

/// The rider's live-trip screen — the stage rail and the driver row.
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
        departAt: DateTime.now().add(const Duration(minutes: 6)),
        seatsLeft: 2,
        seatsTotal: 3,
        vehicleLabel: 'Toyota Prius',
        vehiclePlate: 'WNS-4021',
        status: status,
        startedAt: startedAt,
        // Real coordinates: a zero-length route has no progress to measure, so
        // the mid-trip position would have nothing to work from.
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

  group('the driver row', () {
    testWidgets('offers a call button and no messaging', (tester) async {
      await pump(tester, t: trip(), b: booking());

      expect(find.byIcon(Icons.call_outlined), findsOneWidget);
      // Messaging was removed — neither icon the old row used may come back.
      expect(find.byIcon(Icons.chat_bubble_outline_rounded), findsNothing);
      expect(find.byIcon(Icons.message_outlined), findsNothing);
      expect(find.text('Omar Haddad'), findsOneWidget);
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
    /// The rail marks every stage up to and including the current one.
    int stagesDone(WidgetTester tester) {
      final stepper = tester.widget<TripStepper>(find.byType(TripStepper));
      return stepper.current;
    }

    testWidgets('a posted trip sits on "on the way"', (tester) async {
      await pump(tester, t: trip(status: 1), b: booking());
      expect(stagesDone(tester), 0);
    });

    testWidgets('a full trip is still only "on the way"', (tester) async {
      await pump(tester, t: trip(status: 2), b: booking());
      expect(stagesDone(tester), 0);
    });

    testWidgets('an arrived trip moves the rail to "arrived"', (tester) async {
      await pump(tester, t: trip(status: 6), b: booking());
      expect(stagesDone(tester), 1);
    });

    testWidgets('an active trip moves the rail to "in trip"', (tester) async {
      await pump(tester, t: trip(status: 3), b: booking(status: 3));
      expect(stagesDone(tester), 2);
    });

    testWidgets('a completed trip reaches "done" and offers the rating',
        (tester) async {
      await pump(tester, t: trip(status: 4), b: booking(status: 4, phone: null));

      expect(stagesDone(tester), 3);
      expect(find.text('Rate your trip'), findsOneWidget);
      expect(find.text('Cancel trip'), findsNothing);
    });

    testWidgets('an unfinished trip offers Cancel trip, not the rating',
        (tester) async {
      await pump(tester, t: trip(status: 6), b: booking());

      expect(find.text('Cancel trip'), findsOneWidget);
      expect(find.text('Rate your trip'), findsNothing);
    });
  });

  group('the map', () {
    /// How the map has been told to draw itself for the stage on screen.
    ///
    /// The live-trip screen now renders a real, pannable OpenStreetMap
    /// ([LiveTripMap]) rather than the prototype illustration, but the stage
    /// logic it is driven by is unchanged — same [MapProgress] contract.
    MapProgress progress(WidgetTester tester) {
      final map = tester.widget<LiveTripMap>(find.byType(LiveTripMap));
      return map.progress;
    }

    testWidgets('before pickup the leg down to the rider is the dashed one',
        (tester) async {
      await pump(tester, t: trip(status: 1), b: booking());
      final p = progress(tester);

      // The car is up the road, not on the dot, with the ride beyond it drawn
      // as context only.
      expect(p.at, greaterThan(0));
      expect(p.behind, MapLegStyle.dashed);
      expect(p.ahead, MapLegStyle.faint);
    });

    testWidgets('a nearer pickup puts the car closer to the dot', (tester) async {
      await pump(tester, t: trip(status: 1), b: booking());
      final far = progress(tester).at;

      await tester.pumpWidget(host(LiveTripScreen(
        trip: Trip(
          id: 7,
          driverName: 'Omar Haddad',
          driverRating: 4.8,
          originAddress: 'Abdoun Circle',
          destinationAddress: 'Sweifieh',
          // Due now rather than in six minutes.
          departAt: DateTime.now(),
          seatsLeft: 2,
          seatsTotal: 3,
          status: 1,
        ),
        booking: booking(),
      )));
      await tester.pump();

      expect(progress(tester).at, lessThan(far));
    });

    testWidgets('an arrived driver sits on the pickup dot, pinging',
        (tester) async {
      await pump(tester, t: trip(status: 6), b: booking());
      // The real map fixes its camera on the first frame; its marker layer only
      // knows what is in view — and so builds the car — on the next one.
      await tester.pump();

      expect(progress(tester).at, 0);
      expect(find.byType(PingRings), findsOneWidget);
    });

    testWidgets('once under way the covered road is solid and the drop-off dashed',
        (tester) async {
      await pump(
          tester,
          t: trip(
              status: 3,
              startedAt: DateTime.now().toUtc().subtract(const Duration(minutes: 2))),
          b: booking(status: 3));
      final p = progress(tester);

      expect(p.at, greaterThan(0));
      expect(p.behind, MapLegStyle.solid);
      expect(p.ahead, MapLegStyle.dashed);
      // The rings belong at the pickup, not mid-journey.
      expect(find.byType(PingRings), findsNothing);
    });

    testWidgets('a completed trip draws the whole route solid, car at the end',
        (tester) async {
      await pump(tester, t: trip(status: 4), b: booking(status: 4, phone: null));
      final p = progress(tester);

      expect(p.at, greaterThan(0.9));
      expect(p.behind, MapLegStyle.solid);
      expect(p.ahead, MapLegStyle.solid);
    });

    testWidgets('a cancelled trip fades both legs', (tester) async {
      await pump(tester, t: trip(status: 5), b: booking(status: 5, phone: null));
      final p = progress(tester);

      expect(p.behind, MapLegStyle.faint);
      expect(p.ahead, MapLegStyle.faint);
    });

    testWidgets('offers a refresh, for when the SSE stream has dropped',
        (tester) async {
      await pump(tester, t: trip(status: 6), b: booking());

      // The opening status re-read is already in flight, so the button starts
      // busy and only offers itself once that has come back (failed closed, in
      // a test binding).
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      await tester.pump(const Duration(milliseconds: 50));

      final refresh = find.byIcon(Icons.refresh_rounded);
      expect(refresh, findsOneWidget);

      // Asking again with no connection is harmless, and leaves the rail where
      // the trip's own status put it.
      await tester.tap(refresh);
      await tester.pump(const Duration(milliseconds: 50));

      expect(refresh, findsOneWidget);
      expect(tester.widget<TripStepper>(find.byType(TripStepper)).current, 1);
    });
  });

  group('the real map', () {
    LiveTripMap map(WidgetTester tester) =>
        tester.widget<LiveTripMap>(find.byType(LiveTripMap));

    testWidgets('is plotted on the real trip coordinates, and is pannable',
        (tester) async {
      await pump(
        tester,
        t: Trip(
          id: 7,
          driverName: 'Omar Haddad',
          driverRating: 4.8,
          originAddress: 'Abdoun Circle',
          destinationAddress: 'Sweifieh',
          departAt: DateTime.now().add(const Duration(minutes: 6)),
          seatsLeft: 2,
          seatsTotal: 3,
          originLat: 31.9539,
          originLng: 35.9106,
          destinationLat: 31.9800,
          destinationLng: 35.8600,
        ),
        b: booking(),
      );

      final m = map(tester);
      // The old illustration ignored these entirely and drew the same curve
      // for every trip.
      expect(m.origin.latitude, 31.9539);
      expect(m.origin.longitude, 35.9106);
      expect(m.destination.latitude, 31.9800);
      expect(m.destination.longitude, 35.8600);

      final opts = tester.widget<FlutterMap>(find.byType(FlutterMap)).options;
      expect(opts.interactionOptions.flags & InteractiveFlag.drag, isNot(0));
      expect(opts.interactionOptions.flags & InteractiveFlag.pinchZoom, isNot(0));
      // North-up: the markers are not rotation-aware.
      expect(opts.interactionOptions.flags & InteractiveFlag.rotate, 0);
    });

    testWidgets('credits OpenStreetMap, as its tile policy requires',
        (tester) async {
      await pump(tester, t: trip(), b: booking());
      await tester.pump();
      expect(find.byType(RichAttributionWidget), findsOneWidget);
    });

    testWidgets('has no driver fix to place the car on until one is reported',
        (tester) async {
      await pump(tester, t: trip(status: 1), b: booking());
      // Nothing reported in a test binding, so the car falls back to the
      // stage-derived position rather than showing a bogus 0,0 fix.
      expect(map(tester).driverAt, isNull);
    });

    testWidgets('greys the route out when the trip is cancelled', (tester) async {
      await pump(tester, t: trip(status: 5), b: booking(status: 5, phone: null));
      expect(map(tester).dimmed, isTrue);
    });
  });

  group('the car keeps moving', () {
    LiveTripMap map(WidgetTester tester) =>
        tester.widget<LiveTripMap>(find.byType(LiveTripMap));

    testWidgets('mid-trip it advances with the clock, not a fixed fraction',
        (tester) async {
      // Two identical trips, started at different times. The one that has been
      // going longer must be further along; the old build parked both at 0.45.
      await tester.pumpWidget(host(LiveTripScreen(
        key: const ValueKey('early'),
        trip: trip(
            status: 3,
            startedAt: DateTime.now().toUtc().subtract(const Duration(minutes: 1))),
        booking: booking(status: 3),
      )));
      await tester.pump();
      final early = map(tester).progress.at;

      // A distinct key so the screen is rebuilt from scratch rather than
      // reusing the state — and the trip — of the one above.
      await tester.pumpWidget(host(LiveTripScreen(
        key: const ValueKey('later'),
        trip: trip(
            status: 3,
            startedAt: DateTime.now().toUtc().subtract(const Duration(minutes: 4))),
        booking: booking(status: 3),
      )));
      await tester.pump();
      final later = map(tester).progress.at;

      expect(later, greaterThan(early));
      expect(early, greaterThan(0));
    });

    testWidgets('mid-trip it never runs past the drop-off on its own estimate',
        (tester) async {
      await pump(tester,
          t: trip(
              status: 3,
              startedAt: DateTime.now().toUtc().subtract(const Duration(hours: 4))),
          b: booking(status: 3));
      // Only the driver's own "completed" puts the car on the pin.
      expect(map(tester).progress.at, lessThan(1.0));
      expect(map(tester).progress.at, greaterThan(0.5));
    });

    testWidgets('with no start time it waits at the pickup rather than guessing',
        (tester) async {
      await pump(tester, t: trip(status: 3), b: booking(status: 3));
      expect(map(tester).progress.at, 0.0);
    });

    testWidgets('the approach still closes on the pickup as the clock runs down',
        (tester) async {
      await pump(tester, t: trip(status: 1), b: booking());
      final far = map(tester).progress.at;

      await tester.pumpWidget(host(LiveTripScreen(
        key: const ValueKey('due-now'),
        trip: Trip(
          id: 7,
          driverName: 'Omar Haddad',
          driverRating: 4.8,
          originAddress: 'Abdoun Circle',
          destinationAddress: 'Sweifieh',
          departAt: DateTime.now(), // due now
          seatsLeft: 2,
          seatsTotal: 3,
          originLat: 31.9539,
          originLng: 35.9106,
          destinationLat: 31.9800,
          destinationLng: 35.8600,
        ),
        booking: booking(),
      )));
      await tester.pump();

      expect(map(tester).progress.at, lessThan(far));
    });
  });

  group('the ETA card', () {
    testWidgets('counts down to pickup before the trip starts', (tester) async {
      await pump(tester, t: trip(status: 1), b: booking());
      // Departure is 6 minutes out; the countdown is real, not a fake timer.
      expect(find.textContaining('min'), findsOneWidget);
      expect(find.text('to pickup'), findsOneWidget);
    });

    testWidgets('drops the countdown once under way, showing the stage',
        (tester) async {
      await pump(tester, t: trip(status: 3), b: booking(status: 3));
      expect(find.text('to drop-off'), findsOneWidget);
      expect(find.textContaining('min'), findsNothing);
    });
  });
}
