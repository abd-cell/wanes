import 'package:shared_preferences/shared_preferences.dart';
import 'places.dart';

/// The places this user actually picked, newest first — what the picker and
/// the home screen's "Recent places" list show instead of a hard-coded set.
class RecentPlaces {
  RecentPlaces._();
  static final RecentPlaces instance = RecentPlaces._();

  static const _key = 'recent_places';
  static const _max = 8;

  List<Place>? _cache;

  Future<List<Place>> load() async {
    final cached = _cache;
    if (cached != null) return cached;
    final prefs = await SharedPreferences.getInstance();
    final raw = prefs.getString(_key);
    final list = raw == null ? <Place>[] : Place.decodeList(raw);
    _cache = list;
    return list;
  }

  /// Most recent read without touching disk — safe for a synchronous build().
  List<Place> get cached => _cache ?? const [];

  Future<void> add(Place place) async {
    final list = List<Place>.from(await load())
      ..removeWhere((p) => p.key == place.key)
      ..insert(0, place);
    if (list.length > _max) list.removeRange(_max, list.length);
    _cache = list;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_key, Place.encodeList(list));
  }
}
