import 'dart:convert';
import 'dart:math' as math;

import 'package:flutter/material.dart';

import 'l10n.dart';

/// A point on the map the user can travel from / to.
///
/// Places now come from a geocoder search (see `core/geocoding.dart`) or from
/// the user's own recent picks — [kSuggestedPlaces] is only the empty-state
/// hint shown before anything has been typed or searched.
class Place {
  const Place(this.name, this.lat, this.lng, {this.address, this.kind});

  /// Short label, e.g. "University of Jordan".
  final String name;
  final double lat;
  final double lng;

  /// Full one-line address from the geocoder, when we have one.
  final String? address;

  /// Coarse category behind the pin ("airport", "hospital", …), used only to
  /// pick a row icon. Null for anything we didn't recognise. See [placeIcon].
  final String? kind;

  /// Secondary line for list rows — the address, or the raw coordinates.
  String get detail =>
      address ?? '${lat.toStringAsFixed(4)}, ${lng.toStringAsFixed(4)}';

  /// Identity used for de-duplicating recents (~11 m precision).
  String get key => '${lat.toStringAsFixed(4)},${lng.toStringAsFixed(4)}';

  /// Great-circle metres from this place to a raw coordinate.
  double metresTo(double otherLat, double otherLng) =>
      metresBetween(lat, lng, otherLat, otherLng);

  /// The filter group this place belongs to, derived from [kind].
  PlaceCategory get category => categoryOf(kind);

  /// True when the query matches the name or the address, case-insensitively.
  /// Used to surface saved/recent picks before the geocoder answers.
  bool matches(String query) {
    final q = query.trim().toLowerCase();
    if (q.isEmpty) return false;
    return name.toLowerCase().contains(q) ||
        (address?.toLowerCase().contains(q) ?? false);
  }

  Place copyWith({String? name, String? address, String? kind}) => Place(
        name ?? this.name,
        lat,
        lng,
        address: address ?? this.address,
        kind: kind ?? this.kind,
      );

  Map<String, dynamic> toJson() => {
        'name': name,
        'lat': lat,
        'lng': lng,
        if (address != null) 'address': address,
        if (kind != null) 'kind': kind,
      };

  static Place? fromJson(Object? json) {
    if (json is! Map) return null;
    final name = json['name'];
    final lat = json['lat'];
    final lng = json['lng'];
    if (name is! String || lat is! num || lng is! num) return null;
    final address = json['address'];
    final kind = json['kind'];
    return Place(name, lat.toDouble(), lng.toDouble(),
        address: address is String ? address : null,
        kind: kind is String ? kind : null);
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

/// Great-circle distance in metres (haversine, mean Earth radius). Good to a
/// fraction of a percent at city scale, which is all the picker needs.
double metresBetween(double lat1, double lng1, double lat2, double lng2) {
  const earthRadius = 6371000.0;
  const toRad = math.pi / 180.0;
  final dLat = (lat2 - lat1) * toRad;
  final dLng = (lng2 - lng1) * toRad;
  final a = math.sin(dLat / 2) * math.sin(dLat / 2) +
      math.cos(lat1 * toRad) * math.cos(lat2 * toRad) * math.sin(dLng / 2) * math.sin(dLng / 2);
  return 2 * earthRadius * math.atan2(math.sqrt(a), math.sqrt(1 - a));
}

/// Short distance badge for a list row: metres under 1 km, else one decimal
/// of a kilometre. Unit words are localised.
String formatDistance(double metres) {
  final l = AppLocalizations.current;
  if (metres < 950) {
    final rounded = (metres / 10).round() * 10;
    return l.t('geo.metresAway', {'value': '$rounded'});
  }
  final km = metres / 1000;
  final value = km >= 10 ? km.round().toString() : km.toStringAsFixed(1);
  return l.t('geo.kilometresAway', {'value': value});
}

/// Row icon for a geocoder category. Nominatim's `category`/`type` vocabulary is
/// enormous, so we fold it down to the handful of shapes a rider recognises and
/// fall back to a plain pin for everything else.
IconData placeIcon(String? kind) => switch (kind) {
      'aeroway' || 'airport' => Icons.local_airport_rounded,
      'railway' || 'station' || 'subway' => Icons.directions_subway_rounded,
      'bus_station' || 'bus_stop' => Icons.directions_bus_rounded,
      'hospital' || 'clinic' || 'doctors' || 'pharmacy' => Icons.local_hospital_rounded,
      'university' || 'college' || 'school' || 'kindergarten' => Icons.school_rounded,
      'mall' || 'supermarket' || 'shop' || 'marketplace' => Icons.shopping_bag_rounded,
      'restaurant' || 'cafe' || 'fast_food' => Icons.restaurant_rounded,
      'hotel' || 'guest_house' || 'hostel' => Icons.hotel_rounded,
      'mosque' || 'church' || 'place_of_worship' => Icons.mosque_rounded,
      'bank' || 'atm' => Icons.account_balance_rounded,
      'fuel' => Icons.local_gas_station_rounded,
      'park' || 'garden' || 'leisure' => Icons.park_rounded,
      'stadium' || 'sports_centre' => Icons.sports_soccer_rounded,
      'city' || 'town' || 'village' || 'suburb' || 'neighbourhood' || 'quarter' =>
        Icons.location_city_rounded,
      'road' || 'highway' || 'street' => Icons.signpost_rounded,
      'building' || 'house' || 'residential' => Icons.apartment_rounded,
      'pin' => Icons.my_location_rounded,
      _ => Icons.place_outlined,
    };

/// The coarse groups the place picker filters by.
///
/// Nominatim's `type`/`category` vocabulary runs to hundreds of values, which
/// is useless as a filter row, so each group folds the kinds a rider thinks of
/// as one thing ("food", "transport") into a single chip. Declaration order is
/// chip order, with the two catch-all groups — areas and plain addresses —
/// last, because they are what a query lands in when nothing more specific
/// matched.
enum PlaceCategory {
  transport('placeCategory.transport', Icons.directions_transit_rounded, {
    'aeroway', 'airport', 'aerodrome', 'terminal', 'railway', 'station',
    'halt', 'platform', 'subway', 'tram_stop', 'bus_station', 'bus_stop',
    'taxi', 'ferry_terminal', 'parking',
  }),
  food('placeCategory.food', Icons.restaurant_rounded, {
    'restaurant', 'cafe', 'fast_food', 'bakery', 'bar', 'pub', 'food_court',
    'ice_cream',
  }),
  shopping('placeCategory.shopping', Icons.shopping_bag_rounded, {
    'mall', 'supermarket', 'shop', 'marketplace', 'department_store',
    'convenience', 'retail', 'bakery_shop', 'kiosk',
  }),
  health('placeCategory.health', Icons.local_hospital_rounded, {
    'hospital', 'clinic', 'doctors', 'pharmacy', 'dentist', 'veterinary',
    'healthcare',
  }),
  education('placeCategory.education', Icons.school_rounded, {
    'university', 'college', 'school', 'kindergarten', 'library',
    'driving_school', 'language_school',
  }),
  worship('placeCategory.worship', Icons.mosque_rounded, {
    'mosque', 'church', 'place_of_worship', 'cathedral', 'chapel',
    'synagogue', 'shrine',
  }),
  leisure('placeCategory.leisure', Icons.park_rounded, {
    'park', 'garden', 'leisure', 'stadium', 'sports_centre', 'pitch',
    'playground', 'museum', 'attraction', 'tourism', 'cinema', 'theatre',
    'zoo', 'viewpoint',
  }),
  lodging('placeCategory.lodging', Icons.hotel_rounded, {
    'hotel', 'guest_house', 'hostel', 'motel', 'resort', 'apartments',
  }),
  services('placeCategory.services', Icons.account_balance_rounded, {
    'bank', 'atm', 'fuel', 'charging_station', 'police', 'fire_station',
    'post_office', 'office', 'government', 'embassy', 'townhall', 'courthouse',
  }),
  area('placeCategory.area', Icons.location_city_rounded, {
    'place', 'city', 'town', 'village', 'hamlet', 'suburb', 'neighbourhood',
    'quarter', 'municipality', 'county', 'state', 'district', 'locality',
  }),
  address('placeCategory.address', Icons.signpost_rounded, {
    'road', 'highway', 'street', 'residential', 'building', 'house',
    'apartment', 'pin', 'amenity',
  });

  const PlaceCategory(this.labelKey, this.icon, this.kinds);

  /// i18n key for the chip label — see `l10n/strings_{en,ar}.dart`.
  final String labelKey;
  final IconData icon;

  /// The geocoder kinds that land in this group. Anything unlisted falls back
  /// to [PlaceCategory.address], so a new OSM type is never invisible.
  final Set<String> kinds;

  String get label => AppLocalizations.current.t(labelKey);
}

/// The group a geocoder [kind] belongs to. Unknown and missing kinds are plain
/// addresses — the bucket that means "a spot on the map", not "uncategorised".
PlaceCategory categoryOf(String? kind) {
  final k = kind?.trim().toLowerCase();
  if (k == null || k.isEmpty) return PlaceCategory.address;
  for (final category in PlaceCategory.values) {
    if (category.kinds.contains(k)) return category;
  }
  return PlaceCategory.address;
}

/// The categories actually present in [places], in chip order. The picker only
/// offers a filter that would return something, so a search for "airport"
/// never grows a "Food & drink" chip.
List<PlaceCategory> categoriesIn(Iterable<Place> places) {
  final present = places.map((p) => p.category).toSet();
  return PlaceCategory.values
      .where(present.contains)
      .toList(growable: false);
}

/// What the picker's filter row selects: at most one category, plus an
/// optional "only what is close to me" radius.
///
/// Pure and value-like on purpose — the chip row, the result list and the
/// tests all decide what a filter means by calling the same [allows].
class PlaceFilter {
  const PlaceFilter({this.category, this.nearbyOnly = false});

  /// Nothing filtered — the state the picker opens in and returns to.
  static const none = PlaceFilter();

  /// What "nearby" means: a walk-or-short-drive radius around the rider, wide
  /// enough to cover the city they are standing in and narrow enough to drop
  /// the same-named place two governorates over.
  static const nearbyRadiusMetres = 10000.0;

  final PlaceCategory? category;

  /// Ignored while the rider's position is unknown — there is nothing to
  /// measure from, and hiding every row would be a worse answer than none.
  final bool nearbyOnly;

  bool get isActive => category != null || nearbyOnly;

  PlaceFilter copyWith({PlaceCategory? category, bool? nearbyOnly}) =>
      PlaceFilter(
        category: category ?? this.category,
        nearbyOnly: nearbyOnly ?? this.nearbyOnly,
      );

  /// Same filter with the category cleared — the "All" chip.
  PlaceFilter withoutCategory() => PlaceFilter(nearbyOnly: nearbyOnly);

  bool allows(Place place, {double? lat, double? lng}) {
    if (category != null && place.category != category) return false;
    if (!nearbyOnly || lat == null || lng == null) return true;
    return place.metresTo(lat, lng) <= nearbyRadiusMetres;
  }

  List<Place> apply(List<Place> places, {double? lat, double? lng}) => places
      .where((p) => allows(p, lat: lat, lng: lng))
      .toList(growable: false);

  @override
  bool operator ==(Object other) =>
      other is PlaceFilter &&
      other.category == category &&
      other.nearbyOnly == nearbyOnly;

  @override
  int get hashCode => Object.hash(category, nearbyOnly);
}
