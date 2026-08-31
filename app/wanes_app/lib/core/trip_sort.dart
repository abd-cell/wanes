import 'package:shared_preferences/shared_preferences.dart';

import '../models/models.dart';
import 'geo.dart';

/// Ordering the rider's carpool results.
///
/// The server already returns the page in the rider's chosen order, so this is
/// what makes *changing* the sort instant: re-ordering the list in hand beats a
/// round trip, and re-running the search is not an option — a search that finds
/// nothing opens a hail request as a side effect, so a sort tap must never
/// become one.
extension TripSorting on List<Trip> {
  /// A copy ordered by [by]. The list arrives in the server's best-match
  /// ranking, so that order doubles as the tie-break: two trips that compare
  /// equal on the chosen key keep the order they came in. Dart's own sort is
  /// not stable, hence sorting indices with an explicit fallback.
  ///
  /// [originLat] / [originLng] are the rider's start point, needed only by
  /// [TripSort.pickup]; without them that sort leaves the list untouched.
  List<Trip> sortedBy(TripSort by, {double? originLat, double? originLng}) {
    final order = [for (var i = 0; i < length; i++) i];
    order.sort((a, b) {
      final c = _compare(this[a], this[b], by, originLat, originLng);
      return c != 0 ? c : a.compareTo(b);
    });
    return [for (final i in order) this[i]];
  }

  static int _compare(Trip x, Trip y, TripSort by, double? lat, double? lng) {
    switch (by) {
      case TripSort.best:
        return 0;
      case TripSort.departure:
        return x.departAt.compareTo(y.departAt);
      case TripSort.price:
        // A trip with no price set is not "free" — it sinks to the bottom.
        final px = x.pricePerSeat;
        final py = y.pricePerSeat;
        if (px == null || py == null) {
          if (px == py) return 0;
          return px == null ? 1 : -1;
        }
        return px.compareTo(py);
      case TripSort.rating:
        return y.driverRating.compareTo(x.driverRating);
      case TripSort.pickup:
        if (lat == null || lng == null) return 0;
        return Geo.distanceKm(lat, lng, x.originLat, x.originLng)
            .compareTo(Geo.distanceKm(lat, lng, y.originLat, y.originLng));
      case TripSort.seats:
        return y.seatsLeft.compareTo(x.seatsLeft);
    }
  }
}

/// The rider's last chosen sort, remembered across searches.
///
/// Sent with the next search so the server's twenty-result page is the best
/// twenty *for that sort* — re-ordering locally can only shuffle the page it
/// was already given.
class SortPreference {
  SortPreference._();
  static final SortPreference instance = SortPreference._();

  static const _key = 'trip_sort';

  TripSort _value = TripSort.best;

  /// Readable synchronously from a `build()`; [load] fills it at startup.
  TripSort get value => _value;

  Future<TripSort> load() async {
    final prefs = await SharedPreferences.getInstance();
    _value = TripSort.fromWire(prefs.getInt(_key));
    return _value;
  }

  Future<void> save(TripSort sort) async {
    if (sort == _value) return;
    _value = sort;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setInt(_key, sort.wire);
  }
}
