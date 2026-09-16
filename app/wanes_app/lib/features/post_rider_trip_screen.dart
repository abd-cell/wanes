import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;
import '../core/app_config.dart';
import '../core/fare.dart';
import '../core/geo.dart';
import '../core/l10n.dart';
import '../core/places.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/conditions_card.dart';
import '../widgets/place_picker.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';
import '../widgets/when_picker.dart';
import 'schedule_form_screen.dart';
import 'schedules_screen.dart';
import 'searching_screen.dart';

/// Post a trip — the rider's side of the board.
///
/// The counterpart of the driver's post screen, minus a price: the rider states
/// the need and a driver names the figure. What replaces it is the departure
/// rule, which is the one thing here that can refuse the form: a driver has to
/// gather everybody before they can run the leg, so the earliest departure
/// scales with the seats asked for. That is computed here as the rider changes
/// the seats — being told "not before 08:40" while you are choosing is help;
/// being refused after you tap Post is a rebuke.
class PostRiderTripScreen extends StatefulWidget {
  const PostRiderTripScreen({
    super.key,
    this.from,
    this.to,
    this.seats = 1,
    this.when,
    this.nearby = true,
    this.driverGenderPolicy = GenderPolicy.any,
  });

  /// Pre-filled from the search the rider just ran, when they got here from an
  /// empty results screen.
  final Place? from;
  final Place? to;
  final int seats;
  final DateTime? when;
  final bool nearby;

  /// Who they told the search may drive them. Carried in rather than read off
  /// the account, which no longer stores it: the answer belongs to the journey,
  /// and the rider gave it a screen ago.
  final GenderPolicy driverGenderPolicy;

  @override
  State<PostRiderTripScreen> createState() => _PostRiderTripScreenState();
}

class _PostRiderTripScreenState extends State<PostRiderTripScreen> {
  final _riderTrips = RiderTripService();

  Place? _from;
  Place? _to;
  late int _seats = widget.seats.clamp(1, 8);
  late bool _nearby = widget.nearby;
  late DateTime _departAt;

  late GenderPolicy _driverPolicy = widget.driverGenderPolicy;
  GenderPolicy _coRiderPolicy = GenderPolicy.any;
  int? _minAge;
  int? _maxAge;

  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _from = widget.from;
    _to = widget.to;

    // Who may drive comes in from the search; who else may be aboard starts
    // open. Both used to be read from account defaults, which meant a rider
    // could carry a condition they set months ago onto a posting without
    // noticing it was still on.
    final wanted = widget.when?.toLocal();
    _departAt = wanted != null && wanted.isAfter(_earliest) ? wanted : _earliest;
  }

  /// The earliest departure this posting may name, for the seats and distance
  /// as they stand. Mirrors the server's rule, so the two cannot disagree at
  /// the tap.
  DateTime get _earliest {
    final config = AppConfigController.value;
    final from = _from;
    final to = _to;
    final km = from == null || to == null
        ? 0.0
        : Geo.distanceKm(from.lat, from.lng, to.lat, to.lng);
    return config.earliestDeparture(km, _seats);
  }

  /// What a driver is likely to ask for a seat. A hint, not a commitment —
  /// there is no price on a posting — but a rider deciding whether to post at
  /// all deserves to know roughly what they are asking for.
  double? get _estimate {
    final from = _from;
    final to = _to;
    if (from == null || to == null) return null;
    return Fare.estimateBetween(from.lat, from.lng, to.lat, to.lng);
  }

  void _setSeats(int seats) {
    setState(() {
      _seats = seats;
      // More seats means more gathering, so a departure that was fine for one
      // may now be too soon. Pushed out rather than left to be refused.
      if (_departAt.isBefore(_earliest)) _departAt = _earliest;
    });
  }

  Future<void> _pickPlace(bool isFrom) async {
    final picked = await showPlacePicker(
      context,
      title: context.tr(isFrom ? 'home.pickup' : 'home.destination'),
      hint: context.tr(isFrom ? 'home.searchPickupHint' : 'home.searchDestinationHint'),
    );
    if (picked == null || !mounted) return;
    setState(() {
      if (isFrom) {
        _from = picked;
      } else {
        _to = picked;
      }
      if (_departAt.isBefore(_earliest)) _departAt = _earliest;
    });
  }

  Future<void> _pickDeparture() async {
    // The lead goes into the picker rather than being enforced after it: the
    // sheet greys out everything too soon, so a rider sees why the early slots
    // are closed instead of watching their choice snap forward.
    final earliest = _earliest;
    final sel = await showWhenPicker(context, _departAt, earliest: earliest);
    if (sel == null || !mounted) return;

    // "Now" still resolves to an instant, and the seats or the route may have
    // changed while the sheet was open, so the floor is applied once more here.
    final chosen = sel.dateTime ?? DateTime.now();
    setState(() => _departAt = chosen.isBefore(earliest) ? earliest : chosen);
  }

  Future<void> _post() async {
    final from = _from;
    final to = _to;
    if (from == null || to == null) {
      WanesAlerts.warning(context, context.tr(from == null ? 'home.pickOrigin' : 'home.pickDestination'));
      return;
    }
    if (from.key == to.key) {
      WanesAlerts.warning(context, context.tr('home.pickTwoPlaces'));
      return;
    }

    setState(() => _busy = true);
    final res = await _riderTrips.create(
      originLat: from.lat,
      originLng: from.lng,
      originAddress: from.name,
      destLat: to.lat,
      destLng: to.lng,
      destAddress: to.name,
      departAt: _departAt,
      seats: _seats,
      nearby: _nearby,
      driverGenderPolicy: _driverPolicy,
      coRiderGenderPolicy: _coRiderPolicy,
      minAge: _minAge,
      maxAge: _maxAge,
    );
    if (!mounted) return;
    setState(() => _busy = false);

    final posted = res.data;
    if (!res.success || posted == null) {
      WanesAlerts.failure(context, res, title: context.tr('riderTrip.postFailed'), onRetry: _post);
      return;
    }

    // Straight onto the waiting screen: the posting is live and a driver can
    // claim it from this moment, so the rider should be watching that rather
    // than reading a confirmation.
    Navigator.pushReplacement(
      context,
      MaterialPageRoute(
        builder: (_) => SearchingScreen(
          riderTripId: posted.id,
          departAt: posted.departAt,
          seats: posted.seatsWanted,
          originLat: from.lat,
          originLng: from.lng,
          destLat: to.lat,
          destLng: to.lng,
        ),
      ),
    );
  }

  String _timeLabel(DateTime value) {
    final local = value.toLocal();
    final today = DateTime.now();
    final day = DateTime(local.year, local.month, local.day);
    final diff = day.difference(DateTime(today.year, today.month, today.day)).inDays;
    final locale = context.l10n.localeName;
    final prefix = switch (diff) {
      0 => context.tr('common.today'),
      1 => context.tr('common.tomorrow'),
      _ => DateFormat('EEE d MMM', locale).format(local),
    };
    return '$prefix · ${DateFormat('HH:mm', locale).format(local)}';
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final estimate = _estimate;

    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        bottom: false,
        child: Column(children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 8, 20, 0),
            child: ScreenHeader(title: context.tr('riderTrip.post')),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
              children: [
                _routeCard(t),
                const SizedBox(height: 12),
                _whenAndSeats(t),
                const SizedBox(height: 12),
                _reachCard(t),
                const SizedBox(height: 16),
                ConditionsCard(
                  driverPolicy: _driverPolicy,
                  onDriverPolicy: (p) => setState(() => _driverPolicy = p),
                  coRiderPolicy: _coRiderPolicy,
                  onCoRiderPolicy: (p) => setState(() => _coRiderPolicy = p),
                  coRiderTitleKey: 'conditions.coRiders',
                  minAge: _minAge,
                  maxAge: _maxAge,
                  onAges: (min, max) => setState(() {
                    _minAge = min;
                    _maxAge = max;
                  }),
                ),
                const SizedBox(height: 12),
                _repeatCard(t),
                if (estimate != null) ...[
                  const SizedBox(height: 16),
                  _estimateNote(t, estimate),
                ],
              ],
            ),
          ),
          _bottomBar(t),
        ]),
      ),
    );
  }

  /// "Every weekday?" — the same offer the driver's form makes, because a
  /// rider's commute repeats for exactly the same reason a driver's does.
  ///
  /// It used to live on the profile page, two taps from anywhere and nowhere
  /// near the thing it repeats. Here the route, the hour and the seats are
  /// already on screen, so turning this posting into a standing one costs one
  /// tap and no retyping.
  Widget _repeatCard(WanesTokens t) => Container(
        padding: const EdgeInsets.fromLTRB(14, 10, 8, 10),
        decoration: BoxDecoration(
          color: t.surface2,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: t.border),
        ),
        child: Row(children: [
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(context.tr('riderTrip.repeat'),
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
              const SizedBox(height: 1),
              Text(context.tr('riderTrip.repeatBody'),
                  style: TextStyle(fontSize: 12, color: t.ink2)),
            ]),
          ),
          // Two verbs, because they are two different errands. "Add" seeds a
          // schedule from what is on this form; "my repeats" is where the ones
          // already running are paused or dropped — the only way to them now
          // that they have left the profile page.
          TextButton(
            onPressed: _openSchedules,
            child: Text(context.tr('schedule.mine'),
                style: TextStyle(fontWeight: FontWeight.w600, fontSize: 12.5, color: t.ink2)),
          ),
          TextButton(
            onPressed: _openSchedule,
            child: Text(context.tr('schedule.add'),
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 12.5, color: t.tealInk)),
          ),
        ]),
      );

  Future<void> _openSchedules() async {
    await Navigator.push(
      context,
      MaterialPageRoute(builder: (_) => const SchedulesScreen()),
    );
  }

  /// Turns what is on this form into a standing posting. The route, the hour,
  /// the seats and both conditions come across, so "every weekday" is one
  /// screen rather than a second bout of typing.
  Future<void> _openSchedule() async {
    await Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => ScheduleFormScreen(
          from: _from,
          to: _to,
          seats: _seats,
          timeOfDay: TimeOfDayValue(_departAt.hour, _departAt.minute),
          genderPolicy: _driverPolicy,
          coRiderGenderPolicy: _coRiderPolicy,
          minAge: _minAge,
          maxAge: _maxAge,
        ),
      ),
    );
  }

  Widget _routeCard(WanesTokens t) => GroupedCard(children: [
        GroupedRow(
          icon: Icons.trip_origin,
          title: context.tr('home.pickup'),
          subtitle: _from?.name ?? context.tr('home.searchPickupHint'),
          iconColor: t.teal,
          onTap: () => _pickPlace(true),
        ),
        GroupedRow(
          icon: Icons.place_outlined,
          title: context.tr('home.destination'),
          subtitle: _to?.name ?? context.tr('home.searchDestinationHint'),
          iconColor: t.amberInk,
          onTap: () => _pickPlace(false),
        ),
      ]);

  Widget _whenAndSeats(WanesTokens t) => GroupedCard(children: [
        GroupedRow(
          icon: Icons.schedule_rounded,
          title: context.tr('common.when'),
          subtitle: _timeLabel(_departAt),
          onTap: _pickDeparture,
        ),
        GroupedRow(
          icon: Icons.event_seat_outlined,
          title: context.tr('common.seats'),
          // The rule that bites, stated where it is decided rather than at the
          // tap: more seats means more gathering, and the earliest departure
          // moves with them.
          subtitle: context.tr('riderTrip.earliest', {'time': _timeLabel(_earliest)}),
          trailing: SeatStepper(value: _seats, onChanged: _setSeats, max: 8),
        ),
      ]);

  Widget _reachCard(WanesTokens t) => GroupedCard(children: [
        GroupedRow(
          icon: Icons.my_location_rounded,
          title: context.tr('common.reach'),
          subtitle: context.tr(_nearby ? 'riderTrip.reachNear' : 'riderTrip.reachWide'),
          trailing: WanesPillSwitch(
            value: _nearby,
            onChanged: (value) => setState(() => _nearby = value),
          ),
        ),
      ]);

  Widget _estimateNote(WanesTokens t, double estimate) => Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: t.surface2,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: t.border),
        ),
        child: Row(children: [
          Icon(Icons.info_outline_rounded, size: 18, color: t.ink2),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              context.tr('riderTrip.priceNote', {'amount': Fare.format(estimate)}),
              style: TextStyle(fontSize: 12, height: 1.45, color: t.ink2),
            ),
          ),
        ]),
      );

  Widget _bottomBar(WanesTokens t) => Container(
        padding: EdgeInsets.fromLTRB(20, 12, 20, 12 + MediaQuery.of(context).padding.bottom),
        decoration: BoxDecoration(
          color: t.surface,
          border: Border(top: BorderSide(color: t.border)),
        ),
        child: PrimaryButton(
          label: context.tr('riderTrip.postCta'),
          busy: _busy,
          onPressed: _busy ? null : _post,
        ),
      );
}
