import 'package:flutter/material.dart';

import '../models/models.dart';
import '../services/services.dart';
import 'app_response.dart';
import 'places.dart';
import 'session.dart';

/// The rider's saved places, cached in memory so a screen can paint the
/// shortcuts synchronously and refresh behind them.
///
/// Unlike [RecentPlaces] these live on the server, so the cache is keyed to
/// the signed-in account — a second user on the same device never sees the
/// first one's shortcuts.
class SavedPlaces {
  SavedPlaces._();
  static final SavedPlaces instance = SavedPlaces._();

  final _service = SavedPlaceService();

  List<SavedPlace> _cache = const [];
  bool _loaded = false;
  int? _userId;

  /// Last known list without touching the network — safe inside a `build()`.
  List<SavedPlace> get cached => _cache;

  /// The single Home / Work shortcut, when the rider has set one.
  SavedPlace? byLabel(SavedPlaceLabel label) {
    for (final p in _cache) {
      if (p.label == label) return p;
    }
    return null;
  }

  /// Everything that is not Home or Work, in the order the server returned.
  List<SavedPlace> get favourites =>
      _cache.where((p) => p.label == SavedPlaceLabel.custom).toList();

  /// Fetches once per session and then serves the cache. [force] re-reads the
  /// server after a change.
  Future<List<SavedPlace>> load({bool force = false}) async {
    final userId = Session.instance.profile?.id;
    if (userId != _userId) {
      _cache = const [];
      _loaded = false;
      _userId = userId;
    } else if (_loaded && !force) {
      return _cache;
    }

    final res = await _service.list();
    // A failed load keeps whatever we already had rather than blanking the UI,
    // and stays unloaded so the next screen retries.
    if (res.success && res.data != null) {
      _cache = _sorted(res.data!);
      _loaded = true;
    }
    return _cache;
  }

  /// Saves [place] under [label]. Home and Work are singular in the UI but not
  /// on the server, so replacing one means creating the new entry first and
  /// dropping the old one only once the create succeeded.
  Future<AppResponse<SavedPlace>> save({
    required SavedPlaceLabel label,
    required String name,
    required Place place,
  }) async {
    final replacing = label == SavedPlaceLabel.custom ? null : byLabel(label);
    final res = await _service.add(label: label, name: name, place: place);
    if (!res.success || res.data == null) return res;
    if (replacing != null) await _service.remove(replacing.id);
    await load(force: true);
    return res;
  }

  Future<AppResponse> remove(int id) async {
    final res = await _service.remove(id);
    if (res.success) {
      _cache = _cache.where((p) => p.id != id).toList(growable: false);
    }
    return res;
  }

  /// Drops the cache on sign-out.
  void clear() {
    _cache = const [];
    _loaded = false;
    _userId = null;
  }

  /// Home, then Work, then favourites by name — the order the server uses and
  /// the one every screen shows.
  List<SavedPlace> _sorted(List<SavedPlace> places) {
    final list = List<SavedPlace>.from(places);
    list.sort((a, b) {
      final byLabel = a.label.value.compareTo(b.label.value);
      return byLabel != 0 ? byLabel : a.name.toLowerCase().compareTo(b.name.toLowerCase());
    });
    return list;
  }
}

/// The glyph that stands for a label everywhere it is listed.
IconData savedPlaceIcon(SavedPlaceLabel label) => switch (label) {
      SavedPlaceLabel.home => Icons.home_outlined,
      SavedPlaceLabel.work => Icons.work_outline_rounded,
      SavedPlaceLabel.custom => Icons.star_outline_rounded,
    };
