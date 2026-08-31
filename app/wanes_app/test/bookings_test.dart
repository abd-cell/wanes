import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/features/booking_details_screen.dart';
import 'package:wanes_app/models/models.dart';
import 'package:wanes_app/widgets/wanes_ui.dart';

/// The Bookings tab and its details screen — the rider's own seats.
///
/// Nothing here touches the network on purpose: the details screen is pumped
/// with the booking the list already holds, which is exactly what it paints
/// before the trip lookup lands.
void main() {
  // The theme pulls in google_fonts, which reaches for ServicesBinding even
  // from a plain `test`. Idempotent, and keeps the run free of font warnings.
  TestWidgetsFlutterBinding.ensureInitialized();

  setUpAll(initializeDateFormatting);

  setUp(() => AppLocalizations.current = const AppLocalizations(Locale('en')));

  Widget host(Widget child) => MaterialApp(
        theme: WanesTheme.light(),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: child,
      );

  // Statuses are the server's BookingStatus: 1 Pending, 2 Confirmed,
  // 3 InProgress, 4 Completed, 5 Cancelled.
  Booking booking({
    int id = 42,
    int status = 2,
    int seats = 2,
    DateTime? departAt,
  }) =>
      Booking(
        id: id,
        tripId: 7,
        seats: seats,
        status: status,
        originAddress: 'Abdoun Circle',
        destinationAddress: 'Sweifieh',
        departAt: departAt ?? DateTime.now().add(const Duration(hours: 3)),
      );

  group('the booking model', () {
    test('the reference is the id in base-36, padded and prefixed', () {
      expect(booking(id: 42).reference, 'WNS-0016');
      expect(booking(id: 1).reference, 'WNS-0001');
    });

    test('status maps to a translated key', () {
      expect(booking(status: 1).statusLabel, 'Pending');
      expect(booking(status: 2).statusLabel, 'Confirmed');
      expect(booking(status: 3).statusLabel, 'In progress');
      expect(booking(status: 4).statusLabel, 'Completed');
      expect(booking(status: 5).statusLabel, 'Cancelled');
    });

    test('only a live, future booking counts as upcoming', () {
      final past = DateTime.now().subtract(const Duration(hours: 1));
      expect(booking().isUpcoming, isTrue);
      expect(booking(departAt: past).isUpcoming, isFalse);
      expect(booking(status: 4).isUpcoming, isFalse);
      expect(booking(status: 5).isUpcoming, isFalse);
    });

    test('a trip already under way is still upcoming, not history', () {
      // It departed in the past but has not finished — the rider is aboard.
      final past = DateTime.now().subtract(const Duration(hours: 1));
      expect(booking(status: 3, departAt: past).isUpcoming, isTrue);
    });

    test('a cancelled or completed booking can no longer be cancelled', () {
      // Mirrors BookingService.Cancel, which rejects exactly those two.
      expect(booking(status: 2).isCancellable, isTrue);
      expect(booking(status: 3).isCancellable, isTrue);
      expect(booking(status: 4).isCancellable, isFalse);
      expect(booking(status: 5).isCancellable, isFalse);
    });

    test('the driver phone rides along only while the seat is live', () {
      Booking parse(int status) => Booking.fromJson({
            'id': 1,
            'tripId': 7,
            'seats': 1,
            'status': status,
            'originAddress': 'A',
            'destinationAddress': 'B',
            'driverPhone': '+962790000000',
            'tripStatus': 3,
          });
      expect(parse(2).driverPhone, '+962790000000');
      expect(parse(2).tripStatus, 3);
      // The server withholds it for a finished seat; the model must not invent one.
      expect(Booking.fromJson({
        'id': 1,
        'tripId': 7,
        'seats': 1,
        'status': 4,
        'originAddress': 'A',
        'destinationAddress': 'B',
      }).driverPhone, isNull);
    });
  });

  group('status colours', () {
    // One table drives the rider's list, their booking details and the driver's
    // rider manifest. This is the regression that put a red "Cancelled" pill on
    // every freshly confirmed booking, so it is pinned per status.
    // Built per test, not at group scope: the theme pulls in google_fonts, and
    // touching it before the test binding exists makes it complain loudly.
    WanesTokens tokens() => WanesTheme.light().extension<WanesTokens>()!;

    test('each booking status gets its own colour', () {
      final t = tokens();
      expect(t.bookingStatus(1), t.amber);   // pending
      expect(t.bookingStatus(2), t.success); // confirmed
      expect(t.bookingStatus(3), t.teal);    // in progress
      expect(t.bookingStatus(4), t.info);    // completed
      expect(t.bookingStatus(5), t.alert);   // cancelled
    });

    test('completed and cancelled never share a colour', () {
      final t = tokens();
      expect(t.bookingStatus(4), isNot(t.bookingStatus(5)));
      expect(t.bookingStatus(4), isNot(t.bookingStatus(2)));
    });

    test('a trip status maps the same way for the driver', () {
      final t = tokens();
      expect(t.tripStatus(1), t.amber); // posted
      expect(t.tripStatus(2), t.amber); // full
      expect(t.tripStatus(6), t.teal);  // arrived
      expect(t.tripStatus(3), t.teal);  // active
      expect(t.tripStatus(4), t.info);  // completed
      expect(t.tripStatus(5), t.alert); // cancelled
    });

    testWidgets('the details pill paints a completed booking the completed colour',
        (tester) async {
      await tester.pumpWidget(host(BookingDetailsScreen(
          booking: booking(
              status: 4,
              departAt: DateTime.now().subtract(const Duration(hours: 2))))));
      await tester.pump();

      final pill = tester.widget<StatusPill>(find.byType(StatusPill));
      expect(pill.color, tokens().info);
      expect(find.text('Completed'), findsOneWidget);
    });

    testWidgets('a confirmed booking is not painted as an alert', (tester) async {
      await tester.pumpWidget(host(BookingDetailsScreen(booking: booking(status: 2))));
      await tester.pump();

      final pill = tester.widget<StatusPill>(find.byType(StatusPill));
      expect(pill.color, tokens().success);
      expect(pill.color, isNot(tokens().alert));
    });
  });

  group('initials', () {
    test('take the first and last name', () {
      expect(AvatarBadge.initialsOf('Layla Haddad'), 'LH');
      expect(AvatarBadge.initialsOf('Omar'), 'O');
      expect(AvatarBadge.initialsOf('  '), '?');
    });
  });

  group('booking details', () {
    testWidgets('shows the reference, status, route and seats', (tester) async {
      await tester.pumpWidget(host(BookingDetailsScreen(booking: booking())));
      await tester.pump();

      // The reference is one span inside the header's "Booking ref · …" line.
      expect(find.textContaining('WNS-0016', findRichText: true), findsOneWidget);
      expect(find.text('Confirmed'), findsOneWidget);
      expect(find.text('Abdoun Circle'), findsOneWidget);
      expect(find.text('Sweifieh'), findsOneWidget);
      expect(find.text('2'), findsOneWidget); // seats
      expect(find.text('View trip details'), findsOneWidget);
    });

    testWidgets('offers Cancel booking only while the seat is live',
        (tester) async {
      await tester.pumpWidget(host(BookingDetailsScreen(
          key: const ValueKey('live'), booking: booking())));
      await tester.pump();
      expect(find.text('Cancel booking'), findsOneWidget);

      // A distinct key so the screen is rebuilt from scratch rather than
      // reusing the state (and the booking) of the one above.
      await tester.pumpWidget(host(BookingDetailsScreen(
          key: const ValueKey('cancelled'), booking: booking(status: 5))));
      await tester.pump();
      expect(find.text('Cancel booking'), findsNothing);
    });

    testWidgets('a completed booking offers the rating instead',
        (tester) async {
      await tester.pumpWidget(host(BookingDetailsScreen(
          booking: booking(
              status: 4,
              departAt: DateTime.now().subtract(const Duration(hours: 2))))));
      await tester.pump();

      expect(find.text('Rate your trip'), findsOneWidget);
      expect(find.text('Cancel booking'), findsNothing);
    });
  });

  group('the rider tab bar', () {
    testWidgets('carries Bookings between Home and Trips', (tester) async {
      await tester.pumpWidget(host(Builder(
        builder: (context) => Scaffold(
          bottomNavigationBar: WanesBottomNav(
            index: 1,
            onSelect: (_) {},
            items: WanesBottomNav.riderItems(context),
          ),
        ),
      )));
      // The localisation delegate resolves asynchronously; MaterialApp paints
      // nothing until it has.
      await tester.pump();

      expect(find.text('Home'), findsOneWidget);
      expect(find.text('Bookings'), findsOneWidget);
      expect(find.text('Trips'), findsOneWidget);
      expect(find.text('Profile'), findsOneWidget);
    });
  });
}
