import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;
import '../../core/l10n.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/wanes_ui.dart';
import 'post_trip_screen.dart';

/// A driver's posted trips. Reskinned to the Wanes card language; used as the
/// Trips tab in the driver shell.
class MyTripsScreen extends StatefulWidget {
  const MyTripsScreen({super.key});

  @override
  State<MyTripsScreen> createState() => _MyTripsScreenState();
}

class _MyTripsScreenState extends State<MyTripsScreen> {
  final _trips = TripService();
  List<Trip> _list = [];
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    final res = await _trips.myTrips();
    if (!mounted) return;
    setState(() {
      _loading = false;
      _list = res.data ?? [];
    });
  }

  Future<void> _edit(Trip trip) async {
    final saved = await Navigator.push<bool>(
      context,
      MaterialPageRoute(builder: (_) => PostTripScreen(trip: trip)),
    );
    if (saved == true) _load();
  }

  Color _statusColor(WanesTokens t, int s) => switch (s) {
        3 => t.teal,
        4 => t.info,
        5 => t.alert,
        _ => t.amber,
      };

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return SafeArea(
      bottom: false,
      child: _loading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
                children: [
                  Text(context.tr('driver.myTrips'),
                      style: TextStyle(
                          fontSize: 22,
                          fontWeight: FontWeight.w800,
                          letterSpacing: -0.44,
                          color: t.ink)),
                  const SizedBox(height: 16),
                  if (_list.isEmpty) _empty(t) else ..._list.map((trip) => Padding(
                        padding: const EdgeInsets.only(bottom: 12),
                        child: _tripCard(t, trip),
                      )),
                ],
              ),
            ),
    );
  }

  Widget _tripCard(WanesTokens t, Trip trip) {
    final sc = _statusColor(t, trip.status);
    return WanesCard(
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          Expanded(
            child: Row(children: [
              const RouteDot(),
              const SizedBox(width: 8),
              Flexible(child: Text(trip.originAddress, maxLines: 1, overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink))),
            ]),
          ),
          StatusPill(label: context.tr(trip.statusKey), color: sc, dot: false),
        ]),
        Padding(
          padding: const EdgeInsetsDirectional.only(start: 5, top: 4, bottom: 4),
          child: Container(width: 2, height: 14, color: t.border),
        ),
        Row(children: [
          const RouteDot(destination: true),
          const SizedBox(width: 8),
          Flexible(child: Text(trip.destinationAddress, maxLines: 1, overflow: TextOverflow.ellipsis,
              style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink))),
        ]),
        const SizedBox(height: 12),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          decoration: BoxDecoration(color: t.surface2, borderRadius: BorderRadius.circular(12)),
          child: Row(children: [
            Icon(Icons.schedule, size: 15, color: t.ink2),
            const SizedBox(width: 6),
            Text(
                DateFormat('EEE, MMM d · HH:mm', context.l10n.localeName)
                    .format(trip.departAt.toLocal()),
                style: TextStyle(color: t.ink, fontSize: 12.5, fontWeight: FontWeight.w600)),
            const Spacer(),
            Icon(Icons.event_seat_outlined, size: 15, color: t.ink2),
            const SizedBox(width: 6),
            Text('${trip.seatsLeft}/${trip.seatsTotal}',
                textDirection: TextDirection.ltr,
                style: WanesTheme.mono(size: 12, weight: FontWeight.w700, color: t.ink2)),
          ]),
        ),
        // Editable only until the first rider books a seat.
        if (trip.editable) ...[
          const SizedBox(height: 10),
          Align(
            alignment: AlignmentDirectional.centerEnd,
            child: InkWell(
              borderRadius: BorderRadius.circular(10),
              onTap: () => _edit(trip),
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                child: Row(mainAxisSize: MainAxisSize.min, children: [
                  Icon(Icons.edit_outlined, size: 15, color: t.tealInk),
                  const SizedBox(width: 6),
                  Text(context.tr('driver.editTrip'),
                      style: TextStyle(fontWeight: FontWeight.w700, fontSize: 13, color: t.tealInk)),
                ]),
              ),
            ),
          ),
        ],
      ]),
    );
  }

  Widget _empty(WanesTokens t) => Padding(
        padding: const EdgeInsets.only(top: 60),
        child: Column(children: [
          Container(
            width: 64, height: 64,
            decoration: BoxDecoration(color: t.tealTint, borderRadius: BorderRadius.circular(20)),
            child: Icon(Icons.route_rounded, size: 28, color: t.tealInk),
          ),
          const SizedBox(height: 16),
          Text(context.tr('driver.noTripsPosted'),
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: t.ink)),
          const SizedBox(height: 6),
          Text(context.tr('driver.noTripsPostedBody'),
              textAlign: TextAlign.center, style: TextStyle(color: t.ink2)),
        ]),
      );
}
