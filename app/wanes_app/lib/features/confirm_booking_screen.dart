import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;
import '../core/fare.dart';
import '../core/geo.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/map_backdrop.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';
import 'booking_confirmed_screen.dart';

/// Confirm booking — prototype screen 05. Driver + car card, the pickup point
/// with its map thumbnail, the fare breakdown, then a pinned confirm bar.
class ConfirmBookingScreen extends StatefulWidget {
  const ConfirmBookingScreen({
    super.key,
    required this.trip,
    required this.from,
    required this.to,
    this.seats = 1,
    this.fromLat,
    this.fromLng,
  });

  final Trip trip;
  final String from;
  final String to;
  final int seats;

  /// The rider's start point — null when we don't know it, in which case the
  /// walk line is left off rather than guessed.
  final double? fromLat;
  final double? fromLng;

  @override
  State<ConfirmBookingScreen> createState() => _ConfirmBookingScreenState();
}

class _ConfirmBookingScreenState extends State<ConfirmBookingScreen> {
  final _bookings = BookingService();
  bool _busy = false;

  Trip get trip => widget.trip;

  double get _fare => (trip.pricePerSeat ?? 0) * widget.seats;

  /// Wanes takes no cut yet, so the total is simply the seat price. The row is
  /// still shown (at zero) because the design's receipt has three lines.
  double get _serviceFee => 0;
  double get _total => _fare + _serviceFee;

  static String initialsOf(String name) {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
    return (parts.first[0] + parts.last[0]).toUpperCase();
  }

  Future<void> _confirm() async {
    setState(() => _busy = true);
    final res = await _bookings.book(trip.id, seats: widget.seats);
    if (!mounted) return;
    setState(() => _busy = false);
    if (res.success && res.data != null) {
      Navigator.pushReplacement(
        context,
        MaterialPageRoute(
          builder: (_) => BookingConfirmedScreen(
            trip: trip,
            booking: res.data!,
            seats: widget.seats,
            total: _total,
          ),
        ),
      );
    } else {
      WanesAlerts.failure(context, res,
          title: context.tr('booking.failed'),
          fallbackMessage: context.tr('booking.failedBody'),
          onRetry: _confirm);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        bottom: false,
        child: Column(children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 8, 20, 0),
            child: ScreenHeader(title: context.tr('booking.confirmTitle')),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
              children: [
                _driverCard(t),
                const SizedBox(height: 12),
                _pickupCard(t),
                const SizedBox(height: 12),
                _fareCard(t),
              ],
            ),
          ),
          BottomActionBar(
            child: PrimaryButton(
              label: _total > 0
                  ? '${context.tr('common.confirm')} · ${Fare.format(_total)}'
                  : context.tr('booking.confirmTitle'),
              arrow: false,
              busy: _busy,
              onPressed: _busy ? null : _confirm,
            ),
          ),
        ]),
      ),
    );
  }

  Widget _driverCard(WanesTokens t) {
    final car = trip.vehicleLabel;
    return WanesCard(
      radius: 16,
      child: Column(children: [
        Row(children: [
          AvatarBadge(initialsOf(trip.driverName), size: 52, tint: t.tealTint, fg: t.tealInk),
          const SizedBox(width: 13),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(trip.driverName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 16, color: t.ink)),
              const SizedBox(height: 2),
              InlineRating(
                  '${trip.driverRating.toStringAsFixed(1)} · ${context.tr('role.driver')}',
                  size: 12),
            ]),
          ),
          if (car.isNotEmpty) ...[
            const SizedBox(width: 8),
            Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
              Text(car,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
              if (trip.vehicleColor.isNotEmpty)
                Text(trip.vehicleColor,
                    style: WanesTheme.mono(size: 11, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
            ]),
          ],
        ]),
        if (trip.vehiclePlate.isNotEmpty) ...[
          const SizedBox(height: 14),
          Divider(height: 1, thickness: 1, color: t.border),
          const SizedBox(height: 14),
          Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
            Text(context.tr('vehicle.plate'), style: TextStyle(fontSize: 13, color: t.ink2)),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
              decoration: BoxDecoration(
                color: t.surface2,
                borderRadius: BorderRadius.circular(7),
                border: Border.all(color: t.border),
              ),
              child: Text(trip.vehiclePlate,
                  textDirection: TextDirection.ltr,
                  style: WanesTheme.mono(size: 13, weight: FontWeight.w700, color: t.ink, spacing: 1.0)),
            ),
          ]),
        ],
      ]),
    );
  }

  Widget _pickupCard(WanesTokens t) {
    final lat = widget.fromLat, lng = widget.fromLng;
    final walkKm = (lat == null || lng == null || trip.originLat == 0)
        ? null
        : Geo.distanceKm(lat, lng, trip.originLat, trip.originLng);
    final depart = DateFormat('HH:mm', context.l10n.localeName).format(trip.departAt.toLocal());
    return WanesCard(
      radius: 16,
      padding: const EdgeInsets.all(14),
      child: Row(children: [
        // The design's little map chip — a 10px grid with a teal pin.
        ClipRRect(
          borderRadius: BorderRadius.circular(12),
          child: SizedBox(
            width: 56,
            height: 56,
            child: MapBackdrop(
              gridSize: 10,
              children: [Align(alignment: Alignment.center, child: MapPin(size: 14, color: t.teal))],
            ),
          ),
        ),
        const SizedBox(width: 13),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            MonoLabel(context.tr('booking.pickupPoint'), spacing: 1.0),
            const SizedBox(height: 3),
            Text(trip.originAddress.isEmpty ? widget.from : trip.originAddress,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
            const SizedBox(height: 2),
            Text(
                walkKm == null
                    ? context.tr('booking.departureAt', {'time': depart})
                    : '${walkKm < 0.05 ? context.tr('booking.atYourStart') : context.tr('booking.walk', {
                            'distance': Geo.formatKm(walkKm)
                          })} · ${context.tr('booking.departureAt', {'time': depart})}',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontSize: 12, color: t.ink2)),
          ]),
        ),
      ]),
    );
  }

  Widget _fareCard(WanesTokens t) {
    return WanesCard(
      radius: 16,
      child: Column(children: [
        _line(t, context.trPlural('booking.fareForSeats', widget.seats), Fare.format(_fare)),
        _line(t, context.tr('booking.serviceFee'), Fare.format(_serviceFee)),
        const SizedBox(height: 6),
        Divider(height: 1, thickness: 1, color: t.border),
        const SizedBox(height: 10),
        Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
          Text(context.tr('common.total'),
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15, color: t.ink)),
          Text(Fare.format(_total),
              style: WanesTheme.mono(size: 17, weight: FontWeight.w800, color: t.tealInk, spacing: 0)),
        ]),
        const SizedBox(height: 10),
        Row(children: [
          Icon(Icons.info_outline_rounded, size: 14, color: t.ink2),
          const SizedBox(width: 6),
          Expanded(
            child: Text(context.tr('booking.payDriverDirectly'),
                style: TextStyle(fontSize: 11.5, color: t.ink2, height: 1.35)),
          ),
        ]),
      ]),
    );
  }

  Widget _line(WanesTokens t, String label, String value) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 6),
        child: Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
          Text(label, style: TextStyle(fontSize: 14, color: t.ink2)),
          Text(value, style: WanesTheme.mono(size: 14, weight: FontWeight.w600, color: t.ink, spacing: 0)),
        ]),
      );
}
