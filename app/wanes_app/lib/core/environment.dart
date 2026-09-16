/// App-wide configuration.
///
/// The API base URL is target-specific, so it is read from a compile-time
/// `--dart-define` named `API_BASE_URL`. This keeps one binary configurable
/// per device without editing source:
///
///   flutter run --dart-define=API_BASE_URL=http://192.168.1.43:5000/api/v1/ # LAN (default)
///   flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5000/api/v1/       # Android emulator
///   flutter run --dart-define=API_BASE_URL=http://localhost:5000/api/v1/      # web / desktop on the host
///   flutter run --dart-define=API_BASE_URL=https://www.technzone.com/wanesApi/api/v1/ # staging
///
/// Notes:
/// - Dev defaults to the host PC's LAN address so a real phone, an emulator and
///   the host browser all reach the same backend. The backend must be bound to
///   all interfaces (`https://0.0.0.0:5001;http://0.0.0.0:5000` in
///   launchSettings.json) and TCP 5000 opened in the Windows firewall.
/// - The LAN IP is DHCP-assigned: re-check with `ipconfig` and pass
///   `--dart-define=API_BASE_URL=...` if it moved, or reserve it on the router.
/// - The Android emulator can also reach the host at 10.0.2.2; a real device
///   cannot. Phone and PC must be on the same Wi-Fi.
/// - Prefer the plain-HTTP endpoint (:5000). The self-signed HTTPS dev cert on
///   :5001 trips clients with a HandshakeException / browser warning. Cleartext
///   to private ranges is allowed by the Android network-security config and by
///   NSAllowsLocalNetworking on iOS.
class Environment {
  static const String appName = 'Wanes';

  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://192.168.1.43:5000/api/v1/',
  );

  /// Free-text location search endpoint (OpenStreetMap Nominatim by default).
  /// Point this at a self-hosted / commercial geocoder in production:
  ///
  ///   flutter run --dart-define=GEOCODER_URL=https://geo.example.com/search
  ///
  /// The public Nominatim instance allows ~1 request/second and asks for a
  /// descriptive User-Agent, both of which the client honours.
  static const String geocoderUrl = String.fromEnvironment(
    'GEOCODER_URL',
    defaultValue: 'https://nominatim.openstreetmap.org/search',
  );

  /// ISO 3166-1 alpha-2 country the location search is confined to.
  ///
  /// Wanes runs in Jordan, so a rider typing "London" should be offered nothing
  /// rather than a trip origin the service cannot serve. Nominatim treats
  /// `viewbox` as a preference and would happily return the London one; only
  /// `countrycodes` actually excludes it.
  ///
  /// A comma-separated list works too, and an empty value lifts the restriction
  /// altogether — which is what a second country would need:
  ///
  ///   flutter run --dart-define=GEOCODER_COUNTRIES=jo,ps
  ///   flutter run --dart-define=GEOCODER_COUNTRIES=          # anywhere
  static const String geocoderCountries = String.fromEnvironment(
    'GEOCODER_COUNTRIES',
    defaultValue: 'jo',
  );

  /// Reverse lookup (coordinate -> address), used by "use my current location".
  /// Nominatim exposes it as a sibling of /search, so it is derived from
  /// [geocoderUrl] rather than being a second dart-define to keep in sync.
  /// Override it explicitly when the geocoder does not follow that shape.
  static String get reverseGeocoderUrl {
    const explicit = String.fromEnvironment('REVERSE_GEOCODER_URL');
    if (explicit.isNotEmpty) return explicit;
    final slash = geocoderUrl.lastIndexOf('/');
    if (slash < 0) return geocoderUrl;
    return '${geocoderUrl.substring(0, slash)}/reverse';
  }
}
