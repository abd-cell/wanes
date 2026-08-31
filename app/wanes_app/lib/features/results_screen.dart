import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../core/fare.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../core/trip_sort.dart';
import '../models/models.dart';
import '../widgets/map_backdrop.dart';
import '../widgets/sort_picker.dart';
import '../widgets/wanes_motion.dart';
import '../widgets/wanes_ui.dart';
import 'confirm_booking_screen.dart';
import 'searching_screen.dart';
import 'trip_details_screen.dart';

/// Carpool results — prototype screen 03. A short map strip with the route,
/// then a sheet holding the Carpool / Hail toggle and one compact card per
/// matching trip.
class ResultsScreen extends StatefulWidget {
  const ResultsScreen({
    super.key,
    required this.matches,
    required this.from,
    required this.to,
    this.seats = 1,
    this.rideRequestId,
    this.fromLat,
    this.fromLng,
    this.toLat,
    this.toLng,
  });

  final List<Trip> matches;
  final String from;
  final String to;
  final int seats;

  /// The rider's own start point, used to work out the walk to each pickup.
  final double? fromLat;
  final double? fromLng;

  /// Where they asked to go. Carried so the hail screen can draw the real
  /// route rather than the prototype's illustration.
  final double? toLat;
  final double? toLng;

  /// Set when the search also opened a hail, so the "Hail a ride" tab can
  /// hand the rider straight to the live search.
  final int? rideRequestId;

  @override
  State<ResultsScreen> createState() => _ResultsScreenState();
}

class _ResultsScreenState extends State<ResultsScreen> {
  int _tab = 0;

  /// What the rider last picked, so the sheet opens on their own choice and the
  /// next search asks the server for that order.
  TripSort _sort = SortPreference.instance.value;

  /// Re-ordered locally on every sort change; the widget's own list stays as the
  /// server ranked it, which is what [TripSort.best] restores.
  late List<Trip> _matches = _sorted();

  /// No rider start point means no walk to measure, so that option is hidden.
  bool get _canSortByPickup => widget.fromLat != null && widget.fromLng != null;

  List<Trip> _sorted() => widget.matches
      .sortedBy(_sort, originLat: widget.fromLat, originLng: widget.fromLng);

  Future<void> _pickSort() async {
    final picked = await showSortPicker(context, _sort, allowPickup: _canSortByPickup);
    if (picked == null || picked == _sort || !mounted) return;
    setState(() {
      _sort = picked;
      _matches = _sorted();
    });
    await SortPreference.instance.save(picked);
  }

  static String shortPlace(String address) => address.split(',').first.trim();

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Scaffold(
      backgroundColor: t.bg,
      body: Column(children: [
        _mapStrip(t),
        Expanded(
          child: Transform.translate(
            offset: const Offset(0, -24),
            child: Container(
              decoration: BoxDecoration(
                color: t.bg,
                borderRadius: const BorderRadius.vertical(top: Radius.circular(24)),
              ),
              padding: const EdgeInsets.fromLTRB(18, 12, 18, 0),
              child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                Center(
                  child: Container(
                    width: 38,
                    height: 4,
                    decoration: BoxDecoration(color: t.border, borderRadius: BorderRadius.circular(2)),
                  ),
                ),
                const SizedBox(height: 12),
                SegmentedToggle(
                  labels: [
                    '${context.tr('results.carpool')} · ${widget.matches.length}',
                    context.tr('results.hail'),
                  ],
                  index: _tab,
                  onSelect: _onTab,
                ),
                const SizedBox(height: 14),
                Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
                  Flexible(
                    child: Text(
                        context.tr('common.routeSummary', {
                          'from': shortPlace(widget.from),
                          'to': shortPlace(widget.to),
                        }),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: WanesTheme.mono(
                            size: 11.5, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
                  ),
                  const SizedBox(width: 12),
                  _sortButton(t),
                ]),
                const SizedBox(height: 10),
                Expanded(
                  child: _matches.isEmpty
                      ? _empty(t)
                      : ListView.separated(
                          key: ValueKey(_sort),
                          padding: const EdgeInsets.only(bottom: 24),
                          itemCount: _matches.length,
                          separatorBuilder: (_, __) => const SizedBox(height: 9),
                          itemBuilder: (_, i) => _ResultCard(
                            trip: _matches[i],
                            seats: widget.seats,
                            from: widget.from,
                            to: widget.to,
                            fromLat: widget.fromLat,
                            fromLng: widget.fromLng,
                          ),
                        ),
                ),
              ]),
            ),
          ),
        ),
      ]),
    );
  }

  /// Switching to "Hail a ride" opens the live search rather than filtering a
  /// list — there is nothing to list until a driver accepts.
  void _onTab(int i) {
    if (i == 0) {
      setState(() => _tab = 0);
      return;
    }
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => SearchingScreen(
          rideRequestId: widget.rideRequestId,
          originLat: widget.fromLat,
          originLng: widget.fromLng,
          destLat: widget.toLat,
          destLng: widget.toLng,
        ),
      ),
    );
  }

  /// "Sort: Cheapest ▾" — the label carries the current choice so the rider can
  /// see the order they are looking at without opening the sheet.
  Widget _sortButton(WanesTokens t) {
    return InkWell(
      onTap: _pickSort,
      borderRadius: BorderRadius.circular(999),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
        child: Row(mainAxisSize: MainAxisSize.min, children: [
          Text(
            context.tr('results.sortBy', {'value': context.tr(_sort.labelKey)}),
            style: WanesTheme.mono(
                size: 11.5, weight: FontWeight.w600, color: t.tealInk, spacing: 0),
          ),
          Icon(Icons.expand_more_rounded, size: 15, color: t.tealInk),
        ]),
      ),
    );
  }

  Widget _mapStrip(WanesTokens t) {
    return SizedBox(
      height: 188,
      child: MapBackdrop(
        route: MapRoutes.results,
        routeColor: t.teal,
        children: [
          // pickup — left 13% · top 76%
          const Align(alignment: Alignment(-0.74, 0.52), child: MapDot()),
          // destination — left 77% · top 26%
          const Align(alignment: Alignment(0.54, -0.48), child: MapPin(size: 24)),
          PositionedDirectional(
            start: 16,
            top: MediaQuery.of(context).padding.top + 10,
            child: Material(
              color: t.surface,
              shape: CircleBorder(side: BorderSide(color: t.border)),
              clipBehavior: Clip.antiAlias,
              child: InkWell(
                onTap: () => Navigator.maybePop(context),
                child: SizedBox(
                  width: 38,
                  height: 38,
                  child: Icon(Icons.chevron_left_rounded, size: 20, color: t.ink),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _empty(WanesTokens t) => Center(
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          Icon(Icons.route_outlined, size: 40, color: t.ink2),
          const SizedBox(height: 12),
          Text(context.tr('results.emptyTitle'),
              style: TextStyle(fontWeight: FontWeight.w700, color: t.ink)),
          const SizedBox(height: 6),
          Text(context.tr('results.emptyBody'),
              textAlign: TextAlign.center,
              style: TextStyle(color: t.ink2, fontSize: 13)),
        ]),
      );
}

/// One matching trip: driver, price per seat, then a chip row with the small
/// teal "Book seat" button (design 03).
class _ResultCard extends StatelessWidget {
  const _ResultCard({
    required this.trip,
    required this.seats,
    required this.from,
    required this.to,
    this.fromLat,
    this.fromLng,
  });

  final Trip trip;
  final int seats;
  final String from;
  final String to;
  final double? fromLat;
  final double? fromLng;

  static String initialsOf(String name) {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
    return (parts.first[0] + parts.last[0]).toUpperCase();
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final time = DateFormat('HH:mm', context.l10n.localeName).format(trip.departAt.toLocal());
    final price = trip.pricePerSeat;

    // The card body opens the full trip; the teal button inside still books
    // directly, since its own tap handler wins over this one.
    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTap: () => Navigator.push(
        context,
        MaterialPageRoute(
          builder: (_) => TripDetailsScreen(trip: trip, seats: seats),
        ),
      ),
      child: Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: t.border),
        boxShadow: [
          BoxShadow(color: t.shadow, blurRadius: 14, offset: const Offset(0, 4), spreadRadius: -10),
        ],
      ),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          AvatarBadge(initialsOf(trip.driverName), size: 44, tint: t.tealTint, fg: t.tealInk),
          const SizedBox(width: 12),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(trip.driverName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15, color: t.ink)),
              const SizedBox(height: 2),
              InlineRating(
                '${trip.driverRating.toStringAsFixed(1)}'
                '${trip.vehicleLabel.isEmpty ? '' : ' · ${trip.vehicleLabel}'}',
                size: 11.5,
              ),
            ]),
          ),
          if (price != null) ...[
            const SizedBox(width: 8),
            Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
              Text(Fare.format(price),
                  style: WanesTheme.mono(size: 17, weight: FontWeight.w800, color: t.tealInk, spacing: 0)),
              Text(context.tr('results.perSeat'),
                  style: WanesTheme.mono(size: 10, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
            ]),
          ],
        ]),
        const SizedBox(height: 12),
        Row(children: [
          MetaChip(time),
          const SizedBox(width: 7),
          Flexible(
            child: MetaChip(context.trPlural('results.seatsLeft', trip.seatsLeft)),
          ),
          const Spacer(),
          _bookButton(context, t),
        ]),
      ]),
      ),
    );
  }

  Widget _bookButton(BuildContext context, WanesTokens t) {
    final full = trip.seatsLeft < seats;
    return Opacity(
      opacity: full ? 0.45 : 1,
      // `transition:transform .14s ease` with a 1px lift in the design.
      child: LiftInk(
        color: t.teal,
        onTap: full
            ? null
            : () => Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (_) => ConfirmBookingScreen(
                      trip: trip,
                      seats: seats,
                      from: from,
                      to: to,
                      fromLat: fromLat,
                      fromLng: fromLng,
                    ),
                  ),
                ),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 7),
          child: Text(context.tr(full ? 'results.full' : 'results.bookSeat'),
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 12.5, color: t.onTeal)),
        ),
      ),
    );
  }
}
