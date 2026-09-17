import 'package:shared_preferences/shared_preferences.dart';

import 'api_client.dart';
import 'environment.dart';
import 'session.dart';

/// Things a user has to have read before the app lets them act: the safety
/// notes, and the fact that a Wanes ride is shared.
///
/// Each kind carries a version. Rewording a note in a way that changes what the
/// user agreed to means bumping it, and everybody is asked again — an
/// acknowledgement of copy they never saw is not an acknowledgement.
///
/// The server keeps the record that answers a dispute (`me/acknowledgements`);
/// the device keeps a copy so a sheet already agreed to is not shown again on
/// every tap, and so a new device picks the agreements back up.
enum Acknowledgement {
  riderSafety('riderSafety', 1, 1),
  driverSafety('driverSafety', 1, 2),
  riderSharedRide('riderSharedRide', 1, 3);

  const Acknowledgement(this.key, this.version, this.wire);

  final String key;
  final int version;

  /// `AcknowledgementKind` on the server.
  final int wire;
}

class Acknowledgements {
  Acknowledgements._();

  /// Server agreements already merged into the device copy this session.
  static int? _syncedFor;

  static String _prefKey(Acknowledgement kind) {
    final user = Session.instance.profile?.id ?? 0;
    return '${Environment.appName}_ack_${kind.key}_v${kind.version}_$user';
  }

  static Future<bool> has(Acknowledgement kind) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      if (prefs.getBool(_prefKey(kind)) ?? false) return true;
      await _syncFromServer(prefs);
      return prefs.getBool(_prefKey(kind)) ?? false;
    } catch (_) {
      // Storage the app cannot read is treated as "not yet agreed": asking
      // twice is an annoyance, skipping the notes is not an option.
      return false;
    }
  }

  static Future<void> record(Acknowledgement kind) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setBool(_prefKey(kind), true);
    } catch (_) {
      // Best-effort: the user is asked again next time.
    }
    if (Session.instance.isLoggedIn) {
      // Fire-and-forget: the agreement already happened on screen, and the
      // next sync repairs a record that failed to send.
      ApiClient.instance.post('me/acknowledgements', body: {'kind': kind.wire, 'version': kind.version});
    }
  }

  /// Pulls the account's agreements once per session, so a reinstall or a new
  /// phone does not ask again for something already agreed.
  static Future<void> _syncFromServer(SharedPreferences prefs) async {
    final user = Session.instance.profile?.id;
    if (user == null || !Session.instance.isLoggedIn || _syncedFor == user) return;
    _syncedFor = user;
    final res = await ApiClient.instance.get<List<Map<String, dynamic>>>(
      'me/acknowledgements',
      parse: (d) => (d as List).cast<Map<String, dynamic>>(),
    );
    if (!res.success) {
      _syncedFor = null;
      return;
    }
    for (final row in res.data ?? const <Map<String, dynamic>>[]) {
      final wire = (row['kind'] as num?)?.toInt();
      final version = (row['version'] as num?)?.toInt();
      for (final kind in Acknowledgement.values) {
        if (kind.wire == wire && kind.version == version) {
          await prefs.setBool(_prefKey(kind), true);
        }
      }
    }
  }

  /// Forgets the session sync — on sign-out, and in tests.
  static void reset() => _syncedFor = null;
}
