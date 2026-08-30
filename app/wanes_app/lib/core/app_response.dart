import 'error_messages.dart';

/// Mirror of the backend BaseResponse<T> envelope
/// (`{ data, success, errorCode, message, errors }`).
class AppResponse<T> {
  AppResponse({
    required this.success,
    required this.errorCode,
    String? errorMessage,
    this.serverMessage,
    this.errors = const [],
    this.data,
  }) : errorMessage = success
            ? errorMessage
            : (errorMessage ??
                resolveErrorMessage(
                  code: errorCode,
                  serverMessage: serverMessage,
                  errors: errors,
                ));

  final bool success;
  final int errorCode;

  /// User-facing message. On failure this is always populated (server text →
  /// errors detail → mapped code → generic). Null on success.
  final String? errorMessage;

  /// Raw server-provided message, if any (pre-resolution).
  final String? serverMessage;

  /// Field/validation details returned alongside the code.
  final List<String> errors;

  final T? data;

  /// True when the failure is a client/transport issue rather than a business
  /// rule (useful for deciding whether to offer a retry).
  bool get isTransportError => errorCode <= ClientErrorCode.empty;

  factory AppResponse.fromJson(
    Map<String, dynamic> json,
    T Function(Object? data)? parse,
  ) {
    // Backend field is `message`; tolerate a legacy `errorMessage` too.
    final serverMessage =
        (json['message'] ?? json['errorMessage']) as String?;
    final errors = (json['errors'] as List?)
            ?.map((e) => e?.toString() ?? '')
            .where((e) => e.isNotEmpty)
            .toList() ??
        const <String>[];

    return AppResponse<T>(
      success: json['success'] as bool? ?? false,
      errorCode: (json['errorCode'] as num?)?.toInt() ?? 0,
      serverMessage: serverMessage,
      errors: errors,
      data: json['data'] != null && parse != null ? parse(json['data']) : null,
    );
  }

  /// Build a client-side failure (no HTTP envelope) from a sentinel code.
  factory AppResponse.failure(int code, {String? message}) => AppResponse<T>(
        success: false,
        errorCode: code,
        serverMessage: message,
      );
}
