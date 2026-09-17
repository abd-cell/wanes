import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../models/models.dart';
import 'environment.dart';

/// The admin-controlled platform settings, as the app sees them.
///
/// Mirrored into shared_preferences the moment they arrive, so a cold start
/// paints the configured brand colour before `GET /configuration` comes back —
/// the same trick [ThemeController] uses for light/dark. [WanesApp] listens to
/// [config] and rebuilds the themes, so a change that lands mid-session
/// re-skins the running app rather than waiting for the next launch.
class AppConfigController {
  AppConfigController._();

  static const _key = '${Environment.appName}_config';

  static final ValueNotifier<AppConfig> config = ValueNotifier(AppConfig.fallback);

  static AppConfig get value => config.value;

  /// Brand primary. Every other brand shade is derived from it — see
  /// [WanesTokens.lightFor] / [WanesTokens.darkFor].
  static Color get primary => Color(value.primaryColor);

  /// The configured typeface. [WanesTheme] resolves it to a face per script.
  static AppFont get font => value.font;

  /// Restores the last known settings. Runs before `runApp`, so there is no
  /// flash of the default palette on a device that has already seen the brand.
  static Future<void> load() async {
    final prefs = await SharedPreferences.getInstance();
    final raw = prefs.getString(_key);
    if (raw == null) return;
    try {
      config.value = AppConfig.fromJson(jsonDecode(raw) as Map<String, dynamic>);
    } catch (_) {
      // A cache written by an older build — the defaults stand until the
      // refresh lands, which is a better outcome than failing the launch.
    }
  }

  /// Adopts settings fetched from the server and caches them. A no-op when
  /// nothing actually changed, so the app is not rebuilt on every refresh.
  static Future<void> adopt(AppConfig next) async {
    // Compared through the serialised form, so a field added to the model
    // cannot be forgotten here — a field-by-field list once left the fare rates
    // out, and a device kept quoting the old prices until something else moved.
    final changed = jsonEncode(next.toJson()) != jsonEncode(value.toJson());
    if (!changed) return;

    config.value = next;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_key, jsonEncode(next.toJson()));
  }
}
