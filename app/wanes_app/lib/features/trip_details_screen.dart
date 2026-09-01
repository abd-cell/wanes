import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;
import '../core/fare.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/map_backdrop.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';
import 'confirm_booking_screen.dart';
import '../widgets/wanes_motion.dart';

/// Trip details — the journey behind a search result or a booking, in full.
///
/// Opens either from a [Trip] already in hand (a search match, so the card can
/// paint immediately) or from a bare id (a booking, a notification). Either way
/// it re-reads `GET Trips/{id}` so the seat count and status on screen are the
/// server's, not a stale copy from a list.
class TripDetailsScreen extends StatefulWidget {
  const TripDetailsScreen({
    super.key,
    this.trip,
    this.tripId,
    this.seats = 1,
    this.canBook = true,
  }) : assert(trip != null || tripId != null, 'pass a trip or its id');

  /// What we already know, painted while the refresh is in flight.
  final Trip? trip;

  /// Used when there is no [trip] to hand.
  final int? tripId;

  /// How many seats the rider was searching for — carried into booking.
  final int seats;

  /// False when booking makes no sense from here (the rider already holds a
  /// seat on this trip, so they arrived from their own booking).
  final bool canBook;

  @override
  State<TripDetailsScreen> createState() => _TripDetailsScreenState();
}

class _TripDetailsScreenState extends State<TripDetailsScreen> {
  final _trips = TripService();

  Trip? _trip;
  bool _loading = false;
  String? _error;

  int get _id => widget.trip?.id ?? widget.tripId!;

  @override
  void initState() {
    super.initState();
    _trip = widget.trip;
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      if (_trip == null) _error = null;
    });
    final res = await _trips.get(_id);
    if (!mounted) return;
    setState(() {
      _loading = false;
      if (res.success && res.data != null) {
        _trip = res.data;
        _error = null;
      } else if (_trip == null) {
        // Nothing to fall back on — the screen has to say so.
        _error = res.errorMessage ?? context.tr('tripDetails.loadFailed');
      }
    });
    // We do have a copy to show, so a failed refresh is a toast, not a wall.
    if (!res.success && _trip != null && mounted) {
      WanesAlerts.failure(context, res,
          title: context.tr('tripDetails.loadFailed'), onRetry: _load);
    }
  }

  /// Deliberately not [WanesTokens.tripStatus]: this pill faces a rider looking
  /// at a trip they might book, so Posted reads as "available" (green) rather
  /// than the driver's "still waiting on riders" (amber).
  Color _statusColor(WanesTokens t, int status) => switch (status) {
        3 || 6 => t.teal, // under way, or the driver is at the pickup
        4 => t.info,
        5 => t.alert,
        2 => t.amber,
        _ => t.success,
      };

  /// Posted, in the future and with room for the seats being asked for.
  bool get _bookable {
    final trip = _trip;
    if (trip == null || !widget.canBook) return false;
    return trip.status == 1 &&
        trip.seatsLeft >= widget.seats &&
        trip.departAt.isAfter(DateTime.now());
  }

  void _book() {
    final trip = _trip!;
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => ConfirmBookingScreen(
          trip: trip,
          seats: widget.seats,
          from: trip.originAddress,
          to: trip.destinationAddress,
          fromLat: trip.originLat == 0 ? null : trip.originLat,
          fromLng: trip.originLng == 0 ? null : trip.originLng,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final trip = _trip;

    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        bottom: false,
        child: Column(children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 8, 20, 0),
            child: ScreenHeader(title: context.tr('tripDetails.title')),
          ),
          Expanded(
            child: trip == null
                ? (_error == null
                    ? const Center(child: WanesSpinner())
                    : _errorState(t))
                : RefreshIndicator(
                    onRefresh: _load,
                    child: ListView(
                      padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
                      children: [
                        _routeCard(t, trip),
                        const SizedBox(height: 12),
                        _driverCard(t, trip),
                        const SizedBox(height: 12),
                        _factsCard(t, trip),
                        if (!_bookable && widget.canBook) ...[
                          const SizedBox(height: 12),
                          _notBookableNote(t, trip),
                        ],
                      ],
                    ),
                  ),
          ),
          if (_bookable)
            BottomActionBar(
              child: PrimaryButton(
                label: context.tr('results.bookSeat'),
                arrow: false,
                busy: _loading,
                onPressed: _loading ? null : _book,
              ),
            ),
        ]),
      ),
    );
  }

  Widget _errorState(WanesTokens t) => ListView(
        padding: const EdgeInsets.fromLTRB(20, 60, 20, 24),
        children: [
          Column(children: [
            Container(
              width: 64,
              height: 64,
              decoration: BoxDecoration(color: t.alertTint, borderRadius: BorderRadius.circular(20)),
              child: Icon(Icons.error_outline_rounded, size: 28, color: t.alert),
            ),
            const SizedBox(height: 16),
            Text(context.tr('tripDetails.loadFailed'),
                style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: t.ink)),
            const SizedBox(height: 6),
            Text(_error!, textAlign: TextAlign.center, style: TextStyle(color: t.ink2, fontSize: 13.5)),
            const SizedBox(height: 20),
            SizedBox(
              width: 180,
              child: OutlinedButton(
                onPressed: _loading ? null : _load,
                style: OutlinedButton.styleFrom(
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                ),
                child: Text(context.tr('common.retry')),
              ),
            ),
          ]),
        ],
      );

  /// The map strip with the route addresses under it — origin and destination
  /// on the design's two route dots, joined by the hairline connector.
  Widget _routeCard(WanesTokens t, Trip trip) {
    final depart = trip.departAt.toLocal();
    return WanesCard(
      radius: 18,
      padding: EdgeInsets.zero,
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        ClipRRect(
          borderRadius: const BorderRadius.vertical(top: Radius.circular(18)),
          child: SizedBox(
            height: 132,
            child: MapBackdrop(
              route: MapRoutes.results,
              routeColor: t.teal,
              children: [
                const Align(alignment: Alignment(-0.74, 0.52), child: MapDot()),
                Align(alignment: const Alignment(0.54, -0.48), child: MapPin(size: 24, color: t.amber)),
                PositionedDirectional(
                  end: 10,
                  top: 10,
                  child: StatusPill(
                    label: context.tr(trip.statusKey),
                    color: _statusColor(t, trip.status),
                    dot: trip.status == 3,
                  ),
                ),
              ],
            ),
          ),
        ),
        Padding(
          padding: const EdgeInsets.all(16),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            _routeLeg(t, context.tr('common.from'), trip.originAddress, false),
            Padding(
              padding: const EdgeInsetsDirectional.only(start: 5, top: 4, bottom: 4),
              child: Container(width: 2, height: 16, color: t.border),
            ),
            _routeLeg(t, context.tr('common.to'), trip.destinationAddress, true),
            const SizedBox(height: 14),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
              decoration: BoxDecoration(color: t.surface2, borderRadius: BorderRadius.circular(12)),
              child: Row(children: [
                Icon(Icons.schedule, size: 15, color: t.ink2),
                const SizedBox(width: 6),
                Expanded(
                  child: Text(
                      DateFormat('EEE, MMM d · HH:mm', context.l10n.localeName).format(depart),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(color: t.ink, fontSize: 12.5, fontWeight: FontWeight.w600)),
                ),
                MonoLabel(context.tr('tripDetails.departure'), spacing: 0.8),
              ]),
            ),
          ]),
        ),
      ]),
    );
  }

  Widget _routeLeg(WanesTokens t, String label, String address, bool destination) {
    return Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Padding(
        padding: const EdgeInsets.only(top: 3),
        child: RouteDot(destination: destination),
      ),
      const SizedBox(width: 10),
      Expanded(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          MonoLabel(label, spacing: 1.0),
          const SizedBox(height: 2),
          Text(address.isEmpty ? '—' : address,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
        ]),
      ),
    ]);
  }

  Widget _driverCard(WanesTokens t, Trip trip) {
    final plate = trip.vehiclePlate;
    final car = [trip.vehicleLabel, trip.vehicleColor].where((s) => s.isNotEmpty).join(' · ');
    return WanesCard(
      radius: 16,
      child: Column(children: [
        Row(children: [
          AvatarBadge(AvatarBadge.initialsOf(trip.driverName),
              size: 52, tint: t.tealTint, fg: t.tealInk),
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
            Flexible(
              child: Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
                MonoLabel(context.tr('tripDetails.vehicle'), spacing: 0.8),
                const SizedBox(height: 3),
                Text(car,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    textAlign: TextAlign.end,
                    style: TextStyle(fontWeight: FontWeight.w700, fontSize: 13.5, color: t.ink)),
              ]),
            ),
          ],
        ]),
        if (plate.isNotEmpty) ...[
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
              child: Text(plate,
                  textDirection: TextDirection.ltr,
                  style: WanesTheme.mono(size: 13, weight: FontWeight.w700, color: t.ink, spacing: 1.0)),
            ),
          ]),
        ],
      ]),
    );
  }

  /// Seats and price — the two numbers a rider decides on.
  Widget _factsCard(WanesTokens t, Trip trip) {
    final price = trip.pricePerSeat;
    return WanesCard(
      radius: 16,
      child: Row(children: [
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            MonoLabel(context.tr('common.seats'), spacing: 0.8),
            const SizedBox(height: 4),
            Text('${trip.seatsLeft}/${trip.seatsTotal}',
                textDirection: TextDirection.ltr,
                style: WanesTheme.mono(size: 17, weight: FontWeight.w800, color: t.ink, spacing: 0)),
            const SizedBox(height: 2),
            Text(context.trPlural('results.seatsLeft', trip.seatsLeft),
                style: TextStyle(fontSize: 11.5, color: t.ink2)),
          ]),
        ),
        Container(width: 1, height: 46, color: t.border),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
            MonoLabel(context.tr('common.fare'), spacing: 0.8),
            const SizedBox(height: 4),
            Text(price == null ? '—' : Fare.format(price),
                style: WanesTheme.mono(size: 17, weight: FontWeight.w800, color: t.tealInk, spacing: 0)),
            const SizedBox(height: 2),
            Text(price == null ? context.tr('tripDetails.priceNotSet') : context.tr('results.perSeat'),
                style: TextStyle(fontSize: 11.5, color: t.ink2)),
          ]),
        ),
      ]),
    );
  }

  /// Says *why* there is no Book seat button, rather than leaving a gap.
  Widget _notBookableNote(WanesTokens t, Trip trip) {
    final key = switch (trip.status) {
      1 when trip.seatsLeft < widget.seats => 'tripDetails.noSeatsLeft',
      1 => 'tripDetails.alreadyDeparted',
      2 => 'tripDetails.tripFull',
      _ => 'tripDetails.notBookable',
    };
    return WanesInlineAlert(context.tr(key), kind: WanesAlertKind.info);
  }
}
