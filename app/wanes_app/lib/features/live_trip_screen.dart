import 'dart:async';
import 'dart:math' as math;
import 'package:flutter/material.dart';
import 'package:latlong2/latlong.dart';
import 'package:url_launcher/url_launcher.dart';
import '../core/l10n.dart';
import '../core/sse_client.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/live_trip_map.dart';
import '../widgets/map_backdrop.dart';
import '../widgets/wanes_motion.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';
import 'rate_screen.dart';

/// Active trip — prototype screen 06. The route on the map with the car
/// approaching and an ETA card, over a sheet holding the four-stage progress
/// rail, the driver row and Cancel trip.
///
/// The rail is driven by the trip's real status, not a timer: the driver moves
/// the trip Posted → Arrived → Active → Completed from their own app, and each
/// move reaches this screen as an SSE notification. The status the screen opens
/// on is re-read from the server, because the [Trip] handed over by the booking
/// list can be minutes stale.
///
/// The map reads off the same status: the car glides along the route as the
/// stage changes, the leg still to come is dashed, the covered leg solid, and
/// the pickup dot pings while the driver waits there. It stays a depiction of
/// the *stage* rather than a position — nothing in the API carries the driver's
/// location — with only the approach placed from the real countdown.
class LiveTripScreen extends StatefulWidget {
  const LiveTripScreen({super.key, required this.trip, required this.booking});
  final Trip trip;
  final Booking booking;

  @override
  State<LiveTripScreen> createState() => _LiveTripScreenState();
}

class _LiveTripScreenState extends State<LiveTripScreen> with SingleTickerProviderStateMixin {
  static const _stepKeys = [
    'trip.stepOnTheWay',
    'trip.stepArrived',
    'trip.stepInTrip',
    'trip.stepDone',
  ];

  /// Trip status → rail stage. Posted and Full both mean "not here yet", and a
  /// cancelled trip has no stage of its own — [_cancelled] carries that.
  static const _stageOf = {1: 0, 2: 0, 6: 1, 3: 2, 4: 3};

  /// The SSE notification types that move this trip along.
  static const _stageOfEvent = {
    'DriverArrived': 1,
    'TripStarted': 2,
    'TripCompleted': 3,
  };

  final _trips = TripService();
  final _sse = SseClient();
  StreamSubscription<Map<String, dynamic>>? _sseSub;

  /// Ticks the countdown to pickup; the stage itself never moves on a timer.
  Timer? _clock;

  /// Where the car sits on the route once the trip is under way. There is no
  /// journey telemetry to place it properly, so it marks "on the road, drop-off
  /// still ahead" and stays put until the driver completes the trip.
  /// How long a city leg is assumed to take per kilometre, for the stretch
  /// between pickup and drop-off. Only a fallback — a real driver fix always
  /// wins. ~24 km/h, an ordinary urban average.
  static const _minutesPerKm = 2.5;

  /// The car never quite reaches the pin under its own estimate; only the
  /// driver's own "completed" puts it there.
  static const _underWayCap = 0.92;

  /// How far up the road the driver is when they are [_approachWindow] minutes
  /// or more away, closing on the pickup dot as the departure time arrives.
  static const _approachFrom = 0.9;
  static const _approachTo = 0.08;
  static const _approachWindow = 15;

  late int _step = _stageOf[widget.trip.status] ?? 0;
  bool _cancelled = false;

  /// A status re-read is in flight — the map's refresh button shows it.
  bool _refreshing = false;

  /// The trip as the server last described it — carries `startedAt`, which the
  /// mid-trip position is measured from. Starts as what we were handed.
  late Trip _trip = widget.trip;

  /// The driver's last reported position, once they have reported one. Null
  /// leaves the car where the trip's stage says it should be.
  DriverLocation? _driverAt;

  /// Polls that position while there is still a journey to watch.
  Timer? _locationPoll;

  /// A fix this old is not worth drawing as "where the driver is" — the app
  /// only reports while it is open, so a backgrounded driver goes quiet.
  static const _fixTooOld = Duration(minutes: 5);

  /// `@keyframes carbob` — the car marker rocks gently as it moves. The
  /// keyframe rises and falls within its 2.4s, so a reversing controller runs
  /// half of it per pass.
  late final AnimationController _bob =
      AnimationController(vsync: this, duration: WanesMotion.bob ~/ 2)
        ..repeat(reverse: true);

  @override
  void initState() {
    super.initState();
    _cancelled = widget.trip.status == 5;
    _clock = Timer.periodic(const Duration(seconds: 5), (_) {
      if (!mounted) return;
      // Runs until the car has nowhere left to go. Between driver fixes this
      // is what walks it along the route, in every stage.
      if (_completed || _cancelled) {
        _clock?.cancel();
        return;
      }
      setState(() {});
    });
    _refreshStatus();
    _listen();
    _pollDriverLocation();
    _locationPoll =
        Timer.periodic(const Duration(seconds: 15), (_) => _pollDriverLocation());
  }

  /// The trip we were handed came from a list that may have been loaded a while
  /// ago. Re-read it so the rail opens on the real stage even if every SSE
  /// event for this trip was missed (app backgrounded, phone offline).
  ///
  /// Also the map's refresh button ([manual]), which is the rider's way out of
  /// exactly that: a dropped SSE connection is invisible from here, so asking
  /// again is worth a button — and worth saying so when the ask fails.
  Future<void> _refreshStatus({bool manual = false}) async {
    if (_refreshing) return;
    setState(() => _refreshing = true);
    final res = await _trips.get(widget.trip.id);
    final trip = res.data;
    if (!mounted) return;
    setState(() {
      _refreshing = false;
      if (trip == null) return;
      _trip = trip;
      _cancelled = trip.status == 5;
      // Only ever forward: a stale read must not walk the rail backwards past
      // an event that already arrived.
      final stage = _stageOf[trip.status] ?? 0;
      if (stage > _step) _step = stage;
    });
    if (manual && trip == null) {
      WanesAlerts.error(context, res.errorMessage ?? context.tr('errors.generic'));
    }
  }

  Future<void> _listen() async {
    _sseSub = _sse.events.listen((event) {
      if (!mounted) return;

      // The rider's whole notification stream arrives here, including events
      // for their other bookings — match the trip before touching the rail.
      final data = event['data'];
      final tripId = data is Map<String, dynamic> ? data['tripId'] as int? : null;
      if (tripId != widget.trip.id) return;

      final type = event['type'] as String?;
      if (type == 'TripCancelled') {
        setState(() => _cancelled = true);
        WanesAlerts.info(context, context.tr('trip.tripCancelled'),
            message: context.tr('trip.cancelledByDriver'));
        return;
      }

      final stage = _stageOfEvent[type];
      if (stage != null && stage > _step) setState(() => _step = stage);
    });
    await _sse.connect();
  }

  /// Where the driver actually is. Best-effort: a failure (or a driver whose
  /// app has never reported) simply leaves the marker on its stage-derived
  /// position rather than surfacing an error over the map.
  Future<void> _pollDriverLocation() async {
    // Nothing left to follow once the trip is over.
    if (_completed || _cancelled) {
      _locationPoll?.cancel();
      return;
    }
    final res = await _trips.driverLocation(widget.trip.id);
    if (!mounted) return;
    final fix = res.data;
    final fresh = fix != null &&
        fix.isUsable &&
        (fix.age == null || fix.age! < _fixTooOld);
    if (fresh != (_driverAt != null) || (fresh && fix.lat != _driverAt?.lat) ||
        (fresh && fix.lng != _driverAt?.lng)) {
      setState(() => _driverAt = fresh ? fix : null);
    }
  }

  @override
  void dispose() {
    _locationPoll?.cancel();
    _clock?.cancel();
    _sseSub?.cancel();
    _sse.dispose();
    _bob.dispose();
    super.dispose();
  }

  bool get _completed => _step >= _stepKeys.length - 1;

  /// Minutes until the driver is due, from the trip's own departure time. Null
  /// once the trip is under way, when there is nothing left to count down to.
  int? get _minutesToPickup {
    if (_step >= 2) return null;
    final left = widget.trip.departAt.difference(DateTime.now()).inMinutes;
    return left < 0 ? 0 : left;
  }

  static String initialsOf(String name) {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
    return (parts.first[0] + parts.last[0]).toUpperCase();
  }

  /// Hands the driver's number to the platform dialler. The server only sends
  /// the number while the booking is live, so a finished seat has none.
  Future<void> _callDriver() async {
    final phone = widget.booking.driverPhone;
    if (phone == null || phone.isEmpty) {
      WanesAlerts.info(context, context.tr('common.notAvailableYet'),
          message: context.tr('trip.noDriverPhone'));
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
    final trip = widget.trip;
    final car = [trip.vehicleLabel, trip.vehiclePlate].where((s) => s.isNotEmpty).join(' · ');

    return Scaffold(
      backgroundColor: t.bg,
      body: Column(children: [
        Expanded(
          child: LiveTripMap(
            origin: LatLng(trip.originLat, trip.originLng),
            destination: LatLng(trip.destinationLat, trip.destinationLng),
            driverAt: _driverAt == null ? null : LatLng(_driverAt!.lat, _driverAt!.lng),
            routeColor: t.teal,
            dimmed: _cancelled,
            startMarker: MapDot(color: _cancelled ? t.ink2 : t.teal),
            // The drop-off pin only comes up to full strength once the rider is
            // actually on their way to it, and greys out with the rest of the
            // map if the trip is called off.
            endMarker: Opacity(
              opacity: _step >= 2 && !_cancelled ? 1 : 0.45,
              child: MapPin(size: 24, color: _cancelled ? t.ink2 : t.amber),
            ),
            progress: _mapProgress(t),
            overlays: [
              PositionedDirectional(
                end: 16,
                top: MediaQuery.of(context).padding.top + 10,
                child: _etaCard(t),
              ),
              PositionedDirectional(
                start: 16,
                top: MediaQuery.of(context).padding.top + 10,
                child: _refreshButton(t),
              ),
            ],
          ),
        ),
        MapSheet(
          padding: const EdgeInsets.fromLTRB(20, 14, 20, 18),
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            TripStepper(
                steps: _stepKeys.map(context.tr).toList(growable: false), current: _step),
            const SizedBox(height: 14),
            Divider(height: 1, thickness: 1, color: t.border),
            const SizedBox(height: 14),
            Row(children: [
              AvatarBadge(initialsOf(trip.driverName), size: 50, tint: t.tealTint, fg: t.tealInk),
              const SizedBox(width: 13),
              Expanded(
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(trip.driverName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15, color: t.ink)),
                  const SizedBox(height: 2),
                  Text(car.isEmpty ? trip.statusLabel : car,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: WanesTheme.mono(
                          size: 11.5, weight: FontWeight.w500, color: t.ink2, spacing: 0.4)),
                ]),
              ),
              // Only while the seat is live — a finished booking is no longer a
              // reason to ring the driver, and carries no number to ring.
              if (widget.booking.isLive) ...[
                const SizedBox(width: 8),
                Tooltip(
                  message: context.tr('trip.callDriver'),
                  child: RoundAction(
                      icon: Icons.call_outlined, filled: true, onTap: _callDriver),
                ),
              ],
            ]),
            const SizedBox(height: 14),
            if (_completed)
              PrimaryButton(
                label: context.tr('rate.rateYourTrip'),
                arrow: false,
                onPressed: () => Navigator.pushReplacement(
                  context,
                  MaterialPageRoute(
                    builder: (_) => RateScreen(
                      bookingId: widget.booking.id,
                      driverName: trip.driverName,
                      trip: trip,
                    ),
                  ),
                ),
              )
            else
              SizedBox(
                width: double.infinity,
                height: 46,
                child: OutlinedButton(
                  onPressed: () => Navigator.pop(context),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: t.amberInk,
                    side: BorderSide(color: t.border),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                  ),
                  child: Text(context.tr('trip.cancelTrip'),
                      style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14)),
                ),
              ),
          ]),
        ),
      ]),
    );
  }

  /// Where the car sits on the route for the stage the trip has reached, and
  /// how the two legs either side of it read.
  ///
  /// Before pickup the whole leg down to the rider's dot is dashed — it is the
  /// bit still to happen — and the ride beyond it is faint context. Once the
  /// trip starts the legs swap over: covered road solid behind the car, the
  /// drop-off dashed ahead. A cancelled trip fades both.
  MapProgress _mapProgress(WanesTokens t) {
    final marker = _carMarker(t);
    final at = _positionAlongRoute;
    if (_cancelled) {
      return MapProgress(
          at: at, behind: MapLegStyle.faint, ahead: MapLegStyle.faint, marker: marker);
    }
    return MapProgress(
      at: at,
      behind: _step == 0 ? MapLegStyle.dashed : MapLegStyle.solid,
      ahead: switch (_step) {
        2 => MapLegStyle.dashed,
        3 => MapLegStyle.solid,
        _ => MapLegStyle.faint,
      },
      marker: marker,
    );
  }

  /// Where the car sits, 0 -> 1 along the route.
  ///
  /// A real driver fix is projected onto the route and wins outright — the car
  /// then moves because the driver moved. Without one, each stage is measured
  /// against a real clock rather than parked on a constant: the approach counts
  /// down to the pickup time, and the journey runs from the moment the driver
  /// actually started.
  double get _positionAlongRoute {
    final fix = _driverAt;
    if (fix != null) return _projectOntoRoute(fix.lat, fix.lng);
    return switch (_step) {
      0 => _approach,
      1 => 0.0,
      2 => _underWayEstimate,
      // Just short of the pin, so the car parks beside the drop-off rather
      // than underneath it.
      _ => 0.94,
    };
  }

  /// The driver's real position as a fraction along the origin -> destination
  /// line: its scalar projection onto that vector, clamped to the segment.
  /// Longitude is scaled by cos(lat) so a degree east counts for what it is
  /// actually worth at this latitude.
  double _projectOntoRoute(double lat, double lng) {
    final trip = _trip;
    final k = math.cos(trip.originLat * math.pi / 180).abs().clamp(0.01, 1.0);
    final rx = (trip.destinationLng - trip.originLng) * k;
    final ry = trip.destinationLat - trip.originLat;
    final len2 = rx * rx + ry * ry;
    if (len2 == 0) return 0;
    final px = (lng - trip.originLng) * k;
    final py = lat - trip.originLat;
    return ((px * rx + py * ry) / len2).clamp(0.0, 1.0);
  }

  /// Straight-line kilometres end to end — enough to estimate how long the leg
  /// takes when nobody is reporting a position.
  double get _routeKm {
    final trip = _trip;
    const kmPerDegree = 111.0;
    final k = math.cos(trip.originLat * math.pi / 180).abs().clamp(0.01, 1.0);
    final dx = (trip.destinationLng - trip.originLng) * k * kmPerDegree;
    final dy = (trip.destinationLat - trip.originLat) * kmPerDegree;
    return math.sqrt(dx * dx + dy * dy);
  }

  /// How far through the journey we are, from when the driver actually started.
  /// With no start time there is nothing honest to measure against, so the car
  /// waits at the pickup end rather than sitting somewhere invented.
  double get _underWayEstimate {
    final started = _trip.startedAt;
    if (started == null) return 0.0;
    final expected = _routeKm * _minutesPerKm;
    if (expected <= 0) return 0.0;
    final elapsed = DateTime.now().toUtc().difference(started.toUtc()).inSeconds / 60.0;
    return (elapsed / expected).clamp(0.0, _underWayCap);
  }

  /// How far along the route the driver still is, from the same countdown the
  /// ETA card shows, so the marker and the number never disagree. It stops
  /// short of the dot: only the driver's own "arrived" puts the car on it.
  double get _approach {
    final minutes = (_minutesToPickup ?? 0).clamp(0, _approachWindow);
    return _approachTo + (_approachFrom - _approachTo) * (minutes / _approachWindow);
  }

  /// The car marker — the prototype's bobbing badge, ringed with
  /// `@keyframes waneping` while the driver waits at the pickup, and greyed on
  /// a cancelled trip.
  Widget _carMarker(WanesTokens t) {
    final car = AnimatedBuilder(
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
        child: Icon(Icons.directions_car_rounded,
            size: 22, color: _cancelled ? t.ink2 : t.teal),
      ),
    );
    if (_step != 1 || _cancelled) return car;
    return PingRings(size: 80, color: t.teal, child: car);
  }

  /// Asks the server where the trip has got to. The rail lives on SSE, so this
  /// is the rider's recourse when that connection has quietly dropped.
  Widget _refreshButton(WanesTokens t) {
    if (_refreshing) {
      return Container(
        width: 44,
        height: 44,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: t.surface2,
          shape: BoxShape.circle,
          border: Border.all(color: t.border),
        ),
        child: SizedBox(
          width: 18,
          height: 18,
          child: CircularProgressIndicator(strokeWidth: 2, color: t.tealInk),
        ),
      );
    }
    return Tooltip(
      message: context.tr('common.refresh'),
      child: RoundAction(
          icon: Icons.refresh_rounded, onTap: () => _refreshStatus(manual: true)),
    );
  }

  Widget _etaCard(WanesTokens t) {
    final minutes = _minutesToPickup;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: t.border),
        boxShadow: [
          BoxShadow(color: t.shadow, blurRadius: 14, offset: const Offset(0, 4), spreadRadius: -6),
        ],
      ),
      child: Column(mainAxisSize: MainAxisSize.min, children: [
        Text(
            minutes == null
                ? context.tr(_stepKeys[_step]).replaceAll('\n', ' ')
                : context.tr('units.minutes', {'value': minutes}),
            textAlign: TextAlign.center,
            style: WanesTheme.mono(size: 18, weight: FontWeight.w800, color: t.tealInk, spacing: 0)),
        Text(context.tr(_captionKey), style: TextStyle(fontSize: 10, color: t.ink2)),
      ]),
    );
  }

  /// The line under the headline: what is actually happening, never a guess.
  String get _captionKey {
    if (_cancelled) return 'tripStatus.cancelled';
    return switch (_step) {
      0 => 'trip.toPickup',
      1 => 'trip.atPickup',
      2 => 'trip.toDropoff',
      _ => 'trip.complete',
    };
  }
}
