/// App-wide configuration.
///
/// The API base URL is target-specific, so it is read from a compile-time
/// `--dart-define` named `API_BASE_URL`. This keeps one binary configurable
/// per device without editing source:
///
///   flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5000/api/v1/      # Android emulator
///   flutter run --dart-define=API_BASE_URL=http://localhost:5000/api/v1/     # web / desktop
///   flutter run --dart-define=API_BASE_URL=http://192.168.1.20:5000/api/v1/  # real phone on LAN
///
/// Notes:
/// - The Android emulator reaches the host machine at 10.0.2.2, not localhost.
/// - A real device reaches the host at the PC's LAN IP (see `ipconfig`); the
///   phone and PC must be on the same Wi-Fi.
/// - Dev defaults to the backend's plain-HTTP endpoint (:5000) so the
///   self-signed HTTPS dev cert (:5001) doesn't trip the client with a
///   HandshakeException. Use an https URL only against a trusted cert.
///
/// When no define is passed, the default targets the Android emulator over
/// HTTP — the most common local run target.
class Environment {
  static const String appName = 'Wanes';

  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'https://localhost:5001/api/v1/',
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
}
