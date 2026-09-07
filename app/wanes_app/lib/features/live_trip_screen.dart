import 'dart:async';
import 'dart:math' as math;
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:url_launcher/url_launcher.dart';
import '../core/l10n.dart';
import '../core/sse_client.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';
import 'rate_screen.dart';

/// Active trip — prototype screen 06, drawn as the prototype draws it: a
/// `surface-2` panel carrying the TRIP STATUS headline and its ETA card, the
/// vertical stage rail beneath, and the driver sheet pinned to the bottom.
///
/// The rail is driven by the trip's real status, not a timer: the driver moves
/// the trip Posted → Arrived → Active → Completed from their own app, and each
/// move reaches this screen as an SSE notification. The status the screen opens
/// on is re-read from the server, because the [Trip] handed over by the booking
/// list can be minutes stale.
///
/// SSE is the only thing that advances the rail, and a dropped stream is
/// invisible from here — so the panel pulls to refresh. The prototype has no
/// refresh control to copy, and a gesture costs the design nothing.
class LiveTripScreen extends StatefulWidget {
  const LiveTripScreen({super.key, required this.trip, required this.booking});
  final Trip trip;
  final Booking booking;

  @override
  State<LiveTripScreen> createState() => _LiveTripScreenState();
}

class _LiveTripScreenState extends State<LiveTripScreen> {
  /// The headline over the rail, per stage.
  static const _headKeys = [
    'trip.headOnTheWay',
    'trip.headArrived',
    'trip.headInTrip',
    'trip.headDone',
  ];

  /// Trip status → rail stage. Posted and Full both mean "not here yet", and a
  /// cancelled trip has no stage of its own — [_cancelled] carries that.
  ///
  /// The trip is the fallback, not the truth: it summarises every seat on it, so
  /// on a carpool it can already say "under way" while this rider is still
  /// waiting at the curb. [_stageOfSeat] is what actually describes *them*.
  /// EnRoute sits at stage 0 with Posted and Full: the rail's first headline is
  /// already "on the way", and it is the driver who has moved, not this rider's
  /// seat — they are still at the kerb until the car reaches them.
  static const _stageOf = {1: 0, 2: 0, 7: 0, 6: 1, 3: 2, 4: 3};

  /// This rider's own `BookingStatus` → rail stage: the driver moves each seat
  /// separately (reached → aboard → dropped off).
  static const _stageOfSeat = {1: 0, 2: 0, 6: 1, 3: 2, 4: 3};

  /// The SSE notification types that move this trip along.
  static const _stageOfEvent = {
    'DriverArrived': 1,
    'TripStarted': 2,
    'TripCompleted': 3,
  };

  final _trips = TripService();
  final _bookings = BookingService();
  final _sse = SseClient();
  StreamSubscription<Map<String, dynamic>>? _sseSub;

  /// Ticks the countdowns; the stage itself never moves on a timer.
  Timer? _clock;

  /// How long a city leg is assumed to take per kilometre, for the stretch
  /// between pickup and drop-off. Only ever labelled as an estimate — nothing
  /// in the API carries a real journey time. ~24 km/h, an ordinary urban
  /// average.
  static const _minutesPerKm = 2.5;

  /// This rider's stage: their own seat first, the trip only as a fallback.
  late int _step = _stageOfSeat[widget.booking.status] ??
      _stageOf[widget.trip.status] ??
      0;
  bool _cancelled = false;

  /// The driver waited and left without this rider. Ends the rail like a
  /// cancellation does, but says so in its own words.
  bool _noShow = false;

  /// The rider's ride is over either way — the driver called it off, or left
  /// without them. Everything that greys the rail reads this; only the headline
  /// distinguishes the two.
  bool get _ended => _cancelled || _noShow;

  /// The trip as the server last described it — carries `startedAt`, which the
  /// drop-off countdown is measured from. Starts as what we were handed.
  late Trip _trip = widget.trip;

  /// This rider's own seat as the server last described it. On a carpool the
  /// driver moves each rider separately, so the seat — not the trip — is what
  /// says where *this* rider has got to. It also carries the driver's number
  /// for the call button.
  late Booking _booking = widget.booking;

  @override
  void initState() {
    super.initState();
    _cancelled = widget.trip.status == 5;
    _noShow = widget.booking.isNoShow;
    _clock = Timer.periodic(const Duration(seconds: 5), (_) {
      if (!mounted) return;
      // Only the countdowns move on this; once there is nothing left to count
      // down to, the screen is static until an event arrives.
      if (_completed || _ended) {
        _clock?.cancel();
        return;
      }
      setState(() {});
    });
    _refreshStatus();
    _listen();
  }

  /// The trip and seat we were handed came from a list that may have been
  /// loaded a while ago. Re-read both so the rail opens on the real stage even
  /// if every SSE event was missed (app backgrounded, phone offline).
  ///
  /// The seat is what the rail follows, so it is re-read too — the driver moves
  /// each rider separately, and this rider's own progress is not something the
  /// trip's status can tell us on a carpool.
  ///
  /// Also the panel's pull-to-refresh ([manual]), which is the rider's way out
  /// of exactly that: a dropped SSE connection is invisible from here, so
  /// asking again is worth a gesture — and worth saying so when the ask fails.
  Future<void> _refreshStatus({bool manual = false}) async {
    final res = await _trips.get(widget.trip.id);
    final seats = await _bookings.myBookings();
    final seat = seats.data?.where((b) => b.id == widget.booking.id).firstOrNull;
    final trip = res.data;
    if (!mounted) return;
    setState(() {
      if (seat != null) {
        _booking = seat;
        _cancelled = _cancelled || seat.isCancelled;
        _noShow = _noShow || seat.isNoShow;
      }
      if (trip != null) {
        _trip = trip;
        _cancelled = _cancelled || trip.status == 5;
      }
      // Only ever forward: a stale read must not walk the rail backwards past
      // an event that already arrived. The seat leads, the trip stands in for it
      // when this rider's own row could not be read.
      final stage = seat != null
          ? _stageOfSeat[seat.status] ?? 0
          : _stageOf[trip?.status ?? 0] ?? 0;
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

      // The seat, not the trip: the driver waited at the pickup and left. Sent
      // to this rider alone, and it ends their rail where it stands.
      final bookingId = data is Map<String, dynamic> ? data['bookingId'] as int? : null;
      if (type == 'BookingCancelled' && bookingId == widget.booking.id) {
        setState(() => _noShow = true);
        WanesAlerts.info(context, context.tr('trip.markedNoShow'),
            message: context.tr('trip.markedNoShowBody'));
        return;
      }

      final stage = _stageOfEvent[type];
      if (stage != null && stage > _step) setState(() => _step = stage);
    });
    await _sse.connect();
  }

  @override
  void dispose() {
    _clock?.cancel();
    _sseSub?.cancel();
    _sse.dispose();
    super.dispose();
  }

  bool get _completed => _step >= _headKeys.length - 1;

  /// Minutes until the driver is due, from the trip's own departure time. Null
  /// once the trip is under way, when there is nothing left to count down to.
  int? get _minutesToPickup {
    if (_step >= 2) return null;
    final left = widget.trip.departAt.difference(DateTime.now()).inMinutes;
    return left < 0 ? 0 : left;
  }

  /// Straight-line kilometres end to end — enough to estimate how long the leg
  /// between pickup and drop-off takes. Nothing in the API carries a routed
  /// duration, so every number derived from this is labelled an estimate.
  double get _routeKm {
    final trip = _trip;
    const kmPerDegree = 111.0;
    final k = math.cos(trip.originLat * math.pi / 180).abs().clamp(0.01, 1.0);
    final dx = (trip.destinationLng - trip.originLng) * k * kmPerDegree;
    final dy = (trip.destinationLat - trip.originLat) * kmPerDegree;
    return math.sqrt(dx * dx + dy * dy);
  }

  /// How long the ride itself is reckoned to take, or null when the trip has no
  /// usable coordinates to measure between.
  int? get _rideMinutes {
    final estimate = _routeKm * _minutesPerKm;
    return estimate <= 0 ? null : estimate.round();
  }

  /// Minutes still to run once the driver has started. Falls back to the whole
  /// ride while there is no start time to count from.
  int? get _minutesToDropoff {
    final whole = _rideMinutes;
    if (whole == null) return null;
    final started = _trip.startedAt;
    if (started == null) return whole;
    final elapsed = DateTime.now().toUtc().difference(started.toUtc()).inMinutes;
    final left = whole - elapsed;
    return left < 0 ? 0 : left;
  }

  /// The number on the ETA card: minutes to the pickup while the driver is
  /// coming, minutes to the drop-off once the ride is under way. Null the rest
  /// of the time — a driver already standing at the pickup is not an ETA, and
  /// the headline says so on its own.
  int? get _etaMinutes {
    if (_ended || _completed) return null;
    return switch (_step) {
      0 => _minutesToPickup,
      2 => _minutesToDropoff,
      _ => null,
    };
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
    final phone = _booking.driverPhone;
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

    return Scaffold(
      backgroundColor: t.bg,
      body: Column(children: [
        Expanded(
          child: Container(
            width: double.infinity,
            color: t.surface2,
            child: SafeArea(
              bottom: false,
              child: RefreshIndicator(
                color: t.tealInk,
                backgroundColor: t.surface,
                onRefresh: () => _refreshStatus(manual: true),
                child: ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.fromLTRB(22, 18, 22, 24),
                  children: [
                    _header(t),
                    const SizedBox(height: 22),
                    TripTimeline(stages: _stages(), current: _step + 1, muted: _ended),
                  ],
                ),
              ),
            ),
          ),
        ),
        _driverSheet(t),
      ]),
    );
  }

  /// TRIP STATUS · the headline · the ETA card, exactly the prototype's row.
  Widget _header(WanesTokens t) {
    final eta = _etaMinutes;
    return Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Expanded(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(
            context.tr('trip.statusLabel'),
            style: WanesTheme.mono(size: 10, weight: FontWeight.w600, color: t.ink2, spacing: 1),
          ),
          const SizedBox(height: 3),
          Text(
            context.tr(_headKey),
            style: TextStyle(
              fontWeight: FontWeight.w800,
              fontSize: 19,
              height: 1.2,
              letterSpacing: -0.38,
              color: t.ink,
            ),
          ),
        ]),
      ),
      if (eta != null) ...[
        const SizedBox(width: 12),
        _etaCard(t, eta),
      ],
    ]);
  }

  Widget _etaCard(WanesTokens t, int minutes) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: t.border),
        boxShadow: [
          BoxShadow(color: t.shadow, blurRadius: 14, offset: const Offset(0, 4), spreadRadius: -8),
        ],
      ),
      child: Column(mainAxisSize: MainAxisSize.min, children: [
        Text(
          context.tr('units.minutes', {'value': minutes}),
          textAlign: TextAlign.center,
          style: WanesTheme.mono(size: 18, weight: FontWeight.w800, color: t.tealInk, spacing: 0),
        ),
        Text(context.tr('trip.eta'), style: TextStyle(fontSize: 10, color: t.ink2)),
      ]),
    );
  }

  /// The headline: what is actually happening, never a guess.
  String get _headKey {
    if (_noShow) return 'trip.markedNoShow';
    if (_cancelled) return 'trip.tripCancelled';
    return _headKeys[_step];
  }

  /// The five rows of the rail. The booking itself is the first one — it is
  /// already true the moment this screen can be reached — and the app's four
  /// real stages follow, so the rail reads as the seat's own history.
  List<TripStage> _stages() {
    final trip = _trip;
    final locale = context.l10n.localeName;
    final toPickup = _minutesToPickup;
    final ride = _rideMinutes;

    final pickupDetail = [
      if (trip.originAddress.isNotEmpty) trip.originAddress,
      if (toPickup != null && _step == 0) context.tr('units.minutes', {'value': toPickup}),
    ].join(' · ');

    // "Est. 24 min · arrive 08:39" — the arrival is the departure plus the
    // estimated ride, or the real start plus it once the driver has set off.
    String? rideDetail;
    if (ride != null) {
      final from = trip.startedAt?.toLocal() ?? trip.departAt.toLocal();
      rideDetail = context.tr('trip.estArrive', {
        'minutes': context.tr('units.minutes', {'value': ride}),
        'time': DateFormat('HH:mm', locale).format(from.add(Duration(minutes: ride))),
      });
    }

    return [
      TripStage(
        context.tr('trip.railBooked'),
        context.trPlural('trip.seatWith', _booking.seats,
            {'count': _booking.seats, 'name': trip.driverName}),
      ),
      TripStage(
        context.tr('trip.railToPickup'),
        pickupDetail.isEmpty ? null : pickupDetail,
      ),
      TripStage(
        context.tr('trip.railAtPickup'),
        _step >= 1 ? context.tr('trip.waitingAtPickup') : null,
      ),
      TripStage(context.tr('trip.railInTrip'), rideDetail),
      TripStage(
        context.tr('trip.railCompleted'),
        trip.destinationAddress.isEmpty ? null : trip.destinationAddress,
      ),
    ];
  }

  /// The pinned sheet: who is driving, how to reach them, and the way out.
  Widget _driverSheet(WanesTokens t) {
    final trip = _trip;
    final car = [trip.vehicleLabel, trip.vehiclePlate].where((s) => s.isNotEmpty).join(' · ');

    return MapSheet(
      padding: const EdgeInsets.fromLTRB(20, 14, 20, 18),
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
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
          if (_booking.isLive) ...[
            const SizedBox(width: 8),
            Tooltip(
              message: context.tr('trip.callDriver'),
              child: RoundAction(icon: Icons.call_outlined, filled: true, onTap: _callDriver),
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
                  bookingId: _booking.id,
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
    );
  }
}
