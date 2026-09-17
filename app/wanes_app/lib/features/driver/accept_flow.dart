import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

import '../../core/fare.dart';
import '../../core/geo.dart';
import '../../core/l10n.dart';
import '../../core/session.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/accept_price_sheet.dart';
import '../../widgets/safety_notes.dart';
import '../../widgets/wanes_alerts.dart';

/// The one way a driver takes a ride request, wherever they found it — the
/// dashboard, the marketplace, the full-screen board or their route search.
///
/// 1. the driver safety notes, the first time;
/// 2. the price-and-shared-trip sheet, which knows the car so it can say how
///    many seats stay open;
/// 3. the offer, with that car.
///
/// Returns true when the offer went through. Alerts are shown here, so callers
/// only update their own lists. [onBusy] brackets the network call.
Future<bool> acceptRideRequest(
  BuildContext context,
  RiderTrip request, {
  ValueChanged<bool>? onBusy,
}) async {
  if (!await ensureSafetyAcknowledged(context, SafetyAudience.driver)) return false;
  if (!context.mounted) return false;

  final vehicle = await DriverVehicles.primary();
  if (!context.mounted) return false;

  final terms = await showAcceptPriceSheet(
    context,
    suggestion: request.suggestedPricePerSeat > 0
        ? request.suggestedPricePerSeat
        : Fare.perSeat(Geo.distanceKm(request.originLat, request.originLng,
            request.destinationLat, request.destinationLng)),
    seats: request.seatsWanted,
    riders: request.riderCount,
    vehicleSeats: vehicle?.seatCapacity,
  );
  // Backing out of the sheet is declining, never accepting at the suggestion.
  if (terms == null || !context.mounted) return false;

  onBusy?.call(true);
  final res = await RiderTripService().offer(
    request.id,
    pricePerSeat: terms.pricePerSeat,
    vehicleId: vehicle?.id,
    seatsOffered: terms.seatsOffered,
    minPassengers: terms.minPassengers,
  );
  onBusy?.call(false);
  if (!context.mounted) return res.success;

  if (res.success) {
    final row = res.data;
    if (row != null && !row.isMatched && row.decideAt != null) {
      // Planned work: the offer waits for the riders, or the window.
      WanesAlerts.success(context, context.tr('driver.offerSent'),
          message: context.tr('driver.offerSentBody', {
            'time': DateFormat('HH:mm', context.l10n.localeName).format(row.decideAt!.toLocal()),
          }));
    } else {
      WanesAlerts.success(context, context.tr('driver.requestAccepted'),
          message: context.tr(terms.minPassengers != null
              ? 'driver.requestAcceptedGatheringBody'
              : 'driver.requestAcceptedBody'));
    }
  } else {
    WanesAlerts.failure(context, res,
        title: context.tr('driver.acceptFailed'),
        onRetry: () => acceptRideRequest(context, request, onBusy: onBusy));
  }
  return res.success;
}

/// The car a driver's offers are made with: their default, else the first.
///
/// Cached for the session so a driver working through the marketplace is not
/// charged a vehicles round-trip per card. [reset] drops it after the garage
/// changes.
class DriverVehicles {
  DriverVehicles._();

  static Vehicle? _primary;

  /// Whose garage [_primary] came from — a different account signing in on the
  /// same device must not offer with somebody else's car.
  static int? _loadedFor;

  static Future<Vehicle?> primary() async {
    final user = Session.instance.profile?.id;
    if (_loadedFor != null && _loadedFor == user) return _primary;
    final res = await VehicleService().myVehicles();
    if (!res.success) return null; // not cached: try again next time
    final cars = res.data ?? const <Vehicle>[];
    _primary = cars.where((v) => v.isDefault).firstOrNull ?? cars.firstOrNull;
    _loadedFor = user;
    return _primary;
  }

  static void reset() {
    _primary = null;
    _loadedFor = null;
  }
}
