import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../core/l10n.dart';
import '../core/places.dart';
import '../core/recent_places.dart';
import '../core/saved_places.dart';
import '../core/session.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/place_picker.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';
import '../widgets/when_picker.dart';
import 'saved_places_screen.dart';
import 'results_screen.dart';
import 'searching_screen.dart';


/// Home / search — the rider's landing tab. Mirrors prototype screen 02:
/// a greeting, the FROM→TO route card with a swap control, WHEN / SEATS
/// tiles, the teal "Find rides" CTA, then recent places.
class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key, this.onOpenProfile});

  /// Tapping the header avatar jumps to the Profile tab (wired by the shell).
  final VoidCallback? onOpenProfile;

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  final _search = SearchService();

  /// Null until the rider searches one out — nothing is pre-filled.
  Place? _from;
  Place? _to;
  int _seats = 1;
  bool _busy = false;

  /// The rider's own recent picks, shown under the search card.
  List<Place> _recents = const [];

  /// Home / Work / favourites, from the server. Cached, so this list is
  /// already populated on a second visit to the tab.
  List<SavedPlace> _saved = SavedPlaces.instance.cached;

  /// Departure time. null = "Now" (leave in ~10 min).
  DateTime? _when;

  @override
  void initState() {
    super.initState();
    _loadRecents();
    _loadSaved();
  }

  Future<void> _loadRecents() async {
    final places = await RecentPlaces.instance.load();
    if (mounted) setState(() => _recents = places);
  }

  Future<void> _loadSaved() async {
    final places = await SavedPlaces.instance.load();
    if (mounted) setState(() => _saved = places);
  }

  /// Shortcuts fill the destination — the common case — and the recents list
  /// still records the pick so it stays near the top there too.
  Future<void> _useSaved(SavedPlace saved) => _useRecent(saved.place);

  Future<void> _openSavedPlaces() async {
    await Navigator.push(
        context, MaterialPageRoute(builder: (_) => const SavedPlacesScreen()));
    if (mounted) _loadSaved();
  }

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
    final selection = await showWhenPicker(context, _when);
    if (selection == null || !mounted) return;
    final picked = selection.dateTime;
    // Anything at/just after "now" is treated as leaving now.
    setState(() => _when = (picked == null ||
            picked.isBefore(DateTime.now().add(const Duration(minutes: 2))))
        ? null
        : picked);
  }

  String _greeting() {
    final h = DateTime.now().hour;
    if (h < 12) return context.tr('home.goodMorning');
    if (h < 17) return context.tr('home.goodAfternoon');
    return context.tr('home.goodEvening');
  }

  String _initials(String name) {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
    return (parts.first[0] + parts.last[0]).toUpperCase();
  }

  Future<void> _pickPlace(bool isFrom) async {
    final picked = await showPlacePicker(
      context,
      title: context.tr(isFrom ? 'home.pickup' : 'home.destination'),
      hint: context.tr(isFrom ? 'home.searchPickupHint' : 'home.searchDestinationHint'),
    );
    if (picked == null || !mounted) return;
    setState(() => isFrom ? _from = picked : _to = picked);
    _loadRecents();
  }

  /// Setting a destination straight from the recents list still counts as a
  /// pick, so it bubbles back to the top of the list.
  Future<void> _useRecent(Place place) async {
    setState(() => _to = place);
    await RecentPlaces.instance.add(place);
    if (mounted) _loadRecents();
  }

  Future<void> _runSearch() async {
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
    final res = await _search.search(
      originLat: from.lat, originLng: from.lng, originAddress: from.name,
      destLat: to.lat, destLng: to.lng, destAddress: to.name,
      when: _departAt,
      seats: _seats,
    );
    if (!mounted) return;
    setState(() => _busy = false);

    if (!res.success || res.data == null) {
      WanesAlerts.failure(context, res,
          title: context.tr('home.searchFailed'), onRetry: _runSearch);
      return;
    }
    final result = res.data!;
    if (result.mode == SearchMode.carpool) {
      Navigator.push(context, MaterialPageRoute(
        builder: (_) => ResultsScreen(
          matches: result.matches,
          from: from.name,
          to: to.name,
          seats: _seats,
          fromLat: from.lat,
          fromLng: from.lng,
          rideRequestId: result.rideRequestId,
        ),
      ));
    } else {
      Navigator.push(context, MaterialPageRoute(
        builder: (_) => SearchingScreen(
          rideRequestId: result.rideRequestId,
          driversNotified: result.driversNotified,
        ),
      ));
    }
  }



  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final name = Session.instance.profile?.name.trim();
    final displayName =
        (name?.isNotEmpty ?? false) ? name!.split(' ').first : context.tr('home.there');

    return SafeArea(
      bottom: false,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
        children: [
          // ── greeting + avatar ──
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(context.tr('home.greeting',
                          {'greeting': _greeting(), 'name': displayName}),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: WanesTheme.mono(
                          size: 12, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
                  const SizedBox(height: 4),
                  Text(context.tr('home.whereHeaded'),
                      style: TextStyle(fontSize: 23, fontWeight: FontWeight.w800, letterSpacing: -0.5, height: 1.15, color: t.ink)),
                ]),
              ),
              GestureDetector(
                onTap: widget.onOpenProfile,
                child: AvatarBadge(_initials(name ?? ''), size: 42),
              ),
            ],
          ),
          const SizedBox(height: 18),
          // ── route card ──
          _routeCard(t),
          const SizedBox(height: 12),
          // ── WHEN / SEATS tiles ──
          Row(children: [
            Expanded(child: _whenTile(t)),
            const SizedBox(width: 12),
            Expanded(child: _seatsTile(t)),
          ]),
          const SizedBox(height: 14),
          PrimaryButton(
              label: context.tr('home.findRides'),
              busy: _busy,
              onPressed: _busy ? null : _runSearch),
          const SizedBox(height: 22),
          // ── saved shortcuts ──
          SectionHeader(context.tr('places.savedPlaces'),
              actionLabel: context.tr(_saved.isEmpty ? 'common.add' : 'common.manage'),
              onAction: _openSavedPlaces),
          const SizedBox(height: 4),
          if (_saved.isEmpty)
            WanesListRow(
              icon: Icons.add_location_alt_outlined,
              title: context.tr('places.saveHomeWork'),
              subtitle: context.tr('places.saveHomeWorkBody'),
              onTap: _openSavedPlaces,
            )
          else
            ..._saved.take(3).map((p) => WanesListRow(
                  icon: savedPlaceIcon(p.label),
                  title: p.name,
                  subtitle: p.address.isEmpty ? context.tr('places.tapToSet') : p.address,
                  onTap: () => _useSaved(p),
                )),
          const SizedBox(height: 22),
          // ── recent places ──
          SectionHeader(context.tr(_recents.isEmpty ? 'places.suggestions' : 'places.recent'),
              actionLabel: context.tr('common.search'), onAction: () => _pickPlace(false)),
          const SizedBox(height: 4),
          ...(_recents.isEmpty ? kSuggestedPlaces : _recents).take(3).map((p) => WanesListRow(
                icon: Icons.place_outlined,
                title: p.name,
                subtitle: context.tr('places.tapToSet'),
                onTap: () => _useRecent(p),
              )),
        ],
      ),
    );
  }

  Widget _routeCard(WanesTokens t) {
    return WanesCard(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      child: Stack(
        children: [
          Column(children: [
            _routeRow(t, false, context.tr('common.from'), _from?.name, () => _pickPlace(true),
                placeholder: context.tr('home.searchPickup'), divider: true),
            _routeRow(t, true, context.tr('common.to'), _to?.name, () => _pickPlace(false),
                placeholder: context.tr('home.searchDestination'), divider: false),
          ]),
          PositionedDirectional(
            end: 0, top: 0, bottom: 0,
            child: Center(
              child: GestureDetector(
                onTap: () => setState(() {
                  final tmp = _from; _from = _to; _to = tmp;
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
        ],
      ),
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
              MonoLabel(label),
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
          const SizedBox(width: 44),
        ]),
      ),
    );
  }

  Widget _whenTile(WanesTokens t) {
    return GestureDetector(
      onTap: _pickWhen,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        decoration: BoxDecoration(
          color: t.surface2, borderRadius: BorderRadius.circular(14), border: Border.all(color: t.border),
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          MonoLabel(context.tr('common.when'), spacing: 0.6),
          const SizedBox(height: 3),
          Row(children: [
            Expanded(
              child: Text(_whenLabel(),
                  maxLines: 1, overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
            ),
            Icon(_when == null ? Icons.schedule : Icons.expand_more_rounded, size: 16, color: t.ink2),
          ]),
        ]),
      ),
    );
  }

  Widget _seatsTile(WanesTokens t) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
      decoration: BoxDecoration(
        color: t.surface2, borderRadius: BorderRadius.circular(14), border: Border.all(color: t.border),
      ),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        MonoLabel(context.tr('common.seats'), spacing: 0.6),
        const SizedBox(height: 2),
        SeatStepper(value: _seats, onChanged: (v) => setState(() => _seats = v)),
      ]),
    );
  }
}
