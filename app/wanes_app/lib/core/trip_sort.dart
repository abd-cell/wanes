import 'package:shared_preferences/shared_preferences.dart';

import '../models/models.dart';

/// Ordering the rider's carpool results.
///
/// The server already returns the page in the rider's chosen order, so this is
/// what makes *changing* the sort instant: re-ordering the list in hand beats a
/// round trip, and re-running the search is not an option — a search that finds
/// nothing opens a hail request as a side effect, so a sort tap must never
/// become one.
///
/// It sorts matches rather than trips because one of the keys only exists on
/// the match. On a corridor result the rider walks to a point on the driver's
/// route, not to where the driver set off from — often a different town — and
/// the server sends that distance as [SearchMatch.pickupWalkKm]. Sorting trips
/// meant recomputing the walk from the trip's own origin, so "shortest walk"
/// ordered the whole On-the-way band by a number the rider will never walk.
extension MatchSorting on List<SearchMatch> {
  /// A copy ordered by [by]. The list arrives in the server's best-match
  /// ranking, so that order doubles as the tie-break: two matches that compare
  /// equal on the chosen key keep the order they came in. Dart's own sort is
  /// not stable, hence sorting indices with an explicit fallback.
  List<SearchMatch> sortedBy(TripSort by) {
    final order = [for (var i = 0; i < length; i++) i];
    order.sort((a, b) {
      final c = _compare(this[a], this[b], by);
      return c != 0 ? c : a.compareTo(b);
    });
    return [for (final i in order) this[i]];
  }

  static int _compare(SearchMatch x, SearchMatch y, TripSort by) {
    switch (by) {
      case TripSort.best:
        return 0;
      case TripSort.departure:
        return x.trip.departAt.compareTo(y.trip.departAt);
      case TripSort.price:
        // A trip with no price set is not "free" — it sinks to the bottom.
        final px = x.trip.pricePerSeat;
        final py = y.trip.pricePerSeat;
        if (px == null || py == null) {
          if (px == py) return 0;
          return px == null ? 1 : -1;
        }
        return px.compareTo(py);
      case TripSort.rating:
        return y.trip.driverRating.compareTo(x.trip.driverRating);
      case TripSort.pickup:
        // The server's own figure, so the app cannot disagree with the order it
        // was sent — and so a corridor match is judged on the walk to the road
        // rather than to the driver's front door.
        return x.pickupWalkKm.compareTo(y.pickupWalkKm);
      case TripSort.seats:
        return y.trip.seatsLeft.compareTo(x.trip.seatsLeft);
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

  /// Reads the remembered choice, falling back to [TripSort.best].
  ///
  /// Storage failures are swallowed here and in [save] on purpose: remembering
  /// the sort is a convenience, and the screen that asks for it has a perfectly
  /// good answer without one. This used to throw out of [save] and take the
  /// caller's next step with it — the re-sort stopped happening because the
  /// preference could not be written.
  Future<TripSort> load() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      _value = TripSort.fromWire(prefs.getInt(_key));
    } catch (_) {
      // Keep whatever is in hand.
    }
    return _value;
  }

  Future<void> save(TripSort sort) async {
    if (sort == _value) return;
    // In memory first, so the choice holds for this session even if the write
    // does not.
    _value = sort;
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setInt(_key, sort.wire);
    } catch (_) {
      // Next launch opens on the default. Nothing worth interrupting for.
    }
  }
}
