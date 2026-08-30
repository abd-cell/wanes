import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'package:http/http.dart' as http;
import 'app_response.dart';
import 'error_messages.dart';
import 'environment.dart';
import 'session.dart';

/// Thin HTTP gateway. Adds the Bearer token, decodes the BaseResponse envelope,
/// and turns transport/parse failures into typed [AppResponse] errors instead
/// of throwing — so every caller can rely on `success` + `errorMessage`.
class ApiClient {
  ApiClient._();
  static final ApiClient instance = ApiClient._();

  final _http = http.Client();
  static const _timeout = Duration(seconds: 20);

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
  }) =>
      _send<T>(() => _http.get(_uri(path, query), headers: _headers()), parse);

  Future<AppResponse<T>> post<T>(
    String path, {
    Object? body,
    T Function(Object? data)? parse,
  }) =>
      _send<T>(
        () => _http.post(_uri(path), headers: _headers(), body: jsonEncode(body ?? {})),
        parse,
      );

  Future<AppResponse<T>> put<T>(
    String path, {
    Object? body,
    T Function(Object? data)? parse,
  }) =>
      _send<T>(
        () => _http.put(_uri(path), headers: _headers(), body: jsonEncode(body ?? {})),
        parse,
      );

  Future<AppResponse<T>> patch<T>(
    String path, {
    Object? body,
    T Function(Object? data)? parse,
  }) =>
      _send<T>(
        () => _http.patch(_uri(path), headers: _headers(), body: jsonEncode(body ?? {})),
        parse,
      );

  Future<AppResponse<T>> delete<T>(
    String path, {
    T Function(Object? data)? parse,
  }) =>
      _send<T>(() => _http.delete(_uri(path), headers: _headers()), parse);

  /// Runs [request], mapping every failure mode to a typed [AppResponse]:
  /// no network, timeout, non-JSON body, or a decoded error envelope.
  Future<AppResponse<T>> _send<T>(
    Future<http.Response> Function() request,
    T Function(Object? data)? parse,
  ) async {
    late http.Response res;
    try {
      res = await request().timeout(_timeout);
    } on TimeoutException {
      return AppResponse<T>.failure(ClientErrorCode.timeout);
    } on SocketException {
      return AppResponse<T>.failure(ClientErrorCode.network);
    } on http.ClientException {
      return AppResponse<T>.failure(ClientErrorCode.network);
    } on HandshakeException {
      return AppResponse<T>.failure(ClientErrorCode.network);
    } catch (_) {
      return AppResponse<T>.failure(ClientErrorCode.network);
    }

    // A raw 401 without our envelope means the session is gone — sign out.
    if (res.statusCode == 401 && !_looksLikeEnvelope(res.body)) {
      await Session.instance.clear();
      return AppResponse<T>.failure(105); // SessionExpired
    }

    return _decode(res.statusCode, res.body, parse);
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
    }
  }
}
