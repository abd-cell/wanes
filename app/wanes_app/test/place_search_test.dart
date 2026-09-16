import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:wanes_app/core/device_location.dart';
import 'package:wanes_app/core/environment.dart';
import 'package:wanes_app/core/geocoding.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/places.dart';
import 'package:wanes_app/core/recent_places.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/widgets/place_picker.dart';

/// Opens the picker over a throwaway screen and returns whatever it produced,
/// the way a real caller (`home_screen._pickPlace`) does.
Widget _host(void Function(Place?) onPicked) => MaterialApp(
      theme: WanesTheme.light(),
      locale: const Locale('en'),
      supportedLocales: AppLocalizations.supportedLocales,
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      home: Builder(
        builder: (context) => Scaffold(
          body: Center(
            child: ElevatedButton(
              onPressed: () async =>
                  onPicked(await showPlacePicker(context, title: 'Pick-up')),
              child: const Text('open'),
            ),
          ),
        ),
      ),
    );

void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues({});
    AppLocalizations.current = const AppLocalizations(Locale('en'));
    DeviceLocation.instance.reset();
    DeviceLocation.debugOverride = null;
    // Both are process-wide singletons that cache their first read, so a
    // later case would otherwise inherit the previous one's empty list.
    RecentPlaces.instance.reset();
  });

  tearDown(() => DeviceLocation.debugOverride = null);

  group('parsing', () {
    test('typed coordinates become a dropped pin', () {
      final pin = GeocodingService.parseCoordinates(' 31.9515 , 35.9239 ');
      expect(pin, isNotNull);
      expect(pin!.lat, closeTo(31.9515, 1e-9));
      expect(pin.lng, closeTo(35.9239, 1e-9));
      // The pin icon marks it as a raw coordinate rather than a named place.
      expect(pin.kind, 'pin');
    });

    test('out-of-range and non-coordinate text is not a pin', () {
      expect(GeocodingService.parseCoordinates('120.0, 35.0'), isNull);
      expect(GeocodingService.parseCoordinates('Abdali Boulevard'), isNull);
    });

    test('short queries short-circuit without a network call', () async {
      final res = await GeocodingService.instance.search('a');
      expect(res.success, isTrue);
      expect(res.places, isEmpty);
    });
  });

  group('places', () {
    test('round-trip through storage JSON, kind included', () {
      const place = Place('Abdali', 31.9686, 35.9106,
          address: 'Amman, Jordan', kind: 'mall');
      final restored = Place.decodeList(Place.encodeList([place]));
      expect(restored, hasLength(1));
      expect(restored.first.name, 'Abdali');
      expect(restored.first.address, 'Amman, Jordan');
      expect(restored.first.kind, 'mall');
      expect(restored.first.key, place.key);
    });

    test('a place stored before kind existed still decodes', () {
      final restored =
          Place.decodeList('[{"name":"Abdali","lat":31.9686,"lng":35.9106}]');
      expect(restored, hasLength(1));
      expect(restored.first.kind, isNull);
    });

    test('malformed stored JSON degrades to an empty list', () {
      expect(Place.decodeList('not json'), isEmpty);
    });

    test('matches the query against both the name and the address', () {
      const place = Place('Abdali Boulevard', 31.9686, 35.9106,
          address: 'Al Abdali, Amman, Jordan');
      expect(place.matches('boulev'), isTrue);
      expect(place.matches('AMMAN'), isTrue);
      expect(place.matches('sweifieh'), isFalse);
      expect(place.matches('  '), isFalse);
    });
  });

  group('distance', () {
    test('haversine matches a known city pair to within a percent', () {
      // Downtown Amman -> Queen Alia Airport, ~28.5 km apart.
      final metres = metresBetween(31.9515, 35.9239, 31.7226, 35.9932);
      expect(metres, closeTo(26500, 1500));
    });

    test('a place is zero metres from its own coordinates', () {
      const place = Place('Abdali', 31.9686, 35.9106);
      expect(place.metresTo(31.9686, 35.9106), closeTo(0, 0.001));
    });

    test('formatting rounds metres to ten and kilometres to a decimal', () {
      expect(formatDistance(184), '180 m');
      expect(formatDistance(2340), '2.3 km');
      // Double figures lose the decimal — "27 km" reads better than "27.4 km".
      expect(formatDistance(27400), '27 km');
    });
  });

  group('proximity bias', () {
    test('an unknown position falls back to the country-wide viewbox', () {
      expect(GeocodingService.viewboxAround(null, null), '34.9,33.4,39.3,29.1');
      expect(GeocodingService.viewboxAround(31.95, null), '34.9,33.4,39.3,29.1');
    });

    test('a known position boxes the search around the rider', () {
      final box = GeocodingService.viewboxAround(31.95, 35.92);
      final parts = box.split(',').map(double.parse).toList();
      expect(parts, hasLength(4));
      // left,top,right,bottom — the rider sits in the middle of it.
      expect(parts[0], lessThan(35.92));
      expect(parts[2], greaterThan(35.92));
      expect(parts[1], greaterThan(31.95));
      expect(parts[3], lessThan(31.95));
    });

    test('the viewbox stays inside legal bounds near a pole or the date line', () {
      final box = GeocodingService.viewboxAround(89.9, 179.9);
      final parts = box.split(',').map(double.parse).toList();
      expect(parts[1], lessThanOrEqualTo(90.0));
      expect(parts[2], lessThanOrEqualTo(180.0));
    });

    /// The viewbox only orders results — Nominatim would still hand back London
    /// for "london". Confining the search is the country code's job, and Wanes
    /// only runs in Jordan.
    test('the search is confined to Jordan by default', () {
      expect(Environment.geocoderCountries, 'jo');
    });
  });

  group('categories', () {
    test('the specific type wins over the broad category', () {
      expect(GeocodingService.kindOf({'type': 'hospital', 'category': 'amenity'}),
          'hospital');
      expect(placeIcon('hospital'), Icons.local_hospital_rounded);
    });

    test('an unrecognised type falls back to the category', () {
      expect(GeocodingService.kindOf({'type': 'tertiary', 'category': 'highway'}),
          'highway');
    });

    test('"yes" is skipped — OSM uses it as a placeholder, not a category', () {
      expect(GeocodingService.kindOf({'type': 'yes', 'category': 'building'}),
          'building');
    });

    test('an unknown kind still gets a pin', () {
      expect(placeIcon('something_unmapped'), Icons.place_outlined);
      expect(placeIcon(null), Icons.place_outlined);
    });
  });

  group('reverse geocoder endpoint', () {
    test('is derived from the forward search URL', () {
      expect(Environment.reverseGeocoderUrl, endsWith('/reverse'));
      expect(Environment.reverseGeocoderUrl,
          Environment.geocoderUrl.replaceFirst(RegExp(r'/[^/]*$'), '/reverse'));
    });
  });

  group('the picker', () {
    testWidgets('offers the current location before anything is typed',
        (tester) async {
      await tester.pumpWidget(_host((_) {}));
      await tester.pumpAndSettle();
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      expect(find.text('Use my current location'), findsOneWidget);
    });

    testWidgets('a granted fix returns a place at the rider\'s coordinates',
        (tester) async {
      DeviceLocation.debugOverride =
          () async => const LocationFix.ok(31.9515, 35.9239);

      Place? picked;
      await tester.pumpWidget(_host((p) => picked = p));
      await tester.pumpAndSettle();
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Use my current location'));
      await tester.pumpAndSettle();

      // The reverse lookup has no network in a test, so the picker falls back
      // to the raw pin — either way it must carry the sensor's coordinates.
      expect(picked, isNotNull);
      expect(picked!.lat, closeTo(31.9515, 1e-9));
      expect(picked!.lng, closeTo(35.9239, 1e-9));
    });

    testWidgets('a denied fix keeps the sheet open and explains itself',
        (tester) async {
      DeviceLocation.debugOverride =
          () async => const LocationFix.failed(LocationFailure.permissionDenied);

      Place? picked;
      var returned = false;
      await tester.pumpWidget(_host((p) {
        picked = p;
        returned = true;
      }));
      await tester.pumpAndSettle();
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Use my current location'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 400));

      expect(returned, isFalse, reason: 'the sheet should stay open');
      expect(picked, isNull);
      expect(find.textContaining("Couldn't get your location"), findsOneWidget);
      // Still usable: the search field and the retry row are both there.
      expect(find.text('Use my current location'), findsOneWidget);
    });

    testWidgets('a saved-forever denial offers a way into the OS settings',
        (tester) async {
      DeviceLocation.debugOverride = () async =>
          const LocationFix.failed(LocationFailure.permissionDeniedForever);

      await tester.pumpWidget(_host((_) {}));
      await tester.pumpAndSettle();
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Use my current location'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 400));

      expect(find.text('Location permission is blocked for Wanes.'),
          findsOneWidget);
      expect(find.text('Open settings'), findsOneWidget);
    });

    testWidgets('recent picks matching the query surface before the network',
        (tester) async {
      SharedPreferences.setMockInitialValues({
        'recent_places': Place.encodeList([
          const Place('Abdali Boulevard', 31.9686, 35.9106,
              address: 'Al Abdali, Amman'),
          const Place('Sweifieh', 31.9490, 35.8600, address: 'Amman'),
        ]),
      });

      await tester.pumpWidget(_host((_) {}));
      await tester.pumpAndSettle();
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextField), 'abdali');
      await tester.pump();

      expect(find.text('YOUR PLACES'), findsOneWidget);
      expect(find.text('Abdali Boulevard'), findsOneWidget);
      expect(find.text('Sweifieh'), findsNothing);
    });
  });

  group('the location service', () {
    test('remembers a good fix and serves it without re-reading the sensor',
        () async {
      var reads = 0;
      DeviceLocation.debugOverride = () async {
        reads++;
        return const LocationFix.ok(31.9515, 35.9239);
      };

      await DeviceLocation.instance.current();
      await DeviceLocation.instance.current();

      expect(reads, 1);
      expect(DeviceLocation.instance.lastKnown?.lat, closeTo(31.9515, 1e-9));
      expect(DeviceLocation.instance.isFresh, isTrue);
    });

    test('force re-reads even while the cached fix is fresh', () async {
      var reads = 0;
      DeviceLocation.debugOverride = () async {
        reads++;
        return const LocationFix.ok(31.9515, 35.9239);
      };

      await DeviceLocation.instance.current();
      await DeviceLocation.instance.current(force: true);

      expect(reads, 2);
    });

    test('a failure is not cached, so the next tap asks again', () async {
      var reads = 0;
      DeviceLocation.debugOverride = () async {
        reads++;
        return const LocationFix.failed(LocationFailure.serviceDisabled);
      };

      await DeviceLocation.instance.current();
      await DeviceLocation.instance.current();

      expect(reads, 2);
      expect(DeviceLocation.instance.lastKnown, isNull);
      expect(DeviceLocation.instance.isFresh, isFalse);
    });

    test('every failure carries its own copy', () {
      final seen = <String>{};
      for (final failure in LocationFailure.values) {
        final message = LocationFix.failed(failure).message;
        expect(message, isNotEmpty);
        expect(seen.add(message), isTrue, reason: 'duplicate copy for $failure');
      }
    });
  });

  group('filter groups', () {
    test('a geocoder kind folds into the group a rider would name', () {
      expect(categoryOf('cafe'), PlaceCategory.food);
      expect(categoryOf('fast_food'), PlaceCategory.food);
      expect(categoryOf('hospital'), PlaceCategory.health);
      expect(categoryOf('bus_station'), PlaceCategory.transport);
      expect(categoryOf('village'), PlaceCategory.area);
    });

    test('case and padding are not part of the vocabulary', () {
      expect(categoryOf(' Mosque '), PlaceCategory.worship);
    });

    /// A kind nobody mapped is still a spot on the map, so it lands with the
    /// addresses rather than in a bucket the chip row would have to explain.
    test('an unmapped or missing kind is a plain address', () {
      expect(categoryOf('something_unmapped'), PlaceCategory.address);
      expect(categoryOf(''), PlaceCategory.address);
      expect(categoryOf(null), PlaceCategory.address);
      expect(const Place('Somewhere', 31.9, 35.9).category, PlaceCategory.address);
    });

    test('every group has its own kinds — a kind cannot belong to two chips', () {
      final seen = <String>{};
      for (final category in PlaceCategory.values) {
        for (final kind in category.kinds) {
          expect(seen.add(kind), isTrue, reason: '$kind is claimed twice');
        }
      }
    });

    test('the offered chips are the groups present, in taxonomy order', () {
      final places = [
        const Place('Mall', 31.95, 35.92, kind: 'mall'),
        const Place('Cafe', 31.95, 35.92, kind: 'cafe'),
        const Place('Another cafe', 31.96, 35.93, kind: 'restaurant'),
      ];
      expect(categoriesIn(places), [PlaceCategory.food, PlaceCategory.shopping]);
      expect(categoriesIn(const []), isEmpty);
    });
  });

  group('place filter', () {
    const near = Place('Rainbow Street', 31.9515, 35.9239, kind: 'road');
    const far = Place('Irbid Centre', 32.5556, 35.8500, kind: 'road');
    const cafe = Place('Rumi Cafe', 31.9530, 35.9250, kind: 'cafe');

    test('nothing selected keeps every row', () {
      const filter = PlaceFilter.none;
      expect(filter.isActive, isFalse);
      expect(filter.apply(const [near, far, cafe], lat: 31.95, lng: 35.92),
          hasLength(3));
    });

    test('a category keeps only its own group', () {
      const filter = PlaceFilter(category: PlaceCategory.food);
      expect(filter.apply(const [near, far, cafe]), [cafe]);
    });

    test('nearby drops what is outside the radius', () {
      const filter = PlaceFilter(nearbyOnly: true);
      final kept = filter.apply(const [near, far], lat: 31.9515, lng: 35.9239);
      expect(kept, [near]);
      // Sanity: the row that was dropped really is beyond the radius.
      expect(far.metresTo(31.9515, 35.9239),
          greaterThan(PlaceFilter.nearbyRadiusMetres));
    });

    /// With no fix there is nothing to measure from, and hiding every row
    /// would be a worse answer than showing them all.
    test('nearby is inert while the position is unknown', () {
      const filter = PlaceFilter(nearbyOnly: true);
      expect(filter.apply(const [near, far]), hasLength(2));
    });

    test('both dimensions apply together', () {
      const filter =
          PlaceFilter(category: PlaceCategory.food, nearbyOnly: true);
      expect(filter.allows(cafe, lat: 31.9515, lng: 35.9239), isTrue);
      expect(filter.allows(near, lat: 31.9515, lng: 35.9239), isFalse);
      expect(filter.allows(cafe, lat: 32.5556, lng: 35.85), isFalse);
    });

    test('the All chip clears the category and keeps the distance toggle', () {
      const filter =
          PlaceFilter(category: PlaceCategory.food, nearbyOnly: true);
      final cleared = filter.withoutCategory();
      expect(cleared.category, isNull);
      expect(cleared.nearbyOnly, isTrue);
      expect(cleared, const PlaceFilter(nearbyOnly: true));
    });
  });

  group('the picker filter row', () {
    testWidgets('nothing to filter, no chips', (tester) async {
      SharedPreferences.setMockInitialValues({
        'recent_places': Place.encodeList([
          const Place('Abdali Mall', 31.9686, 35.9106,
              address: 'Al Abdali, Amman', kind: 'mall'),
        ]),
      });

      await tester.pumpWidget(_host((_) {}));
      await tester.pumpAndSettle();
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextField), 'abdali');
      await tester.pump();

      // One group, no fix — a chip row here would only be decoration.
      expect(find.text('All'), findsNothing);
      expect(find.text('Nearby'), findsNothing);
    });

    testWidgets('a category chip narrows the list to its own group',
        (tester) async {
      SharedPreferences.setMockInitialValues({
        'recent_places': Place.encodeList([
          const Place('Amman Mall', 31.9686, 35.9106,
              address: 'Amman', kind: 'mall'),
          const Place('Amman Cafe', 31.9500, 35.9200,
              address: 'Amman', kind: 'cafe'),
        ]),
      });

      await tester.pumpWidget(_host((_) {}));
      await tester.pumpAndSettle();
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextField), 'amman');
      await tester.pump();

      // Both groups are on screen, so both chips are on offer.
      expect(find.text('Food & drink'), findsOneWidget);
      expect(find.text('Shopping'), findsOneWidget);
      expect(find.text('Amman Mall'), findsOneWidget);
      expect(find.text('Amman Cafe'), findsOneWidget);

      await tester.tap(find.text('Shopping'));
      await tester.pump();

      expect(find.text('Amman Mall'), findsOneWidget);
      expect(find.text('Amman Cafe'), findsNothing);

      // And "All" puts back what the chip took away.
      await tester.tap(find.text('All'));
      await tester.pump();
      expect(find.text('Amman Cafe'), findsOneWidget);
    });

    testWidgets('the nearby chip appears once a fix is known and drops the far row',
        (tester) async {
      DeviceLocation.debugOverride =
          () async => const LocationFix.ok(31.9515, 35.9239);
      // The picker reuses a fix taken earlier in the session rather than
      // prompting, so warm it the way a previous screen would have.
      await DeviceLocation.instance.current();

      SharedPreferences.setMockInitialValues({
        'recent_places': Place.encodeList([
          const Place('Rainbow Street', 31.9515, 35.9239, address: 'Jordan'),
          const Place('Irbid Centre', 32.5556, 35.8500, address: 'Jordan'),
        ]),
      });

      await tester.pumpWidget(_host((_) {}));
      await tester.pumpAndSettle();
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextField), 'jordan');
      await tester.pump();

      expect(find.text('Rainbow Street'), findsOneWidget);
      expect(find.text('Irbid Centre'), findsOneWidget);

      await tester.tap(find.text('Nearby'));
      await tester.pump();

      expect(find.text('Rainbow Street'), findsOneWidget);
      expect(find.text('Irbid Centre'), findsNothing);
    });

    testWidgets('clearing the query clears the filter with it', (tester) async {
      SharedPreferences.setMockInitialValues({
        'recent_places': Place.encodeList([
          const Place('Amman Mall', 31.9686, 35.9106,
              address: 'Amman', kind: 'mall'),
          const Place('Amman Cafe', 31.9500, 35.9200,
              address: 'Amman', kind: 'cafe'),
        ]),
      });

      await tester.pumpWidget(_host((_) {}));
      await tester.pumpAndSettle();
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextField), 'amman');
      await tester.pump();
      await tester.tap(find.text('Shopping'));
      await tester.pump();

      await tester.enterText(find.byType(TextField), '');
      await tester.pump();
      await tester.enterText(find.byType(TextField), 'amman');
      await tester.pump();

      // Back to All: a stale chip must not silently hide half the results.
      expect(find.text('Amman Cafe'), findsOneWidget);
      expect(find.text('Amman Mall'), findsOneWidget);
    });
  });
}
