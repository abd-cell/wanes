import 'dart:async';

import 'package:flutter/material.dart';
import '../../core/departure_label.dart';
import '../../core/driver_position.dart';
import '../../core/fare.dart';
import '../../core/geo.dart';
import '../../core/l10n.dart';
import '../../core/places.dart';
import '../../core/push_service.dart';
import '../../core/session.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/wanes_alerts.dart';
import '../../widgets/wanes_ui.dart';
import '../notifications_screen.dart';
import 'accept_flow.dart';
import 'driver_profile_screen.dart';
import 'marketplace_screen.dart';
import 'my_trips_screen.dart';
import 'post_trip_screen.dart';
import 'find_riders_screen.dart';
import 'reliability_screen.dart';
import 'requests_screen.dart';

/// Driver app shell — Home · Market · Trips · Profile behind the bottom nav.
/// Home is the dashboard (screen 08); Market is every open request around the
/// driver, split into rides needed now and journeys to plan.
class DriverHomeScreen extends StatefulWidget {
  const DriverHomeScreen({super.key});

  @override
  State<DriverHomeScreen> createState() => _DriverHomeScreenState();
}

class _DriverHomeScreenState extends State<DriverHomeScreen> {
  static const _marketIndex = 1;
  static const _tripsIndex = 2;

  int _index = 0;

  /// The tabs live in an `IndexedStack`, so each one is built once and kept
  /// alive. That is what we want for scroll position, but it also means Trips
  /// would keep showing the seat count it loaded on first build — a trip that
  /// filled up meanwhile would still read "Posted". Reload it on arrival, and
  /// the marketplace likewise, whose requests other drivers take.
  final _tripsTab = GlobalKey<MyTripsScreenState>();
  final _marketTab = GlobalKey<MarketplaceScreenState>();

  void _select(int i) {
    setState(() => _index = i);
    if (i == _tripsIndex) _tripsTab.currentState?.reload();
    if (i == _marketIndex) _marketTab.currentState?.reload(prompt: true);
  }

  void _openMarket(MarketSegment segment) {
    _select(_marketIndex);
    _marketTab.currentState?.selectSegment(segment);
  }

  @override
  Widget build(BuildContext context) {
    final tabs = [
      DriverDashboard(onGoTrips: () => _select(_tripsIndex), onGoMarket: _openMarket),
      MarketplaceScreen(key: _marketTab),
      MyTripsScreen(key: _tripsTab),
      const DriverProfileScreen(),
    ];
    return Scaffold(
      body: IndexedStack(index: _index, children: tabs),
      bottomNavigationBar: WanesBottomNav(index: _index, onSelect: _select),
    );
  }
}

/// The driver dashboard — prototype screen 08. Online banner, today's earnings
/// hero, the ink "Post a trip" CTA and the live incoming-request stack.
class DriverDashboard extends StatefulWidget {
  const DriverDashboard({super.key, this.onGoTrips, this.onGoMarket});
  final VoidCallback? onGoTrips;

  /// Opens the marketplace tab on a segment. Null outside the shell (tests),
  /// where the dashboard falls back to the full-screen board.
  final ValueChanged<MarketSegment>? onGoMarket;

  @override
  State<DriverDashboard> createState() => _DriverDashboardState();
}

class _DriverDashboardState extends State<DriverDashboard> {
  final _presence = PresenceService();
  final _trips = TripService();
  final _requests = RiderTripService();
  /// Where the driver is. Starts on the last known position and is replaced by
  /// a device fix on refresh; see [DriverPosition].
  Place _here = DriverPosition.last ?? kPlaces.first;

  bool _online = true;
  bool _busyToggle = false;
  bool _accepting = false;
  bool _refreshing = false;
  DateTime? _onlineSince;
  List<Trip> _myTrips = [];
  List<RiderTrip> _incoming = [];

  /// Requests around the driver that leave later than the hour — not hails,
  /// but the marketplace's scheduled work. Counted here to point at it.
  int _scheduledNearby = 0;

  /// Hails this driver waved away. The server has no decline verb — passing is
  /// only ever a local act — so remembering them here is the only thing that
  /// stops the next refresh handing the same card straight back.
  final Set<int> _declined = {};

  /// The last "online for" figure that reached the screen. The ticker runs once
  /// a second; this moves once a minute, so it is the cheap test for whether a
  /// repaint would show anything new.
  String _onlineLabel = '';

  /// Drives the per-request countdown and drops hails once their TTL is up.
  Timer? _tick;

  /// A hail can also end before its countdown does — withdrawn by the rider or
  /// taken by another driver. The server pushes that; the timer cannot see it.
  StreamSubscription<RiderTripClosed>? _closed;

  @override
  void initState() {
    super.initState();
    _refresh();
    _tick = Timer.periodic(const Duration(seconds: 1), (_) {
      if (!mounted) return;
      final live = _incoming.where((r) => r.departAt.isAfter(DateTime.now())).toList();
      final expired = live.length != _incoming.length;
      final label = _onlineFor;
      // Repaint only when something on screen actually moved: a countdown is
      // running, a hail just timed out, or the online-for figure ticked over.
      // The shell keeps this tab alive in an IndexedStack, so the unconditional
      // rebuild ran once a second for as long as the app was open — including
      // while the driver sat on Trips or Profile.
      if (!expired && live.isEmpty && label == _onlineLabel) return;
      setState(() {
        _incoming = live;
        _onlineLabel = label;
      });
      if (expired) _refresh();
    });
    _closed = PushService.instance.riderTripClosed.listen((closed) {
      if (!mounted) return;
      if (!_incoming.any((r) => r.id == closed.riderTripId)) return;
      setState(() => _incoming.removeWhere((r) => r.id == closed.riderTripId));
    });
  }

  @override
  void dispose() {
    _tick?.cancel();
    _closed?.cancel();
    super.dispose();
  }

  /// The trip the driver is out on, if any. One driver drives one car, so while
  /// this is set they are not available for a second ride — the same rule the
  /// server enforces, mirrored here so the dashboard never offers a tap the API
  /// would refuse.
  Trip? get _tripUnderway {
    for (final trip in _myTrips) {
      if (trip.isUnderway) return trip;
    }
    return null;
  }

  Future<void> _refresh() async {
    // The ticker fires a refresh whenever a hail times out, which can land on
    // top of a pull-to-refresh. Two runs race their responses into the same
    // fields, and the loser can put an already-expired hail back on screen.
    if (_refreshing) return;
    _refreshing = true;
    try {
      // Trips first: whether the driver is out on one decides both calls below.
      final trips = await _trips.myTrips();
      // A failed read is not an empty garage. Keeping the last good list matters
      // most for [_tripUnderway]: dropping it would clear the on-trip banner and
      // unlock "Post a trip" in the middle of a ride, over a single timeout.
      final myTrips = trips.success ? (trips.data ?? []) : _myTrips;
      final underway = myTrips.any((t) => t.isUnderway);

      // The location still goes up while driving — that is what the riders in
      // the car are tracking. Only the *online* claim is withdrawn: a driver on
      // the road is not available for another ride, and the server refuses to
      // raise the flag anyway.
      var online = _online;
      final here = await DriverPosition.resolve();
      _here = here;
      if (online || underway) {
        final claiming = online && !underway;
        final presence =
            await _presence.updateLocation(here.lat, here.lng, online: claiming);
        // The banner states the server's view of this driver, not this screen's.
        // A claim the server would not take must not leave it reading
        // "accepting requests" when no hail can ever arrive.
        if (claiming && !presence.success) online = false;
      }
      final reqs = online && !underway ? await _requests.nearby(here.lat, here.lng) : null;
      if (!mounted) return;
      setState(() {
        _myTrips = myTrips;
        _online = online;
        _onlineSince = online ? (_onlineSince ?? DateTime.now()) : null;
        if (reqs == null) {
          _incoming = const <RiderTrip>[];
          _scheduledNearby = 0;
        } else if (reqs.success) {
          final rows = reqs.data ?? const <RiderTrip>[];
          _incoming = _answerable(rows);
          _scheduledNearby = marketSegment(rows, MarketSegment.scheduled).length;
        } else {
          _incoming = _answerable(_incoming);
        }
      });
    } finally {
      _refreshing = false;
    }
  }

  /// Hails still worth showing: leaving within the hour, and not one already
  /// passed on. Both filters have to run over every list the server hands back,
  /// or a declined card comes straight back on the next refresh. Anything later
  /// belongs to the marketplace, not to the dashboard's "now" stack.
  List<RiderTrip> _answerable(List<RiderTrip> rows) => marketSegment(rows, MarketSegment.now)
      .where((r) => !_declined.contains(r.id))
      .toList();

  Future<void> _toggleOnline(bool value) async {
    setState(() => _busyToggle = true);
    // Going online is the explicit act that may ask for location.
    if (value) _here = await DriverPosition.resolve(prompt: true);
    if (!mounted) return;
    final res = value
        ? await _presence.updateLocation(_here.lat, _here.lng, online: true)
        : await _presence.goOffline();
    if (!mounted) return;
    setState(() => _busyToggle = false);
    // The switch reports the server's answer, not the tap. A refused go-online
    // (unverified driver, no vehicle, already out on a trip) used to flip it
    // anyway and leave the driver waiting on hails that could never arrive.
    if (!res.success) {
      WanesAlerts.failure(context, res,
          title: context.tr(value ? 'driver.goOnlineFailed' : 'driver.goOfflineFailed'),
          onRetry: () => _toggleOnline(value));
      return;
    }
    setState(() {
      _online = value;
      _onlineSince = value ? DateTime.now() : null;
      if (!value) {
        _incoming = [];
        _scheduledNearby = 0;
      }
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

  // ── Build ────────────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
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
              NotificationBellButton(
                onTap: () => Navigator.push(context,
                    MaterialPageRoute(builder: (_) => const NotificationsScreen())),
              ),
            ]),
            const SizedBox(height: 16),
            _onlineBanner(t),
            if (Session.instance.profile?.isSuspended == true) ...[
              const SizedBox(height: 10),
              _pausedBanner(t),
            ],
            const SizedBox(height: 14),
            _todayRow(t),
            const SizedBox(height: 14),
            _postTripButton(t),
            const SizedBox(height: 10),
            _findRidersRow(t),
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
                      onAccept: _accepting ? null : () => _acceptRequest(r),
                      onDecline: _accepting ? null : () => _decline(r),
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
            const SizedBox(height: 6),
            _marketplaceRow(t),
          ],
        ),
      ),
    );
  }

  void _openRequests() =>
      Navigator.push(context, MaterialPageRoute(builder: (_) => const RequestsScreen())).then((_) => _refresh());

  void _openMarket(MarketSegment segment) {
    final go = widget.onGoMarket;
    if (go != null) {
      go(segment);
    } else {
      _openRequests();
    }
  }

  Future<void> _acceptRequest(RiderTrip r) async {
    // One driver drives one car: a double tap, or a tap on a second card while
    // the first is still in flight, is an accept the server is bound to refuse.
    if (_accepting) return;
    final ok = await acceptRideRequest(context, r,
        onBusy: (busy) => mounted ? setState(() => _accepting = busy) : null);
    if (!ok || !mounted) return;
    setState(() => _incoming.removeWhere((x) => x.id == r.id));
    _refresh();
  }

  /// The way into the marketplace from Home, with how much is waiting there.
  Widget _marketplaceRow(WanesTokens t) => WanesListRow(
        icon: Icons.storefront_outlined,
        title: context.tr('driver.openMarketplace'),
        subtitle: _scheduledNearby > 0
            ? context.trPlural('driver.scheduledNearby', _scheduledNearby)
            : context.tr('driver.openMarketplaceBody'),
        onTap: () => _openMarket(
            _incoming.isEmpty && _scheduledNearby > 0 ? MarketSegment.scheduled : MarketSegment.now),
      );

  /// Passing on a hail. Local by design — there is no decline verb — but it has
  /// to outlive the card, so the id goes into [_declined] as well.
  void _decline(RiderTrip r) => setState(() {
        _declined.add(r.id);
        _incoming.removeWhere((x) => x.id == r.id);
      });

  /// Solid-teal "You're online" banner with the pill toggle (design 08), plus a
  /// third state the design did not have: out on a trip, where availability is
  /// not the driver's to set until they finish. Tapping that one opens Trips.
  Widget _onlineBanner(WanesTokens t) {
    final busy = _tripUnderway != null;
    final on = _online && !busy;
    return Material(
      color: on ? t.teal : t.surface,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: busy ? widget.onGoTrips : null,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 14),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
            border: on ? null : Border.all(color: busy ? t.amber : t.border),
            boxShadow: on
                ? [BoxShadow(color: t.teal, blurRadius: 26, offset: const Offset(0, 12), spreadRadius: -12)]
                : null,
          ),
          child: Row(children: [
            if (on || busy)
              PulseDot(
                  color: busy ? t.amber : t.onTeal,
                  size: 10,
                  duration: const Duration(milliseconds: 1400))
            else
              Container(width: 10, height: 10, decoration: BoxDecoration(color: t.ink2, shape: BoxShape.circle)),
            const SizedBox(width: 10),
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(
                    context.tr(busy
                        ? 'driver.onTrip'
                        : on
                            ? 'driver.online'
                            : 'driver.offline'),
                    style: TextStyle(
                        fontWeight: FontWeight.w800, fontSize: 15, color: on ? t.onTeal : t.ink)),
                Text(
                    context.tr(busy
                        ? 'driver.onTripHint'
                        : on
                            ? 'driver.acceptingRequests'
                            : 'driver.notReceivingRequests'),
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
              onChanged: _busyToggle || busy ? null : _toggleOnline,
              onTrack: t.onTeal,
              onKnob: t.teal,
              offTrack: t.border,
              offKnob: t.surface2,
            ),
          ]),
        ),
      ),
    );
  }

  /// Instant requests are paused by the reliability record; planned trips are not.
  Widget _pausedBanner(WanesTokens t) => Material(
        color: t.amberTint,
        borderRadius: BorderRadius.circular(14),
        child: InkWell(
          borderRadius: BorderRadius.circular(14),
          onTap: () => Navigator.push(
              context, MaterialPageRoute(builder: (_) => const ReliabilityScreen())),
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Row(children: [
              Icon(Icons.pause_circle_outline_rounded, color: t.amberInk),
              const SizedBox(width: 10),
              Expanded(
                child: Text(context.tr('reliability.pausedBanner'),
                    style: TextStyle(fontSize: 12.5, height: 1.35, color: t.amberInk, fontWeight: FontWeight.w600)),
              ),
              Icon(Icons.chevron_right_rounded, color: t.amberInk),
            ]),
          ),
        ),
      );

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
    // The server refuses a new trip while the driver is out on one, so the
    // button says so rather than opening a form that cannot save.
    final busy = _tripUnderway != null;
    return Material(
      color: busy ? t.surface2 : t.ink,
      borderRadius: BorderRadius.circular(14),
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: busy
            ? null
            : () => Navigator.push(context, MaterialPageRoute(builder: (_) => const PostTripScreen()))
                .then((_) => _refresh()),
        child: Container(
          height: 52,
          alignment: Alignment.center,
          child: Row(mainAxisSize: MainAxisSize.min, children: [
            Icon(busy ? Icons.lock_outline_rounded : Icons.add_rounded,
                size: 18, color: busy ? t.ink2 : t.bg),
            const SizedBox(width: 9),
            Text(context.tr(busy ? 'driver.postTripBlocked' : 'driver.postTrip'),
                style: TextStyle(
                    color: busy ? t.ink2 : t.bg, fontWeight: FontWeight.w800, fontSize: 15)),
          ]),
        ),
      ),
    );
  }

  /// The other way a driver fills a car.
  ///
  /// The stack below is push-driven and local: it answers "who needs a lift
  /// around me, right now". A driver planning Thursday's run to Irbid cannot ask
  /// that question at all, so the search sits here beside Post a trip — the two
  /// things a driver does deliberately, rather than waits for.
  Widget _findRidersRow(WanesTokens t) => WanesListRow(
        icon: Icons.person_search_outlined,
        title: context.tr('driver.findRiders'),
        subtitle: context.tr('driver.findRidersBody'),
        onTap: () => Navigator.push(context,
            MaterialPageRoute(builder: (_) => const FindRidersScreen())),
      );

  Widget _incomingHeader(WanesTokens t) {
    if (_incoming.isEmpty) {
      return Text(context.tr('driver.incomingRequests').toUpperCase(),
          style: WanesTheme.mono(size: 10.5, weight: FontWeight.w600, color: t.ink2, spacing: 1.05));
    }
    return LiveCaption(context.trPlural('driver.incomingCount', _incoming.length));
  }

  Widget _noRequests(WanesTokens t) {
    final busy = _tripUnderway != null;
    return WanesCard(
      child: Row(children: [
        Icon(busy ? Icons.pause_circle_outline_rounded : Icons.notifications_none_rounded,
            color: t.ink2, size: 20),
        const SizedBox(width: 12),
        Expanded(
          child: Text(
              context.tr(busy
                  ? 'driver.requestsPausedOnTrip'
                  : _online
                      ? 'driver.noNearbyRequests'
                      : 'driver.goOnlineHint'),
              style: TextStyle(color: t.ink2, fontSize: 13.5)),
        ),
      ]),
    );
  }
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

  final RiderTrip request;
  final Place from;

  /// Null while another accept is in flight — one driver can only take one.
  final VoidCallback? onAccept;
  final VoidCallback? onDecline;
  final VoidCallback? onTap;

  /// Time left to departure — see [untilLabel].
  static String remainingLabel(BuildContext context, Duration d) => untilLabel(context, d);

  static String shortPlace(String address) => address.split(',').first.trim();

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final pickupKm = Geo.distanceKm(from.lat, from.lng, request.originLat, request.originLng);
    final fare = Fare.estimateBetween(
      request.originLat, request.originLng,
      request.destinationLat, request.destinationLng,
      seats: request.seatsWanted,
    );
    final left = request.departAt.difference(DateTime.now());

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
                      Text(
                          request.isPool
                              ? '${context.trPlural('market.passengers', request.riderCount)} · '
                                  '${context.trPlural('vehicle.seatCount', request.seatsWanted)}'
                              : context.trPlural('vehicle.seatCount', request.seatsWanted),
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
