import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/core/app_config.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/l10n/strings_ar.dart';
import 'package:wanes_app/l10n/strings_en.dart';
import 'package:wanes_app/models/models.dart';

/// A hail that ends before its countdown does.
///
/// Three things can close one — the rider withdraws it, another driver takes
/// it, or the window runs out — and the driver's screen learns about all three
/// the same way: a `rideRequestClosed` frame on the SSE stream. What is pinned
/// here is the wiring that turns that frame into a card leaving the screen: the
/// reason parses, it carries wording, and the deadline the card counts down to
/// is the server's rather than one the client invented.
void main() {
  setUp(() {
    AppLocalizations.current = const AppLocalizations(Locale('en'));
    AppConfigController.config.value = AppConfig.fallback;
  });

  group('the close reason', () {
    test('maps every status the server can close a posting with', () {
      expect(RiderTripClosedReason.fromWire('Cancelled'),
          RiderTripClosedReason.cancelled);
      expect(RiderTripClosedReason.fromWire('Claimed'),
          RiderTripClosedReason.claimed);
      expect(RiderTripClosedReason.fromWire('Expired'),
          RiderTripClosedReason.expired);
    });

    test('falls back rather than throwing on a reason this build has not met', () {
      // A newer server, or a status added after this build shipped. The card
      // still has to go — only the wording degrades.
      expect(RiderTripClosedReason.fromWire('Something'),
          RiderTripClosedReason.unknown);
      expect(RiderTripClosedReason.fromWire(null),
          RiderTripClosedReason.unknown);
    });

    test('every reason has copy in both languages', () {
      // Asserted against the maps, not through t(): a missing Arabic key falls
      // back to the English text, so a lookup would pass on copy that is not
      // actually translated.
      for (final reason in RiderTripClosedReason.values) {
        expect(enStrings, contains(reason.messageKey));
        expect(arStrings, contains(reason.messageKey));
      }
    });
  });

  group('the clock a card counts down to', () {
    RiderTrip posting({DateTime? departAt}) => RiderTrip(
          id: 1,
          originAddress: 'A',
          destinationAddress: 'B',
          departAt: departAt ?? DateTime.now().add(const Duration(minutes: 40)),
        );

    test('is the departure, not a window of its own', () {
      // A hail expired on an admin-set countdown. A posting runs until it
      // leaves — that is the whole deadline, and a driver has exactly that long
      // to take it.
      final departAt = DateTime.now().add(const Duration(minutes: 45));

      final row = posting(departAt: departAt);

      expect(row.departAt, departAt);
      expect(row.timeLeft.inMinutes, inInclusiveRange(44, 45));
    });

    test('never counts below zero once the departure has passed', () {
      final row = posting(departAt: DateTime.now().subtract(const Duration(minutes: 5)));

      expect(row.timeLeft, Duration.zero);
    });

    test('reads the departure off the wire', () {
      final row = RiderTrip.fromJson({
        'id': 4,
        'originAddress': 'A',
        'destinationAddress': 'B',
        'seatsWanted': 2,
        'riderCount': 2,
        'departAt': '2026-08-31T10:20:00Z',
        'status': 1,
      });

      expect(row.departAt.toUtc(), DateTime.utc(2026, 8, 31, 10, 20));
      expect(row.seatsWanted, 2);
      expect(row.isPool, isTrue);
    });
  });

  group('the lead-time rule', () {
    test('scales the earliest departure with the seats asked for', () {
      // One leg-time per seat: a driver has to gather everybody before they can
      // run the leg. The same arithmetic the server refuses on.
      const config = AppConfig(averageSpeedKmh: 30);
      final now = DateTime(2026, 9, 8, 12);

      final one = config.earliestDeparture(30, 1, from: now);
      final four = config.earliestDeparture(30, 4, from: now);

      expect(one.difference(now), const Duration(hours: 1));
      expect(four.difference(now), const Duration(hours: 4));
    });

    test('floors a short hop and caps a long one', () {
      const config = AppConfig(averageSpeedKmh: 30);
      final now = DateTime(2026, 9, 8, 12);

      // "Leaving in forty seconds" is not a posting a driver can reach.
      expect(config.earliestDeparture(0.3, 1, from: now).difference(now),
          const Duration(minutes: 15));
      // And eight seats over an intercity leg must not demand next week.
      expect(config.earliestDeparture(300, 8, from: now).difference(now),
          const Duration(hours: 6));
    });
  });
}
