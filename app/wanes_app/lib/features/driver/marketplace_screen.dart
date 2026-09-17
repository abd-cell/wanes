import 'dart:async';

import 'package:flutter/material.dart';

import '../../core/departure_label.dart';
import '../../core/driver_position.dart';
import '../../core/fare.dart';
import '../../core/geo.dart';
import '../../core/l10n.dart';
import '../../core/places.dart';
import '../../core/push_service.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/repeat_picker.dart';
import '../../widgets/safety_notes.dart';
import '../../widgets/wanes_alerts.dart';
import '../../widgets/wanes_motion.dart';
import '../../widgets/wanes_ui.dart';
import '../series/series_flow.dart';

/// Which part of the marketplace a driver is looking at.
enum MarketSegment { now, scheduled }

/// Splits the board into the two ways a Wanes driver works: rides needed
/// within the hour, and journeys to plan and fill ahead of time.
///
/// Each list is ordered the way the driver decides — the soonest first for
/// "now", the fullest first for "scheduled", because a pool of three is worth
/// planning a run around and a single seat on Thursday usually is not.
List<RiderTrip> marketSegment(List<RiderTrip> rows, MarketSegment segment, {DateTime? now}) {
  final at = now ?? DateTime.now();
  final live = rows.where((r) => r.isOpen && r.departAt.toLocal().isAfter(at));
  final picked = live
      .where((r) => isInstant(r.departAt, now: at) == (segment == MarketSegment.now))
      .toList();
  if (segment == MarketSegment.now) {
    picked.sort((a, b) => a.departAt.compareTo(b.departAt));
  } else {
    picked.sort((a, b) {
      final bySeats = b.seatsWanted.compareTo(a.seatsWanted);
      return bySeats != 0 ? bySeats : a.departAt.compareTo(b.departAt);
    });
  }
  return picked;
}

/// The driver's marketplace tab: every open ride request around them, as
/// shared trips to fill rather than hails to answer.
///
/// A card leads with what a driver weighs — how many people, how many seats,
/// what the run is worth — and the accept goes through the shared-trip sheet,
/// so nobody takes a ride without seeing that more riders may join.
class MarketplaceScreen extends StatefulWidget {
  const MarketplaceScreen({super.key, this.initialSegment = MarketSegment.now});

  final MarketSegment initialSegment;

  @override
  State<MarketplaceScreen> createState() => MarketplaceScreenState();
}

class MarketplaceScreenState extends State<MarketplaceScreen> {
  static const _radii = [5, 25, 50];

  final _service = RiderTripService();

  late MarketSegment _segment = widget.initialSegment;
  int _radiusKm = 25;
  /// Repeating commutes only — the runs worth planning a week around.
  bool _recurringOnly = false;
  Place? _here;
  List<RiderTrip> _rows = [];
  final Set<int> _passed = {};
  bool _loading = true;
  bool _busy = false;
  bool _failed = false;
  Timer? _tick;
  StreamSubscription<RiderTripClosed>? _closed;

  @override
  void initState() {
    super.initState();
    // The shell builds this tab at start-up, before the driver has looked at
    // it, so this first read must not raise the location prompt. Opening the
    // tab (see the shell) and "Use my location" are the explicit asks.
    reload();
    // Countdowns on the "now" cards; the scheduled ones move too slowly to
    // need it, so the repaint is skipped when nothing on screen is inside the hour.
    _tick = Timer.periodic(const Duration(seconds: 1), (_) {
      if (!mounted || _segment != MarketSegment.now || _visible.isEmpty) return;
      setState(() {});
    });
    _closed = PushService.instance.riderTripClosed.listen((closed) {
      if (!mounted || !_rows.any((r) => r.id == closed.riderTripId)) return;
      setState(() => _rows.removeWhere((r) => r.id == closed.riderTripId));
    });
  }

  @override
  void dispose() {
    _tick?.cancel();
    _closed?.cancel();
    super.dispose();
  }

  /// Public so the shell can refresh the tab when the driver comes back to it.
  Future<void> reload({bool prompt = false}) async {
    if (mounted) setState(() => _loading = _rows.isEmpty);
    final here = await DriverPosition.resolve(prompt: prompt);
    final res = await _service.nearby(here.lat, here.lng, radiusMeters: _radiusKm * 1000);
    if (!mounted) return;
    setState(() {
      _here = here;
      _loading = false;
      _failed = !res.success;
      if (res.success) _rows = res.data ?? [];
    });
  }

  void selectSegment(MarketSegment segment) => setState(() => _segment = segment);

  List<RiderTrip> get _visible => marketSegment(_rows, _segment)
      .where((r) => !_passed.contains(r.id))
      .where((r) => !_recurringOnly || r.isRecurring)
      .toList();
  /// Repeating requests in the segment on screen — the filter only appears
  /// when there is something to filter to.
  int get _recurringCount =>
      marketSegment(_rows, _segment).where((r) => !_passed.contains(r.id) && r.isRecurring).length;

  int _count(MarketSegment s) =>
      marketSegment(_rows, s).where((r) => !_passed.contains(r.id)).length;

  Future<void> _accept(RiderTrip r) async {
    if (_busy) return;
    final ok = await takeRideRequest(context, r,
        onBusy: (busy) => mounted ? setState(() => _busy = busy) : null);
    if (ok && mounted) {
      setState(() => _rows.removeWhere((x) => x.id == r.id));
    }
  }

  /// "Tell me when this request reaches N passengers."
  Future<void> _watch(RiderTrip r) async {
    final options = [for (var n = r.seatsWanted + 1; n <= 8; n++) n];
    final picked = await showModalBottomSheet<int>(
      context: context,
      builder: (ctx) => SafeArea(
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 16, 20, 8),
            child: Text(ctx.tr('market.watchTitle'),
                style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 17)),
          ),
          for (final n in options.take(5))
            ListTile(
              leading: const Icon(Icons.groups_2_outlined),
              title: Text(ctx.trPlural('market.watchAt', n)),
              onTap: () => Navigator.pop(ctx, n),
            ),
        ]),
      ),
    );
    if (picked == null || !mounted) return;
    final res = await MarketplaceService().watchRequest(r.id, picked);
    if (!mounted) return;
    if (res.success) {
      WanesAlerts.success(context, context.tr('market.watching'),
          message: context.trPlural('market.watchingBody', picked));
    } else {
      WanesAlerts.failure(context, res, title: context.tr('market.watchFailed'));
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final rows = _visible;

    return SafeArea(
      bottom: false,
      child: RefreshIndicator(
        onRefresh: reload,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
          children: [
            Text(context.tr('market.title'),
                style: TextStyle(fontSize: 22, fontWeight: FontWeight.w800, letterSpacing: -0.44, color: t.ink)),
            const SizedBox(height: 3),
            Text(context.tr('market.subtitle'), style: TextStyle(fontSize: 13, color: t.ink2)),
            const SizedBox(height: 14),
            _segments(t),
            const SizedBox(height: 10),
            _radiusRow(t),
            if (_recurringCount > 0) ...[
              const SizedBox(height: 8),
              _recurringFilter(t),
            ],
            if (_here != null && !DriverPosition.isLive) ...[
              const SizedBox(height: 10),
              _approxBanner(t),
            ],
            const SizedBox(height: 14),
            if (_loading)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 40),
                child: Center(child: WanesSpinner()),
              )
            else if (rows.isEmpty)
              _empty(t)
            else
              for (final r in rows)
                Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: MarketRequestCard(
                    request: r,
                    from: _here,
                    onAccept: _busy || r.iHaveOffered ? null : () => _accept(r),
                    onPass: _busy ? null : () => setState(() => _passed.add(r.id)),
                    // Planned work that is not full enough yet: let the driver
                    // come back when it is, instead of deciding now.
                    onWatch: _segment == MarketSegment.scheduled && r.seatsWanted < 8 && !r.iHaveOffered
                        ? () => _watch(r)
                        : null,
                  ),
                ),
            const SizedBox(height: 4),
            const SafetyReminder(audience: SafetyAudience.driver),
          ],
        ),
      ),
    );
  }

  Widget _segments(WanesTokens t) {
    Widget tab(MarketSegment s, String label, String hint) {
      final active = _segment == s;
      final count = _count(s);
      return Expanded(
        child: Material(
          color: active ? t.surface : Colors.transparent,
          borderRadius: BorderRadius.circular(12),
          child: InkWell(
            borderRadius: BorderRadius.circular(12),
            onTap: () => selectSegment(s),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Row(children: [
                  Text(label,
                      style: TextStyle(
                          fontWeight: FontWeight.w800, fontSize: 14.5, color: active ? t.ink : t.ink2)),
                  const SizedBox(width: 6),
                  if (count > 0)
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 1),
                      decoration: BoxDecoration(
                        color: s == MarketSegment.now ? t.amberTint : t.tealTint,
                        borderRadius: BorderRadius.circular(999),
                      ),
                      child: Text('$count',
                          style: WanesTheme.mono(
                              size: 11,
                              weight: FontWeight.w700,
                              color: s == MarketSegment.now ? t.amberInk : t.tealInk,
                              spacing: 0)),
                    ),
                ]),
                const SizedBox(height: 2),
                Text(hint,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(fontSize: 11, color: t.ink2)),
              ]),
            ),
          ),
        ),
      );
    }

    return Container(
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: t.surface2,
        borderRadius: BorderRadius.circular(15),
        border: Border.all(color: t.border),
      ),
      child: Row(children: [
        tab(MarketSegment.now, context.tr('market.now'), context.tr('market.nowHint')),
        const SizedBox(width: 4),
        tab(MarketSegment.scheduled, context.tr('market.scheduled'), context.tr('market.scheduledHint')),
      ]),
    );
  }

  /// "Repeating (4)" — one chip, because a driver after a commute wants the
  /// runs they can plan a week around and nothing else.
  Widget _recurringFilter(WanesTokens t) => Row(children: [
        Icon(Icons.repeat_rounded, size: 16, color: t.ink2),
        const SizedBox(width: 6),
        Text(context.tr('series.recurringFilter'), style: TextStyle(fontSize: 12.5, color: t.ink2)),
        const Spacer(),
        ChoiceChip(
          label: Text('$_recurringCount'),
          selected: _recurringOnly,
          visualDensity: VisualDensity.compact,
          onSelected: (v) => setState(() => _recurringOnly = v),
        ),
      ]);

  Widget _radiusRow(WanesTokens t) => Row(children: [
        Icon(Icons.radar_rounded, size: 16, color: t.ink2),
        const SizedBox(width: 6),
        Text(context.tr('market.distance'), style: TextStyle(fontSize: 12.5, color: t.ink2)),
        const Spacer(),
        for (final km in _radii)
          Padding(
            padding: const EdgeInsetsDirectional.only(start: 6),
            child: ChoiceChip(
              label: Text(context.tr('units.km', {'value': km})),
              selected: _radiusKm == km,
              visualDensity: VisualDensity.compact,
              onSelected: (_) {
                if (_radiusKm == km) return;
                setState(() => _radiusKm = km);
                reload();
              },
            ),
          ),
      ]);

  Widget _approxBanner(WanesTokens t) => Container(
        padding: const EdgeInsets.fromLTRB(12, 8, 6, 8),
        decoration: BoxDecoration(color: t.amberTint, borderRadius: BorderRadius.circular(12)),
        child: Row(children: [
          Icon(Icons.location_off_outlined, size: 16, color: t.amberInk),
          const SizedBox(width: 8),
          Expanded(
            child: Text(context.tr('market.approxLocation'),
                style: TextStyle(fontSize: 12, height: 1.35, color: t.amberInk)),
          ),
          TextButton(
            onPressed: () => reload(prompt: true),
            child: Text(context.tr('market.useLocation'),
                style: TextStyle(fontWeight: FontWeight.w700, color: t.amberInk)),
          ),
        ]),
      );

  Widget _empty(WanesTokens t) => WanesCard(
        child: Column(children: [
          Icon(_failed ? Icons.cloud_off_rounded : Icons.inbox_outlined, size: 28, color: t.ink2),
          const SizedBox(height: 8),
          Text(
              context.tr(_failed
                  ? 'market.loadFailed'
                  : _segment == MarketSegment.now
                      ? 'market.emptyNow'
                      : 'market.emptyScheduled'),
              textAlign: TextAlign.center,
              style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14.5, color: t.ink)),
          const SizedBox(height: 4),
          Text(context.tr('market.emptyBody'),
              textAlign: TextAlign.center, style: TextStyle(fontSize: 12.5, color: t.ink2)),
          const SizedBox(height: 10),
          OutlinedButton(onPressed: reload, child: Text(context.tr('common.refresh'))),
        ]),
      );
}

/// A ride request as a trip to fill: when, where, how many, and what it pays.
class MarketRequestCard extends StatelessWidget {
  const MarketRequestCard({
    super.key,
    required this.request,
    required this.from,
    required this.onAccept,
    this.onPass,
    this.onWatch,
  });

  /// "Notify me when it fills" — offered on planned requests.
  final VoidCallback? onWatch;

  final RiderTrip request;

  /// Where the driver is. Null leaves the pickup distance off.
  final Place? from;

  /// Null while another accept is in flight, or once this driver has offered.
  final VoidCallback? onAccept;
  final VoidCallback? onPass;

  static String _short(String address) => address.split(',').first.trim();

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final r = request;
    final instant = isInstant(r.departAt);
    final accent = instant ? t.amber : t.teal;
    final accentInk = instant ? t.amberInk : t.tealInk;
    final accentTint = instant ? t.amberTint : t.tealTint;
    final perSeat = r.suggestedPricePerSeat > 0
        ? r.suggestedPricePerSeat
        : Fare.perSeat(Geo.distanceKm(r.originLat, r.originLng, r.destinationLat, r.destinationLng));
    final pickupKm = from == null ? null : Geo.distanceKm(from!.lat, from!.lng, r.originLat, r.originLng);
    final tripKm = r.distanceKm > 0
        ? r.distanceKm
        : Geo.distanceKm(r.originLat, r.originLng, r.destinationLat, r.destinationLng);

    return Container(
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: instant ? accent : t.border),
        boxShadow: [BoxShadow(color: t.shadow, blurRadius: 18, offset: const Offset(0, 6), spreadRadius: -10)],
      ),
      padding: const EdgeInsets.all(14),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        // When
        Row(children: [
          Icon(instant ? Icons.bolt_rounded : Icons.event_rounded, size: 16, color: accentInk),
          const SizedBox(width: 6),
          Expanded(
            child: Text(departureLabel(context, r.departAt),
                style: WanesTheme.mono(size: 12, weight: FontWeight.w700, color: accentInk, spacing: 0)),
          ),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
            decoration: BoxDecoration(color: accentTint, borderRadius: BorderRadius.circular(999)),
            child: Text(context.tr('market.in', {'time': untilLabel(context, r.timeLeft)}),
                style: WanesTheme.mono(size: 11, weight: FontWeight.w700, color: accentInk, spacing: 0)),
          ),
        ]),
        const SizedBox(height: 10),
        // Where
        Text(context.tr('common.routeSummary', {'from': _short(r.originAddress), 'to': _short(r.destinationAddress)}),
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, height: 1.25, color: t.ink)),
        const SizedBox(height: 4),
        Text(
          [
            context.tr('units.km', {'value': tripKm.toStringAsFixed(tripKm < 10 ? 1 : 0)}),
            if (pickupKm != null) context.tr('driver.away', {'distance': Geo.formatKm(pickupKm)}),
          ].join(' · '),
          style: WanesTheme.mono(size: 11, weight: FontWeight.w500, color: t.ink2, spacing: 0),
        ),
        const SizedBox(height: 12),
        // Who, and what it pays
        Row(crossAxisAlignment: CrossAxisAlignment.end, children: [
          Expanded(
            child: Wrap(spacing: 6, runSpacing: 6, children: [
              _chip(t, Icons.people_alt_outlined, context.trPlural('market.passengers', r.riderCount)),
              _chip(t, Icons.event_seat_outlined, context.trPlural('vehicle.seatCount', r.seatsWanted)),
              if (r.isPool) _chip(t, Icons.groups_2_outlined, context.tr('market.pool'), highlight: true),
              if (r.driverGenderPolicy != GenderPolicy.any)
                _chip(t, Icons.shield_outlined, context.tr(r.driverGenderPolicy.labelKey)),
              if (r.iHaveOffered) _chip(t, Icons.check_rounded, context.tr('market.offerSent'), highlight: true),
              // One day of a commute. The badge is what turns a Tuesday
              // morning into "every Tuesday morning" on the card itself.
              if (r.series != null) RepeatBadge(series: r.series!, compact: true),
              if (r.series?.hasDriver == true)
                _chip(t, Icons.person_outline_rounded,
                    context.tr('series.hasDriver', {'name': r.series!.driverName ?? ''})),
              if (r.series?.mySeriesStatus == SeriesStatus.proposed)
                _chip(t, Icons.repeat_rounded, context.tr('series.offerSentTitle'), highlight: true),
            ]),
          ),
          const SizedBox(width: 10),
          Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
            Text(context.tr('market.estTotal'),
                style: WanesTheme.mono(size: 10, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
            Text(Fare.format(perSeat * r.seatsWanted),
                style: WanesTheme.mono(size: 18, weight: FontWeight.w800, color: t.tealInk, spacing: 0)),
            Text(context.tr('market.perSeat', {'amount': Fare.format(perSeat)}),
                style: WanesTheme.mono(size: 10, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
          ]),
        ]),
        const SizedBox(height: 12),
        Row(children: [
          if (onPass != null) ...[
            Expanded(
              child: OutlinedButton(
                onPressed: onPass,
                style: OutlinedButton.styleFrom(
                  foregroundColor: t.ink,
                  side: BorderSide(color: t.border),
                  minimumSize: const Size.fromHeight(42),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                ),
                child: Text(context.tr('market.pass'), style: const TextStyle(fontWeight: FontWeight.w700)),
              ),
            ),
            const SizedBox(width: 9),
          ],
          Expanded(
            flex: 2,
            child: FilledButton(
              onPressed: onAccept,
              style: FilledButton.styleFrom(
                backgroundColor: instant ? t.amber : t.tealInk,
                foregroundColor: instant ? const Color(0xFF2A1000) : null,
                minimumSize: const Size.fromHeight(42),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
              ),
              child: Text(context.tr(r.iHaveOffered ? 'market.offerSent' : 'market.review'),
                  style: const TextStyle(fontWeight: FontWeight.w800)),
            ),
          ),
        ]),
        if (onWatch != null)
          Align(
            alignment: AlignmentDirectional.centerStart,
            child: TextButton.icon(
              onPressed: onWatch,
              icon: Icon(Icons.notifications_active_outlined, size: 16, color: t.tealInk),
              label: Text(context.tr('market.watch'),
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 12.5, color: t.tealInk)),
            ),
          ),
      ]),
    );
  }

  Widget _chip(WanesTokens t, IconData icon, String label, {bool highlight = false}) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
        decoration: BoxDecoration(
          color: highlight ? t.tealTint : t.surface2,
          borderRadius: BorderRadius.circular(999),
          border: Border.all(color: highlight ? t.tealTint : t.border),
        ),
        child: Row(mainAxisSize: MainAxisSize.min, children: [
          Icon(icon, size: 13, color: highlight ? t.tealInk : t.ink2),
          const SizedBox(width: 4),
          Text(label,
              style: TextStyle(
                  fontSize: 11.5, fontWeight: FontWeight.w600, color: highlight ? t.tealInk : t.ink)),
        ]),
      );
}
