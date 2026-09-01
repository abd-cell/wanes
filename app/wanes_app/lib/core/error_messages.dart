/// Maps backend [ErrorCode] values (Wanes/Shareds/Models/ErrorCode.cs) to
/// friendly, user-facing copy, and resolves the best message to show for a
/// given response.
///
/// Priority when resolving: an explicit server `message` → any `errors` detail
/// → a mapped message for the numeric code → a generic fallback. Keep the codes
/// in sync with the backend enum.
///
/// The copy itself lives in `lib/l10n/strings_{en,ar}.dart` under `errors.*`;
/// this file only owns the code → key mapping, so a message comes back in
/// whichever language the user has chosen.
library;

import 'l10n.dart';

/// The few server codes the app has to *branch* on rather than merely show.
///
/// Everything else goes through [_keys] and is only ever rendered, so a bare
/// number is fine there. These are the ones a screen makes a decision from, and
/// a decision written as `== 501` is one nobody can check against the backend
/// enum without going to look it up.
class ServerErrorCode {
  /// `ErrorCode.RequestNotOpen` — the hail has already been cancelled, taken or
  /// expired.
  static const requestNotOpen = 501;
}

/// Client-only sentinel codes (never returned by the server).
class ClientErrorCode {
  static const empty = -1; // empty/blank body
  static const network = -2; // could not reach the server
  static const parse = -3; // malformed response body
  static const timeout = -4; // request timed out
}

const Map<int, String> _keys = {
  // Generic
  1: 'errors.generic',
  2: 'errors.validation',
  3: 'errors.notFound',
  4: 'errors.unauthorized',
  5: 'errors.forbidden',
  6: 'errors.conflict',

  // Auth / accounts
  100: 'errors.invalidCredentials',
  101: 'errors.phoneNotVerified',
  102: 'errors.otpInvalid',
  103: 'errors.otpExpired',
  104: 'errors.phoneTaken',
  105: 'errors.sessionExpired',
  106: 'errors.accountDisabled',

  // Driver / vehicle
  200: 'errors.driverNotVerified',
  202: 'errors.vehicleNotFound',
  203: 'errors.driverOnActiveTrip',
  204: 'errors.driverTripTimeConflict',

  // Trips
  300: 'errors.tripUnavailable',
  301: 'errors.tripNotBookable',
  302: 'errors.departureInPast',
  303: 'errors.tooManySeats',
  304: 'errors.sameOriginDestination',
  305: 'errors.addressNotFound',
  306: 'errors.tripLocked',

  // Bookings
  400: 'errors.bookingNotFound',
  401: 'errors.tripFull',
  402: 'errors.ownTrip',
  403: 'errors.alreadyBooked',

  // Ride requests
  500: 'errors.requestNotFound',
  501: 'errors.requestClosed',

  // Ratings
  600: 'errors.rateAfterCompletion',
  601: 'errors.alreadyRated',

  // Client-side
  ClientErrorCode.empty: 'errors.emptyResponse',
  ClientErrorCode.network: 'errors.network',
  ClientErrorCode.parse: 'errors.parse',
  ClientErrorCode.timeout: 'errors.timeout',
};

/// The friendly message for a bare error code (no server text), or null if the
/// code is unknown/success.
String? messageForCode(int code) {
  final key = _keys[code];
  return key == null ? null : AppLocalizations.current.t(key);
}

/// Best user-facing message for a response: prefer an explicit server message,
/// then any `errors` detail, then the mapped code, then a safe generic.
String resolveErrorMessage({
  required int code,
  String? serverMessage,
  List<String> errors = const [],
}) {
  final trimmed = serverMessage?.trim();
  if (trimmed != null && trimmed.isNotEmpty) return trimmed;
  if (errors.isNotEmpty) return errors.join('\n');
  return messageForCode(code) ?? AppLocalizations.current.t('errors.generic');
}
