import '../core/api_client.dart';
import '../core/app_config.dart';
import '../core/app_response.dart';
import '../core/l10n.dart';
import '../core/places.dart';
import '../core/push_service.dart';
import '../core/saved_places.dart';
import '../core/session.dart';
import '../models/models.dart';

/// The admin-controlled platform settings (currency + brand colour).
class ConfigService {
  final _api = ApiClient.instance;

  /// Fetches the settings and adopts them. Anonymous endpoint, so this works on
  /// the splash screen before sign-in. A failure is deliberately silent: the
  /// cached (or default) brand still paints a usable app, and there is nothing
  /// the user could do about it.
  Future<AppResponse<AppConfig>> refresh() async {
    final res = await _api.get<AppConfig>(
      'configuration',
      parse: (d) => AppConfig.fromJson(d as Map<String, dynamic>),
    );
    if (res.success && res.data != null) await AppConfigController.adopt(res.data!);
    return res;
  }
}

/// Phone-OTP auth + profile.
class AuthService {
  final _api = ApiClient.instance;

  Future<AppResponse> requestOtp(String phone) =>
      _api.post('Accounts/request-otp', body: {'phone': phone});

  Future<AppResponse<AuthResult>> verifyOtp(String phone, String code) async {
    final res = await _api.post<AuthResult>(
      'Accounts/verify-otp',
      body: {
        'phone': phone,
        'code': code,
        'deviceType': PushService.deviceType,
      },
      parse: (d) => AuthResult.fromJson(d as Map<String, dynamic>),
    );
    if (res.success && res.data != null) {
      await Session.instance.save(
          res.data!.token, res.data!.refreshToken, res.data!.profile);
      // Needs the session to exist first — the token is stored against this
      // device's UserLogin row. Prompting here rather than at first launch
      // means the user has context for what the permission is for.
      await PushService.instance.requestPermission();
      await PushService.instance.registerToken();
      await PushService.instance.refreshUnreadCount();
      await PushService.instance.connectStream();
      // The server cannot see the app's locale, and it needs one to choose a
      // language for push payloads. Sent at sign-in as well as on change, so
      // users who never open the language picker still get the right language.
      await updatePreferences(
          language: AppLanguage.fromLanguageCode(LocaleController.value.languageCode));
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

  /// Patches the account's preferences. Same "only what's passed" contract as
  /// [updateProfile] — the theme lives on the account so a fresh install picks
  /// the user's last choice back up.
  Future<AppResponse<Profile>> updatePreferences({
    AppTheme? theme,
    bool? notifPush,
    bool? notifSms,
    AppLanguage? language,
  }) async {
    final res = await _api.patch<Profile>(
      'Accounts/me/preferences',
      body: {
        if (theme != null) 'theme': theme.value,
        if (notifPush != null) 'notifPush': notifPush,
        if (notifSms != null) 'notifSms': notifSms,
        if (language != null) 'language': language.value,
      },
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

  /// Signs out and tears down everything this account left behind on the device.
  ///
  /// Every screen with a "Log out" calls exactly this. It used to be a bare
  /// three lines with each caller adding its own cleanup on top, which is how
  /// one of them ended up clearing the cached places and the other did not —
  /// state that belongs to a signed-out account has no business being anyone
  /// else's problem to remember.
  ///
  /// The account's server-side presence is dropped by the API as it revokes the
  /// session, so there is no separate "go offline" call to lose here.
  Future<void> logout() async {
    // Detach the device while the auth token is still valid, so this handset
    // stops receiving the account's pushes.
    await PushService.instance.clearToken();
    await _api.post('Accounts/logout');
    // Unconditional: a failed or unreachable logout still ends the session on
    // this device. Leaving the user signed in because the network was down is
    // the one outcome nobody wants from tapping Log out.
    await Session.instance.clear();
    SavedPlaces.instance.clear();
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
    bool nearby = true,
    TripSort sortBy = TripSort.best,
  }) {
    return _api.post<SearchResult>(
      'Search',
      body: {
        'origin': {'lat': originLat, 'lng': originLng, 'address': originAddress},
        'destination': {'lat': destLat, 'lng': destLng, 'address': destAddress},
        'when': when.toUtc().toIso8601String(),
        'seats': seats,
        'nearby': nearby,
        // The server orders the page before capping it, so the rider's sort has
        // to travel with the request — re-sorting the reply can only shuffle
        // whichever twenty it chose.
        'sortBy': sortBy.wire,
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

  /// Gives the seat back. The server refuses a booking that is already
  /// cancelled or completed, and returns the seats to the trip.
  Future<AppResponse> cancel(int id) => _api.post('Bookings/$id/cancel');
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

  /// One trip in full — driver, vehicle and live seat count. Anonymous on the
  /// server, so it also works for a trip the rider has not booked.
  Future<AppResponse<Trip>> get(int id) => _api.get<Trip>(
        'Trips/$id',
        parse: (d) => Trip.fromJson(d as Map<String, dynamic>),
      );

  Future<AppResponse<List<Trip>>> myTrips() => _api.get<List<Trip>>(
        'Trips/mine',
        parse: (d) => (d as List)
            .map((e) => Trip.fromJson(e as Map<String, dynamic>))
            .toList(),
      );

  /// The riders holding seats on one of the driver's own trips. The server
  /// scopes this to the caller's trips, so another driver's manifest is a 404.
  Future<AppResponse<List<TripBooking>>> tripBookings(int id) =>
      _api.get<List<TripBooking>>(
        'Trips/$id/bookings',
        parse: (d) => (d as List)
            .map((e) => TripBooking.fromJson(e as Map<String, dynamic>))
            .toList(),
      );

  /// Where the driver last reported. Null data (with a successful response)
  /// simply means they have not reported yet, which is normal before a trip
  /// starts — the caller falls back to the stage-derived position.
  Future<AppResponse<DriverLocation>> driverLocation(int id) =>
      _api.get<DriverLocation>(
        'Trips/$id/driver-location',
        parse: (d) => DriverLocation.fromJson(d as Map<String, dynamic>),
      );

  /// Tracks one rider's seat: reached them, picked them up, dropped them off,
  /// or they never showed. The server enforces the order per seat, derives the
  /// trip's own status from every seat on it, and tells only the rider whose
  /// seat moved — which is what advances that one rider's tracking rail.
  Future<AppResponse<TripBooking>> setBookingStatus(int tripId, int bookingId, int status) =>
      _api.put<TripBooking>(
        'Trips/$tripId/bookings/$bookingId/status',
        body: {'status': status},
        parse: (d) => TripBooking.fromJson(d as Map<String, dynamic>),
      );

  /// Driver lifecycle, trip-wide: the same per-seat moves applied to every rider
  /// at once. The server enforces the order (Posted/Full → Arrived → Active →
  /// Completed) and pushes each move to the riders whose seat it moved, which is
  /// what advances their tracking rail.
  Future<AppResponse<Trip>> arrive(int id) => _transition(id, 'arrive');
  Future<AppResponse<Trip>> start(int id) => _transition(id, 'start');
  Future<AppResponse<Trip>> complete(int id) => _transition(id, 'complete');

  Future<AppResponse<Trip>> _transition(int id, String verb) => _api.post<Trip>(
        'Trips/$id/$verb',
        parse: (d) => Trip.fromJson(d as Map<String, dynamic>),
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

  /// Withdraws the rider's own open hail. The server closes it on every driver
  /// who was offered it, so leaving the search screen without calling this
  /// leaves a card up that can still be accepted.
  Future<AppResponse> cancel(int id) => _api.post('requests/$id/cancel');
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

/// The notification inbox and this device's push registration.
class NotificationsService {
  final _api = ApiClient.instance;

  Future<AppResponse<NotificationFeed>> feed() => _api.get<NotificationFeed>(
        'Notifications/mine',
        parse: (d) => NotificationFeed.fromJson(d as Map<String, dynamic>),
      );

  Future<AppResponse> markRead(int id) => _api.post('Notifications/$id/read');

  Future<AppResponse> markAllRead() => _api.post('Notifications/read-all');

  /// Upsert of this device's FCM token. Safe to call on every launch — the API
  /// treats it as an upsert and detaches the token from any stale session.
  Future<AppResponse> registerDevice(String token, {int? deviceType}) =>
      _api.post('Notifications/device-token', body: {
        'deviceToken': token,
        if (deviceType != null) 'deviceType': deviceType,
      });

  /// Stops push for this device without ending the session.
  Future<AppResponse> clearDevice() => _api.delete('Notifications/device-token');
}

/// The admin-curated help centre.
class FaqService {
  final _api = ApiClient.instance;

  /// The published FAQ. Anonymous endpoint, so the help screen works before
  /// sign-in — which is when most of these questions get asked.
  Future<AppResponse<Faq>> get() => _api.get<Faq>(
        'faq',
        parse: (d) => Faq.fromJson(d as Map<String, dynamic>),
      );
}
