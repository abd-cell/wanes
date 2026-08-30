# Wanes App (Flutter) — Structure

Flutter 3.47 / Dart 3.13 mobile app (rider & driver) for Wanes. Talks to the
Wanes backend API. Brand palette + light/dark theme. Lean stack (no heavy state
lib): `http`, `shared_preferences`, `intl`.

## Layout

```
wanes_app/lib/
├── main.dart                  bootstrap; routes to login or home by session
├── core/
│   ├── environment.dart       apiBaseUrl (10.0.2.2 for Android emulator)
│   ├── app_response.dart       AppResponse<T> envelope parser
│   ├── api_client.dart         HTTP gateway (Bearer token, decode envelope)
│   ├── session.dart            token + profile in shared_preferences
│   └── theme.dart              WanesColors + Material 3 light/dark theme
├── core/places.dart           preset places (stand-in for geocoding/map picker)
├── widgets/radar_view.dart     reusable radar/sonar animation — used IN PLACE OF a real map
├── models/
│   └── models.dart             Profile, AuthResult, Trip, SearchResult, Vehicle, RideRequestRow, Booking
├── services/
│   └── services.dart           Auth, Search, Booking, Profile, Vehicle, Trip, RideRequest, Rating
└── features/
    ├── login_screen.dart       phone → OTP → session
    ├── home_screen.dart        rider: From → To (+ seats) search; "Drive" switch
    ├── results_screen.dart     carpool matches; book a seat → live trip
    ├── searching_screen.dart   HAIL: radar (drivers being pinged)
    ├── live_trip_screen.dart   post-booking: radar (driver approaching) + status stepper → rate
    ├── rate_screen.dart        star rating + comment → POST /Ratings
    └── driver/
        ├── driver_home_screen.dart   bottom-nav shell (Post · My trips · Requests)
        ├── post_trip_screen.dart     pick vehicle + route + seats → POST /Trips
        ├── my_trips_screen.dart      GET /Trips/mine with status chips
        ├── requests_screen.dart      nearby hail requests → accept
        ├── vehicles_screen.dart      list + add vehicle
        └── driver_apply_screen.dart  submit license for verification
```

## Screens (build clean, `flutter analyze` = 0 issues)

- **Login** — phone + OTP (uses `Accounts/request-otp` + `verify-otp`).
- **Home / Search** — pick From/To (preset places stand in for map/geocoding) +
  seats → calls `POST /Search`.
- **Results (carpool)** — ranked driver trips; **Book seat** → `POST /Bookings`.
- **Searching (hail)** — when no trip matches: a live **radar animation** (amber
  center pulse, expanding rings, teal driver dots), "N drivers notified" + a
  ticking countdown + Cancel. Matches the design prototype.

## Run

```bash
cd app/wanes_app
flutter run                 # device/emulator
flutter run -d chrome       # web
flutter build web           # verified building
```

> Set `Environment.apiBaseUrl` per target (Android emulator → `10.0.2.2`,
> iOS sim / web → `localhost`). Backend must be running.

## Driver mode (built)

Tap **Drive** on the rider home → driver shell:
- **Post** — choose a verified vehicle, From/To, seats (≤ capacity) → `POST /Trips`.
- **My trips** — `GET /Trips/mine` with status chips.
- **Requests** — nearby hail requests (`/requests/nearby`) → **Accept** (`/requests/{id}/accept`).
- **Setup menu** — My vehicles (list/add) + Become a verified driver (`/me/driver/apply`).

## Maps = radar animation (by design)

Wanes deliberately uses **no real map SDK**. Everywhere a live map would appear
(the hail "searching" screen and the live-trip screen) it uses the reusable
[`RadarView`](wanes_app/lib/widgets/radar_view.dart) sonar animation — zero API
cost, no keys, no bundle weight. Route/place selection uses the preset places
list (`core/places.dart`).

## Next
- Push/realtime for incoming requests + "driver accepted" (backend FCM + WebSocket).
- File uploads for license/vehicle photos.
- Import the polished Claude Design (needs an interactive session to authorize).
