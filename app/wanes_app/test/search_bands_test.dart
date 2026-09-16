import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/places.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/features/results_screen.dart';
import 'package:wanes_app/models/models.dart';

/// What a search comes back with, and how the screen says it.
///
/// Three things are worth pinning here, because each of them is a promise the
/// rest of the flow relies on: the bands stay apart and are labelled, a trip
/// that merely passes the rider's way says how far they walk to it, and an
/// empty result is an invitation to post rather than a dead end.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUpAll(initializeDateFormatting);
  setUp(() => AppLocalizations.current = const AppLocalizations(Locale('en')));

  final base = DateTime(2026, 9, 1, 9);

  Trip trip({
    required int id,
    required String name,
    int minutesAhead = 30,
    int seatsLeft = 2,
    int minSeatsToConfirm = 1,
    int seatsHeld = 0,
    bool isGathering = false,
  }) =>
      Trip(
        id: id,
        driverName: name,
        driverRating: 4.5,
        originAddress: 'A',
        destinationAddress: 'B',
        departAt: base.add(Duration(minutes: minutesAhead)),
        seatsLeft: seatsLeft,
        seatsTotal: seatsLeft + seatsHeld,
        pricePerSeat: 3,
        originLat: 31.95,
        originLng: 35.92,
        destinationLat: 32.01,
        destinationLng: 35.87,
        minSeatsToConfirm: minSeatsToConfirm,
        seatsHeld: seatsHeld,
        isGathering: isGathering,
      );

  RiderTrip posting({int id = 40, int riders = 2, int seats = 2}) => RiderTrip(
        id: id,
        originAddress: 'A',
        destinationAddress: 'B',
        departAt: base.add(const Duration(hours: 2)),
        seatsWanted: seats,
        riderCount: riders,
      );

  Widget host({
    List<SearchMatch> matches = const [],
    List<RiderTrip> postings = const [],
  }) =>
      MaterialApp(
        theme: WanesTheme.light(),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: ResultsScreen(
          matches: matches,
          postings: postings,
          from: const Place('Abdoun', 31.95, 35.92),
          to: const Place('Sweifieh', 32.01, 35.87),
          earliestDepartAt: base.add(const Duration(minutes: 20)),
        ),
      );

  group('the two bands', () {
    testWidgets('are labelled, and a direct match stays above a passing one',
        (tester) async {
      await tester.pumpWidget(host(matches: [
        SearchMatch(tier: SearchTier.direct, trip: trip(id: 1, name: 'Ali')),
        SearchMatch(
          tier: SearchTier.onTheWay,
          trip: trip(id: 2, name: 'Bilal'),
          pickupWalkKm: 0.3,
          dropoffWalkKm: 0.4,
        ),
      ]));
      await tester.pumpAndSettle();

      expect(find.text('GOING YOUR WAY'), findsOneWidget);
      expect(find.text('PASSING YOUR WAY'), findsOneWidget);
      expect(tester.getTopLeft(find.text('Ali')).dy,
          lessThan(tester.getTopLeft(find.text('Bilal')).dy));
    });

    testWidgets('a passing trip says how far the rider walks to the road',
        (tester) async {
      await tester.pumpWidget(host(matches: [
        SearchMatch(
          tier: SearchTier.onTheWay,
          trip: trip(id: 2, name: 'Bilal'),
          pickupWalkKm: 0.3,
          dropoffWalkKm: 1.2,
        ),
      ]));
      await tester.pumpAndSettle();

      // Metres below a kilometre: "0.3 km" of walking is a number nobody
      // pictures.
      expect(find.textContaining('300 m'), findsOneWidget);
      expect(find.textContaining('1.2 km'), findsOneWidget);
    });

    testWidgets('a direct match says nothing about walking to a route',
        (tester) async {
      await tester.pumpWidget(host(matches: [
        SearchMatch(tier: SearchTier.direct, trip: trip(id: 1, name: 'Ali')),
      ]));
      await tester.pumpAndSettle();

      expect(find.textContaining('to the pickup'), findsNothing);
    });
  });

  testWidgets('a trip short of its threshold says so before it is booked',
      (tester) async {
    await tester.pumpWidget(host(matches: [
      SearchMatch(
        tier: SearchTier.direct,
        trip: trip(id: 1, name: 'Ali', minSeatsToConfirm: 3, seatsHeld: 1, isGathering: true),
      ),
    ]));
    await tester.pumpAndSettle();

    // Hiding this would let a rider book a trip that may never run without
    // knowing that was possible.
    expect(find.textContaining('Runs at 3 seats'), findsOneWidget);
  });

  group('when nothing matches', () {
    testWidgets('the rider is offered the trip they could post', (tester) async {
      await tester.pumpWidget(host());
      await tester.pumpAndSettle();

      expect(find.text('Nobody is driving this yet'), findsOneWidget);
      expect(find.text('Post my trip'), findsOneWidget);
    });

    testWidgets('and nothing was opened on their behalf', (tester) async {
      // Search used to answer an empty result by opening a hail. Now it answers
      // with what it found and what the rider *could* do — posting is their own
      // act, and this screen is where they choose it.
      await tester.pumpWidget(host());
      await tester.pumpAndSettle();

      expect(find.textContaining('Searching'), findsNothing);
      expect(find.textContaining('drivers notified'), findsNothing);
    });
  });

  testWidgets('riders already asking for this route can be joined', (tester) async {
    await tester.pumpWidget(host(postings: [posting(riders: 2)]));
    await tester.pumpAndSettle();

    // On the second tab, because the first is the trips they can book today.
    await tester.tap(find.textContaining('Riders'));
    await tester.pumpAndSettle();

    expect(find.text('2 riders are waiting for a driver'), findsOneWidget);
    expect(find.text('Join'), findsOneWidget);
  });
}
