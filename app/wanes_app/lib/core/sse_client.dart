import 'dart:async';
import 'dart:convert';
import 'package:http/http.dart' as http;
import 'api_client.dart';
import 'environment.dart';
import 'session.dart';

/// Minimal Server-Sent-Events client for the `/sse` stream.
///
/// Yields decoded event payloads ({id, type, title, body, data}). Call
/// [dispose] to close the connection.
///
/// Set [autoReconnect] for a stream that must outlive a flaky connection (the
/// app-wide notification listener). One-shot users — a screen that is only open
/// for a few seconds — leave it off and simply dispose.
class SseClient {
  SseClient({this.autoReconnect = false});

  /// Reconnect delay, doubling up to [_maxRetryDelay] while the server is down.
  static const _baseRetryDelay = Duration(seconds: 2);
  static const _maxRetryDelay = Duration(minutes: 1);

  final bool autoReconnect;

  http.Client? _client;
  StreamSubscription<String>? _sub;
  Timer? _retry;
  int _attempt = 0;
  bool _disposed = false;

  final _controller = StreamController<Map<String, dynamic>>.broadcast();

  Stream<Map<String, dynamic>> get events => _controller.stream;

  /// Opens the stream. [renewed] marks the single retry that follows a token
  /// renewal, so a server that keeps refusing cannot become a renewal loop.
  Future<void> connect({bool renewed = false}) async {
    if (_disposed) return;

    final token = Session.instance.token;
    if (token == null) return;

    try {
      _client = http.Client();
      final request = http.Request('GET', Uri.parse('${Environment.apiBaseUrl}sse'))
        ..headers['Authorization'] = 'Bearer $token'
        ..headers['Accept'] = 'text/event-stream';

      final response = await _client!.send(request);
      if (_disposed) return;

      // The stream is authenticated once, at connect, and cannot be replayed the
      // way [ApiClient] replays a request. An access token that aged out while the
      // connection was down would otherwise have this retrying the same rejected
      // token on a backoff that never recovers.
      if (response.statusCode == 401) {
        _client?.close();
        _client = null;
        if (renewed) {
          _scheduleReconnect();
          return;
        }
        if (await ApiClient.instance.renewSession() && !_disposed) {
          _attempt = 0;
          await connect(renewed: true);
        }
        // A renewal the server refused means the session is over; the notification
        // listener stays down until the next sign-in restarts it.
        return;
      }

      _attempt = 0;
      _sub = response.stream
          .transform(utf8.decoder)
          .transform(const LineSplitter())
          .listen(_onLine,
              onError: (_) => _scheduleReconnect(),
              onDone: _scheduleReconnect,
              cancelOnError: false);
    } catch (_) {
      _scheduleReconnect();
    }
  }

  /// Backs off exponentially so a server restart doesn't turn into a hot loop.
  void _scheduleReconnect() {
    if (_disposed || !autoReconnect || _retry != null) return;

    _sub?.cancel();
    _sub = null;
    _client?.close();
    _client = null;

    final delay = _baseRetryDelay * (1 << _attempt.clamp(0, 5));
    _attempt++;
    _retry = Timer(delay > _maxRetryDelay ? _maxRetryDelay : delay, () {
      _retry = null;
      connect();
    });
  }

  void _onLine(String line) {
    if (!line.startsWith('data:')) return;
    final payload = line.substring(5).trim();
    if (payload.isEmpty) return;
    try {
      _controller.add(jsonDecode(payload) as Map<String, dynamic>);
    } catch (_) {
      // ignore malformed frames
    }
  }

  void dispose() {
    _disposed = true;
    _retry?.cancel();
    _sub?.cancel();
    _client?.close();
    _controller.close();
  }
}
