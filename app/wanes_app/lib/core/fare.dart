import 'package:intl/intl.dart';

import '../models/models.dart';
import 'app_config.dart';
import 'geo.dart';
import 'l10n.dart';

/// Display-only fare maths.
///
/// There is no payment backend: hail requests carry no price, so the figures
/// the driver screens show next to a request are an *estimate* derived from the
/// straight-line distance with the rates below. Anything the driver actually
/// sets (a posted trip's `pricePerSeat`) is used verbatim and never goes
/// through here.
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

  /// Flag-fall, then a per-kilometre rate.
  static const double base = 2.50;
  static const double perKm = 1.20;

  static double estimate(double km, {int seats = 1}) =>
      (base + perKm * km) * (seats < 1 ? 1 : seats);

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
