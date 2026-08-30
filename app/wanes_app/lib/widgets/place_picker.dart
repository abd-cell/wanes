import 'dart:async';
import 'package:flutter/material.dart';
import '../core/geocoding.dart';
import '../core/l10n.dart';
import '../core/places.dart';
import '../core/recent_places.dart';
import '../core/saved_places.dart';
import '../core/theme.dart';
import '../models/models.dart';
import 'wanes_ui.dart';

/// Search-driven location picker. Opens a sheet with a text field, queries the
/// geocoder as the user types (debounced), and returns the chosen [Place].
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

class _PlaceSearchSheet extends StatefulWidget {
  const _PlaceSearchSheet({required this.title, required this.hint});

  final String title;
  final String hint;

  @override
  State<_PlaceSearchSheet> createState() => _PlaceSearchSheetState();
}

class _PlaceSearchSheetState extends State<_PlaceSearchSheet> {
  static const _debounce = Duration(milliseconds: 400);

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

  @override
  void initState() {
    super.initState();
    RecentPlaces.instance.load().then((places) {
      if (mounted) setState(() => _recents = places);
    });
    SavedPlaces.instance.load().then((places) {
      if (mounted) setState(() => _saved = places);
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    _controller.dispose();
    super.dispose();
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
      } else {
        _busy = true;
      }
    });
    if (q.length < 2) return;
    _timer = Timer(_debounce, () => _run(q));
  }

  Future<void> _run(String q) async {
    final id = ++_requestId;
    final res = await GeocodingService.instance.search(q);
    if (!mounted || id != _requestId) return;
    setState(() {
      _busy = false;
      _error = res.error;
      _results = res.places;
    });
  }

  void _pick(Place p) => Navigator.pop(context, p);

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final insets = MediaQuery.of(context).viewInsets.bottom;
    final height = MediaQuery.of(context).size.height * 0.86;

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
            const SizedBox(height: 6),
            Expanded(child: _body(t)),
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
          SizedBox(
            width: 16, height: 16,
            child: CircularProgressIndicator(strokeWidth: 2, color: t.teal),
          )
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

  Widget _body(WanesTokens t) {
    if (_error != null) {
      return _message(t, Icons.wifi_off_rounded, _error!, context.tr('places.searchRetryHint'));
    }

    if (_query.length < 2) {
      final showing = _recents.isNotEmpty ? _recents : kSuggestedPlaces;
      return ListView(
        padding: const EdgeInsets.fromLTRB(20, 10, 20, 20),
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
          ...showing.map((p) => _row(p)),
        ],
      );
    }

    if (_busy && _results.isEmpty) {
      return Center(child: CircularProgressIndicator(strokeWidth: 2.5, color: t.teal));
    }

    if (_results.isEmpty) {
      return _message(
          t,
          Icons.search_off_rounded,
          context.tr('places.noMatches', {'query': _query}),
          context.tr('places.noMatchesHint'));
    }

    return ListView(
      padding: const EdgeInsets.fromLTRB(20, 10, 20, 20),
      children: _results.map((p) => _row(p)).toList(),
    );
  }

  Widget _row(Place p, {IconData icon = Icons.place_outlined}) => WanesListRow(
        icon: icon,
        title: p.name,
        subtitle: p.detail,
        onTap: () => _pick(p),
        trailing: const SizedBox.shrink(),
      );

  Widget _message(WanesTokens t, IconData icon, String title, String hint) => Center(
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
          ]),
        ),
      );
}
