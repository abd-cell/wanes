import 'dart:async';
import 'package:flutter/material.dart';
import '../core/device_location.dart';
import '../core/geocoding.dart';
import '../core/l10n.dart';
import '../core/places.dart';
import '../core/recent_places.dart';
import '../core/saved_places.dart';
import '../core/theme.dart';
import '../models/models.dart';
import 'wanes_alerts.dart';
import 'wanes_ui.dart';
import 'wanes_motion.dart';

/// Search-driven location picker. Opens a sheet with a text field, queries the
/// geocoder as the user types (debounced), and returns the chosen [Place].
///
/// Four things happen before the network is involved:
///   * "Use my current location" takes a GPS fix and reverse-geocodes it, so
///     the rider never has to type where they are standing.
///   * The rider's saved and recent places are matched locally on every
///     keystroke, so the obvious pick appears instantly.
///   * A known position biases and re-ranks the geocoder results by distance,
///     and each row shows how far away it is.
///   * A filter row narrows what came back — by category, and by "nearby"
///     once we know where the rider is. It only offers filters the current
///     results would actually answer, so no chip ever leads to an empty list.
///
/// With an empty box it shows the user's recent picks, falling back to a few
/// suggestions the first time round. Returns null when dismissed.
Future<Place?> showPlacePicker(
  BuildContext context, {
  required String title,
  String? hint,
}) async {
  final t = WanesTokens.of(context);
  hint ??= context.tr('places.searchHint');
  final picked = await showModalBottomSheet<Place>(
    context: context,
    isScrollControlled: true,
    backgroundColor: t.surface,
    shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24))),
    builder: (_) => _PlaceSearchSheet(title: title, hint: hint!),
  );
  if (picked != null) await RecentPlaces.instance.add(picked);
  return picked;
}

/// A saved or recent place that matched what was typed, with the glyph that
/// says which of the two it came from.
typedef _LocalMatch = ({Place place, IconData icon});

/// Everything the list and the filter row are drawn from, computed once per
/// build so the chips and the rows can never disagree about what a filter does.
class _Shortlist {
  const _Shortlist({
    required this.local,
    required this.remote,
    required this.categories,
    required this.category,
    required this.hidden,
  });

  final List<_LocalMatch> local;
  final List<Place> remote;

  /// The chips worth offering: the categories still present once "nearby" has
  /// been applied, in taxonomy order.
  final List<PlaceCategory> categories;

  /// The category actually in force. Null once the chosen one is no longer in
  /// [categories] — which is what a fresh query does to a stale selection.
  final PlaceCategory? category;

  /// How many rows the active filter took off the screen. Drives the "nothing
  /// matches that filter" state, which has to be told apart from "the geocoder
  /// found nothing".
  final int hidden;

  bool get isEmpty => local.isEmpty && remote.isEmpty;
}

class _PlaceSearchSheet extends StatefulWidget {
  const _PlaceSearchSheet({required this.title, required this.hint});

  final String title;
  final String hint;

  @override
  State<_PlaceSearchSheet> createState() => _PlaceSearchSheetState();
}

class _PlaceSearchSheetState extends State<_PlaceSearchSheet> {
  static const _debounce = Duration(milliseconds: 350);

  /// Local suggestions are a shortlist, not a second results page — beyond
  /// this the geocoder is the better answer.
  static const _maxLocalMatches = 4;

  final _controller = TextEditingController();
  Timer? _timer;

  /// Bumped per keystroke so a slow response can't overwrite a newer one.
  int _requestId = 0;

  List<Place> _recents = const [];
  List<SavedPlace> _saved = SavedPlaces.instance.cached;
  List<Place> _results = const [];
  String _query = '';
  bool _busy = false;
  String? _error;

  /// What the chip row has selected. Reset whenever the query is cleared, so a
  /// filter can never sit active behind an empty box where its row is hidden.
  PlaceFilter _filter = PlaceFilter.none;

  /// Where the rider is, once we know: biases the search, orders the results,
  /// feeds the distance badges and unlocks the "nearby" filter. Null until a
  /// fix lands (or for good, if they decline) — everything below degrades to
  /// the un-located behaviour.
  double? _lat;
  double? _lng;
  bool _locating = false;

  bool get _located => _lat != null && _lng != null;

  @override
  void initState() {
    super.initState();
    RecentPlaces.instance.load().then((places) {
      if (mounted) setState(() => _recents = places);
    });
    SavedPlaces.instance.load().then((places) {
      if (mounted) setState(() => _saved = places);
    });
    _primeLocation();
  }

  @override
  void dispose() {
    _timer?.cancel();
    _controller.dispose();
    super.dispose();
  }

  /// Reuses a fix taken earlier this session so results are distance-ranked
  /// from the first keystroke. Deliberately silent: it never prompts and never
  /// complains, because the rider did not ask for their location yet.
  void _primeLocation() {
    final known = DeviceLocation.instance.lastKnown;
    if (known == null || !known.success) return;
    _lat = known.lat;
    _lng = known.lng;
  }

  void _onChanged(String value) {
    _timer?.cancel();
    final q = value.trim();
    setState(() {
      _query = q;
      _error = null;
      if (q.length < 2) {
        _results = const [];
        _busy = false;
        // The chip row lives with the results; clearing the box clears it too.
        _filter = PlaceFilter.none;
      } else {
        _busy = true;
      }
    });
    if (q.length < 2) return;
    _timer = Timer(_debounce, () => _run(q));
  }

  Future<void> _run(String q) async {
    final id = ++_requestId;
    setState(() {
      _busy = true;
      _error = null;
    });
    final res = await GeocodingService.instance.search(q, nearLat: _lat, nearLng: _lng);
    if (!mounted || id != _requestId) return;
    setState(() {
      _busy = false;
      _error = res.error;
      _results = res.places;
    });
  }

  // ── Current location ──────────────────────────────────────────────────────

  /// Takes a fix, names it, and returns it as the pick. A failure explains
  /// itself and leaves the sheet open, so the rider can still type an address.
  Future<void> _useCurrentLocation() async {
    if (_locating) return;
    setState(() => _locating = true);

    final fix = await DeviceLocation.instance.current();
    if (!mounted) return;

    if (!fix.success) {
      setState(() => _locating = false);
      await _reportLocationFailure(fix);
      return;
    }

    final lat = fix.lat!;
    final lng = fix.lng!;
    // Keep the fix even if the naming step fails — it still ranks the results.
    setState(() {
      _lat = lat;
      _lng = lng;
    });

    final named = await GeocodingService.instance.reverse(lat, lng);
    if (!mounted) return;
    setState(() => _locating = false);
    _pick(named ?? currentLocationPlace(lat, lng));
  }

  /// A one-off stumble (GPS still warming up, permission tapped away) is a
  /// toast. A permanent block is a dead end the rider cannot fix from here, so
  /// it gets the error card with a button straight into the OS settings.
  Future<void> _reportLocationFailure(LocationFix fix) async {
    if (fix.failure != LocationFailure.permissionDeniedForever) {
      WanesAlerts.error(context, context.tr('places.locationFailed'),
          message: fix.message);
      return;
    }

    final openSettings = await WanesAlerts.showErrorDialog(
      context,
      title: context.tr('places.locationFailed'),
      message: fix.message,
      retryLabel: context.tr('places.openSettings'),
    );
    if (openSettings) await DeviceLocation.instance.openSettings();
  }

  void _pick(Place p) => Navigator.pop(context, p);

  // ── Filtering ─────────────────────────────────────────────────────────────

  void _selectCategory(PlaceCategory? category) => setState(() => _filter =
      category == null ? _filter.withoutCategory() : _filter.copyWith(category: category));

  void _toggleNearby() =>
      setState(() => _filter = _filter.copyWith(nearbyOnly: !_filter.nearbyOnly));

  void _clearFilter() => setState(() => _filter = PlaceFilter.none);

  /// Saved and recent places that match what has been typed so far, nearest
  /// first, deduplicated against each other. Shown above the geocoder results
  /// so a place the rider already knows never loses to a server row.
  List<_LocalMatch> _localMatches() {
    if (_query.length < 2) return const [];
    final seen = <String>{};
    final out = <_LocalMatch>[];

    for (final s in _saved) {
      if (s.place.matches(_query) && seen.add(s.place.key)) {
        out.add((place: s.place, icon: savedPlaceIcon(s.label)));
      }
    }
    for (final p in _recents) {
      if (p.matches(_query) && seen.add(p.key)) {
        out.add((place: p, icon: Icons.history_rounded));
      }
    }

    final lat = _lat;
    final lng = _lng;
    if (lat != null && lng != null) {
      out.sort((a, b) =>
          a.place.metresTo(lat, lng).compareTo(b.place.metresTo(lat, lng)));
    }
    return out;
  }

  /// Folds the local matches and the geocoder results into what the sheet
  /// shows. "Nearby" is applied first because it decides which category chips
  /// are worth offering; the category then narrows what is left.
  _Shortlist _shortlist() {
    final lat = _lat;
    final lng = _lng;
    final localAll = _localMatches();
    final total = localAll.length + _results.length;

    final nearby = PlaceFilter(nearbyOnly: _filter.nearbyOnly);
    final localNear =
        localAll.where((m) => nearby.allows(m.place, lat: lat, lng: lng)).toList();
    final remoteNear = nearby.apply(_results, lat: lat, lng: lng);

    final categories =
        categoriesIn([...localNear.map((m) => m.place), ...remoteNear]);
    final chosen = _filter.category;
    final category = chosen != null && categories.contains(chosen) ? chosen : null;

    final local = category == null
        ? localNear
        : localNear.where((m) => m.place.category == category).toList();
    final remote = category == null
        ? remoteNear
        : remoteNear.where((p) => p.category == category).toList(growable: false);

    return _Shortlist(
      local: local.length > _maxLocalMatches ? local.sublist(0, _maxLocalMatches) : local,
      remote: remote,
      categories: categories,
      category: category,
      hidden: total - (local.length + remote.length),
    );
  }

  // ── Build ─────────────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final insets = MediaQuery.of(context).viewInsets.bottom;
    final height = MediaQuery.of(context).size.height * 0.86;
    final shortlist = _query.length >= 2 ? _shortlist() : null;

    return SafeArea(
      top: false,
      child: Padding(
        padding: EdgeInsets.only(bottom: insets),
        child: SizedBox(
          height: height,
          child: Column(children: [
            const SizedBox(height: 12),
            Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(color: t.border, borderRadius: BorderRadius.circular(2))),
            const SizedBox(height: 14),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Align(
                alignment: AlignmentDirectional.centerStart,
                child: Text(widget.title,
                    style: TextStyle(fontWeight: FontWeight.w800, fontSize: 18, color: t.ink)),
              ),
            ),
            const SizedBox(height: 12),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: _searchField(t),
            ),
            const SizedBox(height: 4),
            // Pinned above the list, not inside it: it is the fastest answer to
            // "where are you leaving from" and must not scroll away.
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 8, 20, 4),
              child: _currentLocationTile(t),
            ),
            if (shortlist != null && _showFilterBar(shortlist)) _filterBar(t, shortlist),
            Expanded(child: shortlist == null ? _idleList(t) : _body(t, shortlist)),
          ]),
        ),
      ),
    );
  }

  Widget _searchField(WanesTokens t) {
    return Container(
      decoration: BoxDecoration(
        color: t.surface2,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: t.border),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 12),
      child: Row(children: [
        Icon(Icons.search_rounded, size: 18, color: t.ink2),
        const SizedBox(width: 8),
        Expanded(
          child: TextField(
            controller: _controller,
            autofocus: true,
            textInputAction: TextInputAction.search,
            style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: t.ink),
            decoration: InputDecoration(
              isDense: true,
              border: InputBorder.none,
              hintText: widget.hint,
              hintStyle: TextStyle(fontSize: 14, fontWeight: FontWeight.w500, color: t.ink2),
            ),
            onChanged: _onChanged,
            onSubmitted: (v) {
              _timer?.cancel();
              if (v.trim().length >= 2) _run(v.trim());
            },
          ),
        ),
        if (_busy)
          WanesSpinner(size: 16, color: t.teal)
        else if (_query.isNotEmpty)
          GestureDetector(
            onTap: () {
              _controller.clear();
              _onChanged('');
            },
            child: Icon(Icons.close_rounded, size: 18, color: t.ink2),
          ),
      ]),
    );
  }

  /// The "use where I am" affordance: a tinted, full-width tap target that
  /// reads as an action rather than one more search result.
  Widget _currentLocationTile(WanesTokens t) {
    return Semantics(
      button: true,
      child: InkWell(
        onTap: _locating ? null : _useCurrentLocation,
        borderRadius: BorderRadius.circular(14),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 11),
          decoration: BoxDecoration(
            color: t.teal.withValues(alpha: 0.08),
            borderRadius: BorderRadius.circular(14),
            border: Border.all(color: t.teal.withValues(alpha: 0.28)),
          ),
          child: Row(children: [
            SizedBox(
              width: 22,
              height: 22,
              child: Center(
                child: _locating
                    ? WanesSpinner(size: 16, color: t.teal)
                    : Icon(Icons.my_location_rounded, size: 18, color: t.teal),
              ),
            ),
            const SizedBox(width: 11),
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(
                  context.tr(_locating ? 'places.locating' : 'places.useCurrentLocation'),
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink),
                ),
                const SizedBox(height: 1),
                Text(
                  context.tr(_locating ? 'places.locatingHint' : 'places.useCurrentLocationHint'),
                  style: TextStyle(fontSize: 12, color: t.ink2),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ]),
            ),
          ]),
        ),
      ),
    );
  }

  // ── Filter row ────────────────────────────────────────────────────────────

  /// Worth showing when there is a choice to make: more than one category in
  /// the results, or a fix that makes "nearby" meaningful. It also stays up
  /// while a filter is active and has emptied the list, otherwise the rider
  /// would have no way back to the full set.
  bool _showFilterBar(_Shortlist shortlist) =>
      shortlist.categories.length > 1 || _located || _filter.isActive;

  Widget _filterBar(WanesTokens t, _Shortlist shortlist) {
    final chips = <Widget>[
      // Distance first: it is the one filter that is about the rider rather
      // than about the place.
      if (_located)
        _chip(t,
            label: context.tr('places.filterNearby'),
            icon: Icons.near_me_rounded,
            selected: _filter.nearbyOnly,
            onTap: _toggleNearby),
      if (shortlist.categories.length > 1) ...[
        _chip(t,
            label: context.tr('places.filterAll'),
            selected: shortlist.category == null,
            onTap: () => _selectCategory(null)),
        ...shortlist.categories.map((c) => _chip(t,
            label: c.label,
            icon: c.icon,
            selected: shortlist.category == c,
            onTap: () => _selectCategory(c))),
      ],
    ];

    return SizedBox(
      height: 42,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 5),
        itemCount: chips.length,
        separatorBuilder: (_, __) => const SizedBox(width: 8),
        itemBuilder: (_, i) => chips[i],
      ),
    );
  }

  Widget _chip(
    WanesTokens t, {
    required String label,
    required bool selected,
    required VoidCallback onTap,
    IconData? icon,
  }) {
    return Semantics(
      button: true,
      selected: selected,
      child: GestureDetector(
        onTap: onTap,
        child: AnimatedContainer(
          duration: WanesMotion.press,
          curve: Curves.easeOut,
          padding: const EdgeInsets.symmetric(horizontal: 11),
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: selected ? t.tealTint : t.surface2,
            borderRadius: BorderRadius.circular(11),
            border: Border.all(color: selected ? t.teal : t.border),
          ),
          child: Row(children: [
            if (icon != null) ...[
              Icon(icon, size: 14, color: selected ? t.tealInk : t.ink2),
              const SizedBox(width: 6),
            ],
            Text(label,
                style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                    color: selected ? t.tealInk : t.ink)),
          ]),
        ),
      ),
    );
  }

  // ── Lists ─────────────────────────────────────────────────────────────────

  Widget _body(WanesTokens t, _Shortlist shortlist) {
    if (shortlist.isEmpty) {
      if (_busy) return Center(child: WanesSpinner(color: t.teal));

      // A filter that hid everything is the rider's own doing, so say so and
      // hand back the way out rather than claiming there were no matches.
      if (_filter.isActive && shortlist.hidden > 0) {
        return _message(
          t,
          Icons.filter_alt_off_rounded,
          context.tr('places.noFilterMatches'),
          context.tr('places.noFilterMatchesHint', {'query': _query}),
          actionLabel: context.tr('places.clearFilter'),
          onAction: _clearFilter,
        );
      }

      if (_error != null) {
        return _message(
            t, Icons.wifi_off_rounded, _error!, context.tr('places.searchRetryHint'),
            actionLabel: context.tr('common.retry'), onAction: () => _run(_query));
      }

      return _message(
          t,
          Icons.search_off_rounded,
          context.tr('places.noMatches', {'query': _query}),
          context.tr('places.noMatchesHint'));
    }

    return ListView(
      padding: const EdgeInsets.fromLTRB(20, 10, 20, 20),
      keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
      children: [
        if (shortlist.local.isNotEmpty) ...[
          MonoLabel(context.tr('places.yourPlaces'), size: 11),
          const SizedBox(height: 4),
          ...shortlist.local.map((m) => _row(m.place, icon: m.icon)),
          const SizedBox(height: 16),
          Row(children: [
            MonoLabel(context.tr('places.searchResults'), size: 11),
            const SizedBox(width: 8),
            // Local matches arrive instantly, so the network round-trip needs
            // its own progress marker down here once they are on screen.
            if (_busy) WanesSpinner(size: 12, color: t.teal),
          ]),
          const SizedBox(height: 4),
        ],
        // The rider's own places are already on screen, so a dead geocoder is
        // a strip above the missing half — not a takeover of the whole sheet.
        if (_error != null) ...[
          const SizedBox(height: 4),
          WanesInlineAlert(_error!, onTap: () => _run(_query)),
          const SizedBox(height: 8),
        ],
        ...shortlist.remote.map((p) => _row(p)),
      ],
    );
  }

  Widget _idleList(WanesTokens t) {
    final showing = _recents.isNotEmpty ? _recents : kSuggestedPlaces;
    return ListView(
      padding: const EdgeInsets.fromLTRB(20, 10, 20, 20),
      keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
      children: [
        // Saved shortcuts first — picking Home as a pick-up is one tap.
        if (_saved.isNotEmpty) ...[
          MonoLabel(context.tr('places.saved'), size: 11),
          const SizedBox(height: 4),
          ..._saved.map((s) => _row(s.place, icon: savedPlaceIcon(s.label))),
          const SizedBox(height: 16),
        ],
        MonoLabel(
            context.tr(_recents.isNotEmpty ? 'places.recentShort' : 'places.suggestions'),
            size: 11),
        const SizedBox(height: 4),
        ...showing.map((p) => _row(p, icon: _recents.isNotEmpty ? Icons.history_rounded : null)),
      ],
    );
  }

  /// One result row. The icon reflects what kind of place it is, and the
  /// trailing slot carries the distance from the rider when we know it.
  Widget _row(Place p, {IconData? icon}) {
    final t = WanesTokens.of(context);
    final lat = _lat;
    final lng = _lng;
    final away = lat == null || lng == null ? null : formatDistance(p.metresTo(lat, lng));

    return WanesListRow(
      icon: icon ?? placeIcon(p.kind),
      title: p.name,
      subtitle: p.detail,
      onTap: () => _pick(p),
      trailing: away == null
          ? const SizedBox.shrink()
          : Padding(
              padding: const EdgeInsetsDirectional.only(start: 8),
              child: Text(
                away,
                style: WanesTheme.mono(size: 10, weight: FontWeight.w600, color: t.ink2),
              ),
            ),
    );
  }

  Widget _message(
    WanesTokens t,
    IconData icon,
    String title,
    String hint, {
    String? actionLabel,
    VoidCallback? onAction,
  }) =>
      Center(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 32),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Icon(icon, size: 30, color: t.ink2),
            const SizedBox(height: 10),
            Text(title,
                textAlign: TextAlign.center,
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
            const SizedBox(height: 4),
            Text(hint, textAlign: TextAlign.center, style: TextStyle(fontSize: 12, color: t.ink2)),
            if (actionLabel != null && onAction != null) ...[
              const SizedBox(height: 14),
              Semantics(
                button: true,
                child: GestureDetector(
                  onTap: onAction,
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 9),
                    decoration: BoxDecoration(
                      color: t.tealTint,
                      borderRadius: BorderRadius.circular(11),
                      border: Border.all(color: t.teal.withValues(alpha: 0.4)),
                    ),
                    child: Text(actionLabel,
                        style: TextStyle(
                            fontSize: 12, fontWeight: FontWeight.w700, color: t.tealInk)),
                  ),
                ),
              ),
            ],
          ]),
        ),
      );
}
