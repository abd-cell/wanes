import 'dart:async';

import 'package:flutter/material.dart';
import '../../core/fare.dart';
import '../../core/geo.dart';
import '../../core/l10n.dart';
import '../../core/places.dart';
import '../../core/session.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/wanes_alerts.dart';
import '../../widgets/wanes_ui.dart';
import 'driver_profile_screen.dart';
import 'my_trips_screen.dart';
import 'post_trip_screen.dart';
import 'requests_screen.dart';

/// Driver app shell — Home · Trips · Profile behind the bottom nav
/// (prototype's driver tab bar). Home is the dashboard (screen 08).
class DriverHomeScreen extends StatefulWidget {
  const DriverHomeScreen({super.key});

  @override
  State<DriverHomeScreen> createState() => _DriverHomeScreenState();
}

class _DriverHomeScreenState extends State<DriverHomeScreen> {
  int _index = 0;

  @override
  Widget build(BuildContext context) {
    final tabs = [
      DriverDashboard(onGoTrips: () => setState(() => _index = 1)),
      const MyTripsScreen(),
      const DriverProfileScreen(),
    ];
    return Scaffold(
      body: IndexedStack(index: _index, children: tabs),
      bottomNavigationBar: WanesBottomNav(index: _index, onSelect: (i) => setState(() => _index = i)),
    );
  }
}

/// The driver dashboard — prototype screen 08. Online banner, today's earnings
/// hero, the ink "Post a trip" CTA and the live incoming-request stack.
class DriverDashboard extends StatefulWidget {
  const DriverDashboard({super.key, this.onGoTrips});
  final VoidCallback? onGoTrips;

  @override
  State<DriverDashboard> createState() => _DriverDashboardState();
}

class _DriverDashboardState extends State<DriverDashboard> {
  final _presence = PresenceService();
  final _trips = TripService();
  final _requests = RideRequestService();
  final Place _here = kPlaces.first;

  bool _online = true;
  bool _busyToggle = false;
  DateTime? _onlineSince;
  List<Trip> _myTrips = [];
  List<RideRequestRow> _incoming = [];

  /// Drives the per-request countdown and drops hails once their TTL is up.
  Timer? _tick;

  @override
  void initState() {
    super.initState();
    _onlineSince = DateTime.now();
    _refresh();
    _tick = Timer.periodic(const Duration(seconds: 1), (_) {
      if (!mounted) return;
      final before = _incoming.length;
      _incoming = _incoming.where((r) => r.expiresAt.isAfter(DateTime.now())).toList();
      setState(() {}); // countdown labels + online hours re-render each second
      if (before != _incoming.length) _refresh();
    });
  }

  @override
  void dispose() {
    _tick?.cancel();
    super.dispose();
  }

  Future<void> _refresh() async {
    if (_online) {
      await _presence.updateLocation(_here.lat, _here.lng, online: true);
    }
    final trips = await _trips.myTrips();
    final reqs = _online ? await _requests.nearby(_here.lat, _here.lng) : null;
    if (!mounted) return;
    setState(() {
      _myTrips = trips.data ?? [];
      _incoming = (reqs?.data ?? [])
          .where((r) => r.expiresAt.isAfter(DateTime.now()))
          .toList();
    });
  }

  Future<void> _toggleOnline(bool value) async {
    setState(() => _busyToggle = true);
    if (value) {
      await _presence.updateLocation(_here.lat, _here.lng, online: true);
    } else {
      await _presence.goOffline();
    }
    if (!mounted) return;
    setState(() {
      _online = value;
      _busyToggle = false;
      _onlineSince = value ? DateTime.now() : null;
      if (!value) _incoming = [];
    });
    if (value) _refresh();
  }

  // ── Derived figures ──────────────────────────────────────────────────────

  bool _isToday(DateTime d) {
    final now = DateTime.now();
    final l = d.toLocal();
    return l.year == now.year && l.month == now.month && l.day == now.day;
  }

  List<Trip> get _todaysTrips => _myTrips.where((t) => _isToday(t.departAt)).toList();

  /// Booked seats × the driver's own price per seat, for trips departing today.
  double get _todaysEarnings => _todaysTrips.fold(0.0, (sum, t) {
        final price = t.pricePerSeat;
        if (price == null || t.seatsTotal == 0) return sum;
        final booked = (t.seatsTotal - t.seatsLeft).clamp(0, t.seatsTotal);
        return sum + booked * price;
      });

  String get _onlineFor {
    if (!_online || _onlineSince == null) {
      return context.tr('units.hoursShort', {'value': 0});
    }
    final h = DateTime.now().difference(_onlineSince!).inMinutes / 60;
    return h < 1
        ? context.tr('units.minutesShort', {'value': (h * 60).round()})
        : context.tr('units.hoursShort', {'value': h.toStringAsFixed(1)});
  }

  String _firstName() {
    final n = Session.instance.profile?.name.trim() ?? '';
    return n.isEmpty ? context.tr('role.driverLower') : n.split(' ').first;
  }

  static String initialsOf(String name) {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
    return (parts.first[0] + parts.last[0]).toUpperCase();
  }

  // ── Build ────────────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final name = Session.instance.profile?.name ?? '';
    return SafeArea(
      bottom: false,
      child: RefreshIndicator(
        onRefresh: _refresh,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
          children: [
            Row(children: [
              Expanded(
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(context.tr('role.driver'),
                      style: WanesTheme.mono(
                          size: 12, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
                  const SizedBox(height: 3),
                  Text(context.tr('driver.hi', {'name': _firstName()}),
                      style: TextStyle(
                          fontSize: 22, fontWeight: FontWeight.w800, letterSpacing: -0.44, color: t.ink)),
                ]),
              ),
              AvatarBadge(initialsOf(name), size: 42, tint: t.tealTint, fg: t.tealInk),
            ]),
            const SizedBox(height: 16),
            _onlineBanner(t),
            const SizedBox(height: 14),
            _todayRow(t),
            const SizedBox(height: 14),
            _postTripButton(t),
            const SizedBox(height: 20),
            _incomingHeader(t),
            const SizedBox(height: 10),
            if (_incoming.isEmpty)
              _noRequests(t)
            else
              ..._incoming.take(3).map((r) => Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: HailCard(
                      request: r,
                      from: _here,
                      onAccept: () => _acceptRequest(r),
                      onDecline: () => setState(() => _incoming.removeWhere((x) => x.id == r.id)),
                      onTap: _openRequests,
                    ),
                  )),
            if (_incoming.length > 3) ...[
              const SizedBox(height: 4),
              Center(
                child: TextButton(
                  onPressed: _openRequests,
                  child: Text(
                      context.trPlural('driver.seeAllRequests', _incoming.length),
                      style: WanesTheme.mono(size: 12, weight: FontWeight.w700, color: t.tealInk, spacing: 0)),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  void _openRequests() =>
      Navigator.push(context, MaterialPageRoute(builder: (_) => const RequestsScreen())).then((_) => _refresh());

  Future<void> _acceptRequest(RideRequestRow r) async {
    final res = await _requests.accept(r.id);
    if (!mounted) return;
    if (res.success) {
      WanesAlerts.success(context, context.tr('driver.requestAccepted'),
          message: context.tr('driver.requestAcceptedBody'));
    } else {
      WanesAlerts.failure(context, res,
          title: context.tr('driver.acceptFailed'),
          onRetry: () => _acceptRequest(r));
    }
    if (res.success) {
      setState(() => _incoming.removeWhere((x) => x.id == r.id));
      _refresh();
    }
  }

  /// Solid-teal "You're online" banner with the pill toggle (design 08).
  Widget _onlineBanner(WanesTokens t) {
    final on = _online;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 14),
      decoration: BoxDecoration(
        color: on ? t.teal : t.surface,
        borderRadius: BorderRadius.circular(16),
        border: on ? null : Border.all(color: t.border),
        boxShadow: on
            ? [BoxShadow(color: t.teal, blurRadius: 26, offset: const Offset(0, 12), spreadRadius: -12)]
            : null,
      ),
      child: Row(children: [
        if (on)
          PulseDot(color: t.onTeal, size: 10, duration: const Duration(milliseconds: 1400))
        else
          Container(width: 10, height: 10, decoration: BoxDecoration(color: t.ink2, shape: BoxShape.circle)),
        const SizedBox(width: 10),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(context.tr(on ? 'driver.online' : 'driver.offline'),
                style: TextStyle(
                    fontWeight: FontWeight.w800, fontSize: 15, color: on ? t.onTeal : t.ink)),
            Text(context.tr(on ? 'driver.acceptingRequests' : 'driver.notReceivingRequests'),
                style: WanesTheme.mono(
                    size: 11,
                    weight: FontWeight.w500,
                    color: on ? const Color(0xFF0A3B33) : t.ink2,
                    spacing: 0)),
          ]),
        ),
        const SizedBox(width: 12),
        WanesPillSwitch(
          value: on,
          busy: _busyToggle,
          onChanged: _busyToggle ? null : _toggleOnline,
          onTrack: t.onTeal,
          onKnob: t.teal,
          offTrack: t.border,
          offKnob: t.surface2,
        ),
      ]),
    );
  }

  /// Earnings hero + the two stacked mini stats.
  Widget _todayRow(WanesTokens t) {
    return IntrinsicHeight(
      child: Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Expanded(
          flex: 14,
          child: Container(
            padding: const EdgeInsets.all(15),
            decoration: BoxDecoration(
              color: t.surface,
              borderRadius: BorderRadius.circular(16),
              border: Border.all(color: t.border),
              boxShadow: [
                BoxShadow(color: t.shadow, blurRadius: 14, offset: const Offset(0, 4), spreadRadius: -10),
              ],
            ),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              MonoLabel(context.tr('driver.todaysEarnings'), spacing: 1.0),
              const SizedBox(height: 6),
              FittedBox(
                alignment: AlignmentDirectional.centerStart,
                child: Text(Fare.format(_todaysEarnings),
                    style: WanesTheme.mono(size: 28, weight: FontWeight.w800, color: t.tealInk, spacing: 0)),
              ),
            ]),
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          flex: 10,
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            MiniStat(value: '${_todaysTrips.length}', label: context.tr('driver.tripsStat')),
            const SizedBox(height: 12),
            MiniStat(value: _onlineFor, label: context.tr('driver.onlineStat')),
          ]),
        ),
      ]),
    );
  }

  /// The ink (near-black) CTA — deliberately not the teal button; the design
  /// reserves teal for the accept/confirm actions.
  Widget _postTripButton(WanesTokens t) {
    return Material(
      color: t.ink,
      borderRadius: BorderRadius.circular(14),
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const PostTripScreen()))
            .then((_) => _refresh()),
        child: Container(
          height: 52,
          alignment: Alignment.center,
          child: Row(mainAxisSize: MainAxisSize.min, children: [
            Icon(Icons.add_rounded, size: 18, color: t.bg),
            const SizedBox(width: 9),
            Text(context.tr('driver.postTrip'),
                style: TextStyle(color: t.bg, fontWeight: FontWeight.w800, fontSize: 15)),
          ]),
        ),
      ),
    );
  }

  Widget _incomingHeader(WanesTokens t) {
    if (_incoming.isEmpty) {
      return Text(context.tr('driver.incomingRequests').toUpperCase(),
          style: WanesTheme.mono(size: 10.5, weight: FontWeight.w600, color: t.ink2, spacing: 1.05));
    }
    return LiveCaption(context.trPlural('driver.incomingCount', _incoming.length));
  }

  Widget _noRequests(WanesTokens t) => WanesCard(
        child: Row(children: [
          Icon(Icons.notifications_none_rounded, color: t.ink2, size: 20),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
                context.tr(_online ? 'driver.noNearbyRequests' : 'driver.goOnlineHint'),
                style: TextStyle(color: t.ink2, fontSize: 13.5)),
          ),
        ]),
      );
}

/// The amber-edged incoming-request card from screen 08 — rider, route, the
/// fare estimate, and a Decline / "Accept · 0:42" pair sized 1 : 2.
class HailCard extends StatelessWidget {
  const HailCard({
    super.key,
    required this.request,
    required this.from,
    required this.onAccept,
    this.onDecline,
    this.onTap,
  });

  final RideRequestRow request;
  final Place from;
  final VoidCallback onAccept;
  final VoidCallback? onDecline;
  final VoidCallback? onTap;

  /// mm:ss while over a minute, then bare seconds — the design's "12s".
  static String remainingLabel(BuildContext context, Duration d) {
    if (d.isNegative) return context.tr('units.secondsShort', {'value': 0});
    if (d.inSeconds < 60) {
      return context.tr('units.secondsShort', {'value': d.inSeconds});
    }
    return '${d.inMinutes}:${(d.inSeconds % 60).toString().padLeft(2, '0')}';
  }

  static String shortPlace(String address) => address.split(',').first.trim();

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final pickupKm = Geo.distanceKm(from.lat, from.lng, request.originLat, request.originLng);
    final fare = Fare.estimateBetween(
      request.originLat, request.originLng,
      request.destinationLat, request.destinationLng,
      seats: request.seats,
    );
    final left = request.expiresAt.difference(DateTime.now());

    return Container(
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: t.amber),
        boxShadow: [
          BoxShadow(color: t.shadow, blurRadius: 18, offset: const Offset(0, 6), spreadRadius: -10),
        ],
      ),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          borderRadius: BorderRadius.circular(16),
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(14),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(children: [
                AvatarBadge('R', size: 40, tint: t.amberTint, fg: t.amberInk),
                const SizedBox(width: 11),
                Expanded(
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Row(children: [
                      Text(context.tr('driver.riderNumber', {'id': request.riderId}),
                          style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
                      const SizedBox(width: 7),
                      Text(context.trPlural('vehicle.seatCount', request.seats),
                          style: WanesTheme.mono(size: 11, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
                    ]),
                    const SizedBox(height: 2),
                    // Distance first: with long place names the route is what
                    // gets clipped, and the pickup distance is the number the
                    // driver decides on.
                    Text(
                      '${context.tr('driver.away', {
                            'distance': Geo.formatKm(pickupKm)
                          })} · ${context.tr('common.routeSummary', {
                            'from': shortPlace(request.originAddress),
                            'to': shortPlace(request.destinationAddress),
                          })}',
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: WanesTheme.mono(size: 11, weight: FontWeight.w500, color: t.ink2, spacing: 0),
                    ),
                  ]),
                ),
                const SizedBox(width: 8),
                Text(Fare.format(fare),
                    style: WanesTheme.mono(size: 16, weight: FontWeight.w800, color: t.amberInk, spacing: 0)),
              ]),
              const SizedBox(height: 12),
              Row(children: [
                Expanded(
                  flex: 1,
                  child: _flatButton(
                    context,
                    label: context.tr('common.decline'),
                    onTap: onDecline,
                    background: Colors.transparent,
                    foreground: t.ink,
                    border: t.border,
                    weight: FontWeight.w700,
                  ),
                ),
                const SizedBox(width: 9),
                Expanded(
                  flex: 2,
                  child: _flatButton(
                    context,
                    label: '${context.tr('common.accept')} · ${remainingLabel(context, left)}',
                    onTap: onAccept,
                    background: t.amber,
                    foreground: const Color(0xFF2A1000),
                    weight: FontWeight.w800,
                  ),
                ),
              ]),
            ]),
          ),
        ),
      ),
    );
  }

  Widget _flatButton(
    BuildContext context, {
    required String label,
    required VoidCallback? onTap,
    required Color background,
    required Color foreground,
    required FontWeight weight,
    Color? border,
  }) {
    // Material rejects `shape` and `borderRadius` together — pick one.
    return Material(
      color: background,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(10),
        side: border == null ? BorderSide.none : BorderSide(color: border),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(10),
        onTap: onTap,
        child: Container(
          height: 40,
          alignment: Alignment.center,
          child: Text(label,
              maxLines: 1,
              style: TextStyle(color: foreground, fontWeight: weight, fontSize: 13)),
        ),
      ),
    );
  }
}
