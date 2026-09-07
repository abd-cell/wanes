import 'package:intl/intl.dart';

import '../models/models.dart';
import 'app_config.dart';
import 'geo.dart';
import 'l10n.dart';

/// Display-only fare maths.
///
/// There is no payment backend. An open hail carries no price of its own, so the
/// figure the driver screens show next to a request is an *estimate* derived
/// from the straight-line distance and the admin-set rates below — the same
/// arithmetic, off the same rates, that the server uses to stamp a price on the
/// trip that accepting the hail creates. Anything a driver actually set (a
/// posted trip's `pricePerSeat`) is used verbatim and never goes through here.
class Fare {
  const Fare._();

  /// The configured currency symbol, for screens that lay the symbol out
  /// themselves (the price stepper) instead of taking a formatted string.
  static String get symbol => AppConfigController.value.currencySymbol;

  /// True when the symbol belongs after the amount — right-to-left currencies
  /// such as the dinar are written that way.
  static bool get symbolAfterAmount =>
      AppConfigController.value.currencyPosition == CurrencyPosition.after;

  /// Configured fraction digits, clamped to the 0–3 the settings allow. The
  /// dinar carries three, so a screen that writes the figure itself has to ask
  /// rather than assume the two most currencies use.
  static int get decimals => AppConfigController.value.currencyDecimals.clamp(0, 3);

  /// Flag-fall, then a per-kilometre rate — both admin-set.
  ///
  /// Read from the configuration rather than hardcoded because the server
  /// prices a hail-accepted trip from the same two numbers. While these were
  /// constants, the estimate shown on a driver's request card and the price
  /// stamped on the trip they got by accepting it could differ, and an admin
  /// changing the rates moved only one of them.
  static double get base => AppConfigController.value.fareBaseAmount;

  static double get perKm => AppConfigController.value.farePerKm;

  /// The total for [seats] seats over [km]. One seat's share is what the server
  /// stores as `pricePerSeat`, so the multiplication belongs here, not there.
  static double estimate(double km, {int seats = 1}) =>
      perSeat(km) * (seats < 1 ? 1 : seats);

  /// What a single seat over [km] is worth — the figure the server derives.
  static double perSeat(double km) => base + perKm * (km.isFinite && km > 0 ? km : 0);

  /// Estimated fare between two coordinates.
  static double estimateBetween(
    double lat1, double lng1, double lat2, double lng2, {int seats = 1}) =>
      estimate(Geo.distanceKm(lat1, lng1, lat2, lng2), seats: seats);

  /// An amount written in the platform currency the admin configured.
  ///
  /// Grouping and digit shaping follow the active language (Arabic renders its
  /// own digits); the symbol, its side and the precision come from the settings.
  static String format(double amount) {
    final config = AppConfigController.value;
    final digits = decimals;
    final text = NumberFormat.decimalPatternDigits(
      locale: AppLocalizations.current.localeName,
      decimalDigits: digits,
    ).format(amount);

    return config.currencyPosition == CurrencyPosition.after
        ? '$text ${config.currencySymbol}'
        : '${config.currencySymbol}$text';
  }
}
