import 'dart:async';

import 'package:flutter/material.dart';
import '../../core/fare.dart';
import '../../core/geo.dart';
import '../../core/l10n.dart';
import '../../core/places.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/map_backdrop.dart';
import '../../widgets/wanes_alerts.dart';
import '../../widgets/wanes_ui.dart';

/// Incoming ride request — prototype screen 10. The map fills the screen and
/// the top hail sits in a bottom sheet with its countdown ring; declining
/// advances to the next one.
class RequestsScreen extends StatefulWidget {
  const RequestsScreen({super.key});

  @override
  State<RequestsScreen> createState() => _RequestsScreenState();
}

class _RequestsScreenState extends State<RequestsScreen> {
  final _service = RideRequestService();
  final _presence = PresenceService();
  final Place _here = kPlaces.first;

  List<RideRequestRow> _list = [];
  final Set<int> _declined = {};
  bool _loading = true;
  bool _accepting = false;
  Timer? _tick;

  @override
  void initState() {
    super.initState();
    _load();
    _tick = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted) setState(() {}); // countdown ring + label
    });
  }

  @override
  void dispose() {
    _tick?.cancel();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    await _presence.updateLocation(_here.lat, _here.lng, online: true);
    final res = await _service.nearby(_here.lat, _here.lng);
    if (!mounted) return;
    setState(() {
      _loading = false;
      _list = res.data ?? [];
    });
  }

  /// Live queue: not declined, not expired.
  List<RideRequestRow> get _queue => _list
      .where((r) => !_declined.contains(r.id) && r.expiresAt.isAfter(DateTime.now()))
      .toList();

  Future<void> _accept(RideRequestRow r) async {
    setState(() => _accepting = true);
    final res = await _service.accept(r.id);
    if (!mounted) return;
    setState(() {
      _accepting = false;
      if (res.success) _list.removeWhere((x) => x.id == r.id);
    });
    if (res.success) {
      WanesAlerts.success(context, context.tr('driver.requestAccepted'),
          message: context.tr('driver.requestAcceptedBody'));
    } else {
      WanesAlerts.failure(context, res,
          title: context.tr('driver.acceptFailed'), onRetry: () => _accept(r));
    }
    if (res.success && _queue.isEmpty && mounted) Navigator.pop(context);
  }

  void _decline(RideRequestRow r) => setState(() => _declined.add(r.id));

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final queue = _queue;
    final top = queue.isEmpty ? null : queue.first;

    // Map fills the space above the sheet (design 10: `flex:1` map, then the
    // sheet) — not the whole screen, or the artwork scales to the hidden
    // height and the pickup marker lands behind the sheet.
    return Scaffold(
      backgroundColor: t.bg,
      body: Column(children: [
        Expanded(
          child: Stack(children: [
            Positioned.fill(
              child: MapBackdrop(
                route: top == null ? null : MapRoutes.hail,
                routeColor: t.amber,
                startMarker: top == null ? null : const MapDot(),
                endMarker: top == null ? null : const MapPin(),
              ),
            ),
            PositionedDirectional(
              start: 20,
              top: MediaQuery.of(context).padding.top + 12,
              child: CircleIconButton(
                  icon: Icons.chevron_left_rounded, onTap: () => Navigator.maybePop(context)),
            ),
            if (queue.length > 1)
              PositionedDirectional(
                end: 20,
                top: MediaQuery.of(context).padding.top + 12,
                child: TintChip(context.tr('driver.waiting', {'count': queue.length}),
                    tint: t.surface, color: t.ink2, radius: 999),
              ),
          ]),
        ),
        _loading
            ? _sheet(t,
                child: const Padding(
                    padding: EdgeInsets.symmetric(vertical: 28),
                    child: Center(child: CircularProgressIndicator())))
            : top == null
                ? _sheet(t, child: _emptyBody(t))
                : _sheet(t, child: _requestBody(t, top)),
      ]),
    );
  }

  /// The rounded sheet the design floats over the map.
  Widget _sheet(WanesTokens t, {required Widget child}) {
    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: const BorderRadius.vertical(top: Radius.circular(24)),
        boxShadow: [
          BoxShadow(color: t.shadow, blurRadius: 40, offset: const Offset(0, -14), spreadRadius: -14),
        ],
      ),
      child: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(22, 12, 22, 18),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Container(
              width: 38,
              height: 4,
              decoration: BoxDecoration(color: t.border, borderRadius: BorderRadius.circular(2)),
            ),
            const SizedBox(height: 12),
            child,
          ]),
        ),
      ),
    );
  }

  Widget _requestBody(WanesTokens t, RideRequestRow r) {
    final left = r.expiresAt.difference(DateTime.now());
    final progress = left.inMilliseconds / RideRequestRow.ttl.inMilliseconds;
    final pickupKm = Geo.distanceKm(_here.lat, _here.lng, r.originLat, r.originLng);
    final fare = Fare.estimateBetween(
      r.originLat, r.originLng, r.destinationLat, r.destinationLng, seats: r.seats);

    return Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      Row(children: [
        CountdownRing(progress: progress, label: _countdownLabel(context, left)),
        const SizedBox(width: 14),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, mainAxisSize: MainAxisSize.min, children: [
            LiveCaption(context.tr('driver.newRideRequest')),
            const SizedBox(height: 5),
            Text(context.tr('driver.rideNearby'),
                style: TextStyle(
                    fontSize: 20, fontWeight: FontWeight.w800, letterSpacing: -0.4, color: t.ink)),
          ]),
        ),
      ]),
      const SizedBox(height: 16),
      // Rider + estimated fare
      Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        decoration: BoxDecoration(
          color: t.surface2,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: t.border),
        ),
        child: Row(children: [
          AvatarBadge('R', size: 40, tint: t.amberTint, fg: t.amberInk),
          const SizedBox(width: 11),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(context.tr('driver.riderNumber', {'id': r.riderId}),
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
              const SizedBox(height: 1),
              Text(
                  '${context.trPlural('vehicle.seatCount', r.seats)} · '
                  '${context.tr('driver.requestedAgo', {'ago': _ago(context, r.requestedAt)})}',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: WanesTheme.mono(size: 11, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
            ]),
          ),
          const SizedBox(width: 8),
          Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
            Text(Fare.format(fare),
                style: WanesTheme.mono(size: 18, weight: FontWeight.w800, color: t.tealInk, spacing: 0)),
            Text(context.tr('driver.estFare'),
                style: WanesTheme.mono(size: 10, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
          ]),
        ]),
      ),
      const SizedBox(height: 12),
      IntrinsicHeight(
        child: Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Expanded(
            child: _infoTile(
                t,
                context.tr('driver.toPickup'),
                '${Geo.formatKm(pickupKm)} · '
                '${context.tr('units.minutes', {'value': Geo.etaMinutes(pickupKm)})}',
                size: 14),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: _infoTile(
                t,
                context.tr('driver.route'),
                context.tr('common.routeSummary', {
                  'from': _short(r.originAddress),
                  'to': _short(r.destinationAddress),
                }),
                size: 13),
          ),
        ]),
      ),
      const SizedBox(height: 16),
      Row(children: [
        Expanded(
          flex: 1,
          child: SizedBox(
            height: 52,
            child: OutlinedButton(
              onPressed: _accepting ? null : () => _decline(r),
              style: OutlinedButton.styleFrom(
                foregroundColor: t.ink,
                side: BorderSide(color: t.border),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
              ),
              child: Text(context.tr('common.decline'),
                  style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
            ),
          ),
        ),
        const SizedBox(width: 11),
        Expanded(
          flex: 2,
          child: PrimaryButton(
            label: context.tr('driver.acceptRide'),
            arrow: false,
            busy: _accepting,
            onPressed: _accepting ? null : () => _accept(r),
          ),
        ),
      ]),
    ]);
  }

  Widget _infoTile(WanesTokens t, String label, String value, {required double size}) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 13, vertical: 11),
      decoration: BoxDecoration(
        color: t.surface2,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: t.border),
      ),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, mainAxisSize: MainAxisSize.min, children: [
        Text(label.toUpperCase(),
            style: WanesTheme.mono(size: 10, weight: FontWeight.w500, color: t.ink2, spacing: 0.4)),
        const SizedBox(height: 3),
        Text(value,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: WanesTheme.mono(size: size, weight: FontWeight.w700, color: t.ink, spacing: 0)),
      ]),
    );
  }

  Widget _emptyBody(WanesTokens t) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 12),
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          Container(
            width: 56,
            height: 56,
            alignment: Alignment.center,
            decoration: BoxDecoration(color: t.amberTint, borderRadius: BorderRadius.circular(18)),
            child: Icon(Icons.notifications_none_rounded, size: 26, color: t.amberInk),
          ),
          const SizedBox(height: 14),
          Text(context.tr('driver.noNearbyRequestsTitle'),
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 17, color: t.ink)),
          const SizedBox(height: 5),
          Text(context.tr('driver.noNearbyRequestsBody'),
              textAlign: TextAlign.center, style: TextStyle(color: t.ink2, fontSize: 13)),
          const SizedBox(height: 16),
          SizedBox(
            width: double.infinity,
            child: OutlinedButton(
              onPressed: _load,
              style: OutlinedButton.styleFrom(
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
              ),
              child: Text(context.tr('common.refresh')),
            ),
          ),
        ]),
      );

  static String _countdownLabel(BuildContext context, Duration d) {
    if (d.isNegative) return context.tr('units.secondsShort', {'value': 0});
    if (d.inSeconds < 60) {
      return context.tr('units.secondsShort', {'value': d.inSeconds});
    }
    return '${d.inMinutes}:${(d.inSeconds % 60).toString().padLeft(2, '0')}';
  }

  static String _short(String address) => address.split(',').first.trim();

  static String _ago(BuildContext context, DateTime at) {
    final s = DateTime.now().difference(at.toLocal()).inSeconds;
    if (s < 60) return context.tr('time.justNow');
    final m = s ~/ 60;
    return m < 60
        ? context.tr('time.minutesAgo', {'value': m})
        : context.tr('time.hoursAgo', {'value': m ~/ 60});
  }
}
