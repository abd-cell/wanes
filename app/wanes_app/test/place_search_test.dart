import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/core/geocoding.dart';
import 'package:wanes_app/core/places.dart';

void main() {
  test('typed coordinates become a dropped pin', () {
    final pin = GeocodingService.parseCoordinates(' 31.9515 , 35.9239 ');
    expect(pin, isNotNull);
    expect(pin!.lat, closeTo(31.9515, 1e-9));
    expect(pin.lng, closeTo(35.9239, 1e-9));
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

  test('places round-trip through storage JSON', () {
    const place = Place('Abdali', 31.9686, 35.9106, address: 'Amman, Jordan');
    final restored = Place.decodeList(Place.encodeList([place]));
    expect(restored, hasLength(1));
    expect(restored.first.name, 'Abdali');
    expect(restored.first.address, 'Amman, Jordan');
    expect(restored.first.key, place.key);
  });

  test('malformed stored JSON degrades to an empty list', () {
    expect(Place.decodeList('not json'), isEmpty);
  });
}
