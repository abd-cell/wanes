import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/features/driver/trip_lifecycle.dart';
import 'package:wanes_app/models/models.dart';
import 'package:wanes_app/services/services.dart';

/// The driver's side of a trip: which move it offers next, and the manifest row
/// for each rider holding a seat.
void main() {
  setUp(() => AppLocalizations.current = const AppLocalizations(Locale('en')));

  Widget host(Widget child) => MaterialApp(
        theme: WanesTheme.light(),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: Scaffold(body: Center(child: child)),
      );

  // 1 Posted · 2 Full · 3 Active · 4 Completed · 5 Cancelled · 6 Arrived
  Trip trip({int status = 1, int seatsLeft = 2, int seatsTotal = 3}) => Trip(
        id: 7,
        driverName: 'Omar Haddad',
        driverRating: 4.9,
        originAddress: 'Abdoun Circle',
        destinationAddress: 'Sweifieh',
        departAt: DateTime.now().add(const Duration(hours: 2)),
        seatsLeft: seatsLeft,
        seatsTotal: seatsTotal,
        status: status,
      );

  group('the next lifecycle move', () {
    final trips = TripService();
    String? labelFor(int status) => DriverTripStep.forTrip(trip(status: status), trips)?.labelKey;

    test('mirrors the order the server enforces', () {
      // Posted/Full -> EnRoute -> Arrived -> Active -> Completed, then nothing.
      // Setting off comes first because it is what takes the trip out of search;
      // the server still allows Posted -> Arrived for a driver who skips it.
      expect(labelFor(1), 'driver.departTrip');
      expect(labelFor(2), 'driver.departTrip');
      expect(labelFor(7), 'driver.arriveTrip');
      expect(labelFor(6), 'driver.startTrip');
      expect(labelFor(3), 'driver.completeTrip');
      expect(labelFor(4), isNull);
      expect(labelFor(5), isNull);
    });
  });

  group('a trip that takes the driver off the board', () {
    test('is one they are out on, and only that', () {
      // The server treats EnRoute, Arrived and Active as "engaged": on the way
      // to a pickup, at one, or carrying riders. A posted or finished trip
      // leaves the driver available.
      expect(trip(status: 3).isUnderway, isTrue); // Active
      expect(trip(status: 6).isUnderway, isTrue); // Arrived
      expect(trip(status: 7).isUnderway, isTrue); // EnRoute
      expect(trip(status: 1).isUnderway, isFalse); // Posted
      expect(trip(status: 2).isUnderway, isFalse); // Full
      expect(trip(status: 4).isUnderway, isFalse); // Completed
      expect(trip(status: 5).isUnderway, isFalse); // Cancelled
    });
  });

  group('the step button', () {
    testWidgets('shows the move a posted trip offers', (tester) async {
      await tester.pumpWidget(
          host(TripStepButton(trip: trip(status: 1), onChanged: () {})));
      await tester.pump();
      expect(find.text("I'm on my way"), findsOneWidget);
    });

    testWidgets('shows Arrived once the driver has set off', (tester) async {
      await tester.pumpWidget(
          host(TripStepButton(trip: trip(status: 7), onChanged: () {})));
      await tester.pump();
      expect(find.text("I've arrived"), findsOneWidget);
    });

    testWidgets('shows Start once the driver has arrived', (tester) async {
      await tester.pumpWidget(
          host(TripStepButton(trip: trip(status: 6), onChanged: () {})));
      await tester.pump();
      expect(find.text('Start trip'), findsOneWidget);
    });

    testWidgets('renders nothing on a finished trip', (tester) async {
      await tester.pumpWidget(
          host(TripStepButton(trip: trip(status: 4), onChanged: () {})));
      await tester.pump();
      expect(find.byType(FilledButton), findsNothing);
    });
  });

  group('a trip that has sold out', () {
    test('is Full, with no seats left and nothing editable', () {
      final full = trip(status: 2, seatsLeft: 0, seatsTotal: 3);
      expect(full.statusLabel, 'Full');
      expect(full.seatsLeft, 0);
      // Editing is off the table the moment a seat is taken.
      expect(full.editable, isFalse);
      expect(trip(status: 1, seatsLeft: 3, seatsTotal: 3).editable, isTrue);
    });
  });

  group('the manifest row', () {
    TripBooking row(Map<String, dynamic> over) => TripBooking.fromJson({
          'id': 42,
          'riderId': 9,
          'riderName': 'Layla Saeed',
          'riderRating': 4.7,
          'riderPhone': '+962781112222',
          'seats': 2,
          'status': 2,
          'bookedAt': '2026-08-31T09:15:00',
          ...over,
        });

    test('reads the rider, their seats and the shared reference', () {
      final r = row({});
      expect(r.riderName, 'Layla Saeed');
      expect(r.seats, 2);
      expect(r.riderRating, 4.7);
      // The same code the rider sees on their own booking.
      expect(r.reference, 'WNS-0016');
      expect(r.reference, Booking(
        id: 42, tripId: 7, seats: 2, status: 2,
        originAddress: '', destinationAddress: '',
      ).reference);
    });

    test('labels status off the same table the rider uses', () {
      expect(row({'status': 1}).statusKey, 'bookingStatus.pending');
      expect(row({'status': 2}).statusKey, 'bookingStatus.confirmed');
      expect(row({'status': 3}).statusKey, 'bookingStatus.inProgress');
      expect(row({'status': 4}).statusKey, 'bookingStatus.completed');
      expect(row({'status': 5}).statusKey, 'bookingStatus.cancelled');
      expect(row({'status': 6}).statusKey, 'bookingStatus.arrived');
      expect(row({'status': 7}).statusKey, 'bookingStatus.noShow');
    });

    test('only a live seat is callable', () {
      expect(row({'status': 2}).isLive, isTrue);
      expect(row({'status': 3}).isLive, isTrue);
      expect(row({'status': 6}).isLive, isTrue);
      expect(row({'status': 4}).isLive, isFalse);
      expect(row({'status': 5}).isLive, isFalse);
      // A no-show is settled too — nobody gave the seat back, but it is spent.
      expect(row({'status': 7}).isLive, isFalse);
    });

    group('the per-seat moves it offers', () {
      List<int> stepsFor(int status) =>
          SeatStep.forSeat(row({'status': status})).map((s) => s.status).toList();

      test('mirror the order the server enforces per seat', () {
        // Confirmed: reach them, take them aboard, or give up on them.
        expect(stepsFor(2), [6, 3, 7]);
        // Once the driver is at the pickup, arriving again is not a move.
        expect(stepsFor(6), [3, 7]);
        // Aboard: the only thing left is dropping them off — no no-show now.
        expect(stepsFor(3), [4]);
        // Settled seats offer nothing.
        expect(stepsFor(4), isEmpty);
        expect(stepsFor(5), isEmpty);
        expect(stepsFor(7), isEmpty);
      });

      test('only the no-show asks first', () {
        final steps = SeatStep.forSeat(row({'status': 2}));
        expect(steps.where((s) => s.confirm).map((s) => s.status), [7]);
      });
    });

    testWidgets('offers the next move first, and the no-show quietly',
        (tester) async {
      await tester.pumpWidget(host(SeatStepButtons(
          tripId: 7, seat: row({'status': 6}), onChanged: () {})));
      await tester.pump();

      // Picked up leads as the filled button; No show trails as an outline.
      expect(find.widgetWithText(FilledButton, 'Picked up'), findsOneWidget);
      expect(find.widgetWithText(OutlinedButton, 'No show'), findsOneWidget);
    });

    testWidgets('offers nothing once the seat is settled', (tester) async {
      await tester.pumpWidget(host(SeatStepButtons(
          tripId: 7, seat: row({'status': 4}), onChanged: () {})));
      await tester.pump();
      expect(find.byType(FilledButton), findsNothing);
      expect(find.byType(OutlinedButton), findsNothing);
    });

    test('a withheld number comes back null, never an empty string', () {
      // The server drops riderPhone once the seat is done.
      final done = TripBooking.fromJson({
        'id': 42, 'riderId': 9, 'riderName': 'Layla Saeed',
        'seats': 1, 'status': 4,
      });
      expect(done.riderPhone, isNull);
      expect(done.isLive, isFalse);
    });

    test('falls back to a label when the rider has no name on file', () {
      expect(row({'riderName': '  '}).riderName, 'Rider');
    });
  });
}
