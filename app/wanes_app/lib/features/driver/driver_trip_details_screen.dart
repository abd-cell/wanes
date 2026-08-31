import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;
import 'package:url_launcher/url_launcher.dart';
import '../../core/fare.dart';
import '../../core/l10n.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/wanes_alerts.dart';
import '../../widgets/wanes_motion.dart';
import '../../widgets/wanes_ui.dart';
import 'post_trip_screen.dart';
import 'trip_lifecycle.dart';

/// One of the driver's own trips in full: the route and schedule, how the seats
/// have filled, what it is worth, the next lifecycle move, and every rider
/// holding a seat.
///
/// The riders come from `GET Trips/{id}/bookings`, which the server scopes to
/// the caller's own trips. Pops `true` when anything here changed the trip, so
/// the list behind it reloads instead of showing a stale seat count.
class DriverTripDetailsScreen extends StatefulWidget {
  const DriverTripDetailsScreen({super.key, required this.trip});

  final Trip trip;

  @override
  State<DriverTripDetailsScreen> createState() => _DriverTripDetailsScreenState();
}

class _DriverTripDetailsScreenState extends State<DriverTripDetailsScreen> {
  final _trips = TripService();

  late Trip _trip = widget.trip;
  List<TripBooking> _riders = [];
  bool _loading = true;
  bool _changed = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  /// Re-reads the trip alongside its riders: a seat booked since the list was
  /// loaded changes both the manifest and the trip's own seat count/status.
  Future<void> _load() async {
    setState(() => _loading = true);
    final trip = await _trips.get(_trip.id);
    final riders = await _trips.tripBookings(_trip.id);
    if (!mounted) return;
    setState(() {
      _loading = false;
      if (trip.success && trip.data != null) _trip = trip.data!;
      if (riders.success) _riders = riders.data ?? [];
    });
    if (!riders.success && mounted) {
      WanesAlerts.failure(context, riders, title: context.tr('driver.loadRidersFailed'));
    }
  }

  Future<void> _onTripChanged() async {
    _changed = true;
    await _load();
  }

  Future<void> _edit() async {
    final saved = await Navigator.push<bool>(
      context,
      MaterialPageRoute(builder: (_) => PostTripScreen(trip: _trip)),
    );
    if (saved == true) _onTripChanged();
  }

  int get _seatsBooked =>
      (_trip.seatsTotal - _trip.seatsLeft).clamp(0, _trip.seatsTotal == 0 ? 0 : _trip.seatsTotal);

  bool get _full => _trip.seatsTotal > 0 && _trip.seatsLeft <= 0;

  /// What the seats sold are worth. Counts live seats only — a cancelled seat
  /// was given back and is not money the driver is owed.
  double get _expected {
    final price = _trip.pricePerSeat ?? 0;
    if (price <= 0) return 0;
    final seats = _riders.where((r) => r.isLive || r.isCompleted).fold(0, (n, r) => n + r.seats);
    return price * seats;
  }

  Future<void> _callRider(TripBooking rider) async {
    final phone = rider.riderPhone;
    if (phone == null || phone.isEmpty) {
      WanesAlerts.info(context, context.tr('common.notAvailableYet'),
          message: context.tr('driver.noRiderPhone'));
      return;
    }
    var launched = false;
    try {
      launched = await launchUrl(Uri(scheme: 'tel', path: phone));
    } catch (_) {
      launched = false;
    }
    if (!mounted || launched) return;
    WanesAlerts.info(context, context.tr('trip.callFailed'), message: phone);
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) Navigator.pop(context, _changed);
      },
      child: Scaffold(
        backgroundColor: t.bg,
        body: SafeArea(
          bottom: false,
          child: Column(children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 8, 20, 0),
              child: ScreenHeader(
                title: context.tr('driver.tripDetails'),
                onBack: () => Navigator.pop(context, _changed),
              ),
            ),
            Expanded(
              child: RefreshIndicator(
                onRefresh: _load,
                child: ListView(
                  padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
                  children: [
                    _headerCard(t),
                    const SizedBox(height: 12),
                    _routeCard(t),
                    const SizedBox(height: 12),
                    _seatsCard(t),
                    const SizedBox(height: 20),
                    Row(children: [
                      Text(context.tr('driver.riders'),
                          style: TextStyle(
                              fontWeight: FontWeight.w800, fontSize: 15, color: t.ink)),
                      const SizedBox(width: 8),
                      if (_riders.isNotEmpty)
                        StatusPill(label: '${_riders.length}', color: t.teal, dot: false),
                    ]),
                    const SizedBox(height: 10),
                    if (_loading && _riders.isEmpty)
                      const Padding(
                        padding: EdgeInsets.symmetric(vertical: 28),
                        child: Center(child: CircularProgressIndicator()),
                      )
                    else if (_riders.isEmpty)
                      _noRiders(t)
                    else
                      ..._riders.map((r) => Padding(
                            padding: const EdgeInsets.only(bottom: 10),
                            child: _riderCard(t, r),
                          )),
                  ],
                ),
              ),
            ),
            if (DriverTripStep.forTrip(_trip, _trips) != null)
              BottomActionBar(
                child: TripStepButton(trip: _trip, onChanged: _onTripChanged, expand: true),
              ),
          ]),
        ),
      ),
    );
  }

  /// Status, departure and what the trip is worth.
  Widget _headerCard(WanesTokens t) {
    final price = _trip.pricePerSeat ?? 0;
    return WanesCard(
      radius: 16,
      child: Column(children: [
        Row(children: [
          StatusPill(
              label: context.tr(_trip.statusKey),
              color: t.tripStatus(_trip.status),
              dot: _trip.status == 3),
          const Spacer(),
          // Editable only until the first rider books a seat.
          if (_trip.editable)
            InkWell(
              borderRadius: BorderRadius.circular(10),
              onTap: _edit,
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                child: Row(mainAxisSize: MainAxisSize.min, children: [
                  Icon(Icons.edit_outlined, size: 15, color: t.tealInk),
                  const SizedBox(width: 5),
                  Text(context.tr('driver.editTrip'),
                      style:
                          TextStyle(fontWeight: FontWeight.w700, fontSize: 12.5, color: t.tealInk)),
                ]),
              ),
            ),
        ]),
        const SizedBox(height: 14),
        Divider(height: 1, thickness: 1, color: t.border),
        const SizedBox(height: 14),
        Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
          _fact(t, context.tr('common.departs'),
              DateFormat('HH:mm', context.l10n.localeName).format(_trip.departAt.toLocal()),
              CrossAxisAlignment.start),
          _fact(t, context.tr('driver.perSeat'), price > 0 ? Fare.format(price) : '—',
              CrossAxisAlignment.center,
              mono: true),
          _fact(t, context.tr('driver.expectedEarnings'),
              _expected > 0 ? Fare.format(_expected) : '—', CrossAxisAlignment.end,
              valueColor: t.tealInk, mono: true),
        ]),
      ]),
    );
  }

  Widget _fact(WanesTokens t, String label, String value, CrossAxisAlignment align,
      {Color? valueColor, bool mono = false}) {
    return Column(crossAxisAlignment: align, mainAxisSize: MainAxisSize.min, children: [
      MonoLabel(label, spacing: 0.8),
      const SizedBox(height: 3),
      Text(value,
          style: mono
              ? WanesTheme.mono(
                  size: 15, weight: FontWeight.w800, color: valueColor ?? t.ink, spacing: 0)
              : TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: valueColor ?? t.ink)),
    ]);
  }

  Widget _routeCard(WanesTokens t) {
    return WanesCard(
      radius: 16,
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        _leg(t, context.tr('common.from'), _trip.originAddress, false),
        Padding(
          padding: const EdgeInsetsDirectional.only(start: 5, top: 4, bottom: 4),
          child: Container(width: 2, height: 16, color: t.border),
        ),
        _leg(t, context.tr('common.to'), _trip.destinationAddress, true),
        const SizedBox(height: 14),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          decoration: BoxDecoration(color: t.surface2, borderRadius: BorderRadius.circular(12)),
          child: Row(children: [
            Icon(Icons.schedule, size: 15, color: t.ink2),
            const SizedBox(width: 6),
            Expanded(
              child: Text(
                  DateFormat('EEE, MMM d · HH:mm', context.l10n.localeName)
                      .format(_trip.departAt.toLocal()),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(color: t.ink, fontSize: 12.5, fontWeight: FontWeight.w600)),
            ),
            MonoLabel(context.tr('tripDetails.departure'), spacing: 0.8),
          ]),
        ),
      ]),
    );
  }

  Widget _leg(WanesTokens t, String label, String address, bool destination) {
    return Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Padding(padding: const EdgeInsets.only(top: 3), child: RouteDot(destination: destination)),
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

  /// How the trip has filled. The bar and the caption are the seat count the
  /// server holds, which is also what flips the trip to Full.
  Widget _seatsCard(WanesTokens t) {
    final total = _trip.seatsTotal;
    return WanesCard(
      radius: 16,
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Row(children: [
          Icon(Icons.event_seat_outlined, size: 16, color: _full ? t.amberInk : t.ink2),
          const SizedBox(width: 7),
          Expanded(
            child: Text(context.tr('driver.seatsReserved'),
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 13.5, color: t.ink)),
          ),
          Text('$_seatsBooked/$total',
              textDirection: TextDirection.ltr,
              style: WanesTheme.mono(
                  size: 15, weight: FontWeight.w800, color: t.ink, spacing: 0)),
        ]),
        const SizedBox(height: 10),
        GrowBar(
          value: total == 0 ? 0 : _seatsBooked / total,
          color: _full ? t.amber : t.teal,
          track: t.border,
          height: 6,
        ),
        const SizedBox(height: 9),
        Text(
          _full
              ? context.tr('driver.seatsFull')
              : context.tr('driver.seatsAvailable', {'count': _trip.seatsLeft}),
          style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: _full ? t.amberInk : t.ink2),
        ),
      ]),
    );
  }

  Widget _riderCard(WanesTokens t, TripBooking rider) {
    return WanesCard(
      radius: 14,
      child: Column(children: [
        Row(children: [
          AvatarBadge(AvatarBadge.initialsOf(rider.riderName),
              size: 44, tint: t.tealTint, fg: t.tealInk),
          const SizedBox(width: 12),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(rider.riderName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14.5, color: t.ink)),
              const SizedBox(height: 3),
              Row(children: [
                Text(context.trPlural('driver.riderSeats', rider.seats),
                    style: WanesTheme.mono(
                        size: 11, weight: FontWeight.w600, color: t.ink2, spacing: 0.3)),
                Text(' · ', style: TextStyle(color: t.ink2, fontSize: 11)),
                Text(rider.reference,
                    textDirection: TextDirection.ltr,
                    style: WanesTheme.mono(
                        size: 11, weight: FontWeight.w600, color: t.ink2, spacing: 0.8)),
              ]),
            ]),
          ),
          const SizedBox(width: 8),
          StatusPill(label: context.tr(rider.statusKey), color: t.bookingStatus(rider.status), dot: false),
        ]),
        // Reaching the rider is only offered while their seat is live — the
        // server withholds the number otherwise, so there is nothing to dial.
        if (rider.isLive) ...[
          const SizedBox(height: 12),
          Divider(height: 1, thickness: 1, color: t.border),
          const SizedBox(height: 10),
          Row(children: [
            if (rider.riderRating > 0) ...[
              InlineRating(rider.riderRating.toStringAsFixed(1), size: 11),
              const SizedBox(width: 10),
            ],
            if (rider.bookedAt != null)
              Text(
                  '${context.tr('driver.bookedOn')} '
                  '${DateFormat('MMM d', context.l10n.localeName).format(rider.bookedAt!.toLocal())}',
                  style: TextStyle(fontSize: 11.5, color: t.ink2)),
            const Spacer(),
            Tooltip(
              message: context.tr('driver.callRider'),
              child: RoundAction(
                  icon: Icons.call_outlined, size: 36, onTap: () => _callRider(rider)),
            ),
          ]),
        ],
      ]),
    );
  }

  Widget _noRiders(WanesTokens t) => Container(
        padding: const EdgeInsets.symmetric(vertical: 28, horizontal: 20),
        decoration: BoxDecoration(
          color: t.surface2,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: t.border),
        ),
        child: Column(children: [
          Icon(Icons.event_seat_outlined, size: 26, color: t.ink2),
          const SizedBox(height: 10),
          Text(context.tr('driver.noRidersYet'),
              style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
          const SizedBox(height: 4),
          Text(context.tr('driver.noRidersYetBody'),
              textAlign: TextAlign.center, style: TextStyle(fontSize: 12.5, color: t.ink2)),
        ]),
      );
}
