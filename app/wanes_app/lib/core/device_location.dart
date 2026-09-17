import 'dart:async';

import 'package:geolocator/geolocator.dart';

import 'l10n.dart';
import 'places.dart';

/// Why a location fix could not be taken. Each maps to its own copy, because
/// the fix the user has to apply is different every time: turn GPS on, grant
/// the prompt, or open the OS settings because the prompt won't come back.
enum LocationFailure {
  serviceDisabled,
  permissionDenied,
  permissionDeniedForever,
  timeout,
  unavailable,
}

/// A device location attempt — coordinates, or a typed reason it failed.
class LocationFix {
  const LocationFix.ok(this.lat, this.lng) : failure = null;
  const LocationFix.failed(this.failure)
      : lat = null,
        lng = null;

  final double? lat;
  final double? lng;
  final LocationFailure? failure;

  bool get success => failure == null;

  /// Localised one-liner for the alert shown when [success] is false.
  String get message => AppLocalizations.current.t(switch (failure) {
        LocationFailure.serviceDisabled => 'geo.locationOff',
        LocationFailure.permissionDenied => 'geo.locationDenied',
        LocationFailure.permissionDeniedForever => 'geo.locationBlocked',
        LocationFailure.timeout => 'geo.locationTimeout',
        _ => 'geo.locationUnavailable',
      });
}

/// GPS access for the app.
///
/// Everything here is best-effort: the picker still works without a fix, so a
/// denied permission or a dead sensor is a typed [LocationFailure], never a
/// thrown exception.
///
/// The last good fix is kept in memory for [_freshFor] so that reopening the
/// picker, or biasing a search, costs nothing. `flutter_map` screens can read
/// [lastKnown] synchronously inside a `build()`.
class DeviceLocation {
  DeviceLocation._();
  static final DeviceLocation instance = DeviceLocation._();

  /// Swapped out in tests so the picker can be driven without a real sensor.
  static Future<LocationFix> Function()? debugOverride;

  static const _freshFor = Duration(minutes: 2);
  static const _timeout = Duration(seconds: 12);

  LocationFix? _last;
  DateTime? _lastAt;
  Future<LocationFix>? _inFlight;

  /// Last fix taken this session, without touching the sensor. Null until the
  /// first successful [current] call — safe to read from `build()`.
  LocationFix? get lastKnown => _last;

  /// True when a fix is recent enough to reuse without re-reading the sensor.
  bool get isFresh {
    final at = _lastAt;
    return at != null && DateTime.now().difference(at) < _freshFor;
  }

  /// Takes (or reuses) a fix. Concurrent callers share one sensor read —
  /// the picker asks on open and again when the user taps the row, and two
  /// simultaneous prompts would confuse the OS permission dialog.
  ///
  /// Pass [force] to bypass the freshness cache after the user has been sent
  /// to settings to turn location on.
  Future<LocationFix> current({bool force = false}) {
    if (!force && isFresh && _last != null) return Future.value(_last!);
    return _inFlight ??= _read().whenComplete(() => _inFlight = null);
  }

  /// A fix only if the user already granted location — never raises the OS
  /// prompt. For background refreshes, where a dialog nobody asked for would be
  /// the wrong thing to show.
  Future<LocationFix> currentIfPermitted() async {
    if (debugOverride == null) {
      try {
        // Bounded: a background refresh must not stall on the platform.
        final permission = await Geolocator.checkPermission().timeout(const Duration(seconds: 5));
        if (permission == LocationPermission.denied) {
          return const LocationFix.failed(LocationFailure.permissionDenied);
        }
        if (permission == LocationPermission.deniedForever) {
          return const LocationFix.failed(LocationFailure.permissionDeniedForever);
        }
      } catch (_) {
        return const LocationFix.failed(LocationFailure.unavailable);
      }
    }
    return current();
  }

  Future<LocationFix> _read() async {
    final override = debugOverride;
    if (override != null) return _remember(await override());

    try {
      if (!await Geolocator.isLocationServiceEnabled()) {
        return const LocationFix.failed(LocationFailure.serviceDisabled);
      }

      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
      }
      if (permission == LocationPermission.deniedForever) {
        return const LocationFix.failed(LocationFailure.permissionDeniedForever);
      }
      if (permission == LocationPermission.denied) {
        return const LocationFix.failed(LocationFailure.permissionDenied);
      }

      final position = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.high,
          timeLimit: _timeout,
        ),
      ).timeout(_timeout);
      return _remember(LocationFix.ok(position.latitude, position.longitude));
    } on TimeoutException {
      // A cold GPS start can outrun the budget; a stale coarse fix still puts
      // the user in the right city, which is enough to bias a search.
      final fallback = await _lastFromPlatform();
      return fallback ?? const LocationFix.failed(LocationFailure.timeout);
    } catch (_) {
      return const LocationFix.failed(LocationFailure.unavailable);
    }
  }

  Future<LocationFix?> _lastFromPlatform() async {
    try {
      final last = await Geolocator.getLastKnownPosition();
      if (last == null) return null;
      return _remember(LocationFix.ok(last.latitude, last.longitude));
    } catch (_) {
      return null;
    }
  }

  LocationFix _remember(LocationFix fix) {
    if (fix.success) {
      _last = fix;
      _lastAt = DateTime.now();
    }
    return fix;
  }

  /// Opens the OS location settings so a permanently-denied user has a way
  /// back. Returns false when the platform refuses (web).
  Future<bool> openSettings() async {
    try {
      return await Geolocator.openAppSettings();
    } catch (_) {
      return false;
    }
  }

  /// Drops the cached fix — used by tests and on sign-out.
  void reset() {
    _last = null;
    _lastAt = null;
  }
}

/// A [Place] standing in for "wherever I am now", used when reverse geocoding
/// has nothing to offer.
Place currentLocationPlace(double lat, double lng) => Place(
      AppLocalizations.current.t('places.currentLocation'),
      lat,
      lng,
      address: '${lat.toStringAsFixed(5)}, ${lng.toStringAsFixed(5)}',
      kind: 'pin',
    );
