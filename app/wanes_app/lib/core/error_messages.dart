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
  /// `ErrorCode.RiderTripNotOpen` — the posting has already been left, claimed
  /// or passed its departure.
  static const riderTripNotOpen = 501;

  /// `ErrorCode.DepartureTooSoon` — no driver could gather these riders and run
  /// the leg by then. The form recomputes its floor and says so.
  static const departureTooSoon = 502;

  /// `ErrorCode.RiderProfileIncomplete` — the trip's conditions ask for a
  /// profile detail this rider has not given. Fixable in thirty seconds, which
  /// is why it is worth branching on: the app offers the field rather than
  /// merely reporting a refusal.
  static const riderProfileIncomplete = 406;

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
  7: 'errors.fileTooLarge',
  8: 'errors.unsupportedFileType',

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
  205: 'errors.driverDocumentsIncomplete',
  206: 'errors.driverDocumentNotFound',

  // Trips
  300: 'errors.tripUnavailable',
  301: 'errors.tripNotBookable',
  302: 'errors.departureInPast',
  303: 'errors.tooManySeats',
  304: 'errors.sameOriginDestination',
  305: 'errors.addressNotFound',
  306: 'errors.tripLocked',

  307: 'errors.tripNotConfirmable',
  308: 'errors.minSeatsExceedTotal',

  // Bookings
  400: 'errors.bookingNotFound',
  401: 'errors.tripFull',
  402: 'errors.ownTrip',
  403: 'errors.alreadyBooked',
  404: 'errors.bookingStatusNotAllowed',
  406: 'errors.riderProfileIncomplete',
  407: 'errors.riderNotEligible',
  408: 'errors.driverNotEligible',
  409: 'errors.riderSeatTimeConflict',
  410: 'errors.riderOnActiveTrip',

  // Demand — ride requests. The numbers are unchanged from when these were
  // "rider-posted trips": the meanings are identical, and renumbering them
  // would silently re-label every string below.
  500: 'errors.riderTripNotFound',
  501: 'errors.riderTripClosed',
  502: 'errors.departureTooSoon',
  503: 'errors.riderTripSeatsExceeded',
  504: 'errors.riderTripConditionsConflict',
  505: 'errors.riderTripNotJoined',
  506: 'errors.alreadyJoined',
  507: 'errors.cannotServeOwnRequest',
  508: 'errors.driverInterestNotFound',
  509: 'errors.sharedTermsNotAccepted',
  510: 'errors.driverSuspended',
  511: 'errors.offerChoiceNotAvailable',
  512: 'errors.offerNotAvailable',
  513: 'errors.invalidSeatsOffered',
  520: 'errors.demandAlertNotFound',

  // Trip safety and reliability
  320: 'errors.cancelReasonRequired',
  411: 'errors.boardingCodeInvalid',
  412: 'errors.boardingCodeRequired',
  413: 'errors.shareLinkNotFound',
  800: 'errors.safetyIncidentNotFound',

  // Schedules
  550: 'errors.scheduleNotFound',
  551: 'errors.scheduleHasNoOccurrences',

  // Ratings
  600: 'errors.rateAfterCompletion',
  601: 'errors.alreadyRated',

  // Complaints / suggestions
  700: 'errors.feedbackNotFound',
  701: 'errors.tooManyOpenFeedback',

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
