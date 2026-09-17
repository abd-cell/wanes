import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;
import '../core/l10n.dart';
import '../core/places.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/conditions_card.dart';
import '../widgets/place_picker.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';

/// A repeating trip, from either side.
///
/// The form is deliberately the same for both: a schedule is a *generator*, and
/// what it generates — a trip the driver is offering, or a posting the rider
/// needs — differs in three fields (a car, a price, a seat threshold) and
/// nothing else. Two screens would have meant keeping the recurrence, the
/// route, the conditions and the preview in step twice.
///
/// Nothing here matches anything. The server unrolls the recurrence into
/// ordinary rows a fortnight at a time, and [TripSchedule.nextDepartures] is
/// the only honest preview of that: it is the generator run, not a description
/// of it.
class ScheduleFormScreen extends StatefulWidget {
  const ScheduleFormScreen({
    super.key,
    this.schedule,
    this.asDriver = false,
    this.from,
    this.to,
    this.seats,
    this.pricePerSeat,
    this.vehicleId,
    this.timeOfDay,
    this.genderPolicy,
    this.coRiderGenderPolicy,
    this.minAge,
    this.maxAge,
  });

  /// The schedule being edited, or null to write a new one.
  final TripSchedule? schedule;

  /// Which side is writing it. Sent explicitly rather than read off the active
  /// role: that is a mode people flip, and a driver browsing as a rider must
  /// not silently turn their commute into demand.
  final bool asDriver;

  /// Pre-filled from the trip the driver was already posting, so "repeat this"
  /// does not mean typing it all again.
  final Place? from;
  final Place? to;
  final int? seats;
  final double? pricePerSeat;
  final int? vehicleId;
  final TimeOfDayValue? timeOfDay;

  /// The conditions the trip being repeated already carried. "Repeat this" has
  /// to mean the whole thing: a schedule that quietly dropped who may drive, or
  /// who else may be aboard, would generate trips its owner never agreed to.
  final GenderPolicy? genderPolicy;
  final GenderPolicy? coRiderGenderPolicy;
  final int? minAge;
  final int? maxAge;

  @override
  State<ScheduleFormScreen> createState() => _ScheduleFormScreenState();
}

class _ScheduleFormScreenState extends State<ScheduleFormScreen> {
  final _schedules = ScheduleService();

  Place? _from;
  Place? _to;
  late bool _asDriver;
  Recurrence _recurrence = Recurrence.weekly;
  WeekDaySet _days = WeekDaySet.none;
  int _dayOfMonth = 1;
  late TimeOfDayValue _time;
  late DateTime _startDate;
  DateTime? _endDate;
  int _seats = 1;
  double? _price;
  int? _vehicleId;
  int _minSeats = 1;
  GenderPolicy _genderPolicy = GenderPolicy.any;
  GenderPolicy _coRiderPolicy = GenderPolicy.any;
  int? _minAge;
  int? _maxAge;
  bool _paused = false;
  bool _busy = false;

  TripSchedule? get _editing => widget.schedule;

  @override
  void initState() {
    super.initState();
    final existing = _editing;
    _asDriver = existing?.isDriverSchedule ?? widget.asDriver;

    if (existing != null) {
      _from = Place(existing.originAddress, 0, 0);
      _to = Place(existing.destinationAddress, 0, 0);
      _recurrence = existing.recurrence;
      _days = existing.daysOfWeek;
      _dayOfMonth = existing.dayOfMonth ?? 1;
      _time = existing.timeOfDay;
      _startDate = existing.startDate;
      _endDate = existing.endDate;
      _seats = existing.seats;
      _price = existing.pricePerSeat;
      _vehicleId = existing.vehicleId;
      _minSeats = existing.minSeatsToConfirm;
      _genderPolicy = existing.genderPolicy;
      _coRiderPolicy = existing.coRiderGenderPolicy;
      _minAge = existing.minAge;
      _maxAge = existing.maxAge;
      _paused = existing.isPaused;
      return;
    }

    _from = widget.from;
    _to = widget.to;
    _seats = widget.seats ?? 1;
    _price = widget.pricePerSeat;
    _vehicleId = widget.vehicleId;
    _time = widget.timeOfDay ?? const TimeOfDayValue(8, 0);
    _startDate = DateTime.now();
    _genderPolicy = widget.genderPolicy ?? GenderPolicy.any;
    _coRiderPolicy = widget.coRiderGenderPolicy ?? GenderPolicy.any;
    _minAge = widget.minAge;
    _maxAge = widget.maxAge;

    // A weekly schedule with no day chosen never fires, and the server refuses
    // it. Today is the least surprising place to start.
    _days = WeekDaySet(WeekDaySet.bitFor(DateTime.now().weekday));
  }

  /// Where the route came from, when it was pre-filled from a real place. An
  /// edited schedule only carries its addresses, so the picker is how the
  /// coordinates come back.
  bool get _hasCoordinates =>
      _from != null && _to != null && (_from!.lat != 0 || _from!.lng != 0);

  Future<void> _pickPlace(bool isFrom) async {
    final picked = await showPlacePicker(
      context,
      title: context.tr(isFrom ? 'home.pickup' : 'home.destination'),
      hint: context.tr(isFrom ? 'home.searchPickupHint' : 'home.searchDestinationHint'),
    );
    if (picked == null || !mounted) return;
    setState(() => isFrom ? _from = picked : _to = picked);
  }

  Future<void> _pickTime() async {
    final picked = await showTimePicker(
      context: context,
      initialTime: TimeOfDay(hour: _time.hour, minute: _time.minute),
    );
    if (picked == null || !mounted) return;
    setState(() => _time = TimeOfDayValue(picked.hour, picked.minute));
  }

  Future<void> _pickDate({required bool start}) async {
    final now = DateTime.now();

    // A schedule that ends before it begins produces no occurrences at all, so
    // the calendar closes those days rather than offering a range the server
    // would refuse.
    final floor = start ? now : (_startDate.isAfter(now) ? _startDate : now);
    final initial = start ? _startDate : (_endDate ?? _startDate);

    final picked = await showDatePicker(
      context: context,
      initialDate: initial.isBefore(floor) ? floor : initial,
      firstDate: floor,
      lastDate: now.add(const Duration(days: 365 * 2)),
    );
    if (picked == null || !mounted) return;
    setState(() {
      if (start) {
        _startDate = picked;
        // Moving the start past the end would leave the same empty range from
        // the other side. The end follows rather than silently disagreeing.
        if (_endDate != null && _endDate!.isBefore(picked)) _endDate = picked;
      } else {
        _endDate = picked;
      }
    });
  }

  Future<void> _save() async {
    final from = _from;
    final to = _to;
    if (from == null || to == null) {
      WanesAlerts.warning(context, context.tr(from == null ? 'home.pickOrigin' : 'home.pickDestination'));
      return;
    }
    if (!_hasCoordinates) {
      // An edited schedule arrives with addresses only. Rather than send zeroes
      // and quietly move somebody's commute to the Gulf of Guinea, ask for the
      // route again.
      WanesAlerts.warning(context, context.tr('schedule.pickRouteAgain'));
      return;
    }
    if (_recurrence == Recurrence.weekly && _days.isEmpty) {
      WanesAlerts.warning(context, context.tr('schedule.pickDays'));
      return;
    }

    setState(() => _busy = true);
    final res = await _schedules.save(
      id: _editing?.id,
      asDriver: _asDriver,
      originLat: from.lat,
      originLng: from.lng,
      originAddress: from.name,
      destLat: to.lat,
      destLng: to.lng,
      destAddress: to.name,
      recurrence: _recurrence,
      timeOfDay: _time,
      startDate: _startDate,
      daysOfWeek: _recurrence == Recurrence.weekly ? _days : WeekDaySet.none,
      dayOfMonth: _recurrence == Recurrence.monthly ? _dayOfMonth : null,
      // The device's own zone, so "the seven o'clock" stays the seven o'clock
      // wherever the server happens to run.
      timeZoneId: deviceZoneId(),
      endDate: _endDate,
      seats: _seats,
      pricePerSeat: _asDriver ? _price : null,
      vehicleId: _asDriver ? _vehicleId : null,
      minSeatsToConfirm: _asDriver ? _minSeats : 1,
      genderPolicy: _genderPolicy,
      // A driver sets one condition, on their riders; a rider sets two, and the
      // server reads the second only off a rider's schedule.
      coRiderGenderPolicy: _asDriver ? GenderPolicy.any : _coRiderPolicy,
      minAge: _minAge,
      maxAge: _maxAge,
      isPaused: _paused,
    );
    if (!mounted) return;
    setState(() => _busy = false);

    if (!res.success) {
      WanesAlerts.failure(context, res, title: context.tr('schedule.saveFailed'), onRetry: _save);
      return;
    }
    WanesAlerts.success(context, context.tr('schedule.saved'),
        message: context.tr('schedule.savedBody'));
    Navigator.pop(context, true);
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);

    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        bottom: false,
        child: Column(children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 8, 20, 0),
            child: ScreenHeader(
                title: context.tr(_editing == null ? 'schedule.add' : 'schedule.edit')),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
              children: [
                if (_editing == null) _sideToggle(t),
                if (_editing == null) const SizedBox(height: 12),
                _routeCard(t),
                const SizedBox(height: 12),
                _recurrenceCard(t),
                const SizedBox(height: 12),
                _windowCard(t),
                const SizedBox(height: 16),
                ConditionsCard(
                  // On a driver's schedule the single policy is the one on
                  // their passengers; on a rider's it is who may drive, and the
                  // co-rider row appears beside it.
                  coRiderPolicy: _asDriver ? _genderPolicy : _coRiderPolicy,
                  onCoRiderPolicy: (p) => setState(() {
                    if (_asDriver) {
                      _genderPolicy = p;
                    } else {
                      _coRiderPolicy = p;
                    }
                  }),
                  driverPolicy: _asDriver ? null : _genderPolicy,
                  onDriverPolicy:
                      _asDriver ? null : (p) => setState(() => _genderPolicy = p),
                  coRiderTitleKey:
                      _asDriver ? 'conditions.passengers' : 'conditions.coRiders',
                  minAge: _minAge,
                  maxAge: _maxAge,
                  onAges: (min, max) => setState(() {
                    _minAge = min;
                    _maxAge = max;
                  }),
                ),
                const SizedBox(height: 16),
                _previewCard(t),
              ],
            ),
          ),
          Container(
            padding: EdgeInsets.fromLTRB(20, 12, 20, 12 + MediaQuery.of(context).padding.bottom),
            decoration: BoxDecoration(
              color: t.surface,
              border: Border(top: BorderSide(color: t.border)),
            ),
            child: PrimaryButton(
              label: context.tr('common.save'),
              busy: _busy,
              onPressed: _busy ? null : _save,
            ),
          ),
        ]),
      ),
    );
  }

  /// Which side the schedule is for. Only on a new one: switching an existing
  /// schedule from supply to demand would orphan every row it has produced.
  Widget _sideToggle(WanesTokens t) => SegmentedToggle(
        labels: [context.tr('schedule.asRider'), context.tr('schedule.asDriver')],
        index: _asDriver ? 1 : 0,
        onSelect: (i) => setState(() => _asDriver = i == 1),
      );

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

  Widget _recurrenceCard(WanesTokens t) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SegmentedToggle(
            labels: Recurrence.values.map((r) => r.label).toList(),
            index: Recurrence.values.indexOf(_recurrence),
            onSelect: (i) => setState(() => _recurrence = Recurrence.values[i]),
          ),
          if (_recurrence == Recurrence.weekly) ...[
            const SizedBox(height: 12),
            _dayPicker(t),
          ],
          if (_recurrence == Recurrence.monthly) ...[
            const SizedBox(height: 12),
            GroupedCard(children: [
              GroupedRow(
                icon: Icons.calendar_month_outlined,
                title: context.tr('schedule.dayOfMonth'),
                // The 31st is a legitimate choice: the server runs it on the
                // last day of shorter months rather than skipping February.
                subtitle: '$_dayOfMonth',
                trailing: SeatStepper(
                  value: _dayOfMonth,
                  min: 1,
                  max: 31,
                  onChanged: (v) => setState(() => _dayOfMonth = v),
                ),
              ),
            ]),
          ],
        ],
      );

  /// Sun–Sat as seven toggles, in the local week's own order.
  Widget _dayPicker(WanesTokens t) {
    final locale = context.l10n.localeName;
    // DateTime.sunday is 7; the row reads Sunday-first, which is how the week
    // is written in the region this ships to.
    const order = [7, 1, 2, 3, 4, 5, 6];

    return Row(
      children: order.map((weekday) {
        final on = _days.has(weekday);
        final label = DateFormat('EEE', locale)
            .format(DateTime(2026, 9, 6).add(Duration(days: weekday % 7)));
        return Expanded(
          child: Padding(
            padding: const EdgeInsetsDirectional.only(end: 6),
            child: GestureDetector(
              onTap: () => setState(() => _days = _days.toggle(weekday)),
              child: Container(
                height: 42,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: on ? t.tealTint : t.surface,
                  borderRadius: BorderRadius.circular(10),
                  border: Border.all(color: on ? t.teal : t.border),
                ),
                child: Text(label.substring(0, label.length.clamp(0, 3)),
                    style: TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 11,
                        color: on ? t.tealInk : t.ink2)),
              ),
            ),
          ),
        );
      }).toList(),
    );
  }

  Widget _windowCard(WanesTokens t) => GroupedCard(children: [
        GroupedRow(
          icon: Icons.schedule_rounded,
          title: context.tr('schedule.time'),
          subtitle: _time.toString(),
          onTap: _pickTime,
        ),
        GroupedRow(
          icon: Icons.event_available_outlined,
          title: context.tr('schedule.starts'),
          subtitle: DateFormat('EEE d MMM', context.l10n.localeName).format(_startDate),
          onTap: () => _pickDate(start: true),
        ),
        GroupedRow(
          icon: Icons.event_busy_outlined,
          title: context.tr('schedule.ends'),
          subtitle: _endDate == null
              ? context.tr('schedule.noEnd')
              : DateFormat('EEE d MMM', context.l10n.localeName).format(_endDate!),
          trailing: _endDate == null
              ? Icon(Icons.chevron_right_rounded, size: 18, color: t.ink2)
              : IconButton(
                  icon: Icon(Icons.close_rounded, size: 18, color: t.ink2),
                  onPressed: () => setState(() => _endDate = null),
                ),
          onTap: () => _pickDate(start: false),
        ),
        GroupedRow(
          icon: Icons.event_seat_outlined,
          title: context.tr('common.seats'),
          subtitle: context.trPlural('vehicle.seatCount', _seats),
          trailing: SeatStepper(
            value: _seats,
            max: 8,
            onChanged: (v) => setState(() {
              _seats = v;
              if (_minSeats > v) _minSeats = v;
            }),
          ),
        ),
        if (_asDriver)
          GroupedRow(
            icon: Icons.groups_2_outlined,
            title: context.tr('trip.minSeats'),
            subtitle: _minSeats <= 1
                ? context.tr('trip.noThreshold')
                : context.trPlural('vehicle.seatCount', _minSeats),
            trailing: SeatStepper(
              value: _minSeats,
              max: _seats,
              onChanged: (v) => setState(() => _minSeats = v),
            ),
          ),
        GroupedRow(
          icon: Icons.pause_circle_outline_rounded,
          title: context.tr('schedule.paused'),
          subtitle: context.tr('schedule.pausedHint'),
          trailing: WanesPillSwitch(
            value: _paused,
            onChanged: (v) => setState(() => _paused = v),
          ),
        ),
      ]);

  /// What this schedule will actually produce, for an existing one.
  ///
  /// Only shown when the server has computed it: the app could unroll the
  /// recurrence itself, but then a disagreement between the two would be
  /// invisible — and the server's answer is the one that makes the trips.
  Widget _previewCard(WanesTokens t) {
    final next = _editing?.nextDepartures ?? const <DateTime>[];
    if (next.isEmpty) {
      return Text(context.tr('schedule.previewHint'),
          style: TextStyle(fontSize: 11.5, height: 1.45, color: t.ink2));
    }

    final locale = context.l10n.localeName;
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      SectionHeader(context.tr('schedule.next')),
      const SizedBox(height: 8),
      GroupedCard(
        children: next
            .take(4)
            .map((d) => GroupedRow(
                  icon: Icons.event_rounded,
                  title: DateFormat('EEE d MMM', locale).format(d.toLocal()),
                  subtitle: DateFormat('HH:mm', locale).format(d.toLocal()),
                  trailing: const SizedBox.shrink(),
                ))
            .toList(),
      ),
    ]);
  }
}
