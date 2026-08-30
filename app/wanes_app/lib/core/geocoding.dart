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

/// Free-text location search, backed by an OpenStreetMap Nominatim endpoint
/// (override the host with `--dart-define=GEOCODER_URL=...`).
///
/// Nominatim asks for at most one request per second, so callers debounce and
/// we keep a small in-memory cache of the queries already answered.
class GeocodingService {
  GeocodingService._();
  static final GeocodingService instance = GeocodingService._();

  final _http = http.Client();
  final Map<String, List<Place>> _cache = {};
  static const _timeout = Duration(seconds: 12);

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
        address: '${lat.toStringAsFixed(5)}, ${lng.toStringAsFixed(5)}');
  }

  Future<GeoSearchResult> search(String query) async {
    final q = query.trim();
    if (q.length < 2) return const GeoSearchResult.ok([]);

    final pin = parseCoordinates(q);
    if (pin != null) return GeoSearchResult.ok([pin]);

    final cached = _cache[q.toLowerCase()];
    if (cached != null) return GeoSearchResult.ok(cached);

    final uri = Uri.parse(Environment.geocoderUrl).replace(queryParameters: {
      'q': q,
      'format': 'jsonv2',
      'addressdetails': '1',
      'limit': '8',
      'accept-language': 'en',
      // Bias (not restrict) results towards Jordan, where the service runs.
      'viewbox': '34.9,33.4,39.3,29.1',
    });

    try {
      final res = await _http
          .get(uri, headers: kIsWeb ? const {} : const {'User-Agent': 'WanesApp/0.1'})
          .timeout(_timeout);
      if (res.statusCode != 200) {
        return GeoSearchResult.failed(AppLocalizations.current.t('geo.serverError'));
      }
      final decoded = jsonDecode(utf8.decode(res.bodyBytes));
      if (decoded is! List) {
        return GeoSearchResult.failed(AppLocalizations.current.t('geo.unreadable'));
      }
      final places = decoded.map(_toPlace).whereType<Place>().toList();
      _cache[q.toLowerCase()] = places;
      return GeoSearchResult.ok(places);
    } on TimeoutException {
      return GeoSearchResult.failed(AppLocalizations.current.t('geo.timeout'));
    } on SocketException {
      return GeoSearchResult.failed(AppLocalizations.current.t('geo.noConnection'));
    } on http.ClientException {
      return GeoSearchResult.failed(AppLocalizations.current.t('geo.unreachable'));
    } on FormatException {
      return GeoSearchResult.failed(AppLocalizations.current.t('geo.unreadable'));
    }
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

    return Place(title, lat, lng, address: rest.isEmpty ? display : rest);
  }
}
