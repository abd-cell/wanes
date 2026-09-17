import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

import '../../core/app_config.dart';
import '../../core/fare.dart';
import '../../core/l10n.dart';
import '../../core/places.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/place_picker.dart';
import '../../widgets/wanes_alerts.dart';
import '../../widgets/wanes_ui.dart';
import '../../widgets/when_picker.dart';
import '../../widgets/conditions_card.dart';
import '../../widgets/repeat_picker.dart';
import '../schedules_screen.dart';
import 'vehicles_screen.dart';
import '../../widgets/wanes_motion.dart';

/// Post a trip — prototype screen 09. Route card, DEPARTS / SEATS pair, the
/// price-per-seat hero with its suggestion chip, the weekday-repeat switch and
/// a pinned "Review & publish" bar.
///
/// Pass [trip] to reuse the same form for editing a trip the driver already
/// published — allowed while it is still posted and nobody has booked a seat.
class PostTripScreen extends StatefulWidget {
  const PostTripScreen({super.key, this.trip});

  final Trip? trip;

  @override
  State<PostTripScreen> createState() => _PostTripScreenState();
}

class _PostTripScreenState extends State<PostTripScreen> {
  final _vehicleService = VehicleService();
  final _trips = TripService();
  final _profiles = ProfileService();

  /// The departures this driver has already promised, so the picker can grey
  /// them out. Empty until it loads — a picker that opens before the answer
  /// arrives is better than one that will not open.
  DriverAvailability _availability = const DriverAvailability();

  List<Vehicle> _vehicles = [];
  Vehicle? _vehicle;
  /// Null until the driver searches the route out — nothing is pre-filled.
  Place? _from;
  Place? _to;
  int _seats = 3;
  late DateTime _departAt = _defaultDeparture();
  double _price = 5.0;

  /// Seats that must be taken before anybody is confirmed. 1 is no condition,
  /// which is the default: most trips run whoever turns up.
  /// Seeded from the marketplace default, not from 1 — a driver who never
  /// opens this control has not decided that one passenger is worth the run.
  /// Overwritten by the trip's own number when editing.
  int _minSeats = AppConfigController.value.minimumPassengersDefault;
  GenderPolicy _genderPolicy = GenderPolicy.any;
  int? _minAge;
  int? _maxAge;
  bool _loading = true;
  bool _busy = false;

  /// Once, or every week. Asked first, because it decides what this form
  /// makes: one trip, or a schedule that posts one for every chosen day.
  RepeatChoice _repeat = const RepeatChoice();

  static DateTime _defaultDeparture() {
    final n = DateTime.now().add(const Duration(hours: 1));
    return DateTime(n.year, n.month, n.day, n.hour, (n.minute ~/ 15) * 15);
  }

  /// The design's suggestion chip — our own distance-based estimate for one
  /// seat on this route, rounded to the nearest half unit of the currency.
  /// Falls back to the current price until both ends of the route are set.
  double get _suggestedPrice {
    final from = _from;
    final to = _to;
    if (from == null || to == null) return _price;
    final raw = Fare.estimateBetween(from.lat, from.lng, to.lat, to.lng);
    return (raw * 2).roundToDouble() / 2;
  }

  Trip? get _editing => widget.trip;

  @override
  void initState() {
    super.initState();
    final trip = _editing;
    if (trip != null) {
      _from = Place(trip.originAddress, trip.originLat, trip.originLng);
      _to = Place(trip.destinationAddress, trip.destinationLat, trip.destinationLng);
      _seats = trip.seatsTotal > 0 ? trip.seatsTotal : _seats;
      _departAt = trip.departAt.toLocal();
      _price = (trip.pricePerSeat ?? _price).clamp(1, 50).toDouble();
      _minSeats = trip.minSeatsToConfirm.clamp(1, _seats);
      _genderPolicy = trip.genderPolicy;
      _minAge = trip.minAge;
      _maxAge = trip.maxAge;
    }
    _loadVehicles();
    _loadAvailability();
  }

  Future<void> _loadAvailability() async {
    // The trip being edited must not block its own slot.
    final res = await _profiles.driverAvailability(ignoreTripId: _editing?.id);
    if (!mounted || !res.success || res.data == null) return;
    setState(() => _availability = res.data!);
  }

  Future<void> _loadVehicles() async {
    final res = await _vehicleService.myVehicles();
    if (!mounted) return;
    setState(() {
      _loading = false;
      _vehicles = res.data ?? [];
      final onTrip = _editing?.vehicleId;
      _vehicle = _vehicles.isEmpty
          ? null
          : _vehicles.firstWhere((v) => v.id == onTrip, orElse: () => _vehicles.first);
      if (_vehicle != null && _seats > _vehicle!.seatCapacity) {
        _seats = _vehicle!.seatCapacity;
      }
    });
  }

  Future<void> _pickPlace(bool isFrom) async {
    final picked = await showPlacePicker(
      context,
      title: context.tr(isFrom ? 'driver.startPoint' : 'home.destination'),
      hint: context.tr(isFrom ? 'driver.searchStartHint' : 'home.searchDestinationHint'),
    );
    if (picked == null || !mounted) return;
    setState(() => isFrom ? _from = picked : _to = picked);
  }

  Future<void> _pickDeparture() async {
    final sel = await showWhenPicker(
      context,
      _departAt,
      committed: _availability.committedDepartures,
      clashWindow: _availability.clashWindow,
      isEngaged: _availability.isEngaged,
    );
    if (sel == null || !mounted) return;
    setState(() => _departAt = sel.dateTime ?? DateTime.now());
  }

  Future<void> _post() async {
    if (_vehicle == null) return;
    final from = _from;
    final to = _to;
    if (from == null || to == null) {
      WanesAlerts.warning(context,
          context.tr(from == null ? 'driver.pickStart' : 'driver.pickEnd'));
      return;
    }
    if (from.key == to.key) {
      WanesAlerts.warning(context, context.tr('home.pickTwoPlaces'),
          message: context.tr('driver.samePointsBody'));
      return;
    }
    if (!_repeat.isValid) {
      WanesAlerts.warning(context, context.tr('schedule.pickDays'));
      return;
    }
    // A repeating run is a schedule: the server posts one trip per chosen day,
    // a fortnight ahead, and each is an ordinary trip riders book one day — or
    // every day — of.
    if (_repeat.repeat && _editing == null) return _postRepeating(from, to);

    final trip = _editing;
    setState(() => _busy = true);
    final res = trip == null
        ? await _trips.create(
            vehicleId: _vehicle!.id,
            originLat: from.lat, originLng: from.lng, originAddress: from.name,
            destLat: to.lat, destLng: to.lng, destAddress: to.name,
            departAt: _departAt,
            seatsTotal: _seats,
            pricePerSeat: _price,
            minSeatsToConfirm: _minSeats,
            genderPolicy: _genderPolicy,
            minAge: _minAge,
            maxAge: _maxAge,
          )
        : await _trips.update(
            trip.id,
            vehicleId: _vehicle!.id,
            originLat: from.lat, originLng: from.lng, originAddress: from.name,
            destLat: to.lat, destLng: to.lng, destAddress: to.name,
            departAt: _departAt,
            seatsTotal: _seats,
            pricePerSeat: _price,
            minSeatsToConfirm: _minSeats,
            genderPolicy: _genderPolicy,
            minAge: _minAge,
            maxAge: _maxAge,
          );
    if (!mounted) return;
    setState(() => _busy = false);
    if (res.success) {
      WanesAlerts.success(
          context, context.tr(trip == null ? 'driver.tripPosted' : 'driver.tripUpdated'),
          message: context.tr(trip == null ? 'driver.tripPostedBody' : 'driver.tripUpdatedBody'));
    } else {
      WanesAlerts.failure(context, res,
          title: context.tr(trip == null ? 'driver.postFailed' : 'driver.saveFailed'),
          onRetry: _post);
    }
    if (res.success) Navigator.pop(context, true);
  }


  /// The repeating version of this form: one schedule, which writes the next
  /// fortnight of trips at once.
  Future<void> _postRepeating(Place from, Place to) async {
    setState(() => _busy = true);
    final res = await ScheduleService().save(
      asDriver: true,
      originLat: from.lat,
      originLng: from.lng,
      originAddress: from.name,
      destLat: to.lat,
      destLng: to.lng,
      destAddress: to.name,
      recurrence: _repeat.recurrence,
      timeOfDay: TimeOfDayValue(_departAt.hour, _departAt.minute),
      startDate: _departAt,
      daysOfWeek: _repeat.effectiveDays,
      dayOfMonth: _repeat.recurrence == Recurrence.monthly ? _departAt.day : null,
      timeZoneId: deviceZoneId(),
      endDate: _repeat.until,
      seats: _seats,
      pricePerSeat: _price,
      vehicleId: _vehicle?.id,
      minSeatsToConfirm: _minSeats,
      genderPolicy: _genderPolicy,
      minAge: _minAge,
      maxAge: _maxAge,
    );
    if (!mounted) return;
    setState(() => _busy = false);

    if (!res.success) {
      WanesAlerts.failure(context, res, title: context.tr('schedule.saveFailed'));
      return;
    }
    WanesAlerts.success(context, context.tr('repeat.posted'),
        message: context.trPlural('repeat.postedBody', res.data?.generated ?? 0));
    Navigator.pop(context, true);
  }

  /// The repeats already running — paused or dropped from there.
  Future<void> _openSchedules() async {
    await Navigator.push(
      context,
      MaterialPageRoute(builder: (_) => const SchedulesScreen(asDriver: true)),
    );
  }

  /// "Tomorrow · 08:15" — the design's departure summary.
  String get _departLabel {
    final d = _departAt.toLocal();
    final today = DateTime.now();
    final day = DateTime(d.year, d.month, d.day);
    final diff = day.difference(DateTime(today.year, today.month, today.day)).inDays;
    final locale = context.l10n.localeName;
    final prefix = switch (diff) {
      0 => context.tr('common.today'),
      1 => context.tr('common.tomorrow'),
      _ => DateFormat('EEE d MMM', locale).format(d),
    };
    return '$prefix · ${DateFormat('HH:mm', locale).format(d)}';
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: Column(children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 8, 20, 0),
            child: ScreenHeader(
                title: context.tr(_editing == null ? 'driver.postTrip' : 'driver.editTrip')),
          ),
          Expanded(
            child: _loading
                ? const Center(child: WanesSpinner())
                : _vehicles.isEmpty
                    ? _emptyVehicles(t)
                    : ListView(
                        padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
                        children: [
                          if (_editing == null) ...[
                            RepeatPicker(
                              value: _repeat,
                              onChanged: (v) => setState(() => _repeat = v),
                              firstDate: _departAt,
                              onOpenMine: _openSchedules,
                            ),
                            const SizedBox(height: 12),
                          ],
                          _vehicleCard(t),
                          const SizedBox(height: 12),
                          _routeCard(t),
                          const SizedBox(height: 12),
                          IntrinsicHeight(
                            child: Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                              Expanded(child: _departsTile(t)),
                              const SizedBox(width: 12),
                              Expanded(child: _seatsTile(t)),
                            ]),
                          ),
                          const SizedBox(height: 12),
                          _priceCard(t),
                          const SizedBox(height: 12),
                          _minSeatsCard(t),
                          const SizedBox(height: 16),
                          ConditionsCard(
                            coRiderPolicy: _genderPolicy,
                            onCoRiderPolicy: (p) => setState(() => _genderPolicy = p),
                            minAge: _minAge,
                            maxAge: _maxAge,
                            onAges: (min, max) => setState(() {
                              _minAge = min;
                              _maxAge = max;
                            }),
                          ),
                          const SizedBox(height: 14),
                          _earnLine(t),
                        ],
                      ),
          ),
          if (!_loading && _vehicles.isNotEmpty) _publishBar(t),
        ]),
      ),
    );
  }

  Widget _publishBar(WanesTokens t) => Container(
        padding: const EdgeInsets.fromLTRB(20, 14, 20, 14),
        decoration: BoxDecoration(
          color: t.bg,
          border: Border(top: BorderSide(color: t.border)),
        ),
        child: SafeArea(
          top: false,
          child: PrimaryButton(
            label: context.tr(_editing != null
                ? 'common.saveChanges'
                : _repeat.repeat
                    ? 'repeat.postCta'
                    : 'driver.reviewPublish'),
            arrow: false,
            busy: _busy,
            onPressed: _busy ? null : _post,
          ),
        ),
      );

  Widget _vehicleCard(WanesTokens t) {
    return WanesCard(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
      child: Row(children: [
        Container(
          width: 44,
          height: 44,
          alignment: Alignment.center,
          decoration: BoxDecoration(color: t.surface2, borderRadius: BorderRadius.circular(12)),
          child: Icon(Icons.directions_car_filled_rounded, color: t.tealInk, size: 22),
        ),
        const SizedBox(width: 13),
        Expanded(
          child: DropdownButtonHideUnderline(
            child: DropdownButton<Vehicle>(
              value: _vehicle,
              isExpanded: true,
              icon: Icon(Icons.expand_more_rounded, color: t.ink2),
              dropdownColor: t.surface,
              items: _vehicles
                  .map((v) => DropdownMenuItem(
                        value: v,
                        child: Text('${v.make} ${v.model} · ${v.plate}',
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
                      ))
                  .toList(),
              onChanged: (v) => setState(() {
                _vehicle = v;
                if (v != null && _seats > v.seatCapacity) _seats = v.seatCapacity;
              }),
            ),
          ),
        ),
      ]),
    );
  }

  Widget _routeCard(WanesTokens t) {
    return WanesCard(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      child: Column(children: [
        _routeRow(t, false, context.tr('common.from'), _from?.name, () => _pickPlace(true),
            placeholder: context.tr('driver.searchStart'), divider: true),
        _routeRow(t, true, context.tr('common.to'), _to?.name, () => _pickPlace(false),
            placeholder: context.tr('home.searchDestination'), divider: false),
      ]),
    );
  }

  Widget _routeRow(WanesTokens t, bool dest, String label, String? value, VoidCallback onTap,
      {required String placeholder, required bool divider}) {
    return InkWell(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 15),
        decoration: divider ? BoxDecoration(border: Border(bottom: BorderSide(color: t.border))) : null,
        child: Row(children: [
          RouteDot(destination: dest),
          const SizedBox(width: 14),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              MonoLabel(label, spacing: 1.0),
              const SizedBox(height: 2),
              Text(value ?? placeholder,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: 15,
                      color: value == null ? t.ink2 : t.ink)),
            ]),
          ),
        ]),
      ),
    );
  }

  /// A `surface-2` tile with a mono caption — the DEPARTS / SEATS pair.
  Widget _fieldTile(WanesTokens t, String label, Widget child, {VoidCallback? onTap}) {
    final box = Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: t.surface2,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: t.border),
      ),
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, mainAxisSize: MainAxisSize.min, children: [
        MonoLabel(label, spacing: 0.8),
        const SizedBox(height: 3),
        child,
      ]),
    );
    if (onTap == null) return box;
    return InkWell(borderRadius: BorderRadius.circular(14), onTap: onTap, child: box);
  }

  Widget _departsTile(WanesTokens t) => _fieldTile(
        t,
        context.tr('common.departs'),
        Text(_departLabel,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
        onTap: _pickDeparture,
      );

  Widget _seatsTile(WanesTokens t) => _fieldTile(
        t,
        context.tr('common.seats'),
        Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
          _stepButton(t, '–', _seats > 1, () => setState(() => _seats--)),
          Text('$_seats', style: WanesTheme.mono(size: 15, weight: FontWeight.w700, color: t.ink, spacing: 0)),
          _stepButton(t, '+', _seats < (_vehicle?.seatCapacity ?? 1), () => setState(() => _seats++), accent: true),
        ]),
      );

  Widget _stepButton(WanesTokens t, String glyph, bool enabled, VoidCallback onTap, {bool accent = false}) {
    return GestureDetector(
      onTap: enabled ? onTap : null,
      child: Container(
        width: 22,
        height: 22,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: accent ? t.tealTint : t.surface,
          borderRadius: BorderRadius.circular(6),
          border: accent ? null : Border.all(color: t.border),
        ),
        child: Text(glyph,
            style: TextStyle(
                height: 1,
                fontWeight: FontWeight.w800,
                fontSize: 15,
                color: enabled ? (accent ? t.tealInk : t.ink2) : t.ink2.withValues(alpha: .35))),
      ),
    );
  }

  Widget _symbol(WanesTokens t) => Text(Fare.symbol,
      style: WanesTheme.mono(size: 30, weight: FontWeight.w800, color: t.ink2, spacing: 0));

  Widget _amount(WanesTokens t) => Text(_price.toStringAsFixed(Fare.decimals),
      style: WanesTheme.mono(size: 30, weight: FontWeight.w800, color: t.ink, spacing: 0));

  /// PRICE PER SEAT — big mono figure, suggestion chip, tap the amount to edit.
  /// "Confirm at N seats" — the driver's own condition on whether the trip is
  /// worth making.
  ///
  /// Capped at the seats on offer: a threshold the trip cannot reach would
  /// cancel itself at the cutoff however many riders turned up, and the server
  /// refuses it outright.
  Widget _minSeatsCard(WanesTokens t) => GroupedCard(children: [
        GroupedRow(
          icon: Icons.groups_2_outlined,
          title: context.tr('trip.minSeats'),
          subtitle: _minSeats <= 1
              ? context.tr('trip.noThreshold')
              : context.tr('trip.minSeatsHint'),
          trailing: SeatStepper(
            value: _minSeats,
            max: _seats,
            onChanged: (v) => setState(() => _minSeats = v),
          ),
        ),
      ]);

  Widget _priceCard(WanesTokens t) {
    final suggestion = _suggestedPrice;
    return WanesCard(
      radius: 16,
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        MonoLabel(context.tr('driver.pricePerSeat'), spacing: 1.0),
        const SizedBox(height: 8),
        Row(textDirection: TextDirection.ltr, children: [
          _stepButton(t, '–', _price > 1, () => setState(() => _price = (_price - 0.5).clamp(1, 50))),
          const SizedBox(width: 10),
          // The row is pinned LTR so the -/+ buttons keep their sides in both
          // languages, which means the symbol has to be placed by hand rather
          // than left to the text direction.
          ...(Fare.symbolAfterAmount
              ? [_amount(t), const SizedBox(width: 6), _symbol(t)]
              : [_symbol(t), const SizedBox(width: 4), _amount(t)]),
          const SizedBox(width: 10),
          _stepButton(t, '+', _price < 50, () => setState(() => _price = (_price + 0.5).clamp(1, 50)),
              accent: true),
          const Spacer(),
          GestureDetector(
            onTap: () => setState(() => _price = suggestion),
            child: TintChip(
                context.tr('driver.suggestedPrice', {'price': Fare.format(suggestion)})),
          ),
        ]),
      ]),
    );
  }

  Widget _earnLine(WanesTokens t) => Padding(
        padding: const EdgeInsets.symmetric(horizontal: 4),
        child: Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
          Text(context.tr('driver.earnUpTo'), style: TextStyle(fontSize: 13, color: t.ink2)),
          Text(Fare.format(_price * _seats),
              style: WanesTheme.mono(size: 16, weight: FontWeight.w800, color: t.tealInk, spacing: 0)),
        ]),
      );

  Widget _emptyVehicles(WanesTokens t) => Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Container(
              width: 64,
              height: 64,
              decoration: BoxDecoration(color: t.tealTint, borderRadius: BorderRadius.circular(20)),
              child: Icon(Icons.directions_car_outlined, size: 28, color: t.tealInk),
            ),
            const SizedBox(height: 16),
            Text(context.tr('driver.addVehicleFirst'),
                style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: t.ink)),
            const SizedBox(height: 6),
            Text(context.tr('driver.addVehicleFirstBody'),
                textAlign: TextAlign.center, style: TextStyle(color: t.ink2)),
            const SizedBox(height: 20),
            PrimaryButton(
              label: context.tr('vehicle.addVehicleTitle'),
              arrow: false,
              onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const VehiclesScreen()))
                  .then((_) => _loadVehicles()),
            ),
          ]),
        ),
      );
}
