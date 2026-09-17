import 'device_location.dart';
import 'l10n.dart';
import 'places.dart';

/// Where a driver is, for the presence call and for "how far is the pickup".
///
/// The driver screens used to pin this to the first suggested place, so every
/// distance on the board was measured from a fixed point in Amman and the server
/// matched demand against a driver who never moved. This reads the device.
///
/// Only an explicit act — going online, opening the board — may raise the OS
/// permission prompt ([prompt]). A background refresh reads what it is already
/// allowed to, and otherwise keeps the last position it had.
class DriverPosition {
  DriverPosition._();

  static Place? _last;

  /// The last position resolved this session, or null before the first.
  static Place? get last => _last;

  /// True when [last] came from the device rather than the fallback.
  static bool get isLive => _live;
  static bool _live = false;

  static Future<Place> resolve({bool prompt = false}) async {
    final location = DeviceLocation.instance;
    final fix = prompt ? await location.current() : await location.currentIfPermitted();
    if (fix.success && fix.lat != null && fix.lng != null) {
      _live = true;
      return _last = Place(
        AppLocalizations.current.t('places.currentLocation'),
        fix.lat!,
        fix.lng!,
        kind: 'pin',
      );
    }
    if (_last != null) return _last!;
    _live = false;
    return _last = kPlaces.first;
  }

  /// Forgets the cached position — on sign-out, and in tests.
  static void reset() {
    _last = null;
    _live = false;
  }
}
