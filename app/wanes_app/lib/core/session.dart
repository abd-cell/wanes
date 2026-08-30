import 'dart:convert';
import 'package:shared_preferences/shared_preferences.dart';
import '../models/models.dart';
import 'environment.dart';

/// Persists the auth token + cached profile in shared_preferences.
class Session {
  Session._();
  static final Session instance = Session._();

  static const _tokenKey = '${Environment.appName}_token';
  static const _userKey = '${Environment.appName}_user';

  String? _token;
  Profile? _profile;

  String? get token => _token;
  Profile? get profile => _profile;
  bool get isLoggedIn => _token != null;

  Future<void> load() async {
    final prefs = await SharedPreferences.getInstance();
    _token = prefs.getString(_tokenKey);
    final raw = prefs.getString(_userKey);
    if (raw != null) {
      _profile = Profile.fromJson(jsonDecode(raw) as Map<String, dynamic>);
    }
  }

  Future<void> save(String token, Profile profile) async {
    _token = token;
    _profile = profile;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_tokenKey, token);
    await prefs.setString(_userKey, jsonEncode(profile.toJson()));
  }

  /// Refreshes the cached profile without touching the token — used after
  /// `GET /Accounts/me` and after the user edits their profile, so a cold start
  /// shows the current name instead of the one captured at sign-in.
  Future<void> saveProfile(Profile profile) async {
    _profile = profile;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_userKey, jsonEncode(profile.toJson()));
  }

  Future<void> clear() async {
    _token = null;
    _profile = null;
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_tokenKey);
    await prefs.remove(_userKey);
  }
}
