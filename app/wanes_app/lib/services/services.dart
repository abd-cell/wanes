import '../core/api_client.dart';
import '../core/app_response.dart';
import '../core/places.dart';
import '../core/session.dart';
import '../models/models.dart';

/// Phone-OTP auth + profile.
class AuthService {
  final _api = ApiClient.instance;

  Future<AppResponse> requestOtp(String phone) =>
      _api.post('Accounts/request-otp', body: {'phone': phone});

  Future<AppResponse<AuthResult>> verifyOtp(String phone, String code) async {
    final res = await _api.post<AuthResult>(
      'Accounts/verify-otp',
      body: {'phone': phone, 'code': code, 'deviceType': 3},
      parse: (d) => AuthResult.fromJson(d as Map<String, dynamic>),
    );
    if (res.success && res.data != null) {
      await Session.instance.save(res.data!.token, res.data!.profile);
    }
    return res;
  }

  Future<AppResponse<Profile>> getProfile() async {
    final res = await _api.get<Profile>(
      'Accounts/me',
      parse: (d) => Profile.fromJson(d as Map<String, dynamic>),
    );
    if (res.success && res.data != null) await Session.instance.saveProfile(res.data!);
    return res;
  }

  /// Patches the signed-in user's profile. Only the fields passed are sent —
  /// the server treats a missing key as "leave it alone", so `null` here means
  /// "unchanged", not "clear it".
  Future<AppResponse<Profile>> updateProfile({
    String? firstName,
    String? lastName,
    String? displayName,
    String? email,
    Gender? gender,
    DateTime? dateOfBirth,
    String? bio,
  }) async {
    final body = <String, dynamic>{
      if (firstName != null) 'firstName': firstName,
      if (lastName != null) 'lastName': lastName,
      if (displayName != null) 'displayName': displayName,
      if (email != null) 'email': email,
      if (gender != null) 'gender': gender.value,
      if (dateOfBirth != null) 'dateOfBirth': formatDateOnly(dateOfBirth),
      if (bio != null) 'bio': bio,
    };
    final res = await _api.patch<Profile>(
      'Accounts/me',
      body: body,
      parse: (d) => Profile.fromJson(d as Map<String, dynamic>),
    );
    if (res.success && res.data != null) await Session.instance.saveProfile(res.data!);
    return res;
  }

  /// Switches the active role (1 = rider, 2 = driver).
  Future<AppResponse<Profile>> switchRole(int activeRole) async {
    final res = await _api.patch<Profile>(
      'Accounts/me/role',
      body: {'activeRole': activeRole},
      parse: (d) => Profile.fromJson(d as Map<String, dynamic>),
    );
    if (res.success && res.data != null) await Session.instance.saveProfile(res.data!);
    return res;
  }

  Future<void> logout() async {
    await _api.post('Accounts/logout');
    await Session.instance.clear();
  }
}

/// Rider search → carpool matches or an opened hail request.
class SearchService {
  final _api = ApiClient.instance;

  Future<AppResponse<SearchResult>> search({
    required double originLat,
    required double originLng,
    required String originAddress,
    required double destLat,
    required double destLng,
    required String destAddress,
    required DateTime when,
    int seats = 1,
  }) {
    return _api.post<SearchResult>(
      'Search',
      body: {
        'origin': {'lat': originLat, 'lng': originLng, 'address': originAddress},
        'destination': {'lat': destLat, 'lng': destLng, 'address': destAddress},
        'when': when.toUtc().toIso8601String(),
        'seats': seats,
      },
      parse: (d) => SearchResult.fromJson(d as Map<String, dynamic>),
    );
  }
}

/// Booking a seat on a matched trip.
class BookingService {
  final _api = ApiClient.instance;

  Future<AppResponse<Booking>> book(int tripId, {int seats = 1}) =>
      _api.post<Booking>(
        'Bookings',
        body: {'tripId': tripId, 'seats': seats},
        parse: (d) => Booking.fromJson(d as Map<String, dynamic>),
      );

  /// The rider's own bookings, newest first.
  Future<AppResponse<List<Booking>>> myBookings() => _api.get<List<Booking>>(
        'Bookings/mine',
        parse: (d) => (d as List)
            .map((e) => Booking.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

/// Two-way ratings (after a booking completes).
class RatingService {
  final _api = ApiClient.instance;

  Future<AppResponse> rate(int bookingId, int stars, {String? comment}) =>
      _api.post('Ratings', body: {
        'bookingId': bookingId,
        'stars': stars,
        'comment': comment,
      });
}

/// Driver onboarding + role switch.
class ProfileService {
  final _api = ApiClient.instance;

  Future<AppResponse> applyAsDriver({
    required String licenseNumber,
    String licensePhotoUrl = '',
    String idDocumentUrl = '',
  }) =>
      _api.post('me/driver/apply', body: {
        'licenseNumber': licenseNumber,
        'licensePhotoUrl': licensePhotoUrl,
        'idDocumentUrl': idDocumentUrl,
      });
}

/// Driver vehicles.
class VehicleService {
  final _api = ApiClient.instance;

  Future<AppResponse<List<Vehicle>>> myVehicles() => _api.get<List<Vehicle>>(
        'me/vehicles',
        parse: (d) => (d as List)
            .map((e) => Vehicle.fromJson(e as Map<String, dynamic>))
            .toList(),
      );

  Future<AppResponse> add({
    required String make,
    required String model,
    required String plate,
    required int seatCapacity,
  }) =>
      _api.post('me/vehicles', body: {
        'make': make, 'model': model, 'plate': plate,
        'seatCapacity': seatCapacity, 'isDefault': true,
      });
}

/// Driver trips.
class TripService {
  final _api = ApiClient.instance;

  Future<AppResponse> create({
    required int vehicleId,
    required double originLat,
    required double originLng,
    required String originAddress,
    required double destLat,
    required double destLng,
    required String destAddress,
    required DateTime departAt,
    required int seatsTotal,
    double? pricePerSeat,
  }) =>
      _api.post('Trips', body: {
        'vehicleId': vehicleId,
        'origin': {'lat': originLat, 'lng': originLng, 'address': originAddress},
        'destination': {'lat': destLat, 'lng': destLng, 'address': destAddress},
        'departAt': departAt.toUtc().toIso8601String(),
        'seatsTotal': seatsTotal,
        'pricePerSeat': pricePerSeat,
      });

  /// Edits a posted trip. The server rejects it once the trip has a booking.
  Future<AppResponse<Trip>> update(
    int id, {
    required int vehicleId,
    required double originLat,
    required double originLng,
    required String originAddress,
    required double destLat,
    required double destLng,
    required String destAddress,
    required DateTime departAt,
    required int seatsTotal,
    double? pricePerSeat,
  }) =>
      _api.put<Trip>('Trips/$id',
          body: {
            'vehicleId': vehicleId,
            'origin': {'lat': originLat, 'lng': originLng, 'address': originAddress},
            'destination': {'lat': destLat, 'lng': destLng, 'address': destAddress},
            'departAt': departAt.toUtc().toIso8601String(),
            'seatsTotal': seatsTotal,
            'pricePerSeat': pricePerSeat,
          },
          parse: (d) => Trip.fromJson(d as Map<String, dynamic>));

  Future<AppResponse<List<Trip>>> myTrips() => _api.get<List<Trip>>(
        'Trips/mine',
        parse: (d) => (d as List)
            .map((e) => Trip.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

/// Driver presence — report location + online state (for hail targeting).
class PresenceService {
  final _api = ApiClient.instance;

  Future<AppResponse> updateLocation(double lat, double lng, {bool online = true}) =>
      _api.post('me/location', body: {'lat': lat, 'lng': lng, 'online': online});

  Future<AppResponse> goOffline() => _api.post('me/location/offline');
}

/// Ride requests (hail) — driver side.
class RideRequestService {
  final _api = ApiClient.instance;

  Future<AppResponse<List<RideRequestRow>>> nearby(
    double lat, double lng, {int radiusMeters = 5000}) =>
      _api.get<List<RideRequestRow>>(
        'requests/nearby',
        query: {'lat': lat, 'lng': lng, 'radiusMeters': radiusMeters},
        parse: (d) => (d as List)
            .map((e) => RideRequestRow.fromJson(e as Map<String, dynamic>))
            .toList(),
      );

  Future<AppResponse> accept(int id) => _api.post('requests/$id/accept');
}

/// The rider's saved places — Home, Work and named favourites
/// (`api/v1/me/places`). The server has no update verb: an edit is a delete
/// plus a create, which [SavedPlaces] handles.
class SavedPlaceService {
  final _api = ApiClient.instance;

  Future<AppResponse<List<SavedPlace>>> list() => _api.get<List<SavedPlace>>(
        'me/places',
        parse: (d) => (d as List)
            .map((e) => SavedPlace.fromJson(e as Map<String, dynamic>))
            .toList(),
      );

  Future<AppResponse<SavedPlace>> add({
    required SavedPlaceLabel label,
    required String name,
    required Place place,
  }) =>
      _api.post<SavedPlace>(
        'me/places',
        body: {
          'label': label.value,
          'name': name,
          'place': {
            'lat': place.lat,
            'lng': place.lng,
            // The entity keeps the full address; fall back to the short name
            // when the geocoder gave us nothing better.
            'address': place.address ?? place.name,
          },
        },
        parse: (d) => SavedPlace.fromJson(d as Map<String, dynamic>),
      );

  Future<AppResponse> remove(int id) => _api.delete('me/places/$id');
}
