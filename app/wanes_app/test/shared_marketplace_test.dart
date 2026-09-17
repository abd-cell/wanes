import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:wanes_app/core/acknowledgements.dart';
import 'package:wanes_app/core/departure_label.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/places.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/features/driver/marketplace_screen.dart';
import 'package:wanes_app/models/models.dart';
import 'package:wanes_app/widgets/accept_price_sheet.dart';

/// Phase 1 of the shared-ride marketplace: the now/scheduled split, the
/// countdown that no longer reads "1642:25", and the accept sheet that will not
/// arm until the driver agrees the trip is shared.
void main() {
  setUpAll(initializeDateFormatting);

  setUp(() {
    SharedPreferences.setMockInitialValues({});
    AppLocalizations.current = const AppLocalizations(Locale('en'));
  });

  Widget host(Widget child) => MaterialApp(
        theme: WanesTheme.light(),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: Scaffold(body: child),
      );

  RiderTrip request(int id, Duration leavesIn, {int seats = 1, int riders = 1, int status = 1}) =>
      RiderTrip(
        id: id,
        originAddress: 'Abdali Boulevard, Amman',
        destinationAddress: 'Zarqa City Centre',
        departAt: DateTime.now().add(leavesIn),
        seatsWanted: seats,
        riderCount: riders,
        status: status,
        originLat: 31.9632,
        originLng: 35.9106,
        destinationLat: 32.0728,
        destinationLng: 36.0880,
      );

  group('time left', () {
    testWidgets('reads as seconds, minutes, hours or days', (tester) async {
      late BuildContext ctx;
      await tester.pumpWidget(host(Builder(builder: (c) {
        ctx = c;
        return const SizedBox();
      })));
      await tester.pump();

      expect(untilLabel(ctx, const Duration(seconds: 42)), '42s');
      expect(untilLabel(ctx, const Duration(minutes: 12, seconds: 5)), '12:05');
      expect(untilLabel(ctx, const Duration(hours: 3, minutes: 7)), '3h 7m');
      // The case that used to read "1642:25".
      expect(untilLabel(ctx, const Duration(hours: 27, minutes: 22)), '1d 3h');
      expect(untilLabel(ctx, const Duration(seconds: -5)), '0s');
    });
  });

  group('the marketplace split', () {
    test('puts requests leaving within the hour under now, the rest under scheduled', () {
      final rows = [
        request(1, const Duration(minutes: 20)),
        request(2, const Duration(hours: 5)),
        request(3, const Duration(minutes: 50)),
        request(4, const Duration(days: 1)),
      ];
      expect(marketSegment(rows, MarketSegment.now).map((r) => r.id), [1, 3]);
      expect(marketSegment(rows, MarketSegment.scheduled).map((r) => r.id), [2, 4]);
    });

    test('drops closed and departed requests from both', () {
      final rows = [
        request(1, const Duration(minutes: -5)),
        request(2, const Duration(hours: 2), status: 2),
        request(3, const Duration(hours: 2)),
      ];
      expect(marketSegment(rows, MarketSegment.now), isEmpty);
      expect(marketSegment(rows, MarketSegment.scheduled).map((r) => r.id), [3]);
    });

    test('ranks scheduled work by seats, then by departure', () {
      final rows = [
        request(1, const Duration(hours: 3), seats: 1),
        request(2, const Duration(hours: 9), seats: 3),
        request(3, const Duration(hours: 4), seats: 3),
      ];
      expect(marketSegment(rows, MarketSegment.scheduled).map((r) => r.id), [3, 2, 1]);
    });
  });

  group('a marketplace card', () {
    testWidgets('leads with passengers, seats and the run total', (tester) async {
      await tester.pumpWidget(host(SingleChildScrollView(
        child: MarketRequestCard(
          request: request(7, const Duration(hours: 6), seats: 3, riders: 2),
          from: const Place('Here', 31.96, 35.91),
          onAccept: () {},
          onPass: () {},
        ),
      )));
      await tester.pump();

      expect(find.text('2 passengers'), findsOneWidget);
      expect(find.text('3 seats'), findsOneWidget);
      expect(find.text('Shared pool'), findsOneWidget);
      expect(find.text('Review & accept'), findsOneWidget);
    });
  });

  group('the accept sheet', () {
    testWidgets('will not accept until the driver agrees the trip is shared', (tester) async {
      await tester.pumpWidget(host(const SingleChildScrollView(
        child: AcceptPriceSheet(suggestion: 2, seats: 2, riders: 2, vehicleSeats: 4),
      )));
      await tester.pump();

      // Two of four seats stay open, and the sheet says so.
      expect(find.text('Open for more riders'), findsOneWidget);
      expect(find.textContaining('2 seats stay open'), findsOneWidget);

      FilledButton accept() => tester.widget<FilledButton>(find.byType(FilledButton));
      expect(accept().onPressed, isNull);

      await tester.ensureVisible(find.text('I understand this is a shared ride'));
      await tester.tap(find.text('I understand this is a shared ride'));
      await tester.pump();
      expect(accept().onPressed, isNotNull);
    });

    testWidgets('says the car is full when the riders take every seat', (tester) async {
      await tester.pumpWidget(host(const SingleChildScrollView(
        child: AcceptPriceSheet(suggestion: 2, seats: 4, riders: 3, vehicleSeats: 4),
      )));
      await tester.pump();

      expect(find.textContaining('These passengers fill your car'), findsOneWidget);
      expect(find.text('If every seat fills'), findsNothing);
    });
  });

  group('acknowledgements', () {
    test('are remembered once recorded', () async {
      expect(await Acknowledgements.has(Acknowledgement.driverSafety), isFalse);
      await Acknowledgements.record(Acknowledgement.driverSafety);
      expect(await Acknowledgements.has(Acknowledgement.driverSafety), isTrue);
      expect(await Acknowledgements.has(Acknowledgement.riderSafety), isFalse);
    });
  });
}
