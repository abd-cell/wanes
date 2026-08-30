import 'dart:math' as math;

import 'l10n.dart';

/// Small geo helpers used to turn raw lat/lng pairs into the human-scale
/// numbers the driver screens show ("1.2 km · 4 min to pickup").
class Geo {
  const Geo._();

  static const double _earthRadiusKm = 6371.0088;

  /// Great-circle distance between two coordinates, in kilometres.
  static double distanceKm(double lat1, double lng1, double lat2, double lng2) {
    final dLat = _rad(lat2 - lat1);
    final dLng = _rad(lng2 - lng1);
    final a = math.sin(dLat / 2) * math.sin(dLat / 2) +
        math.cos(_rad(lat1)) * math.cos(_rad(lat2)) * math.sin(dLng / 2) * math.sin(dLng / 2);
    return _earthRadiusKm * 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a));
  }

  /// Rough drive time for a straight-line distance — city traffic at ~22 km/h
  /// plus a small constant for pulling out and parking up. Minutes, min 1.
  static int etaMinutes(double km) => math.max(1, (km / 22 * 60 + 1.5).round());

  /// "850 m" under a kilometre, "1.2 km" above it — with the unit in the
  /// user's language.
  static String formatKm(double km) => km < 1
      ? AppLocalizations.current.t('units.metres', {'value': (km * 1000).round()})
      : AppLocalizations.current.t('units.km', {'value': km.toStringAsFixed(1)});

  static double _rad(double deg) => deg * math.pi / 180;
}
