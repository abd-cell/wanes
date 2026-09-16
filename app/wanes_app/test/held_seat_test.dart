import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/models/models.dart';
import 'package:wanes_app/widgets/gathering_chip.dart';

/// A seat that is held but not yet committed.
///
/// There is one kind left. A claimed posting confirms its riders outright — the
/// driver names the price and a rider who does not like it leaves — so the only
/// thing that can leave a seat pending is a trip short of the seats its driver
/// asked for. That seat is waiting on other people, owes nothing, and says so.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() => AppLocalizations.current = const AppLocalizations(Locale('en')));

  Booking booking({
    int status = 1, // Pending
    double? price,
    int minSeatsToConfirm = 1,
    int seatsHeld = 0,
  }) =>
      Booking(
        id: 7,
        tripId: 3,
        seats: 1,
        status: status,
        originAddress: 'A',
        destinationAddress: 'B',
        pricePerSeat: price,
        minSeatsToConfirm: minSeatsToConfirm,
        seatsHeld: seatsHeld,
      );

  Widget host(Widget child) => MaterialApp(
        theme: WanesTheme.light(),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: Scaffold(body: child),
      );

  testWidgets('says how many more seats the trip needs', (tester) async {
    await tester.pumpWidget(host(GatheringChip(
      booking: booking(minSeatsToConfirm: 3, seatsHeld: 1),
    )));
    await tester.pump();

    expect(find.textContaining('2 more seats'), findsOneWidget);
  });

  testWidgets('asks the rider for nothing', (tester) async {
    // The rider owes no answer here — the trip is waiting on other people — so
    // the chip reports and offers no action.
    await tester.pumpWidget(host(GatheringChip(
      booking: booking(minSeatsToConfirm: 3, seatsHeld: 1),
    )));
    await tester.pump();

    expect(find.byType(TextButton), findsNothing);
    expect(find.byType(ElevatedButton), findsNothing);
  });

  test('a held seat is the only pending kind there is', () {
    final held = booking(minSeatsToConfirm: 3, seatsHeld: 1);
    final confirmed = booking(status: 2, price: 4);

    expect(held.isPending, isTrue);
    expect(confirmed.isPending, isFalse);
  });

  test('a seat from a claimed posting arrives confirmed, with its price', () {
    // What the server sends after a claim: no handshake, no deadline — the
    // driver's figure and a seat that is already theirs.
    final seat = Booking.fromJson(const {
      'id': 82,
      'tripId': 79,
      'seats': 2,
      'status': 2,
      'originAddress': 'A',
      'destinationAddress': 'B',
      'pricePerSeat': 3.5,
      'driverPhone': '+962790000000',
    });

    expect(seat.isPending, isFalse);
    expect(seat.pricePerSeat, 3.5);
    // The number comes with it: nothing is being withheld pending an answer.
    expect(seat.driverPhone, isNotNull);
    expect(seat.isCancellable, isTrue);
  });
}
