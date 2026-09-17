import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../core/acknowledgements.dart';
import '../core/fare.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../core/trip_sort.dart';
import '../models/models.dart';
import '../widgets/map_backdrop.dart';
import '../widgets/safety_notes.dart';
import '../widgets/sort_picker.dart';
import '../widgets/wanes_motion.dart';
import '../widgets/wanes_ui.dart';
import '../core/places.dart';
import '../services/services.dart';
import '../widgets/wanes_alerts.dart';
import 'confirm_booking_screen.dart';
import 'post_rider_trip_screen.dart';
import 'searching_screen.dart';
import 'trip_details_screen.dart';

/// Search results — prototype screen 03, now showing everything a search can
/// offer rather than one mode of it.
///
/// Two bands of bookable trip: the ones going the rider's way, then the ones
/// that merely pass it. Then the postings other riders have made on the same
/// route, which this rider can join instead of writing a fourth near-identical
/// one. And, always, a way to post their own — which is what an empty result
/// means now: not a consolation hail, a deliberate act with a time on it.
class ResultsScreen extends StatefulWidget {
  const ResultsScreen({
    super.key,
    required this.matches,
    required this.from,
    required this.to,
    required this.earliestDepartAt,
    this.postings = const [],
    this.seats = 1,
    this.when,
    this.nearby = true,
    this.driverGenderPolicy = GenderPolicy.any,
  });

  final List<SearchMatch> matches;

  /// Open postings on this route the rider could join.
  final List<RiderTrip> postings;

  final Place from;
  final Place to;
  final int seats;

  /// What the rider asked for, carried so the post form opens on their own
  /// search rather than on defaults.
  final DateTime? when;
  final bool nearby;

  /// Who they said may drive them. Re-sent when a sort re-runs the search, and
  /// carried into the post form: a rider who searched for a woman at the wheel
  /// means it just as much on the trip they end up posting themselves.
  final GenderPolicy driverGenderPolicy;

  /// The earliest departure a posting for these seats could name, as the server
  /// worked it out. Shown on the post card so the rider sees the floor before
  /// they open the form.
  final DateTime earliestDepartAt;

  @override
  State<ResultsScreen> createState() => _ResultsScreenState();
}

class _ResultsScreenState extends State<ResultsScreen> {
  int _tab = 0;

  /// What the rider last picked, so the sheet opens on their own choice and the
  /// next search asks the server for that order.
  TripSort _sort = SortPreference.instance.value;

  /// The page in hand. Re-ordered locally the instant the rider picks a sort,
  /// then replaced by the page the server ranks for that sort.
  late List<SearchMatch> _matches = _sorted();

  /// The other half of the same response, which a re-sort refreshes with it.
  late List<RiderTrip> _postings = widget.postings;

  /// A sort is fetching its own page. The list stays on screen and usable while
  /// it does — it is already in the right order, just drawn from the wrong
  /// twenty.
  bool _resorting = false;

  /// The rider always has a start point here, so the walk is always measurable.
  bool get _canSortByPickup => true;

  /// Sorted *within each band*, never across them.
  ///
  /// A direct match beating a corridor match is the whole point of the bands —
  /// "cheapest" means the cheapest trip going the rider's way, not the cheapest
  /// trip that happens to pass within two kilometres — so the sort reorders
  /// each band and the bands keep their order.
  List<SearchMatch> _sorted([List<SearchMatch>? source]) {
    final byTier = <SearchTier, List<SearchMatch>>{};
    for (final match in source ?? widget.matches) {
      byTier.putIfAbsent(match.tier, () => []).add(match);
    }

    final out = <SearchMatch>[];
    for (final tier in SearchTier.values) {
      final band = byTier[tier];
      if (band == null) continue;
      out.addAll(band.sortedBy(_sort));
    }
    return out;
  }

  /// Where each band starts in [_matches], so the list can put a heading above
  /// it without splitting into two scroll views.
  Map<int, SearchTier> get _bandStarts {
    final starts = <int, SearchTier>{};
    SearchTier? seen;
    for (var i = 0; i < _matches.length; i++) {
      final tier = _matches[i].tier;
      if (tier != seen) {
        starts[i] = tier;
        seen = tier;
      }
    }
    return starts;
  }

  Future<void> _post() async {
    await Navigator.push(context, MaterialPageRoute(
      builder: (_) => PostRiderTripScreen(
        from: widget.from,
        to: widget.to,
        seats: widget.seats,
        when: widget.when,
        nearby: widget.nearby,
        driverGenderPolicy: widget.driverGenderPolicy,
      ),
    ));
  }

  Future<void> _join(RiderTrip posting) async {
    // Joining is agreeing to travel with strangers: the rider says so, and has
    // read the safety notes, before their seats are added to the pool.
    if (!await ensureSafetyAcknowledged(context, SafetyAudience.rider) || !mounted) return;
    if (!await confirmSharedJoin(context) || !mounted) return;
    await Acknowledgements.record(Acknowledgement.riderSharedRide);
    if (!mounted) return;
    final res = await RiderTripService().join(posting.id, seats: widget.seats);
    if (!mounted) return;
    if (!res.success) {
      WanesAlerts.failure(context, res, title: context.tr('riderTrip.joinFailed'));
      return;
    }
    final joined = res.data!;
    Navigator.pushReplacement(context, MaterialPageRoute(
      builder: (_) => SearchingScreen(
        riderTripId: joined.id,
        departAt: joined.departAt,
        seats: joined.seatsWanted,
        originLat: widget.from.lat,
        originLng: widget.from.lng,
        destLat: widget.to.lat,
        destLng: widget.to.lng,
      ),
    ));
  }

  Future<void> _pickSort() async {
    final picked = await showSortPicker(context, _sort, allowPickup: _canSortByPickup);
    if (picked == null || picked == _sort || !mounted) return;

    // Re-order what is on screen first, so the tap answers immediately.
    setState(() {
      _sort = picked;
      _matches = _sorted();
      _resorting = true;
    });
    await SortPreference.instance.save(picked);
    await _refetch();
  }

  /// Asks the server for the page that belongs to this sort.
  ///
  /// The server orders before it caps at twenty, so "cheapest" means the
  /// cheapest trips on this route — not the cheapest of the twenty nearest,
  /// which is all a local re-order can ever produce. The app used to settle for
  /// the local one because a search that matched nothing opened a hail as a
  /// side effect and a sort tap must never do that. Search has no side effects
  /// any more: a rider posts a trip deliberately, from its own screen.
  Future<void> _refetch() async {
    final res = await SearchService().search(
      originLat: widget.from.lat,
      originLng: widget.from.lng,
      originAddress: widget.from.name,
      destLat: widget.to.lat,
      destLng: widget.to.lng,
      destAddress: widget.to.name,
      when: widget.when ?? DateTime.now(),
      seats: widget.seats,
      nearby: widget.nearby,
      sortBy: _sort,
      driverGenderPolicy: widget.driverGenderPolicy,
    );
    if (!mounted) return;

    setState(() => _resorting = false);
    final data = res.data;
    if (!res.success || data == null) {
      // What is on screen is already in the order they asked for, so this is a
      // worse page rather than a wrong one. Say so and leave it be.
      WanesAlerts.failure(context, res, title: context.tr('results.sortFailed'));
      return;
    }

    setState(() {
      _matches = _sorted(data.matches);
      _postings = data.requests;
    });
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
                    '${context.tr('results.trips')} · ${widget.matches.length}',
                    '${context.tr('results.riders')} · ${_postings.length}',
                  ],
                  index: _tab,
                  onSelect: (i) => setState(() => _tab = i),
                ),
                const SizedBox(height: 14),
                Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
                  Flexible(
                    child: Text(
                        context.tr('common.routeSummary', {
                          'from': shortPlace(widget.from.name),
                          'to': shortPlace(widget.to.name),
                        }),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: WanesTheme.mono(
                            size: 11.5, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
                  ),
                  const SizedBox(width: 12),
                  // Nothing to reorder with a single match, and an inert control
                  // reads as a broken one.
                  if (_matches.length > 1) _sortButton(t),
                ]),
                const SizedBox(height: 10),
                Expanded(child: _tab == 0 ? _tripList(t) : _postingList(t)),
              ]),
            ),
          ),
        ),
      ]),
    );
  }

  /// The bookable trips, with a small heading wherever the band changes.
  Widget _tripList(WanesTokens t) {
    if (_matches.isEmpty) return _empty(t);
    final starts = _bandStarts;

    return ListView.separated(
      key: ValueKey(_sort),
      padding: const EdgeInsets.only(bottom: 24),
      itemCount: _matches.length + 1,
      separatorBuilder: (_, __) => const SizedBox(height: 9),
      itemBuilder: (_, i) {
        // The post card rides at the end of the list rather than pinned: with
        // matches on screen it is an alternative, not the main action.
        if (i == _matches.length) {
          return Padding(
            padding: const EdgeInsets.only(top: 8),
            child: _postCard(t),
          );
        }

        final match = _matches[i];
        final card = _ResultCard(
          match: match,
          seats: widget.seats,
          from: widget.from.name,
          to: widget.to.name,
          fromLat: widget.from.lat,
          fromLng: widget.from.lng,
        );
        final band = starts[i];
        if (band == null) return card;

        return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          if (i != 0) const SizedBox(height: 6),
          Padding(
            padding: const EdgeInsetsDirectional.only(start: 2, bottom: 7),
            child: Text(band.label,
                style: WanesTheme.mono(
                    size: 10.5, weight: FontWeight.w700, color: t.ink2, spacing: .6)),
          ),
          card,
        ]);
      },
    );
  }

  /// Riders already asking for this route. Joining one is how a rider avoids
  /// posting a fourth near-identical trip on the same road at the same hour.
  Widget _postingList(WanesTokens t) {
    if (_postings.isEmpty) {
      return ListView(padding: const EdgeInsets.only(bottom: 24), children: [
        _noPostings(t),
        const SizedBox(height: 12),
        _postCard(t),
      ]);
    }

    return ListView.separated(
      padding: const EdgeInsets.only(bottom: 24),
      itemCount: _postings.length + 1,
      separatorBuilder: (_, __) => const SizedBox(height: 9),
      itemBuilder: (_, i) => i == _postings.length
          ? Padding(padding: const EdgeInsets.only(top: 8), child: _postCard(t))
          : _PostingCard(posting: _postings[i], onJoin: () => _join(_postings[i])),
    );
  }

  /// "Nobody is driving this yet — post it and let a driver take it."
  Widget _postCard(WanesTokens t) => Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: t.tealTint,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: t.teal.withValues(alpha: .35)),
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Row(children: [
            Icon(Icons.add_road_rounded, size: 18, color: t.tealInk),
            const SizedBox(width: 8),
            Expanded(
              child: Text(context.tr('riderTrip.postTitle'),
                  style: TextStyle(fontWeight: FontWeight.w800, fontSize: 14, color: t.tealInk)),
            ),
          ]),
          const SizedBox(height: 6),
          Text(context.tr('riderTrip.postBody'),
              style: TextStyle(fontSize: 12, height: 1.45, color: t.ink2)),
          const SizedBox(height: 12),
          PrimaryButton(label: context.tr('riderTrip.postCta'), onPressed: _post),
        ]),
      );

  Widget _noPostings(WanesTokens t) => Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: t.surface,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: t.border),
        ),
        child: Row(children: [
          Icon(Icons.groups_outlined, size: 18, color: t.ink2),
          const SizedBox(width: 10),
          Expanded(
            child: Text(context.tr('riderTrip.noPostings'),
                style: TextStyle(fontSize: 12.5, height: 1.4, color: t.ink2)),
          ),
        ]),
      );

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
          if (_resorting)
            Padding(
              padding: const EdgeInsetsDirectional.only(start: 6),
              child: WanesSpinner.mono(t.tealInk, size: 12),
            )
          else
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

  /// Nothing to book. Not a dead end: the rider can post the trip themselves,
  /// and the card that says so is the main action here rather than a footnote.
  Widget _empty(WanesTokens t) => ListView(
        padding: const EdgeInsets.only(bottom: 24),
        children: [
          const SizedBox(height: 18),
          Icon(Icons.route_outlined, size: 40, color: t.ink2),
          const SizedBox(height: 12),
          Text(context.tr('results.emptyTitle'),
              textAlign: TextAlign.center,
              style: TextStyle(fontWeight: FontWeight.w700, color: t.ink)),
          const SizedBox(height: 6),
          Text(context.tr('results.emptyBody'),
              textAlign: TextAlign.center,
              style: TextStyle(color: t.ink2, fontSize: 13)),
          const SizedBox(height: 18),
          _postCard(t),
        ],
      );
}

/// One matching trip: driver, price per seat, then a chip row with the small
/// teal "Book seat" button (design 03).
class _ResultCard extends StatelessWidget {
  const _ResultCard({
    required this.match,
    required this.seats,
    required this.from,
    required this.to,
    this.fromLat,
    this.fromLng,
  });

  final SearchMatch match;

  Trip get trip => match.trip;

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
        // On a corridor match the driver stops somewhere they had not planned,
        // so the walk is to a point on their road — the number the rider is
        // really choosing on, and meaningless without saying which band it is.
        if (match.isOnTheWay) ...[
          const SizedBox(height: 9),
          Row(children: [
            Icon(Icons.alt_route_rounded, size: 13, color: t.ink2),
            const SizedBox(width: 5),
            Expanded(
              child: Text(
                context.tr('results.walkToRoute', {
                  'pickup': _walk(context, match.pickupWalkKm),
                  'dropoff': _walk(context, match.dropoffWalkKm),
                }),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: WanesTheme.mono(
                    size: 10.5, weight: FontWeight.w500, color: t.ink2, spacing: 0),
              ),
            ),
          ]),
        ],
        // A trip that may not run at all: the driver asked for more seats than
        // it has. Hiding this would let a rider book without knowing.
        if (trip.isGathering) ...[
          const SizedBox(height: 9),
          Row(children: [
            Icon(Icons.groups_2_outlined, size: 13, color: t.amberInk),
            const SizedBox(width: 5),
            Expanded(
              child: Text(
                context.tr('results.confirmsAt', {
                  'held': trip.seatsHeld,
                  'min': trip.minSeatsToConfirm,
                }),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: WanesTheme.mono(
                    size: 10.5, weight: FontWeight.w600, color: t.amberInk, spacing: 0),
              ),
            ),
          ]),
        ],
      ]),
      ),
    );
  }

  /// "300 m" / "1.2 km" — metres below a kilometre, because "0.3 km" of walking
  /// is a number nobody pictures.
  static String _walk(BuildContext context, double km) => km < 1
      ? context.tr('results.walkMetres', {'m': (km * 1000).round()})
      : context.tr('results.walkKm', {'km': km.toStringAsFixed(1)});

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


/// A rider-posted trip somebody else already asked for, on the route the rider
/// just searched.
///
/// The whole point is the alternative it offers: joining costs nothing and
/// makes the posting more attractive to a driver, where posting a second
/// identical one splits the same two riders across two rows nobody claims.
class _PostingCard extends StatelessWidget {
  const _PostingCard({required this.posting, required this.onJoin});

  final RiderTrip posting;
  final VoidCallback onJoin;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final time = DateFormat('HH:mm', context.l10n.localeName).format(posting.departAt.toLocal());

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
                posting.isPool
                    ? context.trPlural('riderTrip.ridersWaiting', posting.riderCount)
                    : context.tr('riderTrip.oneRiderWaiting'),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink),
              ),
              const SizedBox(height: 2),
              Text(
                '$time · ${context.trPlural('vehicle.seatCount', posting.seatsWanted)}',
                style: WanesTheme.mono(
                    size: 11, weight: FontWeight.w500, color: t.ink2, spacing: 0),
              ),
            ]),
          ),
        ]),
        if (posting.hasConditions) ...[
          const SizedBox(height: 9),
          Row(children: [
            Icon(Icons.shield_outlined, size: 13, color: t.ink2),
            const SizedBox(width: 5),
            Expanded(
              child: Text(
                context.tr('riderTrip.conditionsSummary', {
                  'driver': posting.driverGenderPolicy.label,
                  'riders': posting.coRiderGenderPolicy.label,
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
          MetaChip(context.tr('riderTrip.noDriverYet')),
          const Spacer(),
          LiftInk(
            color: t.teal,
            onTap: onJoin,
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 7),
              child: Text(context.tr('riderTrip.join'),
                  style: TextStyle(fontWeight: FontWeight.w800, fontSize: 12.5, color: t.onTeal)),
            ),
          ),
        ]),
      ]),
    );
  }
}
