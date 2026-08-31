import 'dart:convert';
import 'package:shared_preferences/shared_preferences.dart';
import '../models/models.dart';
import 'environment.dart';
import 'theme.dart';

/// Persists the auth token + cached profile in shared_preferences.
class Session {
  Session._();
  static final Session instance = Session._();

  static const _tokenKey = '${Environment.appName}_token';
  static const _refreshKey = '${Environment.appName}_refresh';
  static const _userKey = '${Environment.appName}_user';

  String? _token;
  String? _refreshToken;
  Profile? _profile;

  String? get token => _token;

  /// The long-lived half of the pair. The access token expires within the hour;
  /// this is what actually keeps the device signed in, so a cold start with a
  /// stale access token is still a live session.
  String? get refreshToken => _refreshToken;

  Profile? get profile => _profile;
  bool get isLoggedIn => _token != null;

  Future<void> load() async {
    final prefs = await SharedPreferences.getInstance();
    _token = prefs.getString(_tokenKey);
    _refreshToken = prefs.getString(_refreshKey);
    final raw = prefs.getString(_userKey);
    if (raw != null) {
      _profile = Profile.fromJson(jsonDecode(raw) as Map<String, dynamic>);
    }
  }

  Future<void> save(String token, String refreshToken, Profile profile) async {
    _token = token;
    _refreshToken = refreshToken;
    _profile = profile;
    await ThemeController.applyFromProfile(profile);
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_tokenKey, token);
    await prefs.setString(_refreshKey, refreshToken);
    await prefs.setString(_userKey, jsonEncode(profile.toJson()));
  }

  /// Stores a rotated pair. The server invalidates the old refresh token as it
  /// issues the new one, so this has to land before the next call goes out.
  Future<void> saveTokens(String token, String refreshToken) async {
    _token = token;
    _refreshToken = refreshToken;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_tokenKey, token);
    await prefs.setString(_refreshKey, refreshToken);
  }

  /// Refreshes the cached profile without touching the token — used after
  /// `GET /Accounts/me` and after the user edits their profile, so a cold start
  /// shows the current name instead of the one captured at sign-in.
  Future<void> saveProfile(Profile profile) async {
    _profile = profile;
    // Every fresh profile funnels through here, so this is where the account's
    // saved theme gets picked up — signing in on a new device, or a change made
    // on another one.
    await ThemeController.applyFromProfile(profile);
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_userKey, jsonEncode(profile.toJson()));
  }

  Future<void> clear() async {
    _token = null;
    _refreshToken = null;
    _profile = null;
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_tokenKey);
    await prefs.remove(_refreshKey);
    await prefs.remove(_userKey);
  }
}
