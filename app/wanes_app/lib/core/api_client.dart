import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'package:http/http.dart' as http;
import 'api_log.dart';
import 'app_response.dart';
import 'error_messages.dart';
import 'environment.dart';
import 'session.dart';

/// What a refresh attempt settled on. The middle case matters: a refresh that
/// never reached the server says nothing about whether the session is still
/// good, so it must not sign the user out.
enum _RefreshOutcome {
  /// A new token pair was issued and stored.
  renewed,

  /// The server refused the refresh token — the session is over.
  rejected,

  /// The attempt never got an answer (offline, timeout, server down).
  unavailable,
}

/// Thin HTTP gateway. Adds the Bearer token, decodes the BaseResponse envelope,
/// and turns transport/parse failures into typed [AppResponse] errors instead
/// of throwing — so every caller can rely on `success` + `errorMessage`.
///
/// Access tokens are short-lived. A 401 is therefore treated as routine: the
/// client trades its refresh token for a new pair and replays the call once,
/// and only a refusal from the server ends the session.
class ApiClient {
  ApiClient._();
  static final ApiClient instance = ApiClient._();

  final _http = http.Client();
  static const _timeout = Duration(seconds: 20);

  /// In-flight refresh, shared by every call that 401s while it runs. Refresh
  /// tokens rotate on use, so two concurrent refreshes would race and one of
  /// them would come back holding a token the server has already retired.
  Future<_RefreshOutcome>? _refreshing;

  Map<String, String> _headers() {
    final headers = {
      'Content-Type': 'application/json',
      'Accept-Language': 'en',
    };
    final token = Session.instance.token;
    if (token != null) headers['Authorization'] = 'Bearer $token';
    return headers;
  }

  Uri _uri(String path, [Map<String, dynamic>? query]) {
    final base = Uri.parse('${Environment.apiBaseUrl}$path');
    if (query == null) return base;
    return base.replace(
      queryParameters: query.map((k, v) => MapEntry(k, '$v')),
    );
  }

  Future<AppResponse<T>> get<T>(
    String path, {
    Map<String, dynamic>? query,
    T Function(Object? data)? parse,
  }) {
    final uri = _uri(path, query);
    return _send<T>('GET', uri, null, () => _http.get(uri, headers: _headers()), parse);
  }

  Future<AppResponse<T>> post<T>(
    String path, {
    Object? body,
    T Function(Object? data)? parse,
  }) {
    final uri = _uri(path);
    return _send<T>('POST', uri, body,
        () => _http.post(uri, headers: _headers(), body: jsonEncode(body ?? {})), parse);
  }

  Future<AppResponse<T>> put<T>(
    String path, {
    Object? body,
    T Function(Object? data)? parse,
  }) {
    final uri = _uri(path);
    return _send<T>('PUT', uri, body,
        () => _http.put(uri, headers: _headers(), body: jsonEncode(body ?? {})), parse);
  }

  Future<AppResponse<T>> patch<T>(
    String path, {
    Object? body,
    T Function(Object? data)? parse,
  }) {
    final uri = _uri(path);
    return _send<T>('PATCH', uri, body,
        () => _http.patch(uri, headers: _headers(), body: jsonEncode(body ?? {})), parse);
  }

  Future<AppResponse<T>> delete<T>(
    String path, {
    T Function(Object? data)? parse,
  }) {
    final uri = _uri(path);
    return _send<T>('DELETE', uri, null, () => _http.delete(uri, headers: _headers()), parse);
  }

  /// Runs [request], mapping every failure mode to a typed [AppResponse]:
  /// no network, timeout, non-JSON body, or a decoded error envelope.
  ///
  /// [method], [uri] and [body] are carried only so the call can be written to
  /// the on-device [ApiLog]; they play no part in the request itself, which is
  /// wholly described by the [request] closure.
  Future<AppResponse<T>> _send<T>(
    String method,
    Uri uri,
    Object? body,
    Future<http.Response> Function() request,
    T Function(Object? data)? parse, {
    bool allowRetry = true,
  }) async {
    final authenticated = Session.instance.token != null;
    final watch = Stopwatch()..start();

    AppResponse<T> transportFailure(int code, String label) {
      ApiLog.instance.recordFailure(
        method: method,
        uri: uri,
        requestBody: body,
        failure: label,
        durationMs: watch.elapsedMilliseconds,
        authenticated: authenticated,
      );
      return AppResponse<T>.failure(code);
    }

    late http.Response res;
    try {
      res = await request().timeout(_timeout);
    } on TimeoutException {
      return transportFailure(ClientErrorCode.timeout, 'timeout');
    } on SocketException {
      return transportFailure(ClientErrorCode.network, 'no route to host');
    } on http.ClientException {
      return transportFailure(ClientErrorCode.network, 'connection failed');
    } on HandshakeException {
      return transportFailure(ClientErrorCode.network, 'TLS handshake failed');
    } catch (_) {
      return transportFailure(ClientErrorCode.network, 'unknown transport error');
    }

    ApiLog.instance.recordResponse(
      method: method,
      uri: uri,
      requestBody: body,
      statusCode: res.statusCode,
      responseBody: res.body,
      durationMs: watch.elapsedMilliseconds,
      authenticated: authenticated,
    );

    // 401 on a signed-in call is expected once an access token ages out. Renew
    // and replay: [request] rebuilds its headers, so the retry carries the new
    // token. `allowRetry` caps this at one attempt per call.
    if (res.statusCode == 401 && allowRetry && authenticated) {
      switch (await _refreshSession()) {
        case _RefreshOutcome.renewed:
          return _send<T>(method, uri, body, request, parse, allowRetry: false);
        case _RefreshOutcome.rejected:
          return AppResponse<T>.failure(105); // SessionExpired
        case _RefreshOutcome.unavailable:
          break; // Report the 401 as it came; the session is untouched.
      }
    }

    // A raw 401 without our envelope means the session is gone — sign out.
    if (res.statusCode == 401 && !_looksLikeEnvelope(res.body)) {
      await Session.instance.clear();
      return AppResponse<T>.failure(105); // SessionExpired
    }

    return _decode(res.statusCode, res.body, parse);
  }

  /// Renews the token pair on demand, for callers that cannot simply replay a
  /// request — the SSE stream authenticates once when it connects. Returns
  /// whether the session came back usable.
  Future<bool> renewSession() async =>
      await _refreshSession() == _RefreshOutcome.renewed;

  /// Joins the running refresh, or starts one. Callers queue behind a single
  /// rotation instead of each spending the refresh token.
  Future<_RefreshOutcome> _refreshSession() =>
      _refreshing ??= _refresh().whenComplete(() => _refreshing = null);

  /// Exchanges the stored refresh token for a new pair.
  ///
  /// Deliberately not routed through [_send]: this call must never trigger the
  /// 401 handling that invoked it, and it carries no Bearer token — the refresh
  /// token is the credential.
  Future<_RefreshOutcome> _refresh() async {
    final refreshToken = Session.instance.refreshToken;
    if (refreshToken == null) {
      // Signed in on a build that predates refresh tokens, or storage was lost.
      // Nothing can renew this session, so end it rather than loop on 401s.
      await Session.instance.clear();
      return _RefreshOutcome.rejected;
    }

    final uri = _uri('Accounts/refresh');
    final watch = Stopwatch()..start();
    late http.Response res;
    try {
      res = await _http
          .post(uri,
              headers: {'Content-Type': 'application/json', 'Accept-Language': 'en'},
              body: jsonEncode({'refreshToken': refreshToken}))
          .timeout(_timeout);
    } catch (_) {
      ApiLog.instance.recordFailure(
        method: 'POST',
        uri: uri,
        requestBody: _redactedRefreshBody,
        failure: 'refresh unreachable',
        durationMs: watch.elapsedMilliseconds,
        authenticated: true,
      );
      return _RefreshOutcome.unavailable;
    }

    // Both tokens are credentials, so the log keeps the outcome and not the values.
    ApiLog.instance.recordResponse(
      method: 'POST',
      uri: uri,
      requestBody: _redactedRefreshBody,
      statusCode: res.statusCode,
      responseBody: res.statusCode == 200 ? '{"success":true,"data":"<tokens hidden>"}' : res.body,
      durationMs: watch.elapsedMilliseconds,
      authenticated: true,
    );

    // 5xx is the server faltering, not a verdict on the token — keep the session.
    if (res.statusCode >= 500) return _RefreshOutcome.unavailable;

    final pair = _readTokenPair(res.body);
    if (pair == null) {
      await Session.instance.clear();
      return _RefreshOutcome.rejected;
    }

    await Session.instance.saveTokens(pair.$1, pair.$2);
    return _RefreshOutcome.renewed;
  }

  static const _redactedRefreshBody = {'refreshToken': '<hidden>'};

  /// Pulls (access, refresh) out of a refresh response, or null if the body is
  /// not a successful envelope carrying both.
  (String, String)? _readTokenPair(String body) {
    try {
      final decoded = jsonDecode(body);
      if (decoded is! Map<String, dynamic> || decoded['success'] != true) return null;
      final data = decoded['data'];
      if (data is! Map<String, dynamic>) return null;
      final token = data['token'];
      final refresh = data['refreshToken'];
      if (token is! String || refresh is! String) return null;
      if (token.isEmpty || refresh.isEmpty) return null;
      return (token, refresh);
    } catch (_) {
      return null;
    }
  }

  bool _looksLikeEnvelope(String body) =>
      body.trimLeft().startsWith('{') && body.contains('"success"');

  AppResponse<T> _decode<T>(int status, String body, T Function(Object? data)? parse) {
    if (body.trim().isEmpty) {
      // 2xx with no body → treat as success; otherwise a transport failure.
      if (status >= 200 && status < 300) {
        return AppResponse<T>(success: true, errorCode: 0);
      }
      return AppResponse<T>.failure(ClientErrorCode.empty);
    }
    try {
      final decoded = jsonDecode(body);
      if (decoded is! Map<String, dynamic>) {
        return AppResponse<T>.failure(ClientErrorCode.parse);
      }
      return AppResponse<T>.fromJson(decoded, parse);
    } on FormatException {
      return AppResponse<T>.failure(ClientErrorCode.parse);
    } catch (_) {
      // Valid JSON whose *shape* is not what this build expects — typically an
      // API older or newer than the app — throws inside [parse]. Callers rely
      // on this class never throwing, so report it like any other parse
      // failure rather than stranding them mid-await.
      return AppResponse<T>.failure(ClientErrorCode.parse);
    }
  }
}
