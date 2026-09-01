import 'dart:async';
import 'package:flutter/material.dart';
import 'package:latlong2/latlong.dart';
import '../core/app_config.dart';
import '../core/error_messages.dart';
import '../core/l10n.dart';
import '../core/sse_client.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/live_trip_map.dart';
import '../widgets/map_backdrop.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_motion.dart';
import '../widgets/wanes_ui.dart';
import 'live_trip_screen.dart';

/// Hail / no match — prototype screen 04. The map pings out from the rider's
/// pin while nearby drivers are notified; a sheet reports how many were
/// reached and how long the request has left.
class SearchingScreen extends StatefulWidget {
  const SearchingScreen({
    super.key,
    this.rideRequestId,
    this.driversNotified = 0,
    this.expiresAt,
    this.originLat,
    this.originLng,
    this.destLat,
    this.destLng,
  });

  final int? rideRequestId;

  /// How many drivers the search actually reached (from the search response).
  final int driversNotified;

  /// The deadline the server stamped on the hail. Null on an older API, where
  /// the configured window measured from now is the best guess available.
  final DateTime? expiresAt;

  /// What the rider asked for. With these the screen draws a real map; without
  /// them it falls back to the prototype's illustration.
  final double? originLat;
  final double? originLng;
  final double? destLat;
  final double? destLng;

  @override
  State<SearchingScreen> createState() => _SearchingScreenState();
}

class _SearchingScreenState extends State<SearchingScreen>
    with SingleTickerProviderStateMixin {
  final _sse = SseClient();
  final _trips = TripService();
  final _bookings = BookingService();
  final _requests = RideRequestService();
  StreamSubscription<Map<String, dynamic>>? _sseSub;
  Timer? _timer;

  /// Polls the driver's position once one has accepted, so the rider watches
  /// them actually approach rather than staring at a static "found" state.
  Timer? _locationPoll;

  /// The trip the accepting driver created, from the notification payload.
  int? _tripId;
  DriverLocation? _driverAt;
  bool _openingTrip = false;

  /// `@keyframes carbob` — the same gentle rock the live-trip car has.
  late final AnimationController _bob =
      AnimationController(vsync: this, duration: WanesMotion.bob ~/ 2)
        ..repeat(reverse: true);

  /// The hail's deadline, and the time left on it.
  ///
  /// The server's stamp wins: the window is admin-set, so a screen counting
  /// down its own idea of the TTL would keep saying "still looking" after the
  /// request had closed.
  late final DateTime _expiresAt =
      widget.expiresAt?.toLocal() ?? DateTime.now().add(AppConfigController.value.hailTtl);
  late Duration _left = _expiresAt.difference(DateTime.now());

  /// The full window the bar measures against — from when this screen opened
  /// to the deadline, so the strip starts full however long the hail was given.
  late final Duration _window = () {
    final span = _expiresAt.difference(DateTime.now());
    return span > Duration.zero ? span : AppConfigController.value.hailTtl;
  }();

  bool _accepted = false;

  /// Set when the server closed the hail out from under us — the rider took too
  /// long, or an admin ended it. Distinct from [_accepted], and from the local
  /// countdown, which is only ever an estimate of the same thing.
  bool _closed = false;

  /// True while the withdrawal call is in flight, so Cancel cannot be
  /// double-tapped into two requests.
  bool _cancelling = false;

  @override
  void initState() {
    super.initState();
    _timer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (!mounted) return;
      setState(() => _left = _expiresAt.difference(DateTime.now()));
    });
    _listenForAccept();
  }

  Future<void> _listenForAccept() async {
    _sseSub = _sse.events.listen((event) {
      if (!mounted) return;
      // The server closes a hail on every client at once. For the rider that
      // means their own request ended without a driver — usually the window
      // running out, which their countdown was only guessing at.
      if (event['event'] == 'rideRequestClosed') {
        final id = (event['requestId'] as num?)?.toInt();
        final reason = RideRequestClosedReason.fromWire(event['reason'] as String?);
        if (id != null &&
            id == widget.rideRequestId &&
            reason != RideRequestClosedReason.matched) {
          _timer?.cancel();
          setState(() {
            _closed = true;
            _left = Duration.zero;
          });
        }
        return;
      }

      if (event['type'] == 'DriverAccepted') {
        // The payload carries the trip the driver just created for this hail —
        // that is what lets us follow them, and hand over to live tracking.
        final data = event['data'];
        final tripId = data is Map<String, dynamic> ? data['tripId'] as int? : null;
        setState(() {
          _accepted = true;
          _tripId = tripId;
        });
        _timer?.cancel();
        if (tripId != null) {
          _pollDriver();
          _locationPoll =
              Timer.periodic(const Duration(seconds: 10), (_) => _pollDriver());
        }
      }
    });
    await _sse.connect();
  }

  /// Best-effort: a driver who has not reported leaves the car off the map
  /// rather than putting it somewhere invented.
  Future<void> _pollDriver() async {
    final id = _tripId;
    if (id == null) return;
    final res = await _trips.driverLocation(id);
    if (!mounted) return;
    final fix = res.data;
    setState(() => _driverAt = fix != null && fix.isUsable ? fix : null);
  }

  /// Withdraws the hail before leaving.
  ///
  /// Backing out used to just pop the screen, which left the request Open: the
  /// drivers it was pushed to kept the card, and one of them could still accept
  /// a ride the rider had walked away from. Telling the server first is what
  /// takes the card off their screens.
  Future<void> _cancel() async {
    final id = widget.rideRequestId;
    // Nothing to withdraw: no request was opened, or it has already closed.
    if (id == null || _expired) {
      Navigator.pop(context);
      return;
    }

    setState(() => _cancelling = true);
    final res = await _requests.cancel(id);
    if (!mounted) return;
    setState(() => _cancelling = false);

    // A hail that closed while the tap was in flight is not a failure — the
    // rider wanted it gone and it is gone.
    if (res.success || res.errorCode == ServerErrorCode.requestNotOpen) {
      Navigator.pop(context);
      return;
    }
    WanesAlerts.failure(context, res,
        title: context.tr('hail.cancelFailed'), onRetry: _cancel);
  }

  /// Hands over to live tracking. Accepting a hail creates both the trip and a
  /// confirmed booking, so both exist by the time this can be tapped.
  Future<void> _openTrip() async {
    final id = _tripId;
    if (id == null || _openingTrip) {
      Navigator.pop(context);
      return;
    }
    setState(() => _openingTrip = true);
    final trip = await _trips.get(id);
    final mine = await _bookings.myBookings();
    if (!mounted) return;
    setState(() => _openingTrip = false);

    final booking = (mine.data ?? []).where((b) => b.tripId == id).firstOrNull;
    if (trip.data == null || booking == null) {
      WanesAlerts.failure(context, trip, title: context.tr('hail.trackFailed'));
      return;
    }
    if (!mounted) return;
    Navigator.pushReplacement(
      context,
      MaterialPageRoute(
        builder: (_) => LiveTripScreen(trip: trip.data!, booking: booking),
      ),
    );
  }

  @override
  void dispose() {
    _locationPoll?.cancel();
    _bob.dispose();
    _timer?.cancel();
    _sseSub?.cancel();
    _sse.dispose();
    super.dispose();
  }

  String get _clock {
    if (_left.isNegative) return '0:00';
    return '${_left.inMinutes}:${(_left.inSeconds % 60).toString().padLeft(2, '0')}';
  }

  bool get _expired => !_accepted && (_closed || _left.isNegative);

  /// The real map needs somewhere real to be. Without coordinates (an older
  /// entry point) the prototype illustration still stands in.
  bool get _hasCoords =>
      widget.originLat != null &&
      widget.originLng != null &&
      (widget.originLat != 0 || widget.originLng != 0);

  LatLng get _origin => LatLng(widget.originLat!, widget.originLng!);

  /// Falls back to the origin so a hail with no drop-off still has a map.
  LatLng get _destination =>
      LatLng(widget.destLat ?? widget.originLat!, widget.destLng ?? widget.originLng!);

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final accent = _accepted ? t.teal : t.amber;

    return Scaffold(
      backgroundColor: t.bg,
      body: Column(children: [
        Expanded(child: _hasCoords ? _realMap(t, accent) : _illustration(t, accent)),
        MapSheet(
          padding: const EdgeInsets.fromLTRB(22, 14, 22, 20),
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Align(
              alignment: AlignmentDirectional.centerStart,
              child: _accepted
                  ? LiveCaption(context.tr('hail.driverFound'),
                      dotColor: t.teal, textColor: t.tealInk, dotSize: 9)
                  : LiveCaption(
                      context.tr(_expired ? 'hail.expiredTag' : 'hail.liveSearching'),
                      dotSize: 9),
            ),
            const SizedBox(height: 10),
            Text(
              context.tr(_accepted
                  ? 'hail.acceptedTitle'
                  : _expired
                      ? 'hail.expiredTitle'
                      : 'hail.searchingTitle'),
              style: TextStyle(
                  fontSize: 21, fontWeight: FontWeight.w800, letterSpacing: -0.42, color: t.ink),
            ),
            const SizedBox(height: 6),
            Text(
              context.tr(_accepted
                  ? 'hail.acceptedBody'
                  : _expired
                      ? 'hail.expiredBody'
                      : 'hail.searchingBody'),
              style: TextStyle(color: t.ink2, fontSize: 14, height: 1.5),
            ),
            if (!_accepted && !_expired) ...[
              const SizedBox(height: 16),
              _notifiedRow(t),
            ],
            const SizedBox(height: 14),
            if (_accepted)
              PrimaryButton(
                  label: context.tr('hail.trackTrip'),
                  arrow: false,
                  busy: _openingTrip,
                  onPressed: _openingTrip ? null : _openTrip)
            else
              SizedBox(
                width: double.infinity,
                height: 50,
                child: OutlinedButton(
                  onPressed: _cancelling ? null : _cancel,
                  style: OutlinedButton.styleFrom(
                    foregroundColor: t.ink,
                    side: BorderSide(color: t.border),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                  ),
                  child: _cancelling
                      ? WanesSpinner.mono(t.ink2, size: 18)
                      : Text(context.tr(_expired ? 'common.back' : 'hail.cancelRequest'),
                          style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
                ),
              ),
          ]),
        ),
      ]),
    );
  }

  /// The rider's real surroundings: their own pin pinging while we look, and
  /// the accepting driver closing in on it once there is one.
  Widget _realMap(WanesTokens t, Color accent) {
    final fix = _driverAt;
    return LiveTripMap(
      origin: _origin,
      destination: _destination,
      driverAt: fix == null ? null : LatLng(fix.lat, fix.lng),
      routeColor: accent,
      // Until someone accepts there is no journey yet, only a request — so the
      // whole route reads as still-to-happen.
      progress: MapProgress(
        at: 0,
        behind: MapLegStyle.solid,
        ahead: _accepted ? MapLegStyle.dashed : MapLegStyle.faint,
        marker: fix == null ? null : _carMarker(t),
      ),
      startMarker: PingRings(
        color: accent,
        child: Container(
          width: 20,
          height: 20,
          decoration: BoxDecoration(
            color: accent,
            shape: BoxShape.circle,
            border: Border.all(color: t.bg, width: 3),
          ),
        ),
      ),
      endMarker: Opacity(
        opacity: _accepted ? 1 : 0.45,
        child: MapPin(size: 24, color: t.amber),
      ),
    );
  }

  /// The prototype's drawing, for an entry point that never had coordinates.
  Widget _illustration(WanesTokens t, Color accent) => MapBackdrop(
        children: [
          Align(
            alignment: const Alignment(0, -0.12), // left 50% · top 44%
            child: PingRings(
              color: accent,
              child: Container(
                width: 20,
                height: 20,
                decoration: BoxDecoration(
                  color: accent,
                  shape: BoxShape.circle,
                  border: Border.all(color: t.bg, width: 3),
                ),
              ),
            ),
          ),
          // Nearby drivers, at the design's fixed positions.
          const Align(alignment: Alignment(-0.48, -0.40), child: MapDot(size: 12, ring: 2)),
          const Align(alignment: Alignment(0.48, 0.12), child: MapDot(size: 12, ring: 2)),
          const Align(alignment: Alignment(0.28, -0.56), child: MapDot(size: 12, ring: 2)),
        ],
      );

  /// The accepting driver, bobbing along as they approach.
  Widget _carMarker(WanesTokens t) => AnimatedBuilder(
        animation: _bob,
        builder: (_, child) => Transform.translate(
          offset: Offset(0, -3.2 * Curves.easeInOut.transform(_bob.value)),
          child: child,
        ),
        child: Container(
          width: 40,
          height: 40,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: t.ink,
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: t.bg, width: 2),
            boxShadow: const [
              BoxShadow(color: Color(0x66000000), blurRadius: 12, offset: Offset(0, 4)),
            ],
          ),
          child: Icon(Icons.directions_car_rounded, size: 22, color: t.teal),
        ),
      );

  /// Amber-tint strip: who we reached, and the time left on the request.
  Widget _notifiedRow(WanesTokens t) {
    final n = widget.driversNotified;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      decoration: BoxDecoration(color: t.amberTint, borderRadius: BorderRadius.circular(14)),
      child: Column(children: [
        Row(children: [
        if (n > 0) ...[
          // One badge per driver reached, capped at three so the row stays put.
          AvatarStack(initials: List.filled(n > 3 ? 3 : n, 'D')),
          const SizedBox(width: 11),
        ],
        Expanded(
          child: Text(
            n == 0
                ? context.tr('hail.lookingForDrivers')
                : context.trPlural('hail.driversNotified', n),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(fontWeight: FontWeight.w700, fontSize: 13.5, color: t.ink),
          ),
        ),
        Text(_clock,
            style: WanesTheme.mono(size: 15, weight: FontWeight.w700, color: t.amberInk, spacing: 0)),
        ]),
        const SizedBox(height: 10),
        // `@keyframes sbar` — the strip grows in on first paint, then tracks
        // the hail's remaining TTL down as the clock beside it counts.
        GrowBar(
          value: _left.isNegative
              ? 0
              : _left.inMilliseconds / _window.inMilliseconds,
          color: t.amber,
          track: t.amberInk.withValues(alpha: .16),
          height: 5,
        ),
      ]),
    );
  }
}
