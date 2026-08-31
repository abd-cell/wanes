// DTOs mirroring the backend responses (camelCase JSON).

import 'dart:convert';

import '../core/l10n.dart';
import '../core/places.dart';

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

/// Platform settings the admin owns from the CMS — currency, brand colour and
/// the support contact channels behind the Contact us screen.
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
  });

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
      );

  Map<String, dynamic> toJson() => {
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
    this.status = 1,
    this.pricePerSeat,
    this.startedAt,
  });

  final int id;
  final String driverName;
  final double driverRating;
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
  // 1 Posted · 2 Full · 3 Active · 4 Completed · 5 Cancelled · 6 Arrived
  final int status;
  final double? pricePerSeat;

  /// When the driver actually started. Sent only by the single-trip GET, and
  /// null until the trip is under way — the live map advances the car against
  /// this rather than against an assumed departure.
  final DateTime? startedAt;

  static const _statusKeys = {
    1: 'tripStatus.posted',
    2: 'tripStatus.full',
    3: 'tripStatus.active',
    4: 'tripStatus.completed',
    5: 'tripStatus.cancelled',
    6: 'tripStatus.arrived',
  };

  /// l10n key for [status] — resolve with `context.tr(trip.statusKey)`.
  String get statusKey => _statusKeys[status] ?? 'tripStatus.posted';
  String get statusLabel => AppLocalizations.current.t(statusKey);

  /// A posted trip nobody has booked yet — the driver may still edit it.
  /// The server is the authority (it also checks cancelled bookings).
  bool get editable => status == 1 && seatsLeft == seatsTotal;

  factory Trip.fromJson(Map<String, dynamic> j) => Trip(
        id: j['id'] as int,
        driverName: j['driverName'] as String? ?? 'Driver',
        driverRating: (j['driverRating'] as num?)?.toDouble() ?? 0,
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
        pricePerSeat: (j['pricePerSeat'] as num?)?.toDouble(),
        startedAt: parseServerDate(j['startedAt'] as String?),
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

class RideRequestRow {
  RideRequestRow({
    required this.id,
    required this.originAddress,
    required this.destinationAddress,
    required this.seats,
    required this.requestedAt,
    this.riderId = 0,
    this.originLat = 0,
    this.originLng = 0,
    this.destinationLat = 0,
    this.destinationLng = 0,
  });

  final int id;
  final String originAddress;
  final String destinationAddress;
  final int seats;
  final DateTime requestedAt;
  final int riderId;
  final double originLat;
  final double originLng;
  final double destinationLat;
  final double destinationLng;

  /// A hail lives for 10 minutes server-side (SearchService.HailTtl); the
  /// driver-facing countdown mirrors that window.
  static const Duration ttl = Duration(minutes: 10);

  DateTime get expiresAt => requestedAt.add(ttl);

  factory RideRequestRow.fromJson(Map<String, dynamic> j) => RideRequestRow(
        id: j['id'] as int,
        originAddress: j['originAddress'] as String? ?? '',
        destinationAddress: j['destinationAddress'] as String? ?? '',
        seats: j['seats'] as int? ?? 1,
        requestedAt: parseServerDate(j['requestedAt'] as String?) ?? DateTime.now(),
        riderId: j['riderId'] as int? ?? 0,
        originLat: (j['originLat'] as num?)?.toDouble() ?? 0,
        originLng: (j['originLng'] as num?)?.toDouble() ?? 0,
        destinationLat: (j['destinationLat'] as num?)?.toDouble() ?? 0,
        destinationLng: (j['destinationLng'] as num?)?.toDouble() ?? 0,
      );
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
  });

  final int id;
  final int tripId;
  final int riderId;
  final int seats;

  /// Mirrors the server's `BookingStatus`:
  /// 1 Pending · 2 Confirmed · 3 InProgress · 4 Completed · 5 Cancelled.
  final int status;
  final String originAddress;
  final String destinationAddress;
  final DateTime? departAt;

  /// Where the trip carrying this seat has got to — see [Trip.status]. This is
  /// what the live-trip rail advances on; the booking's own status only tells
  /// the rider whether they still hold the seat.
  final int tripStatus;

  /// The driver's number, for the call button. The server sends it only while
  /// the booking is live, so a finished trip leaves this null.
  final String? driverPhone;

  static const _statusKeys = {
    1: 'bookingStatus.pending',
    2: 'bookingStatus.confirmed',
    3: 'bookingStatus.inProgress',
    4: 'bookingStatus.completed',
    5: 'bookingStatus.cancelled',
  };

  /// l10n key for a booking status, shared with [TripBooking] so the rider and
  /// the driver read the same seat the same way.
  static String statusKeyFor(int status) => _statusKeys[status] ?? 'bookingStatus.confirmed';

  /// l10n key for [status] — resolve with `context.tr(booking.statusKey)`.
  String get statusKey => statusKeyFor(status);
  String get statusLabel => AppLocalizations.current.t(statusKey);

  bool get isCancelled => status == 5;
  bool get isCompleted => status == 4;

  /// The rider is aboard — the driver has started the trip.
  bool get isInProgress => status == 3;

  /// The seat is still held: neither given back nor finished.
  bool get isLive => !isCancelled && !isCompleted;

  /// Still to happen. A trip already under way counts — it has not finished, so
  /// it belongs with the rider's live seats rather than their history, even
  /// though its departure time is now in the past.
  bool get isUpcoming =>
      isLive && (isInProgress || (departAt?.isAfter(DateTime.now()) ?? false));

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
        driverPhone: (j['driverPhone'] as String?)?.trim(),
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
  /// 4 Completed · 5 Cancelled.
  final int status;
  final DateTime? bookedAt;

  String get statusKey => Booking.statusKeyFor(status);
  bool get isCancelled => status == 5;
  bool get isCompleted => status == 4;
  bool get isLive => !isCancelled && !isCompleted;

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

enum SearchMode { carpool, hail }

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

class SearchResult {
  SearchResult({
    required this.mode,
    this.matches = const [],
    this.rideRequestId,
    this.driversNotified = 0,
  });

  final SearchMode mode;
  final List<Trip> matches;
  final int? rideRequestId;

  /// How many nearby drivers the hail actually pinged.
  final int driversNotified;

  factory SearchResult.fromJson(Map<String, dynamic> j) {
    final modeNum = j['mode'] as int? ?? 1;
    final list = (j['matches'] as List<dynamic>? ?? [])
        .map((e) => Trip.fromJson(e as Map<String, dynamic>))
        .toList();
    return SearchResult(
      mode: modeNum == 2 ? SearchMode.hail : SearchMode.carpool,
      matches: list,
      rideRequestId: j['rideRequestId'] as int?,
      driversNotified: j['driversNotified'] as int? ?? 0,
    );
  }
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
  rideRequestNearby('RideRequestNearby'),
  bookingConfirmed('BookingConfirmed'),
  tripCancelled('TripCancelled'),
  driverAccepted('DriverAccepted'),
  tripCompleted('TripCompleted'),
  bookingCancelled('BookingCancelled'),
  tripStarted('TripStarted'),
  tripMatched('TripMatched'),
  driverVerified('DriverVerified'),
  driverRejected('DriverRejected'),
  ratingReceived('RatingReceived'),
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
