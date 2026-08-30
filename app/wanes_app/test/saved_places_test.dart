import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/models/models.dart';

void main() {
  test('a saved place parses the API shape', () {
    final place = SavedPlace.fromJson(const {
      'id': 7,
      'label': 2,
      'name': 'Work',
      'address': 'Abdali Boulevard, Amman',
      'lat': 31.9686,
      'lng': 35.9106,
    });

    expect(place.id, 7);
    expect(place.label, SavedPlaceLabel.work);
    expect(place.name, 'Work');
    expect(place.place.lat, closeTo(31.9686, 1e-9));
    expect(place.place.address, 'Abdali Boulevard, Amman');
  });

  test('an unknown label falls back to a favourite', () {
    expect(SavedPlaceLabel.fromValue(99), SavedPlaceLabel.custom);
    expect(SavedPlaceLabel.fromValue(null), SavedPlaceLabel.custom);
    expect(SavedPlaceLabel.fromValue(1), SavedPlaceLabel.home);
  });

  test('a place with no address shows its coordinates instead', () {
    final place = SavedPlace.fromJson(const {
      'id': 1,
      'label': 3,
      'name': 'Dropped pin',
      'address': '',
      'lat': 31.95,
      'lng': 35.92,
    });

    expect(place.place.address, isNull);
    expect(place.place.detail, '31.9500, 35.9200');
  });
}
