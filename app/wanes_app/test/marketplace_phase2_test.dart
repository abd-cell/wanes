import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:wanes_app/core/app_config.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/features/driver/trip_lifecycle.dart';
import 'package:wanes_app/models/models.dart';
import 'package:wanes_app/services/services.dart';
import 'package:wanes_app/widgets/accept_price_sheet.dart';
import 'package:wanes_app/widgets/trip_safety.dart';

/// Phases 2 and 3 on the device: the accept terms a driver sends, the offers a
/// rider compares, what a cancellation would cost, and boarding codes.
void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues({});
    AppLocalizations.current = const AppLocalizations(Locale('en'));
    AppConfigController.config.value = AppConfig.fallback;
  });

  Widget host(Widget child) => MaterialApp(
        theme: WanesTheme.light(),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: Scaffold(body: child),
      );

  group('the accept sheet', () {
    testWidgets('sends the seats opened and a conditional minimum', (tester) async {
      AcceptTerms? result;
      await tester.pumpWidget(host(Builder(
        builder: (context) => TextButton(
          onPressed: () async {
            result = await showAcceptPriceSheet(context, suggestion: 2, seats: 1, riders: 1, vehicleSeats: 4);
          },
          child: const Text('open'),
        ),
      )));
      await tester.pump();
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      // Close one seat: 3 on the trip.
      await tester.tap(find.text('–').at(0));
      await tester.pump();
      expect(find.text('3'), findsWidgets);

      // Only if it reaches 2.
      await tester.ensureVisible(find.byType(Switch));
      await tester.tap(find.byType(Switch));
      await tester.pump();

      await tester.ensureVisible(find.text('I understand this is a shared ride'));
      await tester.tap(find.text('I understand this is a shared ride'));
      await tester.pump();
      await tester.ensureVisible(find.byType(FilledButton));
      await tester.tap(find.byType(FilledButton));
      await tester.pumpAndSettle();

      expect(result, isNotNull);
      expect(result!.seatsOffered, 3);
      expect(result!.minPassengers, 2);
      expect(result!.pricePerSeat, 2);
    });

    testWidgets('offers no condition when the riders already fill the trip', (tester) async {
      await tester.pumpWidget(host(const SingleChildScrollView(
        child: AcceptPriceSheet(suggestion: 2, seats: 4, riders: 2, vehicleSeats: 4),
      )));
      await tester.pump();

      expect(find.text('Only if it fills up'), findsNothing);
    });
  });

  group('the models', () {
    test('an offer reads its conditional minimum and completion rate', () {
      final offer = RideOffer.fromJson({
        'interestId': 9,
        'driverId': 2,
        'driverName': 'Omar',
        'pricePerSeat': 1.75,
        'driverCompletionRate': 0.92,
        'minPassengers': 3,
        'vehicleSeats': 4,
      });
      expect(offer.minPassengers, 3);
      expect(offer.driverCompletionRate, 0.92);
      expect(offer.pricePerSeat, 1.75);
    });

    test('a cancel preview says whether it would pause the driver', () {
      final preview = CancelPreview.fromJson({
        'kind': 3,
        'points': 2,
        'ridersAffected': 2,
        'reasonRequired': true,
        'wouldSuspend': true,
      });
      expect(preview.kind, ReliabilityKind.lateCancel);
      expect(preview.wouldSuspend, isTrue);
    });

    test('a request is comparing offers only while its window is open', () {
      RiderTrip request(DateTime? decideAt) => RiderTrip(
            id: 1,
            originAddress: 'A',
            destinationAddress: 'B',
            departAt: DateTime.now().add(const Duration(hours: 5)),
            interestCount: 2,
            decideAt: decideAt,
          );
      expect(request(DateTime.now().add(const Duration(minutes: 10))).isComparingOffers, isTrue);
      expect(request(DateTime.now().subtract(const Duration(minutes: 1))).isComparingOffers, isFalse);
      expect(request(null).isComparingOffers, isFalse);
    });
  });

  group('settings', () {
    test('a fare change alone is adopted', () async {
      final cheaper = AppConfig.fromJson({...AppConfig.fallback.toJson(), 'farePerKm': 0.05});
      await AppConfigController.adopt(cheaper);
      expect(AppConfigController.value.farePerKm, 0.05);
    });

    test('the safety settings come across', () {
      final config = AppConfig.fromJson({
        'emergencyNumber': '112',
        'boardingCodeRequired': false,
        'shareBaseUrl': 'https://wanes.app',
      });
      expect(config.emergencyNumber, '112');
      expect(config.boardingCodeRequired, isFalse);
      expect(config.shareBaseUrl, 'https://wanes.app');
    });
  });

  group('boarding codes', () {
    Trip arrived() => Trip(
          id: 1,
          driverName: 'D',
          driverRating: 0,
          originAddress: 'A',
          destinationAddress: 'B',
          departAt: DateTime.now(),
          seatsLeft: 1,
          status: 6,
        );

    test('with codes on, nobody is boarded trip-wide', () {
      expect(DriverTripStep.forTrip(arrived(), TripService()), isNull);
    });

    test('with codes off, the trip-wide start is offered', () {
      AppConfigController.config.value = AppConfig.fromJson({'boardingCodeRequired': false});
      expect(DriverTripStep.forTrip(arrived(), TripService())?.labelKey, 'driver.startTrip');
    });

    testWidgets('the rider sees their code', (tester) async {
      await tester.pumpWidget(host(const BoardingCodeCard(code: '4821')));
      await tester.pump();
      expect(find.text('4821'), findsOneWidget);
      expect(find.text('Your boarding code'), findsOneWidget);
    });

    testWidgets('the driver types four digits to board a rider', (tester) async {
      String? code;
      await tester.pumpWidget(host(Builder(
        builder: (context) => TextButton(
          onPressed: () async => code = await showBoardingCodeEntry(context, 'Sara'),
          child: const Text('board'),
        ),
      )));
      await tester.pump();
      await tester.tap(find.text('board'));
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextField), '48a21');
      await tester.tap(find.text('Board rider'));
      await tester.pumpAndSettle();

      expect(code, '4821');
    });
  });
}
