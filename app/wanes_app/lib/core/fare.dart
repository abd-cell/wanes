import 'geo.dart';

/// Display-only fare maths.
///
/// There is no payment backend: hail requests carry no price, so the figures
/// the driver screens show next to a request are an *estimate* derived from the
/// straight-line distance with the rates below. Anything the driver actually
/// sets (a posted trip's `pricePerSeat`) is used verbatim and never goes
/// through here.
class Fare {
  const Fare._();

  static const String symbol = '£';

  /// Flag-fall, then a per-kilometre rate.
  static const double base = 2.50;
  static const double perKm = 1.20;

  static double estimate(double km, {int seats = 1}) =>
      (base + perKm * km) * (seats < 1 ? 1 : seats);

  /// Estimated fare between two coordinates.
  static double estimateBetween(
    double lat1, double lng1, double lat2, double lng2, {int seats = 1}) =>
      estimate(Geo.distanceKm(lat1, lng1, lat2, lng2), seats: seats);

  static String format(double amount) => '$symbol${amount.toStringAsFixed(2)}';
}
