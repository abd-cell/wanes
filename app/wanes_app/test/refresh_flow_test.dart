import 'dart:io';
import 'dart:math';

import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:wanes_app/core/api_client.dart';
import 'package:wanes_app/core/session.dart';
import 'package:wanes_app/models/models.dart';

/// Exercises [ApiClient]'s token renewal against a live backend.
///
/// Unlike the rest of `test/`, this needs the API running and reachable, and it
/// waits out a real access-token expiry, so it is opt-in:
///
///   dotnet run --project backend/Wanes          # with Jwt:AccessMinutes = 1
///   flutter test test/refresh_flow_test.dart \
///     --dart-define=API_BASE_URL=http://localhost:5000/api/v1/ \
///     --dart-define=RUN_LIVE_AUTH_TESTS=true
void main() {
  const live = bool.fromEnvironment('RUN_LIVE_AUTH_TESTS');

  TestWidgetsFlutterBinding.ensureInitialized();
  // The test binding installs an HttpOverrides that answers every request with
  // 400 offline. These tests are about talking to a real server, so drop it.
  HttpOverrides.global = null;

  Future<AuthResult> signIn() async {
    final phone = '+96272${Random().nextInt(9000000) + 1000000}';
    final otp = await ApiClient.instance.post('Accounts/request-otp', body: {'phone': phone});
    expect(otp.success, isTrue, reason: 'is the backend up on API_BASE_URL?');

    final auth = await ApiClient.instance.post<AuthResult>(
      'Accounts/verify-otp',
      body: {'phone': phone, 'code': '1234', 'deviceType': 1},
      parse: (d) => AuthResult.fromJson(d as Map<String, dynamic>),
    );
    expect(auth.success, isTrue);
    await Session.instance.save(auth.data!.token, auth.data!.refreshToken, auth.data!.profile);
    return auth.data!;
  }

  group('token renewal', () {
    setUp(() => SharedPreferences.setMockInitialValues({}));

    test('a call made with an expired access token still succeeds', () async {
      final signedIn = await signIn();

      // Outlive the 1-minute access token the server is configured to issue.
      await Future<void>.delayed(const Duration(seconds: 70));

      final me = await ApiClient.instance.get<Profile>(
        'Accounts/me',
        parse: (d) => Profile.fromJson(d as Map<String, dynamic>),
      );

      expect(me.success, isTrue, reason: 'the 401 should have been repaired, not surfaced');
      expect(Session.instance.token, isNot(signedIn.token), reason: 'access token replaced');
      expect(Session.instance.refreshToken, isNot(signedIn.refreshToken),
          reason: 'refresh token rotated');
    }, timeout: const Timeout(Duration(minutes: 3)), skip: !live);

    test('parallel calls that all expire share one renewal', () async {
      await signIn();
      await Future<void>.delayed(const Duration(seconds: 70));

      // Every one of these 401s at once. Without single-flight they would each
      // spend the refresh token, and the server would read the later ones as a
      // replay and revoke the session.
      final results = await Future.wait(List.generate(
        5,
        (_) => ApiClient.instance.get<Profile>(
          'Accounts/me',
          parse: (d) => Profile.fromJson(d as Map<String, dynamic>),
        ),
      ));

      expect(results.every((r) => r.success), isTrue);
      expect(Session.instance.isLoggedIn, isTrue, reason: 'session survived the burst');
    }, timeout: const Timeout(Duration(minutes: 3)), skip: !live);

    test('a rejected refresh signs the session out', () async {
      await signIn();
      await Session.instance.saveTokens(Session.instance.token!, 'not-a-real-refresh-token');
      await Future<void>.delayed(const Duration(seconds: 70));

      final me = await ApiClient.instance.get<Profile>(
        'Accounts/me',
        parse: (d) => Profile.fromJson(d as Map<String, dynamic>),
      );

      expect(me.success, isFalse);
      expect(me.errorCode, 105, reason: 'SessionExpired');
      expect(Session.instance.isLoggedIn, isFalse, reason: 'local session cleared');
    }, timeout: const Timeout(Duration(minutes: 3)), skip: !live);
  });
}
