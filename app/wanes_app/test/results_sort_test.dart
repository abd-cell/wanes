import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/places.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/core/trip_sort.dart';
import 'package:wanes_app/features/results_screen.dart';
import 'package:wanes_app/models/models.dart';

/// Sorting the carpool results.
///
/// The ordering rules are pure list maths, so most of this is a plain `test`;
/// the last case pumps the real screen to prove the sheet is reachable and that
/// picking an option re-orders the cards without a network call.
///
/// The sort works on matches rather than trips because the walk to the pickup
/// only exists on the match: a corridor result is met on the driver's route,
/// not at the driver's own start point.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUpAll(initializeDateFormatting);
  setUp(() => AppLocalizations.current = const AppLocalizations(Locale('en')));

  final base = DateTime(2026, 9, 1, 9);

  Trip trip({
    required int id,
    required String name,
    double rating = 4.0,
    double? price,
    int minutesAhead = 30,
    int seatsLeft = 2,
    double originLat = 31.95,
  }) =>
      Trip(
        id: id,
        driverName: name,
        driverRating: rating,
        originAddress: 'A',
        destinationAddress: 'B',
        departAt: base.add(Duration(minutes: minutesAhead)),
        seatsLeft: seatsLeft,
        pricePerSeat: price,
        originLat: originLat,
        originLng: 35.92,
        destinationLat: 32.01,
        destinationLng: 35.87,
      );

  // Deliberately not in any of the sorted orders, so a passing assertion means
  // the sort ran rather than the input happening to be right.
  List<Trip> sample() => [
        trip(id: 1, name: 'Ali', rating: 3.2, price: 5, minutesAhead: 60, seatsLeft: 1, originLat: 31.98),
        trip(id: 2, name: 'Bilal', rating: 4.9, price: null, minutesAhead: 10, seatsLeft: 4, originLat: 31.96),
        trip(id: 3, name: 'Cara', rating: 4.1, price: 2, minutesAhead: 45, seatsLeft: 3, originLat: 31.95),
      ];

  /// A direct match, where the rider walks to the trip's own start point.
  /// [walkKm] is what the server measured, as it arrives on the wire.
  SearchMatch match(Trip trip, {double walkKm = 0, SearchTier tier = SearchTier.direct}) =>
      SearchMatch(tier: tier, trip: trip, pickupWalkKm: walkKm);

  /// The sample as matches, with the walk the server would have sent for each:
  /// trip 3 starts on top of the rider, 2 is a little way off, 1 furthest.
  List<SearchMatch> matches() => [
        match(sample()[0], walkKm: 3.3),
        match(sample()[1], walkKm: 1.1),
        match(sample()[2], walkKm: 0.0),
      ];

  List<int> idsFor(TripSort sort) =>
      matches().sortedBy(sort).map((m) => m.trip.id).toList();

  test('best match leaves the server order alone', () {
    expect(idsFor(TripSort.best), [1, 2, 3]);
  });

  test('departure puts the soonest first', () {
    expect(idsFor(TripSort.departure), [2, 3, 1]);
  });

  test('price sorts cheapest first and sinks the unpriced trip', () {
    expect(idsFor(TripSort.price), [3, 1, 2]);
  });

  test('rating sorts highest first', () {
    expect(idsFor(TripSort.rating), [2, 3, 1]);
  });

  test('seats sorts most-free first', () {
    expect(idsFor(TripSort.seats), [2, 3, 1]);
  });

  test('pickup sorts by the walk the server measured', () {
    expect(idsFor(TripSort.pickup), [3, 2, 1]);
  });

  test('pickup uses the projected walk, not the distance to the driver start', () {
    // The case the old trip-based sort got wrong. This trip sets off two towns
    // away and passes the rider's door: the walk is 200 m, while the distance
    // to where the driver started is enormous. Sorting on the trip's own origin
    // put it last; sorting on what the rider actually walks puts it first.
    final passing = match(
      trip(id: 4, name: 'Dana', originLat: 29.50),
      walkKm: 0.2,
      tier: SearchTier.onTheWay,
    );
    final nearby = match(trip(id: 5, name: 'Eman', originLat: 31.95), walkKm: 2.5,
        tier: SearchTier.onTheWay);

    expect([nearby, passing].sortedBy(TripSort.pickup).map((m) => m.trip.id).toList(),
        [4, 5]);
  });

  test('ties keep the server order rather than shuffling', () {
    final tied = [
      match(trip(id: 7, name: 'One', price: 4)),
      match(trip(id: 8, name: 'Two', price: 4)),
      match(trip(id: 9, name: 'Three', price: 4)),
    ];
    expect(tied.sortedBy(TripSort.price).map((m) => m.trip.id).toList(), [7, 8, 9]);
  });

  testWidgets('picking a sort re-orders the cards', (tester) async {
    await tester.pumpWidget(MaterialApp(
      theme: WanesTheme.light(),
      supportedLocales: AppLocalizations.supportedLocales,
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      home: ResultsScreen(
        // Direct matches: the band the sort reorders within.
        matches: matches(),
        from: const Place('A', 31.95, 35.92),
        to: const Place('B', 31.98, 35.86),
        earliestDepartAt: DateTime.now().add(const Duration(minutes: 20)),
      ),
    ));
    await tester.pumpAndSettle();

    // Server order: Ali first.
    expect(tester.getTopLeft(find.text('Ali')).dy,
        lessThan(tester.getTopLeft(find.text('Cara')).dy));

    await tester.tap(find.text('Sort: Best match'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Lowest price'));

    // Not pumpAndSettle: the tap also asks the server for the page that belongs
    // to this sort, and that request never resolves against the test binding.
    // What is asserted here is the half that must not wait for it — the list in
    // hand re-orders on the frame after the tap.
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));

    // Cara is the cheapest seat, so she moves above Ali.
    expect(find.text('Sort: Lowest price'), findsOneWidget);
    expect(tester.getTopLeft(find.text('Cara')).dy,
        lessThan(tester.getTopLeft(find.text('Ali')).dy));
  });
}
