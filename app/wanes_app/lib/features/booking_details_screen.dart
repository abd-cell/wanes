import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;
import '../core/app_config.dart';
import '../core/fare.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/trip_safety.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';
import 'live_trip_screen.dart';
import 'rate_screen.dart';
import 'trip_details_screen.dart';
import '../widgets/wanes_motion.dart';

/// Booking details — one seat the rider holds: the reference they quote to the
/// driver, the route and time, what it costs, and the actions still open on it
/// (track, rate, cancel).
///
/// The booking row the list hands over carries only the trip's addresses and
/// departure, so the driver and the fare are read from `GET Trips/{id}` once
/// the screen opens. Pops `true` when the booking changed, so the list behind
/// it knows to reload.
class BookingDetailsScreen extends StatefulWidget {
  const BookingDetailsScreen({super.key, required this.booking});

  final Booking booking;

  @override
  State<BookingDetailsScreen> createState() => _BookingDetailsScreenState();
}

class _BookingDetailsScreenState extends State<BookingDetailsScreen> {
  final _bookings = BookingService();
  final _trips = TripService();

  late Booking _booking = widget.booking;
  Trip? _trip;
  bool _loadingTrip = true;
  bool _busy = false;

  /// Set once anything on this screen changed the booking, so the caller
  /// reloads instead of showing a stale row.
  bool _changed = false;

  @override
  void initState() {
    super.initState();
    _loadTrip();
  }

  Future<void> _loadTrip() async {
    if (_booking.tripId == 0) {
      setState(() => _loadingTrip = false);
      return;
    }
    setState(() => _loadingTrip = true);
    final res = await _trips.get(_booking.tripId);
    if (!mounted) return;
    setState(() {
      _loadingTrip = false;
      if (res.success) _trip = res.data;
    });
  }

  double get _total => (_trip?.pricePerSeat ?? 0) * _booking.seats;

  Future<void> _cancel() async {
    final t = WanesTokens.of(context);
    // Giving a seat back close to departure goes on the rider's record, and
    // they should know that before they tap, not after.
    final lead = Duration(minutes: AppConfigController.value.lateCancelLeadMinutes);
    final departAt = _booking.departAt;
    final late = departAt != null && departAt.toLocal().difference(DateTime.now()) <= lead;
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(context.tr('bookings.cancelTitle')),
        content: Text(late
            ? '${context.tr('bookings.cancelBody')}\n\n'
                '${context.tr('bookings.cancelLate', {'hours': (lead.inMinutes / 60).round()})}'
            : context.tr('bookings.cancelBody')),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: Text(context.tr('bookings.keepBooking'))),
          TextButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: Text(context.tr('bookings.cancelAction'), style: TextStyle(color: t.alert)),
          ),
        ],
      ),
    );
    if (ok != true || !mounted) return;

    setState(() => _busy = true);
    final res = await _bookings.cancel(_booking.id);
    if (!mounted) return;
    setState(() => _busy = false);
    if (!res.success) {
      WanesAlerts.failure(context, res, title: context.tr('bookings.cancelFailed'));
      return;
    }
    setState(() {
      _changed = true;
      // The server has no "get one booking" verb, so mirror the transition it
      // just made rather than re-reading the whole list to find this row.
      _booking = Booking(
        id: _booking.id,
        tripId: _booking.tripId,
        riderId: _booking.riderId,
        seats: _booking.seats,
        status: 5,
        originAddress: _booking.originAddress,
        destinationAddress: _booking.destinationAddress,
        departAt: _booking.departAt,
        tripStatus: _booking.tripStatus,
        driverPhone: _booking.driverPhone,
      );
    });
    WanesAlerts.success(context, context.tr('bookings.cancelled'));
    // The seat went back to the trip — pull the fresh seat count.
    _loadTrip();
  }

  void _openTrip() => Navigator.push(
        context,
        MaterialPageRoute(
          builder: (_) => TripDetailsScreen(
            trip: _trip,
            tripId: _booking.tripId,
            seats: _booking.seats,
            // The rider already holds a seat here; booking again is not the
            // action they came for.
            canBook: false,
          ),
        ),
      );

  /// Watching the ride is not a read-only errand: the driver arrives, starts
  /// and finishes the trip while the rider is on that screen. Re-read the seat
  /// on the way back, or this screen keeps offering "Track trip" — and keeps
  /// withholding the rating — for a booking that has already finished.
  Future<void> _track() async {
    await Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => LiveTripScreen(trip: _trip!, booking: _booking),
      ),
    );
    if (!mounted) return;
    await _reloadBooking();
    if (mounted) _loadTrip();
  }

  /// The server has no "get one booking" verb, so the rider's own list is the
  /// only way back to a single seat.
  Future<void> _reloadBooking() async {
    final res = await _bookings.myBookings();
    if (!mounted) return;
    final fresh = res.data?.where((b) => b.id == _booking.id).firstOrNull;
    if (fresh == null || fresh.status == _booking.status) return;
    setState(() {
      _booking = fresh;
      _changed = true;
    });
  }

  void _rate() => Navigator.push(
        context,
        MaterialPageRoute(
          builder: (_) => RateScreen(
            bookingId: _booking.id,
            driverName: _trip?.driverName ?? '',
            trip: _trip,
            seats: _booking.seats,
          ),
        ),
      );

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return PopScope(
      // Only stand in the way once there is something to report. Blocking every
      // pop cost the rider the iOS swipe-back gesture and Android's predictive
      // back on a screen that usually has nothing to hand over.
      canPop: !_changed,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) Navigator.pop(context, true);
      },
      child: Scaffold(
        backgroundColor: t.bg,
        body: SafeArea(
          bottom: false,
          child: Column(children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 8, 20, 0),
              child: ScreenHeader(
                title: context.tr('bookings.detailsTitle'),
                onBack: () => Navigator.pop(context, _changed),
              ),
            ),
            Expanded(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
                children: [
                  _headerCard(t),
                  const SizedBox(height: 12),
                  _routeCard(t),
                  const SizedBox(height: 12),
                  _driverCard(t),
                  if (_booking.boardingCode != null && _booking.isLive) ...[
                    const SizedBox(height: 12),
                    BoardingCodeCard(code: _booking.boardingCode!),
                  ],
                  const SizedBox(height: 12),
                  _actionsCard(t),
                ],
              ),
            ),
            if (_booking.isCancellable)
              BottomActionBar(
                child: SizedBox(
                  height: 48,
                  child: OutlinedButton(
                    onPressed: _busy ? null : _cancel,
                    style: OutlinedButton.styleFrom(
                      foregroundColor: t.alert,
                      side: BorderSide(color: t.border),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                    ),
                    child: _busy
                        ? WanesSpinner.mono(t.alert, size: 20)
                        : Text(context.tr('bookings.cancelAction'),
                            style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14)),
                  ),
                ),
              ),
          ]),
        ),
      ),
    );
  }

  /// Status, reference and the headline numbers — seats and what it costs.
  Widget _headerCard(WanesTokens t) {
    return WanesCard(
      radius: 16,
      child: Column(children: [
        Row(children: [
          StatusPill(
            label: context.tr(_booking.statusKey),
            color: t.bookingStatus(_booking.status),
            dot: _booking.isLive,
          ),
          const Spacer(),
          Text.rich(
            TextSpan(children: [
              TextSpan(
                  text: '${context.tr('booking.reference')} · ',
                  style: WanesTheme.mono(size: 11, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
              TextSpan(
                  text: _booking.reference,
                  style: WanesTheme.mono(size: 11, weight: FontWeight.w600, color: t.ink, spacing: 1.0)),
              // The reference is an ASCII code — keep it left-to-right.
              const TextSpan(text: '‎'),
            ]),
          ),
        ]),
        const SizedBox(height: 14),
        Divider(height: 1, thickness: 1, color: t.border),
        const SizedBox(height: 14),
        Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
          _fact(t, context.tr('common.seats'), '${_booking.seats}', CrossAxisAlignment.start),
          _fact(
              t,
              context.tr('common.departs'),
              _booking.departAt == null
                  ? '—'
                  : DateFormat('HH:mm', context.l10n.localeName).format(_booking.departAt!.toLocal()),
              CrossAxisAlignment.center),
          _fact(t, context.tr('common.total'), _totalLabel, CrossAxisAlignment.end,
              valueColor: t.tealInk, mono: true),
        ]),
      ]),
    );
  }

  String get _totalLabel {
    if (_loadingTrip && _trip == null) return '…';
    return _total > 0 ? Fare.format(_total) : '—';
  }

  Widget _fact(WanesTokens t, String label, String value, CrossAxisAlignment align,
      {Color? valueColor, bool mono = false}) {
    return Column(crossAxisAlignment: align, mainAxisSize: MainAxisSize.min, children: [
      MonoLabel(label, spacing: 0.8),
      const SizedBox(height: 3),
      Text(value,
          style: mono
              ? WanesTheme.mono(size: 15, weight: FontWeight.w800, color: valueColor ?? t.ink, spacing: 0)
              : TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: valueColor ?? t.ink)),
    ]);
  }

  Widget _routeCard(WanesTokens t) {
    final depart = _booking.departAt?.toLocal();
    return WanesCard(
      radius: 16,
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        _leg(t, context.tr('common.from'), _booking.originAddress, false),
        Padding(
          padding: const EdgeInsetsDirectional.only(start: 5, top: 4, bottom: 4),
          child: Container(width: 2, height: 16, color: t.border),
        ),
        _leg(t, context.tr('common.to'), _booking.destinationAddress, true),
        const SizedBox(height: 14),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          decoration: BoxDecoration(color: t.surface2, borderRadius: BorderRadius.circular(12)),
          child: Row(children: [
            Icon(Icons.schedule, size: 15, color: t.ink2),
            const SizedBox(width: 6),
            Expanded(
              child: Text(
                  depart == null
                      ? '—'
                      : DateFormat('EEE, MMM d · HH:mm', context.l10n.localeName).format(depart),
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

  /// Who is driving. Placeholder row while the trip is in flight, and a plain
  /// note when it can't be read (a deleted trip shouldn't blank the screen).
  Widget _driverCard(WanesTokens t) {
    final trip = _trip;
    if (trip == null) {
      return WanesCard(
        radius: 16,
        child: Row(children: [
          SizedBox(
            width: 20,
            height: 20,
            child: _loadingTrip
                ? const WanesSpinner(size: 20)
                : Icon(Icons.person_off_outlined, size: 20, color: t.ink2),
          ),
          const SizedBox(width: 12),
          Text(context.tr(_loadingTrip ? 'bookings.loadingTrip' : 'bookings.tripUnavailable'),
              style: TextStyle(fontSize: 13.5, color: t.ink2)),
        ]),
      );
    }
    final car = [trip.vehicleLabel, trip.vehiclePlate].where((s) => s.isNotEmpty).join(' · ');
    return WanesCard(
      radius: 16,
      child: Row(children: [
        AvatarBadge(AvatarBadge.initialsOf(trip.driverName),
            size: 46, tint: t.tealTint, fg: t.tealInk),
        const SizedBox(width: 13),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(trip.driverName,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15, color: t.ink)),
            if (car.isNotEmpty) ...[
              const SizedBox(height: 2),
              Text(car,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: WanesTheme.mono(size: 11.5, weight: FontWeight.w500, color: t.ink2, spacing: 0.4)),
            ],
          ]),
        ),
        const SizedBox(width: 8),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
          decoration: BoxDecoration(
            color: t.surface2,
            borderRadius: BorderRadius.circular(8),
            border: Border.all(color: t.border),
          ),
          child: InlineRating(trip.driverRating.toStringAsFixed(1), size: 11),
        ),
      ]),
    );
  }

  Widget _actionsCard(WanesTokens t) {
    return GroupedCard(children: [
      GroupedRow(
        icon: Icons.route_rounded,
        title: context.tr('bookings.viewTripDetails'),
        subtitle: context.tr('bookings.viewTripDetailsBody'),
        onTap: _openTrip,
      ),
      if (_booking.isUpcoming && _trip != null)
        GroupedRow(
          icon: Icons.my_location_rounded,
          title: context.tr('hail.trackTrip'),
          subtitle: context.tr('bookings.trackTripBody'),
          onTap: _track,
        ),
      // Someone they trust can follow the ride — shared rides are with strangers.
      if (_booking.isLive && !_booking.isPending)
        GroupedRow(
          icon: Icons.ios_share_rounded,
          title: context.tr('share.button'),
          subtitle: context.tr('share.rowBody'),
          onTap: () => shareTrip(context, _booking.id),
        ),
      if (_booking.isCompleted)
        GroupedRow(
          icon: Icons.star_rounded,
          title: context.tr('rate.rateYourTrip'),
          subtitle: context.tr('bookings.rateBody'),
          onTap: _rate,
        ),
    ]);
  }
}
