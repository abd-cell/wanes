import 'dart:async';
import 'dart:convert';
import 'package:http/http.dart' as http;
import 'environment.dart';
import 'session.dart';

/// Minimal Server-Sent-Events client for the `/sse` stream.
///
/// Yields decoded event payloads ({type, title, body, data}). Call [dispose]
/// to close the connection.
class SseClient {
  http.Client? _client;
  StreamSubscription<String>? _sub;
  final _controller = StreamController<Map<String, dynamic>>.broadcast();

  Stream<Map<String, dynamic>> get events => _controller.stream;

  Future<void> connect() async {
    final token = Session.instance.token;
    if (token == null) return;

    _client = http.Client();
    final request = http.Request('GET', Uri.parse('${Environment.apiBaseUrl}sse'))
      ..headers['Authorization'] = 'Bearer $token'
      ..headers['Accept'] = 'text/event-stream';

    final response = await _client!.send(request);
    _sub = response.stream
        .transform(utf8.decoder)
        .transform(const LineSplitter())
        .listen(_onLine, onError: (_) {}, cancelOnError: false);
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
    _sub?.cancel();
    _client?.close();
    _controller.close();
  }
}
