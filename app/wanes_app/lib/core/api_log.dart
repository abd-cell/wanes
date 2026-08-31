import 'dart:convert';

import 'package:flutter/foundation.dart';

/// ─────────────────────────────────────────────────────────────────────────
/// In-app API log — the on-device counterpart to the backend's api-log
/// "checker". Every call that leaves [ApiClient] is recorded here so the
/// traffic can be inspected on a real phone, where there is no terminal to
/// watch.
///
/// It records what the server log cannot: timeouts, DNS/socket failures and
/// TLS handshake errors never reach the backend, and those are exactly the
/// failures worth seeing when the app is pointed at a LAN address.
///
/// Debug builds only — [enabled] is `kDebugMode`, so in release every
/// `record*` call is a cheap no-op and nothing is retained.
/// ─────────────────────────────────────────────────────────────────────────
class ApiLog {
  ApiLog._();

  static final ApiLog instance = ApiLog._();

  /// Recording is a development aid; release builds keep nothing in memory.
  static bool get enabled => kDebugMode;

  /// Newest first. Capped so a long session cannot grow without bound.
  static const _maxEntries = 200;

  /// Bodies are held for inspection, so they are clipped rather than kept
  /// whole — a stray large payload should not pin megabytes in memory.
  static const _maxBodyChars = 20000;

  /// The screen listens to this; a new list instance is published on every
  /// change so [ValueListenableBuilder] repaints.
  final ValueNotifier<List<ApiLogEntry>> entries =
      ValueNotifier<List<ApiLogEntry>>(const []);

  int _seq = 0;

  void _add(ApiLogEntry entry) {
    if (!enabled) return;
    final next = [entry, ...entries.value];
    if (next.length > _maxEntries) next.removeRange(_maxEntries, next.length);
    entries.value = next;
  }

  /// A call that came back with an HTTP status (success or error alike).
  void recordResponse({
    required String method,
    required Uri uri,
    Object? requestBody,
    required int statusCode,
    required String responseBody,
    required int durationMs,
    required bool authenticated,
  }) {
    if (!enabled) return;
    _add(ApiLogEntry(
      id: ++_seq,
      at: DateTime.now(),
      method: method,
      uri: uri,
      requestBody: _clip(_encode(requestBody)),
      statusCode: statusCode,
      responseBody: _clip(responseBody),
      durationMs: durationMs,
      authenticated: authenticated,
    ));
  }

  /// A call that never produced a response — timeout, no route to host,
  /// refused connection, bad certificate. [failure] is a short cause label.
  void recordFailure({
    required String method,
    required Uri uri,
    Object? requestBody,
    required String failure,
    required int durationMs,
    required bool authenticated,
  }) {
    if (!enabled) return;
    _add(ApiLogEntry(
      id: ++_seq,
      at: DateTime.now(),
      method: method,
      uri: uri,
      requestBody: _clip(_encode(requestBody)),
      statusCode: null,
      responseBody: '',
      durationMs: durationMs,
      authenticated: authenticated,
      failure: failure,
    ));
  }

  void clear() => entries.value = const [];

  /// Bodies are logged as sent, except the one field never worth persisting:
  /// a bearer token. Presence is kept as a flag on the entry instead.
  static String _encode(Object? body) {
    if (body == null) return '';
    if (body is String) return body;
    try {
      return jsonEncode(body);
    } catch (_) {
      return body.toString();
    }
  }

  static String _clip(String s) => s.length <= _maxBodyChars
      ? s
      : '${s.substring(0, _maxBodyChars)}\n… ${s.length - _maxBodyChars} more characters';
}

/// One recorded call.
class ApiLogEntry {
  ApiLogEntry({
    required this.id,
    required this.at,
    required this.method,
    required this.uri,
    required this.requestBody,
    required this.statusCode,
    required this.responseBody,
    required this.durationMs,
    required this.authenticated,
    this.failure,
  });

  final int id;
  final DateTime at;
  final String method;
  final Uri uri;
  final String requestBody;

  /// `null` when the request never reached the server — see [failure].
  final int? statusCode;
  final String responseBody;
  final int durationMs;

  /// Whether a bearer token was attached (the token itself is never stored).
  final bool authenticated;

  /// Short cause label for a transport failure, e.g. `timeout`, `network`.
  final String? failure;

  bool get isTransportFailure => statusCode == null;

  /// The backend answers domain errors with HTTP 200 and `success:false`, so
  /// "did this call actually work" cannot be read from the status alone.
  bool get isEnvelopeError =>
      statusCode != null &&
      statusCode! >= 200 &&
      statusCode! < 300 &&
      responseBody.contains('"success":false');

  bool get isOk =>
      statusCode != null && statusCode! >= 200 && statusCode! < 300 && !isEnvelopeError;

  /// Path with the leading `/api/v1` trimmed — the prefix is on every row and
  /// carries no information in a list.
  String get shortPath {
    var p = uri.path;
    const prefix = '/api/v1';
    if (p.startsWith(prefix)) p = p.substring(prefix.length);
    if (p.isEmpty) p = '/';
    return uri.hasQuery ? '$p?${uri.query}' : p;
  }

  /// Pretty-printed when the payload is JSON, verbatim otherwise.
  static String pretty(String raw) {
    final text = raw.trim();
    if (text.isEmpty) return '';
    if (!text.startsWith('{') && !text.startsWith('[')) return raw;
    try {
      return const JsonEncoder.withIndent('  ').convert(jsonDecode(text));
    } catch (_) {
      return raw;
    }
  }
}
