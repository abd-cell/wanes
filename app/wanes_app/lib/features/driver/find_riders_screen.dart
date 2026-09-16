import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;
import '../../core/l10n.dart';
import '../../core/places.dart';
import '../../core/theme.dart';
import '../../core/trip_sort.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/place_picker.dart';
import '../../widgets/wanes_alerts.dart';
import '../../widgets/wanes_ui.dart';
import '../../widgets/when_picker.dart';
import 'rider_matches_screen.dart';

/// The driver's search — the mirror of the rider's home screen.
///
/// The rider says where they want to go and finds trips going that way. The
/// driver says where they are *about to drive* and finds people who want that
/// journey. Same form, same two bands in the results, opposite side of the car.
///
/// This is not the dashboard's incoming stack, which answers a different
/// question: that one is push-driven and asks "who needs a lift around me, right
/// now", off the driver's live position. A driver planning tomorrow's run to
/// Irbid cannot ask that question at all, which is the gap this fills.
class FindRidersScreen extends StatefulWidget {
  const FindRidersScreen({super.key});

  @override
  State<FindRidersScreen> createState() => _FindRidersScreenState();
}

class _FindRidersScreenState extends State<FindRidersScreen> {
  final _riderTrips = RiderTripService();

  Place? _from;
  Place? _to;
  DateTime? _when;

  /// Match radius. true = nearby (5 km around each end), false = wide (50 km).
  bool _nearby = true;

  /// Smallest pool worth diverting for. 0 = any.
  int _minSeats = 0;

  /// Who the driver will carry, for this journey. Stated per search, exactly as
  /// the rider states who may drive them — it shapes this list and is written
  /// onto nothing.
  GenderPolicy _carrying = GenderPolicy.any;

  bool _busy = false;

  DateTime get _departAt => _when ?? DateTime.now().add(const Duration(minutes: 10));

  String _whenLabel() {
    if (_when == null) return context.tr('home.now');
    final now = DateTime.now();
    final d = _when!;
    final tomorrow = now.add(const Duration(days: 1));
    final locale = context.l10n.localeName;
    final time = DateFormat('HH:mm', locale).format(d);
    if (d.year == now.year && d.month == now.month && d.day == now.day) {
      return '${context.tr('common.today')} · $time';
    }
    if (d.year == tomorrow.year && d.month == tomorrow.month && d.day == tomorrow.day) {
      return '${context.tr('common.tomorrow')} · $time';
    }
    return '${DateFormat('EEE, MMM d', locale).format(d)} · $time';
  }

  Future<void> _pickWhen() async {
    final selection = await showWhenPicker(context, _when, audience: WhenAudience.driver);
    if (selection == null || !mounted) return;
    final picked = selection.dateTime;
    setState(() => _when = (picked == null ||
            picked.isBefore(DateTime.now().add(const Duration(minutes: 2))))
        ? null
        : picked);
  }

  Future<void> _pickPlace(bool isFrom) async {
    final picked = await showPlacePicker(
      context,
      title: context.tr(isFrom ? 'home.pickup' : 'home.destination'),
      hint: context.tr(isFrom ? 'home.searchPickupHint' : 'home.searchDestinationHint'),
    );
    if (picked == null || !mounted) return;
    setState(() => isFrom ? _from = picked : _to = picked);
  }

  Future<void> _run() async {
    final from = _from;
    final to = _to;
    if (from == null || to == null) {
      WanesAlerts.warning(context,
          context.tr(from == null ? 'home.pickOrigin' : 'home.pickDestination'));
      return;
    }
    if (from.key == to.key) {
      WanesAlerts.warning(context, context.tr('home.pickTwoPlaces'),
          message: context.tr('home.pickTwoPlacesBody'));
      return;
    }

    setState(() => _busy = true);
    final res = await _riderTrips.findRiders(
      originLat: from.lat, originLng: from.lng, originAddress: from.name,
      destLat: to.lat, destLng: to.lng, destAddress: to.name,
      when: _departAt,
      nearbyOnly: _nearby,
      minSeats: _minSeats,
      sortBy: SortPreference.instance.value,
      riderGenderPolicy: _carrying,
    );
    if (!mounted) return;
    setState(() => _busy = false);

    if (!res.success || res.data == null) {
      WanesAlerts.failure(context, res,
          title: context.tr('findRiders.failed'), onRetry: _run);
      return;
    }

    Navigator.push(context, MaterialPageRoute(
      builder: (_) => RiderMatchesScreen(
        result: res.data!,
        from: from,
        to: to,
        when: _departAt,
      ),
    ));
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
            child: ScreenHeader(title: context.tr('findRiders.title')),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
              children: [
          Text(context.tr('findRiders.lead'),
              style: TextStyle(fontSize: 13, height: 1.45, color: t.ink2)),
          const SizedBox(height: 16),

          _routeCard(t),
          const SizedBox(height: 12),

          Row(children: [
            Expanded(child: _whenTile(t)),
            const SizedBox(width: 12),
            Expanded(child: _minSeatsTile(t)),
          ]),
          const SizedBox(height: 12),

          SegmentedToggle(
            labels: [context.tr('home.rangeNearby'), context.tr('home.rangeWide')],
            index: _nearby ? 0 : 1,
            onSelect: (i) => setState(() => _nearby = i == 0),
          ),
          const SizedBox(height: 12),

          MonoLabel(context.tr('findRiders.carryFilter')),
          const SizedBox(height: 8),
          SegmentedToggle(
            labels: [
              context.tr('conditions.anyone'),
              context.tr('conditions.womenOnly'),
              context.tr('conditions.menOnly'),
            ],
            index: switch (_carrying) {
              GenderPolicy.any => 0,
              GenderPolicy.femaleOnly => 1,
              GenderPolicy.maleOnly => 2,
            },
            onSelect: (i) => setState(() => _carrying = switch (i) {
                  1 => GenderPolicy.femaleOnly,
                  2 => GenderPolicy.maleOnly,
                  _ => GenderPolicy.any,
                }),
          ),
          const SizedBox(height: 14),

          PrimaryButton(
            label: context.tr('findRiders.search'),
            busy: _busy,
            onPressed: _busy ? null : _run,
          ),
          const SizedBox(height: 16),

                Text(context.tr('findRiders.seatsNote'),
                    style: TextStyle(fontSize: 12, height: 1.45, color: t.ink2)),
              ],
            ),
          ),
        ]),
      ),
    );
  }

  Widget _routeCard(WanesTokens t) => WanesCard(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
        child: Stack(children: [
          Column(children: [
            _routeRow(t, context.tr('common.from'), _from?.name, () => _pickPlace(true),
                placeholder: context.tr('home.searchPickup'), divider: true),
            _routeRow(t, context.tr('common.to'), _to?.name, () => _pickPlace(false),
                placeholder: context.tr('home.searchDestination'), divider: false),
          ]),
          PositionedDirectional(
            end: 0, top: 0, bottom: 0,
            child: Center(
              child: GestureDetector(
                onTap: () => setState(() {
                  final tmp = _from;
                  _from = _to;
                  _to = tmp;
                }),
                child: Container(
                  width: 38, height: 38,
                  decoration: BoxDecoration(
                    color: t.surface2, shape: BoxShape.circle, border: Border.all(color: t.border),
                  ),
                  child: Icon(Icons.swap_vert_rounded, size: 18, color: t.ink2),
                ),
              ),
            ),
          ),
        ]),
      );

  Widget _routeRow(WanesTokens t, String label, String? value, VoidCallback onTap,
      {required String placeholder, required bool divider}) =>
      InkWell(
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 15),
          decoration: divider
              ? BoxDecoration(border: Border(bottom: BorderSide(color: t.border)))
              : null,
          child: Row(children: [
            SizedBox(
              width: 44,
              child: Text(label,
                  style: WanesTheme.mono(size: 10, weight: FontWeight.w600, color: t.ink2)),
            ),
            Expanded(
              child: Text(value ?? placeholder,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                      fontSize: 14,
                      fontWeight: value == null ? FontWeight.w500 : FontWeight.w700,
                      color: value == null ? t.ink2 : t.ink)),
            ),
            const SizedBox(width: 44),
          ]),
        ),
      );

  Widget _whenTile(WanesTokens t) => InkWell(
        onTap: _pickWhen,
        borderRadius: BorderRadius.circular(14),
        child: WanesCard(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            MonoLabel(context.tr('common.when')),
            const SizedBox(height: 6),
            Text(_whenLabel(),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700, color: t.ink)),
          ]),
        ),
      );

  /// The smallest pool worth a diversion. Zero means "any" — a driver with an
  /// empty car usually wants every match, and one filling the last seat of a
  /// seven-seater may not want to detour for a single rider.
  Widget _minSeatsTile(WanesTokens t) => WanesCard(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          MonoLabel(context.tr('findRiders.minSeats')),
          const SizedBox(height: 2),
          Row(children: [
            Expanded(
              child: Text(
                _minSeats == 0
                    ? context.tr('findRiders.anySize')
                    : context.trPlural('findRiders.seatsPlus', _minSeats),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: t.ink),
              ),
            ),
            SeatStepper(
              value: _minSeats,
              min: 0,
              max: 8,
              onChanged: (v) => setState(() => _minSeats = v),
            ),
          ]),
        ]),
      );
}
