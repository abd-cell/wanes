import 'dart:typed_data';

import '../core/api_client.dart';
import '../core/app_config.dart';
import '../core/app_response.dart';
import '../core/l10n.dart';
import '../core/places.dart';
import '../core/push_service.dart';
import '../core/saved_places.dart';
import '../core/session.dart';
import '../models/marketplace_models.dart';
import '../models/models.dart';
import '../models/series_models.dart';

export '../models/marketplace_models.dart';
export '../models/series_models.dart';

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
    String? emergencyContactName,
    String? emergencyContactPhone,
  }) async {
    final body = <String, dynamic>{
      if (emergencyContactName != null) 'emergencyContactName': emergencyContactName,
      if (emergencyContactPhone != null) 'emergencyContactPhone': emergencyContactPhone,
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
    GenderPolicy driverGenderPolicy = GenderPolicy.any,
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
        // Who the rider will get in a car with, for this journey. It used to be
        // an account setting; it belongs to the search because the airport run
        // at dawn and the commute home are not the same question.
        'driverGenderPolicy': driverGenderPolicy.value,
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
        // The booking screen shows the shared-ride notice before this tap.
        body: {'tripId': tripId, 'seats': seats, 'acceptSharedRide': true},
        parse: (d) => Booking.fromJson(d as Map<String, dynamic>),
      );

  /// The rider's own bookings, newest first.
  Future<AppResponse<List<Booking>>> myBookings() => _api.get<List<Booking>>(
        'Bookings/mine',
        parse: (d) => (d as List)
            .map((e) => Booking.fromJson(e as Map<String, dynamic>))
            .toList(),
      );

  /// Gives the seat back — leaving.
  ///
  /// The rider's one way off a trip, whatever put them on it: a seat they
  /// booked, or a seat a driver created by claiming the trip they posted at a
  /// price they would rather not pay. The server refuses a booking that is
  /// already cancelled or completed, and returns the seats to the trip.
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

  /// The departures this driver is already promised to, and how close another
  /// one may not be. Asked for before a departure is chosen, so the picker can
  /// grey out what the API would refuse.
  ///
  /// [ignoreTripId] is the trip being edited — without it, moving a trip's time
  /// would find the trip's own departure blocking the slot next to it.
  Future<AppResponse<DriverAvailability>> driverAvailability({int? ignoreTripId}) =>
      _api.get<DriverAvailability>(
        'me/driver/availability',
        query: ignoreTripId == null ? null : {'ignoreTripId': ignoreTripId},
        parse: (d) => DriverAvailability.fromJson(d as Map<String, dynamic>),
      );

  /// The departures this rider already holds a seat at, and how close another
  /// one may not be. The rider-side twin of [driverAvailability], asked for the
  /// same reason: search greys out the times a booking would be refused at
  /// instead of letting the rider pick one, search, and read the refusal.
  Future<AppResponse<RiderAvailability>> riderAvailability({int? ignoreTripId}) =>
      _api.get<RiderAvailability>(
        'me/rider/availability',
        query: ignoreTripId == null ? null : {'ignoreTripId': ignoreTripId},
        parse: (d) => RiderAvailability.fromJson(d as Map<String, dynamic>),
      );

  /// Where the driver's application stands, with its documents. One call, so
  /// the screen never has to stitch a status together from two.
  Future<AppResponse<DriverVerification>> driverVerification() =>
      _api.get<DriverVerification>(
        'me/driver/verification',
        parse: (d) => DriverVerification.fromJson(d as Map<String, dynamic>),
      );

  /// Uploads (or replaces) one document. The server keeps one live copy per
  /// type, so re-sending a retaken photo is the same call.
  Future<AppResponse<DriverDocument>> uploadDriverDocument({
    required DriverDocumentType type,
    required Uint8List bytes,
    required String filename,
    required String contentType,
  }) =>
      _api.postFile<DriverDocument>(
        'me/driver/documents',
        field: 'file',
        bytes: bytes,
        filename: filename,
        contentType: contentType,
        fields: {'type': '${type.value}'},
        parse: (d) => DriverDocument.fromJson(d as Map<String, dynamic>),
      );

  Future<AppResponse> deleteDriverDocument(int id) =>
      _api.delete('me/driver/documents/$id');

  /// The bytes of one of the driver's own documents, for the thumbnail.
  Future<AppResponse<Uint8List>> driverDocumentBytes(int id) =>
      _api.getBytes('me/driver/documents/$id/content');

  /// Submits the application for manual review. Refused by the server until
  /// every required document is on file.
  Future<AppResponse> applyAsDriver({required String licenseNumber}) =>
      _api.post('me/driver/apply', body: {'licenseNumber': licenseNumber});
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
    int minSeatsToConfirm = 1,
    GenderPolicy genderPolicy = GenderPolicy.any,
    GenderPolicy coRiderGenderPolicy = GenderPolicy.any,
    int? minAge,
    int? maxAge,
  }) =>
      _api.post('Trips', body: {
        'vehicleId': vehicleId,
        'origin': {'lat': originLat, 'lng': originLng, 'address': originAddress},
        'destination': {'lat': destLat, 'lng': destLng, 'address': destAddress},
        'departAt': departAt.toUtc().toIso8601String(),
        'seatsTotal': seatsTotal,
        'pricePerSeat': pricePerSeat,
        'minSeatsToConfirm': minSeatsToConfirm,
        'genderPolicy': genderPolicy.value,
        'minAge': minAge,
        'maxAge': maxAge,
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
    int minSeatsToConfirm = 1,
    GenderPolicy genderPolicy = GenderPolicy.any,
    int? minAge,
    int? maxAge,
  }) =>
      _api.put<Trip>('Trips/$id',
          body: {
            'vehicleId': vehicleId,
            'origin': {'lat': originLat, 'lng': originLng, 'address': originAddress},
            'destination': {'lat': destLat, 'lng': destLng, 'address': destAddress},
            'departAt': departAt.toUtc().toIso8601String(),
            'seatsTotal': seatsTotal,
            'pricePerSeat': pricePerSeat,
            'minSeatsToConfirm': minSeatsToConfirm,
            'genderPolicy': genderPolicy.value,
            'minAge': minAge,
            'maxAge': maxAge,
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
  Future<AppResponse<TripBooking>> setBookingStatus(int tripId, int bookingId, int status,
          {String? boardingCode}) =>
      _api.put<TripBooking>(
        'Trips/$tripId/bookings/$bookingId/status',
        body: {'status': status, if (boardingCode != null) 'boardingCode': boardingCode},
        parse: (d) => TripBooking.fromJson(d as Map<String, dynamic>),
      );

  /// Driver lifecycle, trip-wide: the same per-seat moves applied to every rider
  /// at once. The server enforces the order (Posted/Full → Arrived → Active →
  /// Completed) and pushes each move to the riders whose seat it moved, which is
  /// what advances their tracking rail.
  /// The driver has set off for the first pickup. Takes the trip out of search;
  /// no rider's seat moves, because none of them has been reached yet.
  Future<AppResponse<Trip>> depart(int id) => _transition(id, 'depart');

  Future<AppResponse<Trip>> arrive(int id) => _transition(id, 'arrive');
  Future<AppResponse<Trip>> start(int id) => _transition(id, 'start');
  Future<AppResponse<Trip>> complete(int id) => _transition(id, 'complete');

  /// Runs a trip that never reached the seats the driver asked for. Every held
  /// seat is committed and the condition is dropped.
  Future<AppResponse<Trip>> confirm(int id) => _api.post<Trip>(
        'Trips/$id/confirm',
        parse: (d) => Trip.fromJson(d as Map<String, dynamic>),
      );

  /// Calls a trip off for want of riders. Distinct from an ordinary cancel: the
  /// riders are told why, in the words of the empty seats.
  Future<AppResponse<Trip>> cancelForLowSeats(int id) => _api.post<Trip>(
        'Trips/$id/cancel-low-seats',
        parse: (d) => Trip.fromJson(d as Map<String, dynamic>),
      );

  /// What cancelling this trip now would cost the driver.
  Future<AppResponse<CancelPreview>> cancelPreview(int id) => _api.get<CancelPreview>(
        'Trips/$id/cancel-preview',
        parse: (d) => CancelPreview.fromJson(d as Map<String, dynamic>),
      );

  /// Cancels the trip. Once riders depend on it the server wants a [reason].
  Future<AppResponse> cancel(int id, {CancelReason? reason, String? note}) => _api.post(
        'Trips/$id/cancel',
        body: {
          if (reason != null) 'reason': reason.value,
          if (note != null && note.trim().isNotEmpty) 'note': note.trim(),
        },
      );

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

/// Rider-posted trips: the demand board.
///
/// Both sides live here because it is one resource. A rider posts, joins and
/// leaves; a driver browses and claims. Splitting it by audience would put
/// `nearby` and `claim` somewhere that does not own the row they act on.
class RiderTripService {
  final _api = ApiClient.instance;

  /// Posts a trip. The caller holds its first seats.
  ///
  /// No price: that is the shape of the exchange — the rider states the need, a
  /// driver names the figure, and the rider answers it.
  Future<AppResponse<RiderTrip>> create({
    required double originLat,
    required double originLng,
    required String originAddress,
    required double destLat,
    required double destLng,
    required String destAddress,
    required DateTime departAt,
    int seats = 1,
    bool nearby = true,
    GenderPolicy driverGenderPolicy = GenderPolicy.any,
    GenderPolicy coRiderGenderPolicy = GenderPolicy.any,
    int? minAge,
    int? maxAge,
  }) =>
      _api.post<RiderTrip>(
        'ride-requests',
        body: {
          'origin': {'lat': originLat, 'lng': originLng, 'address': originAddress},
          'destination': {'lat': destLat, 'lng': destLng, 'address': destAddress},
          'departAt': departAt.toUtc().toIso8601String(),
          'seats': seats,
          'nearby': nearby,
          'driverGenderPolicy': driverGenderPolicy.value,
          'coRiderGenderPolicy': coRiderGenderPolicy.value,
          'minAge': minAge,
          'maxAge': maxAge,
          // The posting form will not submit until the rider ticked this.
          'acceptSharedRide': true,
        },
        parse: (d) => RiderTrip.fromJson(d as Map<String, dynamic>),
      );

  /// Every posting the caller holds a seat on — the ones they wrote and the
  /// ones they joined, which are the same thing to them.
  Future<AppResponse<List<RiderTrip>>> mine() => _api.get<List<RiderTrip>>(
        'ride-requests/mine',
        parse: (d) => (d as List)
            .map((e) => RiderTrip.fromJson(e as Map<String, dynamic>))
            .toList(),
      );

  Future<AppResponse<RiderTrip>> get(int id) => _api.get<RiderTrip>(
        'ride-requests/$id',
        parse: (d) => RiderTrip.fromJson(d as Map<String, dynamic>),
      );

  /// Takes seats on somebody else's posting, with the joiner's own conditions.
  Future<AppResponse<RiderTrip>> join(
    int id, {
    int seats = 1,
    GenderPolicy driverGenderPolicy = GenderPolicy.any,
    GenderPolicy coRiderGenderPolicy = GenderPolicy.any,
    int? minAge,
    int? maxAge,
  }) =>
      _api.post<RiderTrip>(
        'ride-requests/$id/join',
        body: {
          'seats': seats,
          'driverGenderPolicy': driverGenderPolicy.value,
          'coRiderGenderPolicy': coRiderGenderPolicy.value,
          'minAge': minAge,
          'maxAge': maxAge,
          // Joining goes through the shared-ride sheet first.
          'acceptSharedRide': true,
        },
        parse: (d) => RiderTrip.fromJson(d as Map<String, dynamic>),
      );

  /// Gives the caller's seats back. The posting closes with the last of them,
  /// and the server clears it off every driver's screen — so leaving a screen
  /// without calling this leaves a card up that can still be claimed.
  Future<AppResponse> leave(int id) => _api.post('ride-requests/$id/leave');

  /// The driver's search: trips wanting the journey they are about to drive.
  ///
  /// A different question from [nearby], which answers "who needs a lift around
  /// me, right now" off the driver's live position. This one takes a route and a
  /// time and comes back in the same two bands the rider's search uses — trips
  /// going the driver's way, and trips lying along it.
  Future<AppResponse<DemandSearchResult>> findRiders({
    required double originLat,
    required double originLng,
    required String originAddress,
    required double destLat,
    required double destLng,
    required String destAddress,
    required DateTime when,
    bool nearbyOnly = true,
    int seats = 0,
    int minSeats = 0,
    TripSort sortBy = TripSort.best,
    GenderPolicy riderGenderPolicy = GenderPolicy.any,
  }) =>
      _api.post<DemandSearchResult>(
        'ride-requests/search',
        body: {
          'origin': {'lat': originLat, 'lng': originLng, 'address': originAddress},
          'destination': {'lat': destLat, 'lng': destLng, 'address': destAddress},
          'when': when.toUtc().toIso8601String(),
          // 0 means "read it off my car" — the usual case.
          'seats': seats,
          'nearby': nearbyOnly,
          'minSeats': minSeats,
          'sortBy': sortBy.wire,
          'riderGenderPolicy': riderGenderPolicy.value,
        },
        parse: (d) => DemandSearchResult.fromJson(d as Map<String, dynamic>),
      );

  /// The driver's board: postings near them they could actually take.
  Future<AppResponse<List<RiderTrip>>> nearby(
    double lat,
    double lng, {
    int radiusMeters = 5000,
  }) =>
      _api.get<List<RiderTrip>>(
        'ride-requests/nearby',
        query: {'lat': lat, 'lng': lng, 'radiusMeters': radiusMeters},
        parse: (d) => (d as List)
            .map((e) => RiderTrip.fromJson(e as Map<String, dynamic>))
            .toList(),
      );

  /// Offers to serve a request at [pricePerSeat] — what the driver is charging
  /// for a seat on the trip this becomes. A request carries no price of its
  /// own, so this is the only place it can be set.
  ///
  /// Where the marketplace selects immediately — the shipped setting — the
  /// answer already carries the ride: `matchedTripId` is the trip, and it is
  /// what the driver's screens follow from here. Where offers accumulate, the
  /// answer is the request with this driver's offer recorded on it, and the
  /// decision arrives later as a notification.
  Future<AppResponse<RiderTrip>> offer(
    int id, {
    required double pricePerSeat,
    int? vehicleId,
    String? message,
    int? seatsOffered,
    int? minPassengers,
    bool acceptSharedTrip = true,
  }) =>
      _api.post<RiderTrip>(
        'ride-requests/$id/interest',
        body: {
          'pricePerSeat': pricePerSeat,
          'vehicleId': vehicleId,
          'message': message,
          'acceptSharedTrip': acceptSharedTrip,
          if (seatsOffered != null) 'seatsOffered': seatsOffered,
          if (minPassengers != null) 'minPassengers': minPassengers,
        },
        parse: (d) => RiderTrip.fromJson(d as Map<String, dynamic>),
      );

  /// Takes the offer back, while nobody has been selected.
  Future<AppResponse> withdraw(int id) => _api.delete('ride-requests/$id/interest');

  /// The drivers who offered — for the request's own riders to compare.
  Future<AppResponse<List<RideOffer>>> offers(int id) => _api.get<List<RideOffer>>(
        'ride-requests/$id/offers',
        parse: (d) => (d as List)
            .map((e) => RideOffer.fromJson(e as Map<String, dynamic>))
            .toList(),
      );

  /// A rider picks one offer; the trip forms with that driver.
  Future<AppResponse<RiderTrip>> chooseOffer(int id, int interestId) => _api.post<RiderTrip>(
        'ride-requests/$id/offers/$interestId/choose',
        parse: (d) => RiderTrip.fromJson(d as Map<String, dynamic>),
      );
}

/// Recurring postings, from either side (`api/v1/schedules`).
///
/// The server turns a schedule into ordinary trips and postings over a rolling
/// fortnight; nothing here matches anything. `nextDepartures` on the row is the
/// only honest preview of what one means.
class ScheduleService {
  final _api = ApiClient.instance;

  Future<AppResponse<List<TripSchedule>>> mine() => _api.get<List<TripSchedule>>(
        'schedules/mine',
        parse: (d) => (d as List)
            .map((e) => TripSchedule.fromJson(e as Map<String, dynamic>))
            .toList(),
      );

  Future<AppResponse<TripSchedule>> save({
    int? id,
    required bool asDriver,
    required double originLat,
    required double originLng,
    required String originAddress,
    required double destLat,
    required double destLng,
    required String destAddress,
    required Recurrence recurrence,
    required TimeOfDayValue timeOfDay,
    required DateTime startDate,
    WeekDaySet daysOfWeek = WeekDaySet.none,
    int? dayOfMonth,
    String? timeZoneId,
    DateTime? endDate,
    int seats = 1,
    double? pricePerSeat,
    int? vehicleId,
    int minSeatsToConfirm = 1,
    GenderPolicy genderPolicy = GenderPolicy.any,
    /// Rider-owned schedules only: who else may be aboard. Ignored server-side
    /// on a driver's schedule, which states one condition, not two.
    GenderPolicy coRiderGenderPolicy = GenderPolicy.any,
    int? minAge,
    int? maxAge,
    bool isPaused = false,
  }) {
    final body = {
      'ownerRole': asDriver ? 2 : 1,
      'origin': {'lat': originLat, 'lng': originLng, 'address': originAddress},
      'destination': {'lat': destLat, 'lng': destLng, 'address': destAddress},
      'recurrence': recurrence.value,
      'daysOfWeek': daysOfWeek.mask,
      'dayOfMonth': dayOfMonth,
      'timeOfDay': timeOfDay.wire,
      'timeZoneId': timeZoneId,
      'startDate': _dateOnly(startDate),
      'endDate': endDate == null ? null : _dateOnly(endDate),
      'seats': seats,
      'pricePerSeat': pricePerSeat,
      'vehicleId': vehicleId,
      'minSeatsToConfirm': minSeatsToConfirm,
      'genderPolicy': genderPolicy.value,
      'coRiderGenderPolicy': coRiderGenderPolicy.value,
      'minAge': minAge,
      'maxAge': maxAge,
      'isPaused': isPaused,
    };

    TripSchedule parse(Object? d) => TripSchedule.fromJson(d as Map<String, dynamic>);

    return id == null
        ? _api.post<TripSchedule>('schedules', body: body, parse: parse)
        : _api.put<TripSchedule>('schedules/$id', body: body, parse: parse);
  }

  /// Removes the schedule and calls off the occurrences nobody is on. A booked
  /// one still runs — it stopped being part of a series the moment somebody
  /// took a seat.
  Future<AppResponse> remove(int id) => _api.delete('schedules/$id');

  /// A date with no time, which is what the server's `DateOnly` parses.
  static String _dateOnly(DateTime date) =>
      '${date.year.toString().padLeft(4, '0')}-'
      '${date.month.toString().padLeft(2, '0')}-'
      '${date.day.toString().padLeft(2, '0')}';
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

  /// Clears one notification off the inbox. Soft delete server-side: the row
  /// survives for the admin console, it just stops being served here.
  Future<AppResponse> delete(int id) => _api.delete('Notifications/$id');

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

/// Complaints and suggestions: file one, and read what the desk said back.
///
/// Authorized, unlike [FaqService] beside it — a submission the desk cannot
/// reply to is a dead end.
class FeedbackService {
  final _api = ApiClient.instance;

  /// The user's own submissions, newest first.
  Future<AppResponse<List<FeedbackEntry>>> mine() => _api.get<List<FeedbackEntry>>(
        'feedback',
        parse: (d) => (d as List? ?? [])
            .map((e) => FeedbackEntry.fromJson(e as Map<String, dynamic>))
            .toList(),
      );

  /// Files one. [tripId] must be a trip the user was actually on — the server
  /// answers `TripNotFound` otherwise.
  Future<AppResponse<FeedbackEntry>> submit({
    required FeedbackKind kind,
    required String subject,
    required String message,
    int? tripId,
  }) =>
      _api.post<FeedbackEntry>(
        'feedback',
        body: {
          'kind': kind.value,
          'subject': subject,
          'message': message,
          if (tripId != null) 'tripId': tripId,
        },
        parse: (d) => FeedbackEntry.fromJson(d as Map<String, dynamic>),
      );
}

/// Agreements, route alerts and the reliability record — the parts of the
/// shared marketplace that belong to the signed-in user.
class MarketplaceService {
  final _api = ApiClient.instance;

  Future<AppResponse<List<int>>> acknowledgedKinds() => _api.get<List<int>>(
        'me/acknowledgements',
        parse: (d) => (d as List)
            .map((e) => ((e as Map<String, dynamic>)['kind'] as num).toInt())
            .toList(),
      );

  Future<AppResponse> acknowledge(int kind, {int version = 1}) =>
      _api.post('me/acknowledgements', body: {'kind': kind, 'version': version});

  Future<AppResponse<List<DemandAlert>>> alerts() => _api.get<List<DemandAlert>>(
        'me/demand-alerts',
        parse: (d) => (d as List)
            .map((e) => DemandAlert.fromJson(e as Map<String, dynamic>))
            .toList(),
      );

  /// A standing route alert.
  Future<AppResponse<DemandAlert>> addRouteAlert({
    required Place from,
    required Place to,
    required int minSeats,
    int radiusMeters = 3000,
    bool recurringOnly = false,
  }) =>
      _api.post<DemandAlert>(
        'me/demand-alerts',
        body: {
          'origin': {'lat': from.lat, 'lng': from.lng, 'address': from.name},
          'destination': {'lat': to.lat, 'lng': to.lng, 'address': to.name},
          'minSeats': minSeats,
          'radiusMeters': radiusMeters,
          'recurringOnly': recurringOnly,
        },
        parse: (d) => DemandAlert.fromJson(d as Map<String, dynamic>),
      );

  /// "Tell me when this request reaches [minSeats]."
  Future<AppResponse<DemandAlert>> watchRequest(int rideRequestId, int minSeats) =>
      _api.post<DemandAlert>(
        'me/demand-alerts',
        body: {'rideRequestId': rideRequestId, 'minSeats': minSeats},
        parse: (d) => DemandAlert.fromJson(d as Map<String, dynamic>),
      );

  Future<AppResponse> deleteAlert(int id) => _api.delete('me/demand-alerts/$id');

  Future<AppResponse<ReliabilityRecord>> reliability() => _api.get<ReliabilityRecord>(
        'me/reliability',
        parse: (d) => ReliabilityRecord.fromJson(d as Map<String, dynamic>),
      );
}

/// The emergency button and "follow my trip" links.
class SafetyApi {
  final _api = ApiClient.instance;

  /// Raises an SOS (kind 1) or a report (kind 2). The admin team is alerted; an
  /// SOS also messages the emergency contact on the profile.
  Future<AppResponse<SafetyIncidentResult>> raise({
    int kind = 1,
    int? tripId,
    int? bookingId,
    double? lat,
    double? lng,
    String? note,
  }) =>
      _api.post<SafetyIncidentResult>(
        'safety/incidents',
        body: {
          'kind': kind,
          if (tripId != null) 'tripId': tripId,
          if (bookingId != null) 'bookingId': bookingId,
          if (lat != null) 'lat': lat,
          if (lng != null) 'lng': lng,
          if (note != null && note.trim().isNotEmpty) 'note': note.trim(),
        },
        parse: (d) => SafetyIncidentResult.fromJson(d as Map<String, dynamic>),
      );

  Future<AppResponse<ShareLink>> share(int bookingId) => _api.post<ShareLink>(
        'safety/bookings/$bookingId/share',
        parse: (d) => ShareLink.fromJson(d as Map<String, dynamic>),
      );

  Future<AppResponse> stopSharing(int bookingId) => _api.delete('safety/bookings/$bookingId/share');
}

/// The device's zone as the server reads it: a fixed offset such as
/// `UTC+03:00`. A phone's zone *name* ("+03", "EEST") is not an id the server
/// can look up, and read as UTC a 07:30 commute would leave at 10:30.
String deviceZoneId([DateTime? at]) {
  final offset = (at ?? DateTime.now()).timeZoneOffset;
  final sign = offset.isNegative ? '-' : '+';
  final minutes = offset.inMinutes.abs();
  final hh = (minutes ~/ 60).toString().padLeft(2, '0');
  final mm = (minutes % 60).toString().padLeft(2, '0');
  return 'UTC$sign$hh:$mm';
}

/// Whole-series commitments (`api/v1/series`).
///
/// A driver offers to drive every day of a rider's recurring request; a rider
/// books every day of a driver's recurring trip. Each day stays its own trip.
class SeriesApi {
  final _api = ApiClient.instance;

  static SeriesCommitment _one(Object? d) => SeriesCommitment.fromJson(d as Map<String, dynamic>);
  static List<SeriesCommitment> _many(Object? d) =>
      (d as List).map((e) => SeriesCommitment.fromJson(e as Map<String, dynamic>)).toList();
  static SeriesResult _result(Object? d) => SeriesResult.fromJson(d as Map<String, dynamic>);

  static String? _date(DateTime? d) => d == null ? null : formatDateOnly(d);

  /// A driver offers for the whole series the request [rideRequestId] is a day of.
  Future<AppResponse<SeriesCommitment>> propose(
    int rideRequestId, {
    required int vehicleId,
    required double pricePerSeat,
    int? seatsOffered,
    WeekDaySet days = WeekDaySet.none,
    DateTime? until,
    String? message,
  }) =>
      _api.post<SeriesCommitment>(
        'series/ride-requests/$rideRequestId',
        body: {
          'vehicleId': vehicleId,
          'pricePerSeat': pricePerSeat,
          'seatsOffered': seatsOffered,
          'daysOfWeek': days.mask,
          'until': _date(until),
          if (message != null && message.trim().isNotEmpty) 'message': message.trim(),
          'acceptSharedTrip': true,
        },
        parse: _one,
      );

  /// A rider books every upcoming day of the recurring trip [tripId] is a day of.
  Future<AppResponse<SeriesResult>> join(
    int tripId, {
    int seats = 1,
    WeekDaySet days = WeekDaySet.none,
    DateTime? until,
  }) =>
      _api.post<SeriesResult>(
        'series/trips/$tripId',
        body: {
          'seats': seats,
          'daysOfWeek': days.mask,
          'until': _date(until),
          'acceptSharedRide': true,
        },
        parse: _result,
      );

  /// The offers on the rider's own schedule, and the driver it already has.
  Future<AppResponse<List<SeriesCommitment>>> offersFor(int scheduleId) =>
      _api.get<List<SeriesCommitment>>('series/schedules/$scheduleId', parse: _many);

  Future<AppResponse<List<SeriesCommitment>>> mine() =>
      _api.get<List<SeriesCommitment>>('series/mine', parse: _many);

  Future<AppResponse<SeriesCommitment>> get(int id) =>
      _api.get<SeriesCommitment>('series/$id', parse: _one);

  Future<AppResponse<SeriesResult>> accept(int id) =>
      _api.post<SeriesResult>('series/$id/accept', parse: _result);

  Future<AppResponse> decline(int id) => _api.post('series/$id/decline');

  Future<AppResponse> withdraw(int id) => _api.delete('series/$id');

  Future<AppResponse<SeriesEndPreview>> endPreview(int id) => _api.get<SeriesEndPreview>(
        'series/$id/end-preview',
        parse: (d) => SeriesEndPreview.fromJson(d as Map<String, dynamic>),
      );

  Future<AppResponse<SeriesCommitment>> end(int id,
          {bool immediately = false, CancelReason? reason, String? note}) =>
      _api.post<SeriesCommitment>(
        'series/$id/end',
        body: {
          'immediately': immediately,
          if (reason != null) 'reason': reason.value,
          if (note != null && note.trim().isNotEmpty) 'note': note.trim(),
        },
        parse: _one,
      );
}
