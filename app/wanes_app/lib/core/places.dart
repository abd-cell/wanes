import 'dart:convert';

import 'l10n.dart';

/// A point on the map the user can travel from / to.
///
/// Places now come from a geocoder search (see `core/geocoding.dart`) or from
/// the user's own recent picks — [kSuggestedPlaces] is only the empty-state
/// hint shown before anything has been typed or searched.
class Place {
  const Place(this.name, this.lat, this.lng, {this.address});

  /// Short label, e.g. "University of Jordan".
  final String name;
  final double lat;
  final double lng;

  /// Full one-line address from the geocoder, when we have one.
  final String? address;

  /// Secondary line for list rows — the address, or the raw coordinates.
  String get detail =>
      address ?? '${lat.toStringAsFixed(4)}, ${lng.toStringAsFixed(4)}';

  /// Identity used for de-duplicating recents (~11 m precision).
  String get key => '${lat.toStringAsFixed(4)},${lng.toStringAsFixed(4)}';

  Map<String, dynamic> toJson() =>
      {'name': name, 'lat': lat, 'lng': lng, if (address != null) 'address': address};

  static Place? fromJson(Object? json) {
    if (json is! Map) return null;
    final name = json['name'];
    final lat = json['lat'];
    final lng = json['lng'];
    if (name is! String || lat is! num || lng is! num) return null;
    final address = json['address'];
    return Place(name, lat.toDouble(), lng.toDouble(),
        address: address is String ? address : null);
  }

  static String encodeList(List<Place> places) =>
      jsonEncode(places.map((p) => p.toJson()).toList());

  static List<Place> decodeList(String raw) {
    try {
      final decoded = jsonDecode(raw);
      if (decoded is! List) return const [];
      return decoded.map(Place.fromJson).whereType<Place>().toList();
    } catch (_) {
      return const [];
    }
  }
}

/// Shown only when the user has no recent places yet, so the picker is never
/// an empty box. Everything else is searched live — a geocoder result keeps
/// whatever name the geocoder gives it, so only these built-in hints are
/// translated.
List<Place> get kSuggestedPlaces {
  final l = AppLocalizations.current;
  return <Place>[
    Place(l.t('suggested.downtownAmman'), 31.9515, 35.9239),
    Place(l.t('suggested.abdaliBoulevard'), 31.9686, 35.9106),
    Place(l.t('suggested.universityOfJordan'), 32.0136, 35.8719),
    Place(l.t('suggested.queenAliaAirport'), 31.7226, 35.9932),
    Place(l.t('suggested.sweifieh'), 31.9490, 35.8600),
  ];
}

/// Kept for the driver screens that still need a stand-in "current location".
List<Place> get kPlaces => kSuggestedPlaces;
