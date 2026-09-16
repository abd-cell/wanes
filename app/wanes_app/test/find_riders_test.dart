import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/places.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/features/driver/rider_matches_screen.dart';
import 'package:wanes_app/models/models.dart';

/// The driver's side of search.
///
/// Two bands, and the card only says what is true of its band: a pool going the
/// driver's way costs no diversion, so the detour line is absent there and
/// present on the ones that do ask for one. That is the whole reason the driver
/// is choosing between them.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() => AppLocalizations.current = const AppLocalizations(Locale('en')));

  RiderTrip trip({
    int id = 30,
    int seatsWanted = 1,
    int riderCount = 1,
    String from = 'Amman',
    String to = 'Irbid',
  }) =>
      RiderTrip(
        id: id,
        riderName: 'Sam',
        originAddress: from,
        destinationAddress: to,
        departAt: DateTime.now().add(const Duration(hours: 3)),
        seatsWanted: seatsWanted,
        riderCount: riderCount,
        suggestedPricePerSeat: 3.5,
      );

  DemandMatch match({
    SearchTier tier = SearchTier.direct,
    double total = 0,
    int minutes = 0,
    int id = 30,
    int seatsWanted = 1,
    int riderCount = 1,
  }) =>
      DemandMatch(
        tier: tier,
        trip: trip(id: id, seatsWanted: seatsWanted, riderCount: riderCount),
        pickupDetourKm: total / 2,
        dropoffDetourKm: total / 2,
        totalDetourKm: total,
        minutesFromWhen: minutes,
      );

  Widget host(List<DemandMatch> matches) => MaterialApp(
        theme: WanesTheme.light(),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: RiderMatchesScreen(
          result: DemandSearchResult(matches: matches, seatsOffered: 4),
          from: const Place('Amman', 31.95, 35.92),
          to: const Place('Irbid', 32.55, 35.85),
          when: DateTime.now().add(const Duration(hours: 3)),
        ),
      );

  testWidgets('a pool going the driver way is shown under its own band', (tester) async {
    await tester.pumpWidget(host([match()]));
    await tester.pump();

    expect(find.text('GOING YOUR WAY'), findsOneWidget);
    expect(find.text('ON YOUR WAY'), findsNothing);
  });

  testWidgets('the two bands are headed separately, direct first', (tester) async {
    await tester.pumpWidget(host([
      match(id: 30),
      match(id: 31, tier: SearchTier.onTheWay, total: 4.2),
    ]));
    await tester.pump();

    final direct = tester.getTopLeft(find.text('GOING YOUR WAY')).dy;
    final onTheWay = tester.getTopLeft(find.text('ON YOUR WAY')).dy;
    expect(direct, lessThan(onTheWay));
  });

  // The screen snapshots its matches into state — it removes rows as the driver
  // takes them — so a second pumpWidget of the same type reuses that state.
  // These cases each get their own pump rather than sharing one.

  testWidgets('a pool going the driver way names no diversion', (tester) async {
    // On a direct match the diversion is nothing, and "0.0 km out of your way"
    // on every card in the band would be noise.
    await tester.pumpWidget(host([match(total: 0)]));
    await tester.pump();

    expect(find.textContaining('out of your way'), findsNothing);
  });

  testWidgets('a pool along the way names the diversion it costs', (tester) async {
    await tester.pumpWidget(host([match(tier: SearchTier.onTheWay, total: 4.2)]));
    await tester.pump();

    expect(find.textContaining('4.2 km out of your way'), findsOneWidget);
  });

  testWidgets('a pool wanting a later hour says how much later', (tester) async {
    await tester.pumpWidget(host([match(minutes: 25)]));
    await tester.pump();

    expect(find.text('25 min later'), findsOneWidget);
  });

  testWidgets('and one wanting an earlier hour says that instead', (tester) async {
    await tester.pumpWidget(host([match(minutes: -15)]));
    await tester.pump();

    expect(find.text('15 min earlier'), findsOneWidget);
  });

  testWidgets('a pool of several riders reads as a group', (tester) async {
    await tester.pumpWidget(host([match(seatsWanted: 3, riderCount: 2)]));
    await tester.pump();

    expect(find.textContaining('2'), findsWidgets);
    expect(find.text('Take it'), findsOneWidget);
  });

  testWidgets('nothing found says what to try instead', (tester) async {
    await tester.pumpWidget(host([]));
    await tester.pump();

    expect(find.text('Nobody is going your way yet'), findsOneWidget);
    expect(find.text('Take it'), findsNothing);
  });

  test('a match parses off the wire', () {
    final parsed = DemandMatch.fromJson(const {
      'tier': 2,
      'trip': {'id': 42, 'seatsWanted': 2, 'riderCount': 2, 'status': 8},
      'pickupDetourKm': 1.25,
      'dropoffDetourKm': 0.75,
      'totalDetourKm': 2.0,
      'minutesFromWhen': -10,
    });

    expect(parsed.tier, SearchTier.onTheWay);
    expect(parsed.isOnTheWay, isTrue);
    expect(parsed.trip.id, 42);
    expect(parsed.totalDetourKm, 2.0);
    expect(parsed.minutesFromWhen, -10);
    expect(parsed.isDoorToDoor, isFalse);
  });

  test('a match with no diversion worth naming is door to door', () {
    expect(DemandMatch(tier: SearchTier.direct, trip: trip(), totalDetourKm: 0.2).isDoorToDoor,
        isTrue);
  });
}
