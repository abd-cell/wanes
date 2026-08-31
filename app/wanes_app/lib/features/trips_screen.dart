import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;
import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';
import 'notifications_screen.dart';
import 'trip_details_screen.dart';

/// The rider "Trips" tab — the journeys behind the rider's bookings, as
/// opposed to the bookings themselves (that's the Bookings tab). One row per
/// trip, opening the full [TripDetailsScreen] with the driver, the vehicle and
/// the live seat count.
///
/// The rider has no "my trips" endpoint of their own — a trip belongs to its
/// driver — so the list is derived from `Bookings/mine`, one entry per distinct
/// trip.
class TripsScreen extends StatefulWidget {
  const TripsScreen({super.key});

  @override
  State<TripsScreen> createState() => TripsScreenState();
}

class TripsScreenState extends State<TripsScreen> {
  final _bookings = BookingService();

  int _tab = 0; // 0 upcoming · 1 past
  List<Booking> _list = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    load();
  }

  /// Public so the shell can refresh the tab when the rider returns to it.
  Future<void> load() async {
    if (mounted) setState(() => _loading = _list.isEmpty);
    final res = await _bookings.myBookings();
    if (!mounted) return;
    setState(() {
      _loading = false;
      if (res.success) {
        _list = _oneRowPerTrip(res.data ?? []);
        _error = null;
      } else {
        _error = res.errorMessage ?? context.tr('bookings.loadFailed');
      }
    });
  }

  /// Cancelling and re-booking leaves two bookings on the same trip; the trip
  /// is still one journey. The feed is newest-first, so the first row wins.
  static List<Booking> _oneRowPerTrip(List<Booking> bookings) {
    final seen = <int>{};
    return bookings.where((b) => b.tripId == 0 || seen.add(b.tripId)).toList();
  }

  List<Booking> get _upcoming => _list.where((b) => b.isUpcoming).toList();
  List<Booking> get _past => _list.where((b) => !b.isUpcoming).toList();

  void _open(Booking booking) => Navigator.push(
        context,
        MaterialPageRoute(
          builder: (_) => TripDetailsScreen(
            tripId: booking.tripId,
            seats: booking.seats,
            // The rider is already on this trip — nothing to book from here.
            canBook: false,
          ),
        ),
      );

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final rows = _tab == 0 ? _upcoming : _past;

    return SafeArea(
      bottom: false,
      child: RefreshIndicator(
        onRefresh: load,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
          children: [
            Row(children: [
              Expanded(
                child: Text(context.tr('trips.title'),
                    style: TextStyle(
                        fontSize: 23, fontWeight: FontWeight.w800, letterSpacing: -0.4, color: t.ink)),
              ),
              NotificationBellButton(
                onTap: () => Navigator.push(context,
                    MaterialPageRoute(builder: (_) => const NotificationsScreen())),
              ),
            ]),
            const SizedBox(height: 16),
            SegmentedToggle(
              labels: [context.tr('trips.upcoming'), context.tr('trips.past')],
              index: _tab,
              onSelect: (i) => setState(() => _tab = i),
            ),
            const SizedBox(height: 18),
            if (_loading)
              const Padding(
                padding: EdgeInsets.only(top: 60),
                child: Center(child: CircularProgressIndicator()),
              )
            else if (_error != null)
              WanesInlineAlert(_error!, onTap: load)
            else if (rows.isEmpty)
              _empty(t)
            else
              ...rows.map((b) => Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: _tripCard(t, b),
                  )),
          ],
        ),
      ),
    );
  }

  Widget _tripCard(WanesTokens t, Booking b) {
    final depart = b.departAt?.toLocal();
    return WanesCard(
      radius: 16,
      padding: const EdgeInsets.all(14),
      onTap: b.tripId == 0 ? null : () => _open(b),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          const RouteDot(),
          const SizedBox(width: 8),
          Expanded(
            child: Text(b.originAddress.isEmpty ? '—' : b.originAddress,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
          ),
          Icon(Icons.chevron_right_rounded, size: 18, color: t.ink2),
        ]),
        Padding(
          padding: const EdgeInsetsDirectional.only(start: 5, top: 4, bottom: 4),
          child: Container(width: 2, height: 14, color: t.border),
        ),
        Row(children: [
          const RouteDot(destination: true),
          const SizedBox(width: 8),
          Expanded(
            child: Text(b.destinationAddress.isEmpty ? '—' : b.destinationAddress,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
          ),
        ]),
        const SizedBox(height: 12),
        Row(children: [
          MetaChip(depart == null
              ? '—'
              : DateFormat('MMM d · HH:mm', context.l10n.localeName).format(depart)),
          const SizedBox(width: 7),
          Flexible(child: MetaChip(context.trPlural('vehicle.seatCount', b.seats))),
          const Spacer(),
          Text(context.tr('trips.viewDetails'),
              style: TextStyle(fontWeight: FontWeight.w700, fontSize: 12.5, color: t.tealInk)),
        ]),
      ]),
    );
  }

  Widget _empty(WanesTokens t) {
    return Padding(
      padding: const EdgeInsets.only(top: 48),
      child: Column(children: [
        Container(
          width: 64,
          height: 64,
          decoration: BoxDecoration(color: t.tealTint, borderRadius: BorderRadius.circular(20)),
          child: Icon(_tab == 0 ? Icons.route_rounded : Icons.history_rounded,
              color: t.tealInk, size: 28),
        ),
        const SizedBox(height: 16),
        Text(context.tr(_tab == 0 ? 'trips.noUpcoming' : 'trips.noPast'),
            style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: t.ink)),
        const SizedBox(height: 6),
        Text(context.tr(_tab == 0 ? 'trips.noUpcomingBody' : 'trips.noPastBody'),
            textAlign: TextAlign.center, style: TextStyle(color: t.ink2, fontSize: 13.5)),
      ]),
    );
  }
}
