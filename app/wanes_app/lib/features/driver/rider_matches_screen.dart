import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;
import '../../core/fare.dart';
import '../../core/geo.dart';
import '../../core/l10n.dart';
import '../../core/places.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/wanes_alerts.dart';
import '../../widgets/wanes_motion.dart';
import '../../widgets/wanes_ui.dart';
import '../../widgets/accept_price_sheet.dart';

/// What a driver's search found — the mirror of the rider's results screen.
///
/// Two bands, in the order the server ranked them. **Going your way** is a pool
/// that wants the journey the driver is already making; **on your way** is one
/// that wants a stretch of it, and asks for a diversion. The diversion is the
/// number the driver is really choosing on, so it is on every card in the second
/// band and left off the first, where it is nothing.
class RiderMatchesScreen extends StatefulWidget {
  const RiderMatchesScreen({
    super.key,
    required this.result,
    required this.from,
    required this.to,
    required this.when,
  });

  final DemandSearchResult result;
  final Place from;
  final Place to;
  final DateTime when;

  @override
  State<RiderMatchesScreen> createState() => _RiderMatchesScreenState();
}

class _RiderMatchesScreenState extends State<RiderMatchesScreen> {
  final _riderTrips = RiderTripService();

  /// A copy, not the widget's list: rows leave it as the driver takes them, and
  /// mutating what was handed in would edit the caller's own result.
  late final List<DemandMatch> _matches = [...widget.result.matches];
  bool _busy = false;

  /// Takes a pool at a price the driver names.
  ///
  /// The same two steps as the dashboard's incoming stack, deliberately: a
  /// driver who has learned to take a ride one way should not meet a different
  /// flow here. The sheet opens on the distance estimate the card already shows,
  /// so the figure is confirmed rather than invented.
  Future<void> _take(DemandMatch match) async {
    final trip = match.trip;
    final price = await showAcceptPriceSheet(
      context,
      suggestion: trip.suggestedPricePerSeat > 0
          ? trip.suggestedPricePerSeat
          : Fare.perSeat(Geo.distanceKm(
              trip.originLat, trip.originLng, trip.destinationLat, trip.destinationLng)),
      seats: trip.seatsWanted,
    );
    // Backing out of the sheet is declining, not taking it at the suggestion.
    if (price == null || !mounted) return;

    setState(() => _busy = true);
    final res = await _riderTrips.offer(trip.id, pricePerSeat: price);
    if (!mounted) return;
    setState(() {
      _busy = false;
      if (res.success) _matches.removeWhere((m) => m.trip.id == trip.id);
    });

    if (res.success) {
      WanesAlerts.success(context, context.tr('driver.requestAccepted'),
          message: context.tr('driver.requestAcceptedBody'));
      if (_matches.isEmpty && mounted) Navigator.pop(context);
    } else {
      WanesAlerts.failure(context, res,
          title: context.tr('driver.acceptFailed'), onRetry: () => _take(match));
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final locale = context.l10n.localeName;

    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        bottom: false,
        child: Column(children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 8, 20, 0),
            child: ScreenHeader(title: context.tr('findRiders.resultsTitle')),
          ),
          Expanded(
            child: _matches.isEmpty
                ? _empty(t)
                : ListView(
                    padding: const EdgeInsets.fromLTRB(20, 12, 20, 24),
                    children: [
                      _routeStrip(t, locale),
                      const SizedBox(height: 14),
                      ..._sections(t),
                    ],
                  ),
          ),
        ]),
      ),
    );
  }

  /// One heading per band, and only for bands that actually have rows. The
  /// server already ordered them, so this walks the list and breaks where the
  /// tier changes rather than filtering it twice.
  List<Widget> _sections(WanesTokens t) {
    final widgets = <Widget>[];
    SearchTier? seen;

    for (final match in _matches) {
      if (match.tier != seen) {
        if (seen != null) widgets.add(const SizedBox(height: 18));
        widgets.add(SectionHeader(context.tr(match.tier == SearchTier.direct
            ? 'findRiders.bandDirect'
            : 'findRiders.bandOnTheWay')));
        widgets.add(const SizedBox(height: 8));
        seen = match.tier;
      }
      widgets.add(Padding(
        padding: const EdgeInsets.only(bottom: 10),
        child: _MatchCard(match: match, busy: _busy, onTake: () => _take(match)),
      ));
    }
    return widgets;
  }

  Widget _routeStrip(WanesTokens t, String locale) {
    final time = DateFormat('HH:mm', locale).format(widget.when.toLocal());
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 11),
      decoration: BoxDecoration(
        color: t.surface2,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: t.border),
      ),
      child: Row(children: [
        Icon(Icons.alt_route_rounded, size: 15, color: t.ink2),
        const SizedBox(width: 8),
        Expanded(
          child: Text('${widget.from.name} → ${widget.to.name}',
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontSize: 12.5, fontWeight: FontWeight.w700, color: t.ink)),
        ),
        const SizedBox(width: 8),
        Text(time,
            style: WanesTheme.mono(size: 11, weight: FontWeight.w600, color: t.ink2, spacing: 0)),
      ]),
    );
  }

  Widget _empty(WanesTokens t) => ListView(
        padding: const EdgeInsets.fromLTRB(20, 40, 20, 24),
        children: [
          Icon(Icons.person_search_outlined, size: 44, color: t.ink2),
          const SizedBox(height: 14),
          Text(context.tr('findRiders.noneTitle'),
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800, color: t.ink)),
          const SizedBox(height: 8),
          Text(context.tr('findRiders.noneBody'),
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 13, height: 1.5, color: t.ink2)),
        ],
      );
}

/// One pool, and what taking it would cost the driver.
class _MatchCard extends StatelessWidget {
  const _MatchCard({required this.match, required this.busy, required this.onTake});

  final DemandMatch match;
  final bool busy;
  final VoidCallback onTake;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final trip = match.trip;
    final locale = context.l10n.localeName;
    final time = DateFormat('HH:mm', locale).format(trip.departAt.toLocal());

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: t.border),
      ),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          AvatarBadge('R', size: 40, tint: t.amberTint, fg: t.amberInk),
          const SizedBox(width: 12),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(
                trip.isPool
                    ? context.trPlural('riderTrip.ridersWaiting', trip.riderCount)
                    : context.tr('riderTrip.oneRiderWaiting'),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink),
              ),
              const SizedBox(height: 2),
              Text('$time · ${context.trPlural('vehicle.seatCount', trip.seatsWanted)}',
                  style: WanesTheme.mono(
                      size: 11, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
            ]),
          ),
        ]),

        const SizedBox(height: 9),
        Row(children: [
          Icon(Icons.route_outlined, size: 13, color: t.ink2),
          const SizedBox(width: 5),
          Expanded(
            child: Text('${trip.originAddress} → ${trip.destinationAddress}',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontSize: 12, color: t.ink2)),
          ),
        ]),

        // The diversion, but only when there is one worth naming. On a pool
        // going the driver's way it is nothing, and a "0.0 km detour" row is
        // noise on every card in the first band.
        if (!match.isDoorToDoor) ...[
          const SizedBox(height: 7),
          Row(children: [
            Icon(Icons.turn_slight_right_rounded, size: 13, color: t.ink2),
            const SizedBox(width: 5),
            Expanded(
              child: Text(
                context.tr('findRiders.detour', {
                  'total': match.totalDetourKm.toStringAsFixed(1),
                  'pickup': match.pickupDetourKm.toStringAsFixed(1),
                  'dropoff': match.dropoffDetourKm.toStringAsFixed(1),
                }),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: WanesTheme.mono(
                    size: 10.5, weight: FontWeight.w500, color: t.ink2, spacing: 0),
              ),
            ),
          ]),
        ],

        if (trip.hasConditions) ...[
          const SizedBox(height: 7),
          Row(children: [
            Icon(Icons.shield_outlined, size: 13, color: t.ink2),
            const SizedBox(width: 5),
            Expanded(
              child: Text(
                context.tr('riderTrip.conditionsSummary', {
                  'driver': trip.driverGenderPolicy.label,
                  'riders': trip.coRiderGenderPolicy.label,
                }),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: WanesTheme.mono(
                    size: 10.5, weight: FontWeight.w500, color: t.ink2, spacing: 0),
              ),
            ),
          ]),
        ],

        const SizedBox(height: 12),
        Row(children: [
          if (match.minutesFromWhen != 0)
            MetaChip(context.tr(
                match.minutesFromWhen > 0 ? 'findRiders.laterBy' : 'findRiders.earlierBy',
                {'mins': match.minutesFromWhen.abs()})),
          const Spacer(),
          LiftInk(
            color: t.teal,
            onTap: busy ? null : onTake,
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 9),
              child: Text(context.tr('findRiders.take'),
                  style: const TextStyle(
                      color: Colors.white, fontWeight: FontWeight.w800, fontSize: 13)),
            ),
          ),
        ]),
      ]),
    );
  }
}
