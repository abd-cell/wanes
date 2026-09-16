import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:http/http.dart' as http;
import 'environment.dart';
import 'l10n.dart';
import 'places.dart';

/// Outcome of a geocoder query — either results or a human-readable error.
class GeoSearchResult {
  const GeoSearchResult.ok(this.places) : error = null;
  const GeoSearchResult.failed(this.error) : places = const [];

  final List<Place> places;
  final String? error;

  bool get success => error == null;
}

/// Free-text location search and reverse lookup, backed by an OpenStreetMap
/// Nominatim endpoint (override the host with `--dart-define=GEOCODER_URL=...`).
///
/// Nominatim asks for at most one request per second, so callers debounce and
/// we keep a small in-memory cache of the queries already answered.
class GeocodingService {
  GeocodingService._();
  static final GeocodingService instance = GeocodingService._();

  final _http = http.Client();
  final Map<String, List<Place>> _cache = {};
  static const _timeout = Duration(seconds: 12);

  /// Half-width of the box we bias results towards when the rider's position is
  /// known — roughly 35 km, wide enough to cover a metro area without hiding
  /// the intercity results that Wanes exists to match.
  static const _nearbyDegrees = 0.35;

  /// Fallback bias when we have no fix: Jordan, where the service runs. This
  /// only *orders* the results — [Environment.geocoderCountries] is what keeps
  /// them inside the country.
  static const _defaultViewbox = '34.9,33.4,39.3,29.1';

  /// "31.95, 35.92" typed straight into the box — treat it as a dropped pin.
  static final _coordPattern =
      RegExp(r'^\s*(-?\d{1,2}(?:\.\d+)?)\s*,\s*(-?\d{1,3}(?:\.\d+)?)\s*$');

  static Place? parseCoordinates(String query) {
    final m = _coordPattern.firstMatch(query);
    if (m == null) return null;
    final lat = double.tryParse(m.group(1)!);
    final lng = double.tryParse(m.group(2)!);
    if (lat == null || lng == null) return null;
    if (lat.abs() > 90 || lng.abs() > 180) return null;
    return Place(AppLocalizations.current.t('places.droppedPin'), lat, lng,
        address: '${lat.toStringAsFixed(5)}, ${lng.toStringAsFixed(5)}', kind: 'pin');
  }

  /// A `viewbox` centred on the rider, or the country-wide default. Nominatim
  /// reads this as a preference, not a filter, so a far-away match still shows.
  static String viewboxAround(double? lat, double? lng) {
    if (lat == null || lng == null) return _defaultViewbox;
    final left = (lng - _nearbyDegrees).clamp(-180.0, 180.0);
    final right = (lng + _nearbyDegrees).clamp(-180.0, 180.0);
    final top = (lat + _nearbyDegrees).clamp(-90.0, 90.0);
    final bottom = (lat - _nearbyDegrees).clamp(-90.0, 90.0);
    return '$left,$top,$right,$bottom';
  }

  /// Searches for [query], preferring results near ([nearLat], [nearLng]) when
  /// the rider's position is known: the viewbox biases what the server returns
  /// and the distance re-sorts what comes back, so the coffee shop on this
  /// street outranks the same chain three cities over.
  Future<GeoSearchResult> search(
    String query, {
    double? nearLat,
    double? nearLng,
  }) async {
    final q = query.trim();
    if (q.length < 2) return const GeoSearchResult.ok([]);

    final pin = parseCoordinates(q);
    if (pin != null) return GeoSearchResult.ok([pin]);

    // The bias is part of the answer, so it is part of the cache key.
    final viewbox = viewboxAround(nearLat, nearLng);
    final cacheKey = '${q.toLowerCase()}|$viewbox';
    final cached = _cache[cacheKey];
    if (cached != null) return GeoSearchResult.ok(_ranked(cached, nearLat, nearLng));

    final uri = Uri.parse(Environment.geocoderUrl).replace(queryParameters: {
      'q': q,
      'format': 'jsonv2',
      'addressdetails': '1',
      // A slightly deeper page than the eye needs: the picker filter row
      // narrows this client-side, and a category is only worth offering when
      // enough of the answer came back to fill it.
      'limit': '18',
      'accept-language': AppLocalizations.current.localeName,
      'viewbox': viewbox,
      // Bias by viewbox, restrict by country: a rider whose fix has drifted
      // over a border — or who is searching before any fix arrives — still
      // gets Jordanian results, because a trip Wanes cannot serve is not a
      // useful search result.
      if (Environment.geocoderCountries.isNotEmpty)
        'countrycodes': Environment.geocoderCountries,
    });

    final res = await _get(uri);
    if (res.error != null) return GeoSearchResult.failed(res.error);

    final rows = res.body;
    if (rows is! List) {
      return GeoSearchResult.failed(AppLocalizations.current.t('geo.unreadable'));
    }
    final places = _dedupe(rows.map(_toPlace).whereType<Place>());
    _cache[cacheKey] = places;
    return GeoSearchResult.ok(_ranked(places, nearLat, nearLng));
  }

  /// Turns a coordinate into a named place ("Rainbow Street, Amman"). Used by
  /// the picker's "use my current location" row so the rider sees a street
  /// name rather than six decimal places.
  ///
  /// A failure here is never fatal: the caller falls back to the raw pin.
  Future<Place?> reverse(double lat, double lng) async {
    final uri = Uri.parse(Environment.reverseGeocoderUrl).replace(queryParameters: {
      'lat': '$lat',
      'lon': '$lng',
      'format': 'jsonv2',
      'addressdetails': '1',
      'zoom': '18',
      'accept-language': AppLocalizations.current.localeName,
    });

    final res = await _get(uri);
    final body = res.body;
    if (res.error != null || body is! Map) return null;
    final place = _toPlace(body);
    if (place == null) return null;
    // Keep the sensor's own coordinates — the reverse lookup snaps to the
    // centre of whatever feature it matched, which can be a block away.
    return Place(place.name, lat, lng, address: place.address, kind: place.kind);
  }

  /// One place to turn transport, status and JSON faults into localised copy.
  Future<({Object? body, String? error})> _get(Uri uri) async {
    try {
      final res = await _http
          .get(uri, headers: kIsWeb ? const {} : const {'User-Agent': 'WanesApp/0.1'})
          .timeout(_timeout);
      if (res.statusCode != 200) {
        return (body: null, error: AppLocalizations.current.t('geo.serverError'));
      }
      return (body: jsonDecode(utf8.decode(res.bodyBytes)), error: null);
    } on TimeoutException {
      return (body: null, error: AppLocalizations.current.t('geo.timeout'));
    } on SocketException {
      return (body: null, error: AppLocalizations.current.t('geo.noConnection'));
    } on http.ClientException {
      return (body: null, error: AppLocalizations.current.t('geo.unreachable'));
    } on FormatException {
      return (body: null, error: AppLocalizations.current.t('geo.unreadable'));
    }
  }

  /// Nominatim happily returns the same junction more than once under different
  /// OSM ids. Fold anything within ~11 m that shares a name into one row.
  static List<Place> _dedupe(Iterable<Place> places) {
    final seen = <String>{};
    final out = <Place>[];
    for (final p in places) {
      if (seen.add('${p.key}|${p.name.toLowerCase()}')) out.add(p);
    }
    return out;
  }

  /// Nearest first when we know where the rider is; otherwise the server's own
  /// order, which is already relevance-ranked.
  static List<Place> _ranked(List<Place> places, double? lat, double? lng) {
    if (lat == null || lng == null || places.length < 2) return places;
    return List<Place>.from(places)
      ..sort((a, b) => a.metresTo(lat, lng).compareTo(b.metresTo(lat, lng)));
  }

  /// Nominatim rows carry a long "display_name"; the first comma-separated
  /// chunk makes the title and the rest becomes the address line.
  static Place? _toPlace(Object? row) {
    if (row is! Map) return null;
    final lat = double.tryParse('${row['lat']}');
    final lng = double.tryParse('${row['lon']}');
    if (lat == null || lng == null) return null;

    final display = '${row['display_name'] ?? ''}'.trim();
    final named = '${row['name'] ?? ''}'.trim();
    final parts = display.split(',').map((p) => p.trim()).where((p) => p.isNotEmpty).toList();
    final title = named.isNotEmpty
        ? named
        : (parts.isEmpty ? AppLocalizations.current.t('places.unnamed') : parts.first);
    final rest = parts.isEmpty ? '' : parts.skip(named.isNotEmpty ? 0 : 1).join(', ');

    return Place(title, lat, lng, address: rest.isEmpty ? display : rest, kind: kindOf(row));
  }

  /// Folds Nominatim's `category`/`type` pair down to the vocabulary
  /// [placeIcon] understands. `type` is the specific one ("hospital"), so it
  /// wins; `category` ("aeroway", "leisure") is the fallback.
  static String? kindOf(Map<Object?, Object?> row) {
    final type = '${row['type'] ?? ''}'.trim().toLowerCase();
    final category = '${row['category'] ?? row['class'] ?? ''}'.trim().toLowerCase();
    for (final candidate in [type, category]) {
      if (candidate.isEmpty || candidate == 'yes') continue;
      if (placeIcon(candidate) != placeIcon(null)) return candidate;
    }
    return category.isEmpty ? null : category;
  }
}
