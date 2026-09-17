// DTOs mirroring the backend responses (camelCase JSON).

import 'dart:convert';

import '../core/l10n.dart';
import '../core/places.dart';
import 'series_models.dart';

/// The API serialises `DateTime` values that are UTC but carry no offset
/// (EF materialises them with `DateTimeKind.Unspecified`), e.g.
/// `2026-08-29T04:08:54.4403987`. `DateTime.parse` would read those as *local*
/// time, shifting every timestamp by the device's offset. Treat a value with
/// no trailing `Z`/±hh:mm as UTC, which is what the server means.
DateTime? parseServerDate(String? raw) {
  if (raw == null || raw.isEmpty) return null;
  final hasZone = raw.endsWith('Z') || RegExp(r'[+-]\d{2}:?\d{2}$').hasMatch(raw);
  return DateTime.tryParse(hasZone ? raw : '${raw}Z');
}

/// A date of birth is a calendar date, not an instant — the server sends it as
/// `2000-05-14T00:00:00`. Reading it through [parseServerDate] would shift it
/// into the device's zone and can land on the previous day, so keep just the
/// date part and build a local midnight.
DateTime? parseDateOnly(String? raw) {
  if (raw == null || raw.length < 10) return null;
  return DateTime.tryParse(raw.substring(0, 10));
}

/// The `yyyy-MM-dd` form the API accepts back for a date of birth.
String formatDateOnly(DateTime d) =>
    '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

/// Gender as the API models it (`Wanes.Shareds.Enums.Gender`).
enum Gender {
  unspecified(0, 'gender.unspecified'),
  male(1, 'gender.male'),
  female(2, 'gender.female');

  const Gender(this.value, this.labelKey);
  final int value;

  /// l10n key — resolve with `context.tr(gender.labelKey)`.
  final String labelKey;

  String get label => AppLocalizations.current.t(labelKey);

  static Gender fromValue(int? v) =>
      Gender.values.firstWhere((g) => g.value == v, orElse: () => Gender.unspecified);
}

/// Light/dark preference as the API models it (`Wanes.Shareds.Enums.AppTheme`).
/// Stored on the account so the choice follows the user to a new device.
enum AppTheme {
  system(0),
  light(1),
  dark(2);

  const AppTheme(this.value);
  final int value;

  static AppTheme fromValue(int? v) =>
      AppTheme.values.firstWhere((t) => t.value == v, orElse: () => AppTheme.system);
}

/// UI language as the API models it (`Wanes.Shareds.Enums.Language`).
///
/// The server needs this to pick a language for *push* payloads: an FCM message
/// carries one title and one body, so unlike the in-app inbox it cannot ship
/// both and let the client choose. Keep it in sync whenever the app's locale
/// changes, or out-of-app notifications arrive in the wrong language.
enum AppLanguage {
  en(1),
  ar(2);

  const AppLanguage(this.value);
  final int value;

  /// Takes a code rather than a `Locale` so this file stays free of Flutter
  /// imports — it is a DTO layer.
  static AppLanguage fromLanguageCode(String code) =>
      code == 'ar' ? AppLanguage.ar : AppLanguage.en;
}

/// Which side of the amount the currency symbol sits on
/// (`Wanes.Shareds.Enums.CurrencyPosition`).
enum CurrencyPosition {
  before(1),
  after(2);

  const CurrencyPosition(this.value);
  final int value;

  static CurrencyPosition fromValue(int? v) =>
      CurrencyPosition.values.firstWhere((p) => p.value == v, orElse: () => CurrencyPosition.before);
}

/// The admin-chosen typeface (`Wanes.Shareds.Enums.AppFont`).
///
/// A closed set, not a family name: each value is a *pairing* of a Latin face
/// and an Arabic one, because a Latin display face carries no Arabic glyphs and
/// the app runs in both scripts. `core/theme.dart` holds the faces each value
/// resolves to; this layer only carries the choice.
enum AppFont {
  jakarta(1),
  inter(2),
  rubik(3),
  noto(4),
  tajawal(5),
  system(6),
  almarai(7),
  readexPro(8),
  alexandria(9),
  poppins(10),
  montserrat(11),
  amiri(12);

  const AppFont(this.value);
  final int value;

  /// Unknown values — a server that knows a font this build does not, or the
  /// `0` a row written before the column existed would give — fall back to the
  /// shipped pairing rather than leaving the app with no face at all.
  static AppFont fromValue(int? v) =>
      AppFont.values.firstWhere((f) => f.value == v, orElse: () => AppFont.jakarta);
}

/// Platform settings the admin owns from the CMS — currency, brand colour,
/// typeface and the support contact channels behind the Contact us screen.
///
/// Served anonymously by `GET /configuration`, so the app can paint the right
/// brand on the splash screen before anyone has signed in. The defaults here
/// are the shipped design, and they stand whenever the server cannot be reached.
class AppConfig {
  const AppConfig({
    this.currencyCode = 'JOD',
    this.currencySymbol = 'د.أ',
    this.currencyPosition = CurrencyPosition.after,
    this.currencyDecimals = 3,
    this.primaryColor = 0xFF0FAE9E,
    this.font = AppFont.jakarta,
    this.confirmCutoffMinutes = 60,
    this.confirmDecisionLeadMinutes = 15,
    this.minimumPassengersDefault = 3,
    this.averageSpeedKmh = 35,
    this.fareBaseAmount = 0.50,
    this.farePerKm = 0.10,
    this.scheduledSelectionWindowMinutes = 20,
    this.riderOfferChoice = true,
    this.boardingCodeRequired = true,
    this.lateCancelLeadMinutes = 120,
    this.freeCancelGraceMinutes = 3,
    this.emergencyNumber = '911',
    this.shareBaseUrl = '',
    this.seriesEnabled = true,
    this.seriesSkipNoticeHours = 24,
    this.seriesEndNoticeDays = 7,
    this.seriesDecisionHours = 12,
    this.supportPhone = '',
    this.supportWhatsApp = '',
    this.supportEmail = '',
    this.supportWebsite = '',
    this.supportHours = '',
  });

  final String currencyCode;
  final String currencySymbol;
  final CurrencyPosition currencyPosition;
  final int currencyDecimals;

  /// Brand primary as an ARGB int, ready for `Color(...)`.
  final int primaryColor;

  /// The display/body typeface. `WanesTheme` resolves it to a concrete face per
  /// script; the mono/data face is fixed and not part of this choice.
  final AppFont font;

  /// How long before departure a driver has to have answered the run-or-cancel
  /// question on a trip short of its seat threshold, and how long before that
  /// they are asked. Both are here because both draw clocks: the driver's card
  /// counts down to the decision, and a gathering trip's card tells the rider
  /// when they will know whether it runs.
  final int confirmCutoffMinutes;
  final int confirmDecisionLeadMinutes;

  Duration get confirmCutoff => Duration(minutes: confirmCutoffMinutes);

  /// The average speed behind every duration estimate, in km/h.
  ///
  /// The app needs it as well as the server: the earliest departure a rider may
  /// post for is one leg-time per seat, and being told "not before 08:40" while
  /// you are still choosing a time is help — being refused after you tap Post
  /// is a rebuke.
  /// How many passengers a new trip asks for before it confirms, unless the
  /// driver says otherwise.
  ///
  /// From the server because it is a marketplace decision, and on the posting
  /// form because the field has to *open* on it — a driver who never touches
  /// the control should still post under the platform's rule, and the app has
  /// no way to guess the number.
  final int minimumPassengersDefault;

  final double averageSpeedKmh;

  /// How long a [km]-long leg should take at [averageSpeedKmh].
  Duration estimatedLeg(double km) => Duration(
      minutes: (km.isFinite && km > 0 ? (km / averageSpeedKmh) * 60 : 0).round());

  /// The earliest departure a posting for [seats] seats over [km] may name —
  /// the mirror of the server's `RiderTripRules.EarliestDeparture`, floored and
  /// capped exactly as it is so the two cannot disagree at the tap.
  DateTime earliestDeparture(double km, int seats, {DateTime? from}) {
    final now = from ?? DateTime.now();
    final raw = estimatedLeg(km) * (seats < 1 ? 1 : seats);
    const floor = Duration(minutes: 15);
    const ceiling = Duration(hours: 6);
    final lead = raw < floor ? floor : (raw > ceiling ? ceiling : raw);
    return now.add(lead);
  }

  /// Flag-fall and per-kilometre rate behind an estimated fare.
  ///
  /// Configuration rather than constants because the server prices a
  /// hail-accepted trip off these same two numbers. While the app carried its
  /// own copies, the figure a driver saw on a request card and the price stamped
  /// on the trip they got by accepting it were free to disagree — and did, the
  /// moment an admin touched the rates.
  final double fareBaseAmount;
  final double farePerKm;

  // ── Shared marketplace, reliability, safety ──

  /// How long a request leaving later than the hour collects offers.
  final int scheduledSelectionWindowMinutes;

  /// Riders may pick an offer themselves while the window is open.
  final bool riderOfferChoice;

  /// The driver must type the rider's code to mark them aboard.
  final bool boardingCodeRequired;

  /// A cancellation closer than this to departure is late.
  final int lateCancelLeadMinutes;
  final int freeCancelGraceMinutes;

  /// The number the SOS button dials.
  final String emergencyNumber;

  /// Where trip links point (the public web host), or empty.
  final String shareBaseUrl;

  /// Whole-series commitments are on, and the notice their rules ask for.
  final bool seriesEnabled;
  final int seriesSkipNoticeHours;
  final int seriesEndNoticeDays;
  final int seriesDecisionHours;

  // ── Support contact ──
  //
  // Empty means "the admin hasn't configured this channel". Null from the API
  // collapses to the same empty string, so the screen only has to test one
  // thing before it decides whether to offer the row.
  final String supportPhone;
  final String supportWhatsApp;
  final String supportEmail;
  final String supportWebsite;

  /// Free text ("Sun–Thu, 9:00–17:00"), shown verbatim.
  final String supportHours;

  /// Whether there is any channel at all to show on the Contact us screen.
  bool get hasSupportChannel =>
      supportPhone.isNotEmpty ||
      supportWhatsApp.isNotEmpty ||
      supportEmail.isNotEmpty ||
      supportWebsite.isNotEmpty;

  static const fallback = AppConfig();

  factory AppConfig.fromJson(Map<String, dynamic> json) => AppConfig(
        currencyCode: (json['currencyCode'] as String?)?.trim().isNotEmpty == true
            ? (json['currencyCode'] as String).trim()
            : fallback.currencyCode,
        currencySymbol: (json['currencySymbol'] as String?)?.trim().isNotEmpty == true
            ? (json['currencySymbol'] as String).trim()
            : fallback.currencySymbol,
        currencyPosition: CurrencyPosition.fromValue((json['currencyPosition'] as num?)?.toInt()),
        currencyDecimals:
            ((json['currencyDecimals'] as num?)?.toInt() ?? fallback.currencyDecimals).clamp(0, 3),
        primaryColor: parseHexColor(json['primaryColor'] as String?) ?? fallback.primaryColor,
        font: AppFont.fromValue((json['fontFamily'] as num?)?.toInt()),
        // Clamped to the same 1..240 the server enforces: a zero from an older
        // API — or a row that predates the column — would otherwise give every
        // countdown a window that has already run out.
        confirmCutoffMinutes:
            ((json['confirmCutoffMinutes'] as num?)?.toInt() ?? fallback.confirmCutoffMinutes)
                .clamp(5, 720),
        confirmDecisionLeadMinutes: ((json['confirmDecisionLeadMinutes'] as num?)?.toInt() ??
                fallback.confirmDecisionLeadMinutes)
            .clamp(1, 240),
        minimumPassengersDefault:
            ((json['minimumPassengersDefault'] as num?)?.toInt() ??
                    fallback.minimumPassengersDefault)
                .clamp(1, 8),
        averageSpeedKmh:
            ((json['averageSpeedKmh'] as num?)?.toDouble() ?? fallback.averageSpeedKmh)
                .clamp(5.0, 120.0),
        // Zero is a legitimate rate (a flat flag-fall, or a fare that is all
        // distance), so only a missing value falls back to the shipped one.
        fareBaseAmount:
            (json['fareBaseAmount'] as num?)?.toDouble() ?? fallback.fareBaseAmount,
        farePerKm: (json['farePerKm'] as num?)?.toDouble() ?? fallback.farePerKm,
        scheduledSelectionWindowMinutes: (json['scheduledSelectionWindowMinutes'] as num?)?.toInt() ??
            fallback.scheduledSelectionWindowMinutes,
        riderOfferChoice: json['riderOfferChoice'] as bool? ?? fallback.riderOfferChoice,
        boardingCodeRequired: json['boardingCodeRequired'] as bool? ?? fallback.boardingCodeRequired,
        lateCancelLeadMinutes:
            (json['lateCancelLeadMinutes'] as num?)?.toInt() ?? fallback.lateCancelLeadMinutes,
        freeCancelGraceMinutes:
            (json['freeCancelGraceMinutes'] as num?)?.toInt() ?? fallback.freeCancelGraceMinutes,
        emergencyNumber: _text(json['emergencyNumber']).isEmpty
            ? fallback.emergencyNumber
            : _text(json['emergencyNumber']),
        shareBaseUrl: _text(json['shareBaseUrl']),
        seriesEnabled: json['seriesCommitmentsEnabled'] as bool? ?? fallback.seriesEnabled,
        seriesSkipNoticeHours:
            (json['seriesSkipNoticeHours'] as num?)?.toInt() ?? fallback.seriesSkipNoticeHours,
        seriesEndNoticeDays:
            (json['seriesEndNoticeDays'] as num?)?.toInt() ?? fallback.seriesEndNoticeDays,
        seriesDecisionHours:
            (json['seriesDecisionHours'] as num?)?.toInt() ?? fallback.seriesDecisionHours,
        supportPhone: _text(json['supportPhone']),
        supportWhatsApp: _text(json['supportWhatsApp']),
        supportEmail: _text(json['supportEmail']),
        supportWebsite: _text(json['supportWebsite']),
        supportHours: _text(json['supportHours']),
      );

  Map<String, dynamic> toJson() => {
        'currencyCode': currencyCode,
        'currencySymbol': currencySymbol,
        'currencyPosition': currencyPosition.value,
        'currencyDecimals': currencyDecimals,
        'primaryColor': hexColor,
        'fontFamily': font.value,
        'confirmCutoffMinutes': confirmCutoffMinutes,
        'confirmDecisionLeadMinutes': confirmDecisionLeadMinutes,
        'averageSpeedKmh': averageSpeedKmh,
        'minimumPassengersDefault': minimumPassengersDefault,
        'fareBaseAmount': fareBaseAmount,
        'farePerKm': farePerKm,
        'scheduledSelectionWindowMinutes': scheduledSelectionWindowMinutes,
        'riderOfferChoice': riderOfferChoice,
        'boardingCodeRequired': boardingCodeRequired,
        'lateCancelLeadMinutes': lateCancelLeadMinutes,
        'freeCancelGraceMinutes': freeCancelGraceMinutes,
        'emergencyNumber': emergencyNumber,
        'shareBaseUrl': shareBaseUrl,
        'seriesCommitmentsEnabled': seriesEnabled,
        'seriesSkipNoticeHours': seriesSkipNoticeHours,
        'seriesEndNoticeDays': seriesEndNoticeDays,
        'seriesDecisionHours': seriesDecisionHours,
        'supportPhone': supportPhone,
        'supportWhatsApp': supportWhatsApp,
        'supportEmail': supportEmail,
        'supportWebsite': supportWebsite,
        'supportHours': supportHours,
      };

  /// The `#RRGGBB` form, for round-tripping through the cache.
  String get hexColor =>
      '#${(primaryColor & 0xFFFFFF).toRadixString(16).padLeft(6, '0').toUpperCase()}';

  /// Optional string field → trimmed text, with null and non-strings as empty.
  static String _text(Object? raw) => raw is String ? raw.trim() : '';
}

/// `#RRGGBB` / `#RGB` / `RRGGBB` → opaque ARGB. Null when it isn't a hex colour,
/// so the caller can fall back rather than paint something arbitrary.
int? parseHexColor(String? raw) {
  var hex = raw?.trim().replaceFirst('#', '') ?? '';
  if (hex.length == 3) hex = hex.split('').map((c) => '$c$c').join();
  if (hex.length != 6 || !RegExp(r'^[0-9a-fA-F]{6}$').hasMatch(hex)) return null;
  return 0xFF000000 | int.parse(hex, radix: 16);
}

class Profile {
  Profile({
    required this.id,
    required this.phone,
    this.firstName = '',
    this.lastName = '',
    this.displayName,
    this.email,
    this.gender = Gender.unspecified,
    this.dateOfBirth,
    this.bio,
    this.avatarUrl,
    this.driverStatus = 0,
    this.ratingAvg = 0,
    this.isRider = true,
    this.isDriver = false,
    this.theme = AppTheme.system,
    this.emergencyContactName,
    this.emergencyContactPhone,
    this.tripsAsDriver = 0,
    this.driverCancellations = 0,
    this.driverCompletionRate,
    this.suspendedUntil,
  });

  /// Who the SOS button messages.
  final String? emergencyContactName;
  final String? emergencyContactPhone;

  // The driver's own reliability figures.
  final int tripsAsDriver;
  final int driverCancellations;

  /// 0–1, or null with no history yet.
  final double? driverCompletionRate;

  /// Instant requests are paused until this passes.
  final DateTime? suspendedUntil;

  bool get isSuspended => suspendedUntil != null && suspendedUntil!.isAfter(DateTime.now());

  final int id;
  final String phone;
  final String firstName;
  final String lastName;
  final String? displayName;
  final String? email;
  final Gender gender;
  final DateTime? dateOfBirth;
  final String? bio;
  final String? avatarUrl;
  final int driverStatus;
  final double ratingAvg;
  final bool isRider;
  final bool isDriver;
  final AppTheme theme;

  // No ride-with preferences on the profile. Who a rider travels with is a
  // decision about one journey, so it is stated on the search that finds the
  // trip and on the posting they write — not once, in a settings screen, for
  // every journey they will ever take.

  String get name =>
      displayName?.isNotEmpty == true ? displayName! : '$firstName $lastName'.trim();

  /// A phone-only account straight out of OTP sign-up has no name yet. Riders
  /// and drivers see each other by name, so we ask for one before letting the
  /// account into the app (see CompleteProfileScreen).
  bool get isComplete => firstName.trim().isNotEmpty && lastName.trim().isNotEmpty;

  factory Profile.fromJson(Map<String, dynamic> j) => Profile(
        id: j['id'] as int,
        phone: j['phone'] as String? ?? '',
        firstName: j['firstName'] as String? ?? '',
        lastName: j['lastName'] as String? ?? '',
        displayName: j['displayName'] as String?,
        email: j['email'] as String?,
        gender: Gender.fromValue(j['gender'] as int?),
        dateOfBirth: parseDateOnly(j['dateOfBirth'] as String?),
        bio: j['bio'] as String?,
        avatarUrl: j['avatarUrl'] as String?,
        driverStatus: j['driverStatus'] as int? ?? 0,
        ratingAvg: (j['ratingAvg'] as num?)?.toDouble() ?? 0,
        isRider: j['isRider'] as bool? ?? true,
        isDriver: j['isDriver'] as bool? ?? false,
        theme: AppTheme.fromValue(j['theme'] as int?),
        emergencyContactName: j['emergencyContactName'] as String?,
        emergencyContactPhone: j['emergencyContactPhone'] as String?,
        tripsAsDriver: (j['tripsAsDriver'] as num?)?.toInt() ?? 0,
        driverCancellations: (j['driverCancellations'] as num?)?.toInt() ?? 0,
        driverCompletionRate: (j['driverCompletionRate'] as num?)?.toDouble(),
        suspendedUntil: parseServerDate(j['suspendedUntil'] as String?),
      );

  Map<String, dynamic> toJson() => {
        'emergencyContactName': emergencyContactName,
        'emergencyContactPhone': emergencyContactPhone,
        'tripsAsDriver': tripsAsDriver,
        'driverCancellations': driverCancellations,
        'driverCompletionRate': driverCompletionRate,
        'suspendedUntil': suspendedUntil?.toUtc().toIso8601String(),
        'id': id,
        'phone': phone,
        'firstName': firstName,
        'lastName': lastName,
        'displayName': displayName,
        'email': email,
        'gender': gender.value,
        'dateOfBirth': dateOfBirth == null ? null : formatDateOnly(dateOfBirth!),
        'bio': bio,
        'avatarUrl': avatarUrl,
        'driverStatus': driverStatus,
        'ratingAvg': ratingAvg,
        'isRider': isRider,
        'isDriver': isDriver,
        'theme': theme.value,
      };
}

class AuthResult {
  AuthResult({
    required this.token,
    required this.refreshToken,
    required this.isNewUser,
    required this.profile,
  });

  /// Short-lived bearer token. [refreshToken] is what outlives it.
  final String token;
  final String refreshToken;
  final bool isNewUser;
  final Profile profile;

  factory AuthResult.fromJson(Map<String, dynamic> j) => AuthResult(
        token: j['token'] as String,
        refreshToken: j['refreshToken'] as String? ?? '',
        isNewUser: j['isNewUser'] as bool? ?? false,
        profile: Profile.fromJson(j['profile'] as Map<String, dynamic>),
      );
}

class Trip {
  Trip({
    required this.id,
    required this.driverName,
    required this.driverRating,
    required this.originAddress,
    required this.destinationAddress,
    required this.departAt,
    required this.seatsLeft,
    this.vehicleId = 0,
    this.vehicleLabel = '',
    this.vehicleColor = '',
    this.vehiclePlate = '',
    this.originLat = 0,
    this.originLng = 0,
    this.destinationLat = 0,
    this.destinationLng = 0,
    this.seatsTotal = 0,
    this.status = 8,
    this.pricePerSeat,
    this.startedAt,
    this.minSeatsToConfirm = 1,
    this.seatsHeld = 0,
    this.isGathering = false,
    this.isConfirmed = false,
    this.isFull = false,
    this.genderPolicy = GenderPolicy.any,
    this.minAge,
    this.maxAge,
    this.driverTrips = 0,
    this.driverCompletionRate,
    this.scheduleId,
    this.occurrenceDate,
    this.seriesCommitmentId,
    this.series,
  });

  /// The driver's recurring schedule this trip is a day of, and that day.
  final int? scheduleId;
  final DateTime? occurrenceDate;

  /// The series commitment the seat (booking) or the day (trip) was made under.
  final int? seriesCommitmentId;

  /// The recurrence behind it — what the repeat badge and "whole series" read.
  final SeriesInfo? series;

  final int id;
  final String driverName;
  final double driverRating;

  /// Trips the driver completed, and the share of accepted trips they did not
  /// cancel (0–1, null for a newcomer).
  final int driverTrips;
  final double? driverCompletionRate;

  final int vehicleId;

  /// "Toyota Prius" / "Silver" / "WNS-4021" — shown to the rider on the
  /// results, confirm-booking, active-trip and booked screens.
  final String vehicleLabel;
  final String vehicleColor;
  final String vehiclePlate;
  final String originAddress;
  final double originLat;
  final double originLng;
  final String destinationAddress;
  final double destinationLat;
  final double destinationLng;

  final DateTime departAt;
  final int seatsLeft;
  final int seatsTotal;
  // The LIFECYCLE only: 1 Posted · 3 Active · 4 Completed · 5 Cancelled ·
  // 6 Arrived · 7 EnRoute. Whether the car is full and whether the trip is
  // confirmed are separate questions with their own fields — 2 Full is a
  // pre-v2 value the server no longer sends and only old rows can carry.
  final int status;
  final double? pricePerSeat;

  /// When the driver actually started. Sent only by the single-trip GET, and
  /// null until the trip is under way — the live map advances the car against
  /// this rather than against an assumed departure.
  final DateTime? startedAt;

  /// Seats that must be held before anybody is confirmed; 1 means no condition.
  final int minSeatsToConfirm;

  /// Seats already held, committed or not.
  final int seatsHeld;

  /// The trip is short of the seats its driver asked for, so it may yet not
  /// run. Shown on the card rather than hidden: a rider booking one should know
  /// that is possible.
  final bool isGathering;

  /// Its passengers are committed. Not the negation of [isGathering]: a trip
  /// with no threshold is neither until somebody takes a seat.
  final bool isConfirmed;

  /// No seats left. Sent by the server rather than inferred from [seatsLeft] so
  /// the app and the console agree on one rule — this used to be a status, and
  /// anything still comparing against that value is quietly wrong.
  final bool isFull;

  /// Who may take a seat, and any age bounds on them.
  final GenderPolicy genderPolicy;
  final int? minAge;
  final int? maxAge;

  /// Whether the driver asked anything of their riders — what the card shows a
  /// conditions row for.
  bool get hasConditions =>
      genderPolicy != GenderPolicy.any || minAge != null || maxAge != null;

  /// How many more seats this trip needs before it is on, or 0 when it is
  /// already there.
  int get seatsToConfirm {
    final missing = minSeatsToConfirm - seatsHeld;
    return missing > 0 ? missing : 0;
  }

  static const _statusKeys = {
    1: 'tripStatus.posted',
    2: 'tripStatus.full',
    3: 'tripStatus.active',
    4: 'tripStatus.completed',
    5: 'tripStatus.cancelled',
    6: 'tripStatus.arrived',
    7: 'tripStatus.enRoute',
  };

  /// l10n key for what to show as this trip's state — resolve with
  /// `context.tr(trip.statusKey)`.
  ///
  /// Capacity is folded back in here, and only here. It stopped being a status
  /// server-side (the lifecycle and the seats are separate questions now), but a
  /// rider looking at a card still wants one word for it, and "Posted" on a
  /// trip with no seats left is not that word.
  String get statusKey =>
      isFull && status == 1 ? 'tripStatus.full' : (_statusKeys[status] ?? 'tripStatus.posted');
  String get statusLabel => AppLocalizations.current.t(statusKey);

  /// A posted trip nobody has booked yet — the driver may still edit it.
  /// The server is the authority (it also checks cancelled bookings).
  bool get editable => status == 1 && seatsLeft == seatsTotal;

  /// Nothing more happens on this trip: no seat on it can be moved either.
  bool get isFinished => status == 4 || status == 5;

  /// The driver is out on this one — on their way to a pickup, at one, or
  /// carrying riders. They are unavailable for a second ride while it lasts
  /// (server rule; see `DriverAvailabilityRules.IsEngaged`, which this mirrors).
  bool get isUnderway => status == 3 || status == 6 || status == 7;

  /// One day of a series: the driver's own schedule, or a rider's they took.
  bool get isRecurring => scheduleId != null || seriesCommitmentId != null;

  factory Trip.fromJson(Map<String, dynamic> j) => Trip(
        id: j['id'] as int,
        driverName: j['driverName'] as String? ?? 'Driver',
        driverRating: (j['driverRating'] as num?)?.toDouble() ?? 0,
        driverTrips: (j['driverTrips'] as num?)?.toInt() ?? 0,
        driverCompletionRate: (j['driverCompletionRate'] as num?)?.toDouble(),
        vehicleId: j['vehicleId'] as int? ?? 0,
        vehicleLabel: j['vehicleLabel'] as String? ?? '',
        vehicleColor: j['vehicleColor'] as String? ?? '',
        vehiclePlate: j['vehiclePlate'] as String? ?? '',
        originAddress: j['originAddress'] as String? ?? '',
        originLat: (j['originLat'] as num?)?.toDouble() ?? 0,
        originLng: (j['originLng'] as num?)?.toDouble() ?? 0,
        destinationAddress: j['destinationAddress'] as String? ?? '',
        destinationLat: (j['destinationLat'] as num?)?.toDouble() ?? 0,
        destinationLng: (j['destinationLng'] as num?)?.toDouble() ?? 0,
        departAt: parseServerDate(j['departAt'] as String?) ?? DateTime.now(),
        seatsLeft: j['seatsLeft'] as int? ?? 0,
        seatsTotal: j['seatsTotal'] as int? ?? 0,
        status: j['status'] as int? ?? 1,
        minSeatsToConfirm: j['minSeatsToConfirm'] as int? ?? 1,
        seatsHeld: j['seatsHeld'] as int? ?? 0,
        isGathering: j['isGathering'] as bool? ?? false,
        isConfirmed: j['isConfirmed'] as bool? ?? false,
        isFull: j['isFull'] as bool? ?? ((j['seatsLeft'] as int? ?? 1) <= 0),
        genderPolicy: GenderPolicy.fromValue(j['genderPolicy'] as int?),
        minAge: (j['minAge'] as num?)?.toInt(),
        maxAge: (j['maxAge'] as num?)?.toInt(),
        pricePerSeat: (j['pricePerSeat'] as num?)?.toDouble(),
        startedAt: parseServerDate(j['startedAt'] as String?),
        scheduleId: (j['scheduleId'] as num?)?.toInt(),
        occurrenceDate: parseDateOnly(j['occurrenceDate'] as String?),
        seriesCommitmentId: (j['seriesCommitmentId'] as num?)?.toInt(),
        series: SeriesInfo.tryParse(j['series']),
      );
}

class Vehicle {
  Vehicle({
    required this.id,
    required this.make,
    required this.model,
    required this.plate,
    required this.seatCapacity,
    this.isDefault = false,
  });

  final int id;
  final String make;
  final String model;
  final String plate;
  final int seatCapacity;
  final bool isDefault;

  String get label => '$make $model · $plate';

  factory Vehicle.fromJson(Map<String, dynamic> j) => Vehicle(
        id: j['id'] as int,
        make: j['make'] as String? ?? '',
        model: j['model'] as String? ?? '',
        plate: j['plate'] as String? ?? '',
        seatCapacity: j['seatCapacity'] as int? ?? 1,
        isDefault: j['isDefault'] as bool? ?? false,
      );
}

/// Who a ride is open to, as a condition one side sets on the other
/// (`Wanes.Shareds.Enums.GenderPolicy`).
///
/// The same three values express both directions — a driver's condition on
/// their riders, and a rider's on their driver and co-riders — so one picker
/// and one set of labels serve both screens.
enum GenderPolicy {
  any(0, 'conditions.anyone'),
  maleOnly(1, 'conditions.menOnly'),
  femaleOnly(2, 'conditions.womenOnly');

  const GenderPolicy(this.value, this.labelKey);

  final int value;
  final String labelKey;

  String get label => AppLocalizations.current.t(labelKey);

  static GenderPolicy fromValue(int? v) =>
      GenderPolicy.values.firstWhere((g) => g.value == v, orElse: () => GenderPolicy.any);
}

/// Why a rider-posted trip stopped being answerable. Mirrors the backend's
/// `RiderTripClosedReason` — the three ways a card can stop being answerable. It
/// is deliberately not the trip's status: "nobody took it" and "they withdrew
/// it" both land on Cancelled, and a driver who was looking at the card deserves
/// to be told which.
enum RiderTripClosedReason {
  /// The last rider on it left.
  cancelled,

  /// Another driver got there first.
  claimed,

  /// Its departure came and went with nobody claiming it.
  expired,

  /// A close from a newer server whose reason this build does not know. The
  /// card still goes; only the wording falls back to the neutral one.
  unknown;

  static RiderTripClosedReason fromWire(String? raw) => switch (raw) {
        'Cancelled' => RiderTripClosedReason.cancelled,
        'Claimed' => RiderTripClosedReason.claimed,
        'Expired' => RiderTripClosedReason.expired,
        _ => RiderTripClosedReason.unknown,
      };

  /// The l10n key for the one-line notice shown when a card disappears.
  String get messageKey => switch (this) {
        RiderTripClosedReason.cancelled => 'driver.requestWithdrawn',
        RiderTripClosedReason.claimed => 'driver.requestTaken',
        RiderTripClosedReason.expired => 'driver.requestExpired',
        RiderTripClosedReason.unknown => 'driver.requestClosed',
      };
}

/// The server saying "this posting is over" — see `SseConnectionManager.BroadcastAsync`.
class RiderTripClosed {
  const RiderTripClosed({required this.riderTripId, required this.reason});

  final int riderTripId;
  final RiderTripClosedReason reason;
}

/// A trip a rider posted: demand, not supply.
///
/// One shape for both sides, because they need the same facts — the riders on
/// it read their own seats off [mySeats], and a driver deciding whether to take
/// it reads [suggestedPricePerSeat] and [distanceKm].
class RiderTrip {
  RiderTrip({
    required this.id,
    required this.originAddress,
    required this.destinationAddress,
    required this.departAt,
    this.riderId = 0,
    this.riderName = '',
    this.seatsWanted = 1,
    this.riderCount = 1,
    this.mySeats = 0,
    this.originLat = 0,
    this.originLng = 0,
    this.destinationLat = 0,
    this.destinationLng = 0,
    this.driverGenderPolicy = GenderPolicy.any,
    this.coRiderGenderPolicy = GenderPolicy.any,
    this.minAge,
    this.maxAge,
    this.status = 1,
    this.matchedTripId,
    this.interestCount = 0,
    this.iHaveOffered = false,
    this.distanceKm = 0,
    this.suggestedPricePerSeat = 0,
    this.decideAt,
    this.reopenedFromRequestId,
    this.scheduleId,
    this.occurrenceDate,
    this.series,
  });

  /// When the collected offers are decided — in the future while a scheduled
  /// request is still comparing drivers, null before the first offer.
  final DateTime? decideAt;

  /// The request this one replaced after its driver cancelled.
  final int? reopenedFromRequestId;

  /// Offers are in and the riders may still compare them.
  bool get isComparingOffers =>
      isOpen && interestCount > 0 && decideAt != null && decideAt!.isAfter(DateTime.now());

  /// The rider's recurring schedule this request is a day of, and that day.
  final int? scheduleId;
  final DateTime? occurrenceDate;

  /// The recurrence behind the card — the repeat badge and "take the whole series".
  final SeriesInfo? series;

  bool get isRecurring => scheduleId != null;

  final int id;
  final int riderId;
  final String riderName;
  final String originAddress;
  final String destinationAddress;

  /// The departure its riders want — what a driver taking this is agreeing to,
  /// and what the trip they create leaves at.
  final DateTime departAt;

  /// Seats wanted in total, across every rider holding one.
  final int seatsWanted;

  /// How many riders are on it. More than one means a pool.
  final int riderCount;

  /// The caller's own seats, or 0 when they hold none — which is how a rider's
  /// own list tells "mine" from a posting they are merely looking at.
  final int mySeats;

  final double originLat;
  final double originLng;
  final double destinationLat;
  final double destinationLng;

  /// Who may claim it, and who may join it.
  final GenderPolicy driverGenderPolicy;
  final GenderPolicy coRiderGenderPolicy;
  final int? minAge;
  final int? maxAge;

  /// The request's own lifecycle — 1 open, 2 matched, 3 cancelled, 4 expired.
  ///
  /// Not a [Trip.status]. Demand has states a trip has no word for: "nobody has
  /// answered this yet" and "its departure passed unanswered" are not places a
  /// trip can be, which is why sharing the enum quietly mislabelled both.
  final int status;

  /// The trip this became, once a driver was selected — null while it is still
  /// demand.
  ///
  /// **Follow this, never [id], once [isMatched].** A request and the ride it
  /// produces are two rows, and this is the only thread between them: a card, a
  /// notification or a screen that was open when the driver was chosen finds
  /// the ride here.
  final int? matchedTripId;

  /// How many drivers have said they are willing. On a rider's card it is the
  /// one thing that visibly moves while they wait.
  final int interestCount;

  /// This driver has a live offer on it.
  final bool iHaveOffered;

  /// Straight-line length of the leg, and what the platform's rates make a seat
  /// on it worth. The second is a *suggestion*: the driver's own figure is what
  /// the trip lists at, and this only decides what the price sheet opens on.
  final double distanceKm;
  final double suggestedPricePerSeat;

  /// Still on the board, waiting for a driver.
  bool get isOpen => status == 1;

  /// A driver was selected and the ride exists — at [matchedTripId], not [id].
  bool get isMatched => status == 2;

  /// Closed without a driver: the riders withdrew, or its departure passed.
  bool get isClosed => status == 3 || status == 4;

  /// Somebody else joined a posting this rider is on.
  bool get isPool => riderCount > 1;

  /// Whether the riders asked for anything of their driver or of each other —
  /// what the card shows a conditions row for.
  bool get hasConditions =>
      driverGenderPolicy != GenderPolicy.any ||
      coRiderGenderPolicy != GenderPolicy.any ||
      minAge != null ||
      maxAge != null;

  /// How long until it leaves, which is also how long a driver has to take it:
  /// a posting runs until its own departure and not on a countdown of its own.
  Duration get timeLeft {
    final left = departAt.toLocal().difference(DateTime.now());
    return left > Duration.zero ? left : Duration.zero;
  }

  static const _statusKeys = {
    1: 'riderTripStatus.open',
    2: 'riderTripStatus.claimed',
    3: 'riderTripStatus.cancelled',
    4: 'riderTripStatus.expired',
  };

  String get statusKey => _statusKeys[status] ?? 'riderTripStatus.open';
  String get statusLabel => AppLocalizations.current.t(statusKey);

  factory RiderTrip.fromJson(Map<String, dynamic> j) => RiderTrip(
        id: j['id'] as int,
        riderId: j['riderId'] as int? ?? 0,
        riderName: j['riderName'] as String? ?? '',
        originAddress: j['originAddress'] as String? ?? '',
        destinationAddress: j['destinationAddress'] as String? ?? '',
        departAt: parseServerDate(j['departAt'] as String?) ?? DateTime.now(),
        seatsWanted: j['seatsWanted'] as int? ?? 1,
        riderCount: j['riderCount'] as int? ?? 1,
        mySeats: j['mySeats'] as int? ?? 0,
        matchedTripId: j['matchedTripId'] as int?,
        interestCount: j['interestCount'] as int? ?? 0,
        iHaveOffered: j['iHaveOffered'] as bool? ?? false,
        originLat: (j['originLat'] as num?)?.toDouble() ?? 0,
        originLng: (j['originLng'] as num?)?.toDouble() ?? 0,
        destinationLat: (j['destinationLat'] as num?)?.toDouble() ?? 0,
        destinationLng: (j['destinationLng'] as num?)?.toDouble() ?? 0,
        driverGenderPolicy: GenderPolicy.fromValue(j['driverGenderPolicy'] as int?),
        coRiderGenderPolicy: GenderPolicy.fromValue(j['coRiderGenderPolicy'] as int?),
        minAge: (j['minAge'] as num?)?.toInt(),
        maxAge: (j['maxAge'] as num?)?.toInt(),
        status: j['status'] as int? ?? 1,
        distanceKm: (j['distanceKm'] as num?)?.toDouble() ?? 0,
        suggestedPricePerSeat: (j['suggestedPricePerSeat'] as num?)?.toDouble() ?? 0,
        decideAt: parseServerDate(j['decideAt'] as String?),
        reopenedFromRequestId: (j['reopenedFromRequestId'] as num?)?.toInt(),
        scheduleId: (j['scheduleId'] as num?)?.toInt(),
        occurrenceDate: parseDateOnly(j['occurrenceDate'] as String?),
        series: SeriesInfo.tryParse(j['series']),
      );
}

/// One trip wanting a driver, and what taking it would cost them — the driver's
/// side of [SearchMatch].
///
/// The rider's match measures a *walk*; this measures a *diversion*. Same two
/// bands, same corridor maths on the server, opposite side of the car.
class DemandMatch {
  const DemandMatch({
    required this.tier,
    required this.trip,
    this.pickupDetourKm = 0,
    this.dropoffDetourKm = 0,
    this.totalDetourKm = 0,
    this.minutesFromWhen = 0,
  });

  final SearchTier tier;

  /// The trip itself — a trip with no driver on it yet.
  final RiderTrip trip;

  /// How far off the driver's own route each end sits, in kilometres.
  final double pickupDetourKm;
  final double dropoffDetourKm;

  /// Both ends together: what the driver is really choosing on.
  final double totalDetourKm;

  /// How far the pool's wanted departure is from the driver's, in minutes.
  /// Negative means the riders want to leave earlier than the driver said.
  final int minutesFromWhen;

  bool get isOnTheWay => tier == SearchTier.onTheWay;

  /// A diversion small enough not to be worth a line on the card.
  bool get isDoorToDoor => totalDetourKm < 0.5;

  factory DemandMatch.fromJson(Map<String, dynamic> j) => DemandMatch(
        tier: SearchTier.fromValue(j['tier'] as int?),
        trip: RiderTrip.fromJson((j['trip'] as Map<String, dynamic>?) ?? const {'id': 0}),
        pickupDetourKm: (j['pickupDetourKm'] as num?)?.toDouble() ?? 0,
        dropoffDetourKm: (j['dropoffDetourKm'] as num?)?.toDouble() ?? 0,
        totalDetourKm: (j['totalDetourKm'] as num?)?.toDouble() ?? 0,
        minutesFromWhen: (j['minutesFromWhen'] as num?)?.toInt() ?? 0,
      );
}

/// What a driver's search comes back with.
class DemandSearchResult {
  const DemandSearchResult({this.matches = const [], this.seatsOffered = 0});

  final List<DemandMatch> matches;

  /// Seats the driver has to offer — read off their car unless they said
  /// otherwise, so the list can say when the car is full.
  final int seatsOffered;

  bool get isEmpty => matches.isEmpty;

  factory DemandSearchResult.fromJson(Map<String, dynamic> j) => DemandSearchResult(
        matches: ((j['matches'] as List?) ?? const [])
            .map((e) => DemandMatch.fromJson(e as Map<String, dynamic>))
            .toList(),
        seatsOffered: (j['seatsOffered'] as num?)?.toInt() ?? 0,
      );
}

/// How often a schedule produces a trip (`Wanes.Shareds.Enums.Recurrence`).
enum Recurrence {
  daily(1, 'schedule.daily'),
  weekly(2, 'schedule.weekly'),
  monthly(3, 'schedule.monthly');

  const Recurrence(this.value, this.labelKey);

  final int value;
  final String labelKey;

  String get label => AppLocalizations.current.t(labelKey);

  static Recurrence fromValue(int? v) =>
      Recurrence.values.firstWhere((r) => r.value == v, orElse: () => Recurrence.weekly);
}

/// The days a weekly schedule runs on, as the server's bit flags. The bit order
/// follows `DateTime.sunday..saturday` shifted to zero, so the conversion is a
/// shift and not a lookup.
class WeekDaySet {
  const WeekDaySet(this.mask);

  final int mask;

  static const none = WeekDaySet(0);

  /// Dart's `DateTime.weekday` is 1 = Monday … 7 = Sunday; the server's flags
  /// start at Sunday. This is the one place that difference is handled.
  static int bitFor(int dartWeekday) => 1 << (dartWeekday % 7);

  bool has(int dartWeekday) => mask & bitFor(dartWeekday) != 0;

  WeekDaySet toggle(int dartWeekday) => WeekDaySet(mask ^ bitFor(dartWeekday));

  bool get isEmpty => mask == 0;
}

/// A recurring posting, from either side.
///
/// It matches nothing itself: the server turns it into ordinary trips and
/// postings over a rolling fortnight, which is why [nextDepartures] — computed,
/// never stored — is the only honest way to show what one means.
class TripSchedule {
  TripSchedule({
    required this.id,
    required this.originAddress,
    required this.destinationAddress,
    required this.recurrence,
    required this.timeOfDay,
    required this.startDate,
    this.ownerRole = 1,
    this.daysOfWeek = WeekDaySet.none,
    this.dayOfMonth,
    this.timeZoneId,
    this.endDate,
    this.seats = 1,
    this.pricePerSeat,
    this.vehicleId,
    this.minSeatsToConfirm = 1,
    this.genderPolicy = GenderPolicy.any,
    this.coRiderGenderPolicy = GenderPolicy.any,
    this.minAge,
    this.maxAge,
    this.isPaused = false,
    this.nextDepartures = const [],
    this.generated = 0,
  });

  final int id;

  /// 1 Rider · 2 Driver — which side wrote it, and so what it generates.
  final int ownerRole;
  final String originAddress;
  final String destinationAddress;
  final Recurrence recurrence;
  final WeekDaySet daysOfWeek;
  final int? dayOfMonth;

  /// The wall-clock departure, in [timeZoneId]. Local time on purpose: "the
  /// seven o'clock" stays the seven o'clock across a clock change.
  final TimeOfDayValue timeOfDay;
  final String? timeZoneId;
  final DateTime startDate;
  final DateTime? endDate;
  final int seats;
  final double? pricePerSeat;
  final int? vehicleId;
  final int minSeatsToConfirm;
  /// A driver's condition on their passengers, or a rider's on who may drive
  /// them — whichever side owns the schedule.
  final GenderPolicy genderPolicy;

  /// Rider-owned schedules only: who else may be aboard. A rider's posting
  /// carries two conditions where a driver's trip carries one.
  final GenderPolicy coRiderGenderPolicy;

  final int? minAge;
  final int? maxAge;

  /// Generation is stopped; what already exists still runs.
  final bool isPaused;

  /// On create: how many days the server wrote straight away.
  final int generated;

  /// The next few departures it will produce.
  final List<DateTime> nextDepartures;

  bool get isDriverSchedule => ownerRole == 2;

  factory TripSchedule.fromJson(Map<String, dynamic> j) => TripSchedule(
        id: j['id'] as int,
        ownerRole: j['ownerRole'] as int? ?? 1,
        originAddress: j['originAddress'] as String? ?? '',
        destinationAddress: j['destinationAddress'] as String? ?? '',
        recurrence: Recurrence.fromValue(j['recurrence'] as int?),
        daysOfWeek: WeekDaySet((j['daysOfWeek'] as num?)?.toInt() ?? 0),
        dayOfMonth: (j['dayOfMonth'] as num?)?.toInt(),
        timeOfDay: TimeOfDayValue.parse(j['timeOfDay'] as String?),
        timeZoneId: j['timeZoneId'] as String?,
        startDate: parseDateOnly(j['startDate'] as String?) ?? DateTime.now(),
        endDate: parseDateOnly(j['endDate'] as String?),
        seats: j['seats'] as int? ?? 1,
        pricePerSeat: (j['pricePerSeat'] as num?)?.toDouble(),
        vehicleId: (j['vehicleId'] as num?)?.toInt(),
        minSeatsToConfirm: j['minSeatsToConfirm'] as int? ?? 1,
        genderPolicy: GenderPolicy.fromValue(j['genderPolicy'] as int?),
        minAge: (j['minAge'] as num?)?.toInt(),
        maxAge: (j['maxAge'] as num?)?.toInt(),
        isPaused: j['isPaused'] as bool? ?? false,
        generated: (j['generated'] as num?)?.toInt() ?? 0,
        nextDepartures: (j['nextDepartures'] as List<dynamic>? ?? [])
            .map((e) => parseServerDate(e as String?)?.toLocal())
            .whereType<DateTime>()
            .toList(),
      );
}

/// An `HH:mm:ss` wall-clock time, kept apart from Flutter's own `TimeOfDay` so
/// the model layer stays free of widget imports.
class TimeOfDayValue {
  const TimeOfDayValue(this.hour, this.minute);

  final int hour;
  final int minute;

  static TimeOfDayValue parse(String? raw) {
    final parts = (raw ?? '').split(':');
    if (parts.length < 2) return const TimeOfDayValue(8, 0);
    return TimeOfDayValue(
      int.tryParse(parts[0])?.clamp(0, 23) ?? 8,
      int.tryParse(parts[1])?.clamp(0, 59) ?? 0,
    );
  }

  /// The `HH:mm:ss` the server parses back into a `TimeOnly`.
  String get wire =>
      '${hour.toString().padLeft(2, '0')}:${minute.toString().padLeft(2, '0')}:00';

  @override
  String toString() => '${hour.toString().padLeft(2, '0')}:${minute.toString().padLeft(2, '0')}';
}

class Booking {
  Booking({
    required this.id,
    required this.tripId,
    required this.seats,
    required this.status,
    required this.originAddress,
    required this.destinationAddress,
    this.riderId = 0,
    this.departAt,
    this.tripStatus = 1,
    this.driverPhone,
    this.pricePerSeat,
    this.minSeatsToConfirm = 1,
    this.seatsHeld = 0,
    this.boardingCode,
    this.hasShareLink = false,
    this.scheduleId,
    this.occurrenceDate,
    this.seriesCommitmentId,
    this.tripSeriesCommitmentId,
    this.series,
  });

  /// The driver's recurring schedule this trip is a day of, and that day.
  final int? scheduleId;
  final DateTime? occurrenceDate;

  /// The series commitment the seat (booking) or the day (trip) was made under.
  final int? seriesCommitmentId;

  /// The driver's series the trip itself was formed under.
  final int? tripSeriesCommitmentId;

  /// The recurrence behind it — what the repeat badge and "whole series" read.
  final SeriesInfo? series;

  final int id;
  final int tripId;
  final int riderId;
  final int seats;

  /// The four digits the rider reads to the driver at pickup. Sent only while
  /// it is useful: the seat is committed and the rider is not aboard yet.
  final String? boardingCode;

  /// The rider has a live "follow my trip" link out.
  final bool hasShareLink;

  /// Mirrors the server's `BookingStatus`: 1 Pending · 2 Confirmed ·
  /// 3 InProgress · 4 Completed · 5 Cancelled · 6 Arrived · 7 NoShow.
  final int status;
  final String originAddress;
  final String destinationAddress;
  final DateTime? departAt;

  /// Where the trip carrying this seat has got to — see [Trip.status]. This is
  /// what the live-trip rail advances on; the booking's own status only tells
  /// the rider whether they still hold the seat.
  final int tripStatus;

  /// The driver's number, for the call button. The server sends it only on a
  /// seat that is both live and committed, so a finished trip — or one still
  /// waiting on the trip's own seat threshold — leaves this null.
  final String? driverPhone;

  /// What the seat costs, per seat. Display-only: there are no payments.
  ///
  /// On a seat that came from a claimed posting this is the figure the driver
  /// named. The rider never agreed to it in advance — they answer it by staying
  /// or by leaving, which costs them nothing.
  final double? pricePerSeat;

  /// The trip's seat threshold and what it holds, so a held seat can say
  /// "confirms at 3 of 4" without a second call.
  final int minSeatsToConfirm;
  final int seatsHeld;

  /// The seat is held but nobody is committed: the trip has not reached the
  /// seats its driver asked for. The only kind of pending seat there is.
  bool get isPending => status == 1;

  static const _statusKeys = {
    1: 'bookingStatus.pending',
    2: 'bookingStatus.confirmed',
    3: 'bookingStatus.inProgress',
    4: 'bookingStatus.completed',
    5: 'bookingStatus.cancelled',
    6: 'bookingStatus.arrived',
    7: 'bookingStatus.noShow',
  };

  /// The seat is still held — the mirror of the server's
  /// `BookingStatusRules.IsLive`. This gates the phone numbers and the driver's
  /// live position, so a status missing from here silently hides them.
  static bool isLiveStatus(int status) => status == 1 || status == 2 || status == 3 || status == 6;

  /// l10n key for a booking status, shared with [TripBooking] so the rider and
  /// the driver read the same seat the same way.
  static String statusKeyFor(int status) => _statusKeys[status] ?? 'bookingStatus.confirmed';

  /// l10n key for [status] — resolve with `context.tr(booking.statusKey)`.
  String get statusKey => statusKeyFor(status);
  String get statusLabel => AppLocalizations.current.t(statusKey);

  bool get isCancelled => status == 5;
  bool get isCompleted => status == 4;

  /// The driver waited and the rider never boarded. Terminal, and not the same
  /// thing as the rider cancelling.
  bool get isNoShow => status == 7;

  /// The rider is aboard — the driver has picked them up.
  bool get isInProgress => status == 3;

  /// The driver is at this rider's pickup, waiting for them.
  bool get isDriverArrived => status == 6;

  /// The seat is still held: neither given back, missed, nor finished.
  bool get isLive => isLiveStatus(status);

  /// Still to happen. A trip already under way counts — it has not finished, so
  /// it belongs with the rider's live seats rather than their history, even
  /// though its departure time is now in the past.
  bool get isUpcoming => isLive &&
      (isInProgress || isDriverArrived || (departAt?.isAfter(DateTime.now()) ?? false));

  /// The rider can still give the seat back — the server allows a cancel for
  /// anything that is neither already cancelled nor completed.
  bool get isCancellable => isLive;

  /// Booking reference — "WNS-" plus the booking id in base-36, which is what
  /// the rider quotes to the driver. Derived, not invented.
  String get reference =>
      'WNS-${id.toRadixString(36).toUpperCase().padLeft(4, '0')}';

  factory Booking.fromJson(Map<String, dynamic> j) => Booking(
        id: j['id'] as int,
        tripId: j['tripId'] as int? ?? 0,
        riderId: j['riderId'] as int? ?? 0,
        seats: j['seats'] as int? ?? 1,
        status: j['status'] as int? ?? 1,
        originAddress: j['originAddress'] as String? ?? '',
        destinationAddress: j['destinationAddress'] as String? ?? '',
        departAt: parseServerDate(j['departAt'] as String?),
        tripStatus: j['tripStatus'] as int? ?? 1,
        pricePerSeat: (j['pricePerSeat'] as num?)?.toDouble(),
        minSeatsToConfirm: j['minSeatsToConfirm'] as int? ?? 1,
        seatsHeld: j['seatsHeld'] as int? ?? 0,
        driverPhone: (j['driverPhone'] as String?)?.trim(),
        boardingCode: (j['boardingCode'] as String?)?.trim(),
        hasShareLink: j['hasShareLink'] as bool? ?? false,
        scheduleId: (j['scheduleId'] as num?)?.toInt(),
        occurrenceDate: parseDateOnly(j['occurrenceDate'] as String?),
        seriesCommitmentId: (j['seriesCommitmentId'] as num?)?.toInt(),
        tripSeriesCommitmentId: (j['tripSeriesCommitmentId'] as num?)?.toInt(),
        series: SeriesInfo.tryParse(j['series']),
      );
}

/// Where the driver last reported themselves (`GET Trips/{id}/driver-location`).
/// The server sends this only to the trip's own driver or a rider holding a
/// live seat, and returns no data at all until the driver's app has reported.
class DriverLocation {
  const DriverLocation({
    required this.lat,
    required this.lng,
    this.reportedAt,
    this.online = false,
  });

  final double lat;
  final double lng;
  final DateTime? reportedAt;
  final bool online;

  /// A position with no coordinates is not a position — the server should never
  /// send one, but a 0,0 marker in the Gulf of Guinea is worse than none.
  bool get isUsable => lat != 0 || lng != 0;

  /// How stale the fix is. Used to decide whether it is still worth drawing.
  Duration? get age =>
      reportedAt == null ? null : DateTime.now().toUtc().difference(reportedAt!.toUtc());

  factory DriverLocation.fromJson(Map<String, dynamic> j) => DriverLocation(
        lat: (j['lat'] as num?)?.toDouble() ?? 0,
        lng: (j['lng'] as num?)?.toDouble() ?? 0,
        reportedAt: parseServerDate(j['reportedAt'] as String?),
        online: j['online'] as bool? ?? false,
      );
}

/// One rider's seat on a trip, as the driver sees it (`GET Trips/{id}/bookings`).
/// The mirror of [Booking.driverPhone]: both parties to a live booking can
/// reach each other, and the number disappears once the seat is finished.
class TripBooking {
  TripBooking({
    required this.id,
    required this.riderId,
    required this.riderName,
    required this.seats,
    required this.status,
    this.riderRating = 0,
    this.riderPhone,
    this.bookedAt,
  });

  final int id;
  final int riderId;
  final String riderName;
  final double riderRating;
  final String? riderPhone;
  final int seats;

  /// Same scale as [Booking.status] — 1 Pending · 2 Confirmed · 3 InProgress ·
  /// 4 Completed · 5 Cancelled · 6 Arrived · 7 NoShow. This is the seat the
  /// driver moves along as they reach, collect and drop off this rider, and
  /// what the trip's own status is derived from.
  final int status;
  final DateTime? bookedAt;

  String get statusKey => Booking.statusKeyFor(status);
  bool get isCancelled => status == 5;
  bool get isCompleted => status == 4;
  bool get isNoShow => status == 7;

  /// The driver is at this rider's pickup, waiting for them.
  bool get isDriverArrived => status == 6;

  /// This rider is aboard.
  bool get isInProgress => status == 3;

  bool get isLive => Booking.isLiveStatus(status);

  /// The same reference the rider quotes — derived identically, so the two
  /// sides of the booking are talking about the same code.
  String get reference => 'WNS-${id.toRadixString(36).toUpperCase().padLeft(4, '0')}';

  factory TripBooking.fromJson(Map<String, dynamic> j) => TripBooking(
        id: j['id'] as int,
        riderId: j['riderId'] as int? ?? 0,
        riderName: (j['riderName'] as String?)?.trim().isNotEmpty == true
            ? (j['riderName'] as String).trim()
            : 'Rider',
        riderRating: (j['riderRating'] as num?)?.toDouble() ?? 0,
        riderPhone: (j['riderPhone'] as String?)?.trim(),
        seats: j['seats'] as int? ?? 1,
        status: j['status'] as int? ?? 1,
        bookedAt: parseServerDate(j['bookedAt'] as String?),
      );
}

/// Which band a match came back in (`Wanes.Areas.Services.Search.SearchTier`).
///
/// A band, not a score: a trip that merely passes the rider's way is as
/// bookable as one going door to door, it just asks the driver to stop
/// somewhere they had not planned. The home list renders these as sections in
/// this order, which the server's ranking already guarantees.
enum SearchTier {
  direct(1, 'search.tierDirect'),
  onTheWay(2, 'search.tierOnTheWay');

  const SearchTier(this.value, this.labelKey);

  final int value;
  final String labelKey;

  String get label => AppLocalizations.current.t(labelKey);

  static SearchTier fromValue(int? v) =>
      SearchTier.values.firstWhere((t) => t.value == v, orElse: () => SearchTier.direct);
}

/// One matched trip, and what matching it costs the rider.
class SearchMatch {
  SearchMatch({
    required this.tier,
    required this.trip,
    this.pickupWalkKm = 0,
    this.dropoffWalkKm = 0,
    this.pickupLat,
    this.pickupLng,
    this.dropoffLat,
    this.dropoffLng,
  });

  final SearchTier tier;
  final Trip trip;

  /// How far the rider walks at each end. On a corridor match these are
  /// measured to the points on the route the driver will actually pass — which
  /// is the whole difference between the two bands, and the number the rider is
  /// really choosing on.
  final double pickupWalkKm;
  final double dropoffWalkKm;

  /// Where on the route the driver would stop, on a corridor match. Null on a
  /// direct one, where the trip's own ends already say it.
  final double? pickupLat;
  final double? pickupLng;
  final double? dropoffLat;
  final double? dropoffLng;

  bool get isOnTheWay => tier == SearchTier.onTheWay;

  factory SearchMatch.fromJson(Map<String, dynamic> j) => SearchMatch(
        tier: SearchTier.fromValue(j['tier'] as int?),
        trip: Trip.fromJson(j['trip'] as Map<String, dynamic>? ?? const {}),
        pickupWalkKm: (j['pickupWalkKm'] as num?)?.toDouble() ?? 0,
        dropoffWalkKm: (j['dropoffWalkKm'] as num?)?.toDouble() ?? 0,
        pickupLat: (j['pickupLat'] as num?)?.toDouble(),
        pickupLng: (j['pickupLng'] as num?)?.toDouble(),
        dropoffLat: (j['dropoffLat'] as num?)?.toDouble(),
        dropoffLng: (j['dropoffLng'] as num?)?.toDouble(),
      );
}

/// How the rider wants carpool matches ordered
/// (`Wanes.Areas.Services.Search.SearchSort`).
///
/// The wire values are the server's, so this list must keep its order. [best]
/// is the server's combined-proximity ranking — picking it means "leave the
/// results as they came".
enum TripSort {
  best(1),
  departure(2),
  price(3),
  rating(4),
  pickup(5),
  seats(6);

  const TripSort(this.wire);
  final int wire;

  /// The i18n key for this option's label, e.g. `sort.price`.
  String get labelKey => 'sort.$name';

  static TripSort fromWire(int? value) =>
      TripSort.values.firstWhere((s) => s.wire == value, orElse: () => TripSort.best);
}

/// Everything a search can offer, in one response.
///
/// Not a mode. Search used to answer either "here are trips" or "we opened a
/// hail for you", and the second was a consolation prize dressed as a result.
/// Now every search comes back with the same four things: trips going the
/// rider's way, trips passing it, postings they could join, and — always — the
/// earliest they could post one themselves.
class SearchResult {
  SearchResult({
    required this.earliestDepartAt,
    this.matches = const [],
    this.requests = const [],
  });

  final List<SearchMatch> matches;

  /// Open ride requests on the same route the rider could join instead of creating a
  /// near-identical one.
  final List<RiderTrip> requests;

  /// The earliest departure this rider could post for, given the seats and
  /// distance they just searched. Sent on every search, not only an empty one:
  /// the form needs it while they are still choosing a time.
  final DateTime earliestDepartAt;

  bool get isEmpty => matches.isEmpty && requests.isEmpty;

  /// The trips only, for the screens that do not care which band they came from.
  List<Trip> get trips => matches.map((m) => m.trip).toList();

  factory SearchResult.fromJson(Map<String, dynamic> j) => SearchResult(
        matches: (j['matches'] as List<dynamic>? ?? [])
            .map((e) => SearchMatch.fromJson(e as Map<String, dynamic>))
            .toList(),
        requests: (j['requests'] as List<dynamic>? ?? [])
            .map((e) => RiderTrip.fromJson(e as Map<String, dynamic>))
            .toList(),
        earliestDepartAt: parseServerDate(j['earliestDepartAt'] as String?) ??
            DateTime.now().add(const Duration(minutes: 15)),
      );
}

/// How a saved place is labelled (`Wanes.Shareds.Enums.SavedPlaceLabel`).
/// Home and Work are the two singular shortcuts; everything else is a
/// favourite the rider named themselves.
enum SavedPlaceLabel {
  home(1, 'places.home'),
  work(2, 'places.work'),
  custom(3, 'places.favourite');

  const SavedPlaceLabel(this.value, this.labelKey);
  final int value;

  /// l10n key — resolve with `context.tr(label.labelKey)`.
  final String labelKey;

  String get label => AppLocalizations.current.t(labelKey);

  static SavedPlaceLabel fromValue(int? v) => SavedPlaceLabel.values
      .firstWhere((l) => l.value == v, orElse: () => SavedPlaceLabel.custom);
}

/// A rider's Home / Work / favourite place (`api/v1/me/places`). Unlike a
/// recent pick this lives on the server, so it follows the account across
/// devices.
class SavedPlace {
  SavedPlace({
    required this.id,
    required this.label,
    required this.name,
    required this.address,
    required this.lat,
    required this.lng,
  });

  final int id;
  final SavedPlaceLabel label;

  /// What the rider calls it — "Home", "Work", "Mum's".
  final String name;

  /// The geocoded one-line address behind the name.
  final String address;

  final double lat;
  final double lng;

  /// The searchable form the pickers and the search call work with.
  Place get place =>
      Place(name, lat, lng, address: address.isEmpty ? null : address);

  factory SavedPlace.fromJson(Map<String, dynamic> j) => SavedPlace(
        id: j['id'] as int,
        label: SavedPlaceLabel.fromValue(j['label'] as int?),
        name: j['name'] as String? ?? '',
        address: j['address'] as String? ?? '',
        lat: (j['lat'] as num?)?.toDouble() ?? 0,
        lng: (j['lng'] as num?)?.toDouble() ?? 0,
      );
}

/// The notification kinds the API can send (`Wanes.Shareds.Enums.NotificationType`).
///
/// Each carries the icon/accent the inbox draws it with, so a new backend type
/// only needs one line here.
enum NotificationKind {
  riderTripNearby('RiderTripNearby'),
  bookingConfirmed('BookingConfirmed'),
  tripCancelled('TripCancelled'),
  driverAccepted('DriverAccepted'),
  tripCompleted('TripCompleted'),
  bookingCancelled('BookingCancelled'),
  tripStarted('TripStarted'),
  /// The driver reached the pickup point (`NotificationType.DriverArrived`).
  driverArrived('DriverArrived'),
  tripMatched('TripMatched'),
  driverVerified('DriverVerified'),
  driverRejected('DriverRejected'),
  ratingReceived('RatingReceived'),

  /// The support desk answered a complaint or suggestion
  /// (`NotificationType.FeedbackReplied`).
  feedbackReplied('FeedbackReplied'),

  /// A trip reached the seats its driver asked for, so every held seat on it is
  /// now committed (`NotificationType.TripConfirmed`).
  tripConfirmed('TripConfirmed'),

  /// A trip was called off for want of riders (`NotificationType.TripNotEnoughRiders`).
  tripNotEnoughRiders('TripNotEnoughRiders'),

  /// The driver has to say whether a trip short of its threshold still runs
  /// (`NotificationType.ConfirmDecision`).
  confirmDecision('ConfirmDecision'),

  /// Something moved on a ride request — a rider joined, a driver offered
  /// (`NotificationType.RideRequest`).
  rideRequest('RideRequest'),

  /// A driver's route alert or request watch reached their seat count.
  demandAlert('DemandAlert'),

  /// The user's reliability record changed — a warning, a pause.
  reliability('Reliability'),

  /// A safety report, for admins.
  safetyIncident('SafetyIncident'),

  /// A series moved — an offer, an acceptance, a skipped day, the week ahead.
  series('Series'),

  general('General');

  const NotificationKind(this.wire);

  /// The `Type` string the API serialises.
  final String wire;

  static NotificationKind fromWire(String? value) => NotificationKind.values
      .firstWhere((k) => k.wire == value, orElse: () => NotificationKind.general);
}

/// One row in the notification inbox (`GET /Notifications/mine`).
class AppNotification {
  AppNotification({
    required this.id,
    required this.kind,
    required this.title,
    required this.body,
    required this.isRead,
    this.titleAr,
    this.bodyAr,
    this.data,
    this.createdAt,
  });

  final int id;
  final NotificationKind kind;

  /// Default wording as stored — English for anything the system sent.
  final String title;
  final String body;

  /// Arabic wording, when the sender had one. Null for notifications an admin
  /// typed in one language only, and for rows written before this existed.
  final String? titleAr;
  final String? bodyAr;

  final bool isRead;

  /// The title to show a reader of [languageCode], falling back to [title].
  String titleFor(String languageCode) =>
      languageCode == 'ar' && (titleAr?.isNotEmpty ?? false) ? titleAr! : title;

  /// The body to show a reader of [languageCode], falling back to [body].
  String bodyFor(String languageCode) =>
      languageCode == 'ar' && (bodyAr?.isNotEmpty ?? false) ? bodyAr! : body;

  /// Whether an Arabic reader would be shown Arabic text. False means the row
  /// only ever had one language, so the inbox falls back to the type label.
  bool hasArabic(String languageCode) =>
      languageCode != 'ar' || (titleAr?.isNotEmpty ?? false);

  /// Decoded `data` payload — `tripId`, `bookingId`, `requestId` as sent.
  final Map<String, dynamic>? data;

  final DateTime? createdAt;

  int? get tripId => _int('tripId');
  int? get bookingId => _int('bookingId');
  int? get requestId => _int('requestId');

  /// The demand the notification is about (`rideRequestId` in the payload).
  int? get rideRequestId => _int('rideRequestId');

  /// The series commitment a notification is about (`seriesId`).
  int? get seriesId => _int('seriesId');

  /// The recurring schedule it names (`scheduleId`).
  int? get scheduleId => _int('scheduleId');

  /// Present only on "a driver offered" — the request is comparing offers.
  bool get carriesOfferWindow => data?['decideAt'] != null;
  int? get feedbackId => _int('feedbackId');

  int? _int(String key) {
    final value = data?[key];
    return value is int ? value : int.tryParse('$value');
  }

  AppNotification copyWith({bool? isRead}) => AppNotification(
        id: id,
        kind: kind,
        title: title,
        body: body,
        titleAr: titleAr,
        bodyAr: bodyAr,
        isRead: isRead ?? this.isRead,
        data: data,
        createdAt: createdAt,
      );

  factory AppNotification.fromJson(Map<String, dynamic> j) => AppNotification(
        id: j['id'] as int? ?? 0,
        kind: NotificationKind.fromWire(j['type'] as String?),
        title: j['title'] as String? ?? '',
        body: j['body'] as String? ?? '',
        titleAr: j['titleAr'] as String?,
        bodyAr: j['bodyAr'] as String?,
        isRead: j['isRead'] as bool? ?? false,
        // The API stores the payload as a JSON *string*; SSE and FCM deliver the
        // same shape, so both paths land on decodeNotificationData.
        data: decodeNotificationData(j['data']),
        createdAt: parseServerDate(j['creationDate'] as String?),
      );
}

/// The `data` field arrives as a JSON string over REST/FCM and as a real object
/// over SSE. Accept either, and never throw on a malformed payload.
Map<String, dynamic>? decodeNotificationData(Object? raw) {
  if (raw == null) return null;
  if (raw is Map<String, dynamic>) return raw;
  if (raw is Map) return raw.map((k, v) => MapEntry('$k', v));
  if (raw is String) {
    if (raw.isEmpty) return null;
    try {
      final decoded = jsonDecode(raw);
      if (decoded is Map) return decoded.map((k, v) => MapEntry('$k', v));
    } catch (_) {
      // hand-edited payload from the admin console — ignore it
    }
  }
  return null;
}

/// Inbox page + badge count (`GET /Notifications/mine`).
class NotificationFeed {
  const NotificationFeed({required this.items, required this.unreadCount});

  final List<AppNotification> items;
  final int unreadCount;

  static const empty = NotificationFeed(items: [], unreadCount: 0);

  factory NotificationFeed.fromJson(Map<String, dynamic> j) => NotificationFeed(
        items: (j['items'] as List? ?? [])
            .map((e) => AppNotification.fromJson(e as Map<String, dynamic>))
            .toList(),
        unreadCount: j['unreadCount'] as int? ?? 0,
      );
}

/// Section a FAQ entry is filed under. Mirrors the backend `FaqCategory`; the
/// help screen renders one group per category, in this order.
enum FaqCategory {
  general(1, 'faq.categoryGeneral'),
  riding(2, 'faq.categoryRiding'),
  driving(3, 'faq.categoryDriving'),
  account(4, 'faq.categoryAccount'),
  safety(5, 'faq.categorySafety');

  const FaqCategory(this.value, this.labelKey);
  final int value;

  /// l10n key — resolve with `context.tr(category.labelKey)`.
  final String labelKey;

  String get label => AppLocalizations.current.t(labelKey);

  static FaqCategory fromValue(int? v) => FaqCategory.values
      .firstWhere((c) => c.value == v, orElse: () => FaqCategory.general);
}

/// One published help entry (`GET /faq`).
///
/// The server sends both languages on every entry rather than picking one, so
/// switching language on the help screen needs no round trip — same as the
/// bundled string tables everywhere else in the app.
class FaqItem {
  const FaqItem({
    required this.id,
    required this.category,
    required this.questionEn,
    required this.questionAr,
    required this.answerEn,
    required this.answerAr,
  });

  final int id;
  final FaqCategory category;
  final String questionEn;
  final String questionAr;
  final String answerEn;
  final String answerAr;

  /// The question in the language on screen, falling back to the other one so a
  /// half-translated entry still reads as something rather than a blank row.
  String get question => _pick(questionAr, questionEn);

  String get answer => _pick(answerAr, answerEn);

  static String _pick(String ar, String en) {
    final wantsArabic = AppLocalizations.current.isArabic;
    final preferred = wantsArabic ? ar : en;
    if (preferred.trim().isNotEmpty) return preferred;
    return wantsArabic ? en : ar;
  }

  factory FaqItem.fromJson(Map<String, dynamic> j) => FaqItem(
        id: j['id'] as int,
        category: FaqCategory.fromValue(j['category'] as int?),
        questionEn: j['questionEn'] as String? ?? '',
        questionAr: j['questionAr'] as String? ?? '',
        answerEn: j['answerEn'] as String? ?? '',
        answerAr: j['answerAr'] as String? ?? '',
      );
}

/// The published FAQ as one response (`GET /faq`).
class Faq {
  const Faq({required this.items});

  final List<FaqItem> items;

  static const empty = Faq(items: []);

  /// The entries grouped by category, in enum order, skipping empty groups.
  /// The server already sorts within a category, so insertion order is kept.
  Map<FaqCategory, List<FaqItem>> get byCategory {
    final grouped = <FaqCategory, List<FaqItem>>{};
    for (final category in FaqCategory.values) {
      final group = items.where((i) => i.category == category).toList();
      if (group.isNotEmpty) grouped[category] = group;
    }
    return grouped;
  }

  factory Faq.fromJson(Map<String, dynamic> j) => Faq(
        items: (j['items'] as List? ?? [])
            .map((e) => FaqItem.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

/// Complaint or suggestion (`Wanes.Shareds.Enums.FeedbackKind`).
enum FeedbackKind {
  complaint(1, 'feedback.kindComplaint'),
  suggestion(2, 'feedback.kindSuggestion');

  const FeedbackKind(this.value, this.labelKey);
  final int value;

  /// l10n key — resolve with `context.tr(kind.labelKey)`.
  final String labelKey;

  String get label => AppLocalizations.current.t(labelKey);

  static FeedbackKind fromValue(int? v) => FeedbackKind.values
      .firstWhere((k) => k.value == v, orElse: () => FeedbackKind.complaint);
}

/// Where a submission stands with the support desk
/// (`Wanes.Shareds.Enums.FeedbackStatus`).
enum FeedbackStatus {
  isNew(1, 'feedback.statusNew'),
  inReview(2, 'feedback.statusInReview'),
  resolved(3, 'feedback.statusResolved'),
  dismissed(4, 'feedback.statusDismissed');

  const FeedbackStatus(this.value, this.labelKey);
  final int value;
  final String labelKey;

  String get label => AppLocalizations.current.t(labelKey);

  /// Still with the desk — the user may yet hear back on it.
  bool get isOpen => this == FeedbackStatus.isNew || this == FeedbackStatus.inReview;

  static FeedbackStatus fromValue(int? v) => FeedbackStatus.values
      .firstWhere((s) => s.value == v, orElse: () => FeedbackStatus.isNew);
}

/// One complaint or suggestion the user sent, with the desk's answer if it has
/// arrived (`GET /feedback`).
///
/// No language pair, unlike [FaqItem]: this is one person's own words and one
/// person's answer to them, so there is nothing to switch between — the server
/// records which language the submission was written in and the desk replies in
/// it.
class FeedbackEntry {
  const FeedbackEntry({
    required this.id,
    required this.kind,
    required this.status,
    required this.subject,
    required this.message,
    this.tripId,
    this.reply,
    this.repliedAt,
    this.createdAt,
  });

  final int id;
  final FeedbackKind kind;
  final FeedbackStatus status;
  final String subject;
  final String message;
  final int? tripId;

  /// The desk's answer, as written. Null until someone replies.
  final String? reply;
  final DateTime? repliedAt;
  final DateTime? createdAt;

  bool get hasReply => (reply?.trim().isNotEmpty ?? false);

  factory FeedbackEntry.fromJson(Map<String, dynamic> j) => FeedbackEntry(
        id: j['id'] as int? ?? 0,
        kind: FeedbackKind.fromValue(j['kind'] as int?),
        status: FeedbackStatus.fromValue(j['status'] as int?),
        subject: j['subject'] as String? ?? '',
        message: j['message'] as String? ?? '',
        tripId: j['tripId'] as int?,
        reply: j['reply'] as String?,
        repliedAt: parseServerDate(j['repliedAt'] as String?),
        createdAt: parseServerDate(j['creationDate'] as String?),
      );
}

/// The kinds of paperwork the platform asks a driver for.
///
/// Mirrors `Shareds/Enums/DriverDocumentType.cs`. [required] marks the ones an
/// application cannot be submitted without — the server decides that too and
/// says so in `missingTypes`; this is only so the screen can label the slots
/// before anything has been uploaded.
enum DriverDocumentType {
  licenseFront(1, 'driver.docLicenseFront', required: true),
  licenseBack(2, 'driver.docLicenseBack', required: true),
  idDocument(3, 'driver.docId', required: true),
  vehicleRegistration(4, 'driver.docRegistration'),
  insurance(5, 'driver.docInsurance');

  const DriverDocumentType(this.value, this.labelKey, {this.required = false});

  final int value;
  final String labelKey;
  final bool required;

  String get label => AppLocalizations.current.t(labelKey);

  static DriverDocumentType fromValue(int? v) => DriverDocumentType.values
      .firstWhere((t) => t.value == v, orElse: () => DriverDocumentType.idDocument);
}

/// One uploaded document. Carries no URL — the bytes come from an authorized
/// endpoint keyed on [id].
class DriverDocument {
  const DriverDocument({
    required this.id,
    required this.type,
    this.fileName = '',
    this.contentType = '',
    this.sizeBytes = 0,
    this.uploadedAt,
    this.isPdf = false,
  });

  final int id;
  final DriverDocumentType type;
  final String fileName;
  final String contentType;
  final int sizeBytes;
  final DateTime? uploadedAt;
  final bool isPdf;

  factory DriverDocument.fromJson(Map<String, dynamic> j) => DriverDocument(
        id: j['id'] as int? ?? 0,
        type: DriverDocumentType.fromValue(j['type'] as int?),
        fileName: j['fileName'] as String? ?? '',
        contentType: j['contentType'] as String? ?? '',
        sizeBytes: (j['sizeBytes'] as num?)?.toInt() ?? 0,
        uploadedAt: parseServerDate(j['uploadedAt'] as String?),
        isPdf: j['isPdf'] as bool? ?? false,
      );
}

/// Where a driver's application stands, with everything the screen needs to
/// show it: what is uploaded, what is still missing, and — after a rejection —
/// what the reviewer said about it.
class DriverVerification {
  const DriverVerification({
    this.status = 0,
    this.licenseNumber,
    this.appliedAt,
    this.reviewNote,
    this.reviewedAt,
    this.documents = const [],
    this.missingTypes = const [],
    this.canSubmit = false,
  });

  /// Matches `DriverStatus`: 0 none, 1 pending, 2 verified, 3 rejected, 4 suspended.
  final int status;
  final String? licenseNumber;
  final DateTime? appliedAt;
  final String? reviewNote;
  final DateTime? reviewedAt;
  final List<DriverDocument> documents;
  final List<DriverDocumentType> missingTypes;

  /// Whether the required set is complete. Computed by the server, so the app
  /// can never offer a submit the API would refuse.
  final bool canSubmit;

  bool get isPending => status == 1;
  bool get isVerified => status == 2;
  bool get isRejected => status == 3;

  DriverDocument? documentOf(DriverDocumentType type) {
    for (final d in documents) {
      if (d.type == type) return d;
    }
    return null;
  }

  factory DriverVerification.fromJson(Map<String, dynamic> j) => DriverVerification(
        status: j['status'] as int? ?? 0,
        licenseNumber: j['licenseNumber'] as String?,
        appliedAt: parseServerDate(j['appliedAt'] as String?),
        reviewNote: j['reviewNote'] as String?,
        reviewedAt: parseServerDate(j['reviewedAt'] as String?),
        documents: (j['documents'] as List? ?? [])
            .map((e) => DriverDocument.fromJson(e as Map<String, dynamic>))
            .toList(),
        missingTypes: (j['missingTypes'] as List? ?? [])
            .map((e) => DriverDocumentType.fromValue(e as int?))
            .toList(),
        canSubmit: j['canSubmit'] as bool? ?? false,
      );
}

/// The departures a driver cannot take, as the server sees them.
///
/// One driver drives one car, so two promises the same distance apart as the
/// matching window are the same promise twice. The window travels with the list
/// rather than being restated in Dart: it is a platform rule, and a client
/// carrying its own copy is a client that will offer a slot the API refuses.
class DriverAvailability {
  const DriverAvailability({
    this.committedDepartures = const [],
    this.clashWindow = Duration.zero,
    this.isEngaged = false,
  });

  /// Departures already promised, in the device's own time.
  final List<DateTime> committedDepartures;
  final Duration clashWindow;

  /// Out on a trip right now — no departure is available until they finish.
  final bool isEngaged;

  factory DriverAvailability.fromJson(Map<String, dynamic> j) => DriverAvailability(
        committedDepartures: (j['committedDepartures'] as List? ?? [])
            .map((e) => parseServerDate(e as String?)?.toLocal())
            .whereType<DateTime>()
            .toList(),
        clashWindow: Duration(minutes: (j['clashWindowMinutes'] as num?)?.toInt() ?? 0),
        isEngaged: j['isEngaged'] as bool? ?? false,
      );
}

/// What the rider's own diary blocks off.
///
/// The mirror of [DriverAvailability], and the same shape on purpose: one rider
/// rides in one car, so a seat they already hold rules out another leaving at
/// about the same moment. Search asks for this before the time picker opens, so
/// the slots a booking would be refused at are greyed out rather than found out
/// about at the tap that mattered.
class RiderAvailability {
  const RiderAvailability({
    this.committedDepartures = const [],
    this.clashWindow = Duration.zero,
    this.isEngaged = false,
  });

  /// Departures the rider already holds a seat at, in the device's own time.
  final List<DateTime> committedDepartures;

  /// How close to one of those another departure may not be. Carried from the
  /// API rather than restated here: a client with its own copy of the number is
  /// a client that will one day offer a slot the API refuses.
  final Duration clashWindow;

  /// On a ride right now, so nothing at all is open until it ends.
  final bool isEngaged;

  factory RiderAvailability.fromJson(Map<String, dynamic> j) => RiderAvailability(
        committedDepartures: (j['committedDepartures'] as List? ?? [])
            .map((e) => parseServerDate(e as String?)?.toLocal())
            .whereType<DateTime>()
            .toList(),
        clashWindow: Duration(minutes: (j['clashWindowMinutes'] as num?)?.toInt() ?? 0),
        isEngaged: j['isEngaged'] as bool? ?? false,
      );
}
