// DTOs mirroring the backend responses (camelCase JSON).

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
      };
}

class AuthResult {
  AuthResult({required this.token, required this.isNewUser, required this.profile});
  final String token;
  final bool isNewUser;
  final Profile profile;

  factory AuthResult.fromJson(Map<String, dynamic> j) => AuthResult(
        token: j['token'] as String,
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
  final int status; // 1 Posted · 2 Full · 3 Active · 4 Completed · 5 Cancelled
  final double? pricePerSeat;

  static const _statusKeys = {
    1: 'tripStatus.posted',
    2: 'tripStatus.full',
    3: 'tripStatus.active',
    4: 'tripStatus.completed',
    5: 'tripStatus.cancelled',
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
    this.departAt,
  });

  final int id;
  final int tripId;
  final int seats;

  /// 1 Confirmed · 2 Cancelled · 3 Completed
  final int status;
  final String originAddress;
  final String destinationAddress;
  final DateTime? departAt;

  bool get isCancelled => status == 2;
  bool get isCompleted => status == 3;

  /// Still to happen: not cancelled, not completed, and in the future.
  bool get isUpcoming =>
      !isCancelled && !isCompleted && (departAt?.isAfter(DateTime.now()) ?? false);

  factory Booking.fromJson(Map<String, dynamic> j) => Booking(
        id: j['id'] as int,
        tripId: j['tripId'] as int? ?? 0,
        seats: j['seats'] as int? ?? 1,
        status: j['status'] as int? ?? 1,
        originAddress: j['originAddress'] as String? ?? '',
        destinationAddress: j['destinationAddress'] as String? ?? '',
        departAt: parseServerDate(j['departAt'] as String?),
      );
}

enum SearchMode { carpool, hail }

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
