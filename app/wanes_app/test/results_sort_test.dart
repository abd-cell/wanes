import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/core/trip_sort.dart';
import 'package:wanes_app/features/results_screen.dart';
import 'package:wanes_app/models/models.dart';

/// Sorting the carpool results.
///
/// The ordering rules are pure list maths, so most of this is a plain `test`;
/// the last case pumps the real screen to prove the sheet is reachable and that
/// picking an option re-orders the cards without a network call.
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

  List<int> idsFor(TripSort sort, {double? lat, double? lng}) => sample()
      .sortedBy(sort, originLat: lat, originLng: lng)
      .map((t) => t.id)
      .toList();

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

  test('pickup sorts by the walk from the rider start point', () {
    expect(idsFor(TripSort.pickup, lat: 31.95, lng: 35.92), [3, 2, 1]);
  });

  test('pickup with no rider start point leaves the order alone', () {
    expect(idsFor(TripSort.pickup), [1, 2, 3]);
  });

  test('ties keep the server order rather than shuffling', () {
    final tied = [
      trip(id: 7, name: 'One', price: 4),
      trip(id: 8, name: 'Two', price: 4),
      trip(id: 9, name: 'Three', price: 4),
    ];
    expect(tied.sortedBy(TripSort.price).map((t) => t.id).toList(), [7, 8, 9]);
  });

  testWidgets('picking a sort re-orders the cards', (tester) async {
    await tester.pumpWidget(MaterialApp(
      theme: WanesTheme.light(),
      supportedLocales: AppLocalizations.supportedLocales,
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      home: ResultsScreen(
        matches: sample(),
        from: 'A',
        to: 'B',
        fromLat: 31.95,
        fromLng: 35.92,
      ),
    ));
    await tester.pumpAndSettle();

    // Server order: Ali first.
    expect(tester.getTopLeft(find.text('Ali')).dy,
        lessThan(tester.getTopLeft(find.text('Cara')).dy));

    await tester.tap(find.text('Sort: Best match'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Lowest price'));
    await tester.pumpAndSettle();

    // Cara is the cheapest seat, so she moves above Ali.
    expect(find.text('Sort: Lowest price'), findsOneWidget);
    expect(tester.getTopLeft(find.text('Cara')).dy,
        lessThan(tester.getTopLeft(find.text('Ali')).dy));
  });
}
