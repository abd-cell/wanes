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
    test('maps every status the server can close a hail with', () {
      expect(RideRequestClosedReason.fromWire('Cancelled'),
          RideRequestClosedReason.cancelled);
      expect(RideRequestClosedReason.fromWire('Matched'),
          RideRequestClosedReason.matched);
      expect(RideRequestClosedReason.fromWire('Expired'),
          RideRequestClosedReason.expired);
    });

    test('falls back rather than throwing on a reason this build has not met', () {
      // A newer server, or a status added after this build shipped. The card
      // still has to go — only the wording degrades.
      expect(RideRequestClosedReason.fromWire('Something'),
          RideRequestClosedReason.unknown);
      expect(RideRequestClosedReason.fromWire(null),
          RideRequestClosedReason.unknown);
    });

    test('every reason has copy in both languages', () {
      // Asserted against the maps, not through t(): a missing Arabic key falls
      // back to the English text, so a lookup would pass on copy that is not
      // actually translated.
      for (final reason in RideRequestClosedReason.values) {
        expect(enStrings, contains(reason.messageKey));
        expect(arStrings, contains(reason.messageKey));
      }
    });
  });

  group('the deadline a card counts down to', () {
    RideRequestRow row({DateTime? expiresAt, DateTime? requestedAt}) =>
        RideRequestRow(
          id: 1,
          originAddress: 'A',
          destinationAddress: 'B',
          seats: 1,
          requestedAt: requestedAt ?? DateTime.now(),
          expiresAt: expiresAt,
        );

    test('is the one the server stamped, not the configured window', () {
      // The admin can change the window while requests are already open; a card
      // must keep counting down to the deadline its own request was given.
      final requestedAt = DateTime.now().subtract(const Duration(minutes: 2));
      final expiresAt = requestedAt.add(const Duration(minutes: 45));

      final r = row(requestedAt: requestedAt, expiresAt: expiresAt);

      expect(r.expiresAt, expiresAt);
      expect(r.ttl.inMinutes, 45);
    });

    test('falls back to the configured window when the API sent none', () {
      AppConfigController.config.value = const AppConfig(hailTtlMinutes: 3);
      final requestedAt = DateTime.now();

      final r = row(requestedAt: requestedAt);

      expect(r.expiresAt.difference(requestedAt).inMinutes, 3);
      expect(r.ttl.inMinutes, 3);
    });

    test('reads the deadline off the wire', () {
      final r = RideRequestRow.fromJson({
        'id': 4,
        'originAddress': 'A',
        'destinationAddress': 'B',
        'seats': 1,
        'requestedAt': '2026-08-31T10:00:00Z',
        'expiresAt': '2026-08-31T10:20:00Z',
      });

      expect(r.ttl.inMinutes, 20);
    });
  });

  group('the configured window', () {
    test('rides along with the rest of the configuration', () {
      final config = AppConfig.fromJson(const {
        'primaryColor': '#0FAE9E',
        'hailRequestTtlMinutes': 25,
      });

      expect(config.hailTtlMinutes, 25);
      expect(config.hailTtl, const Duration(minutes: 25));
      // Round-trips through the cache, so a cold start counts down correctly.
      expect(AppConfig.fromJson(config.toJson()).hailTtlMinutes, 25);
    });

    test('is clamped to what the server would accept', () {
      // A zero — an older API, or a settings row written before the column
      // existed — would otherwise expire every hail the instant it opened.
      expect(AppConfig.fromJson(const {'hailRequestTtlMinutes': 0}).hailTtlMinutes, 1);
      expect(AppConfig.fromJson(const {'hailRequestTtlMinutes': 9999}).hailTtlMinutes, 240);
    });

    test('falls back to the shipped default when the API omits it', () {
      expect(AppConfig.fromJson(const {}).hailTtlMinutes,
          AppConfig.fallback.hailTtlMinutes);
    });
  });
}
