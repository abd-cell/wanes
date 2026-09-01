# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Wanes — a ride-matching platform. A rider asks for a trip **from → to**; Wanes first shows
trips drivers already posted (**carpool**), and if nothing matches it opens a request and
notifies nearby drivers to accept (**hail**). One trip carries several riders. **No payments
in scope.** Bilingual throughout (en LTR / ar RTL).

Three stacks in one repo, each with its own toolchain:

| Path | Stack | Role |
|---|---|---|
| `backend/Wanes/` | ASP.NET Core 9 + EF Core 9 (SQL Server + NetTopologySuite) | the API contract every client depends on |
| `cms/wanes-cp/` | Angular 22 (standalone, SSR, signals) | admin control panel |
| `app/wanes_app/` | Flutter 3.47 / Dart 3.13 | rider + driver mobile app |

`docs/BUSINESS_LOGIC.md` is the authority on domain rules (trip/booking/request lifecycles,
seat math, ranking, audit). Read it before changing any state transition.

## Toolchain

- **Flutter is not on PATH** — always call `C:\flutter\bin\flutter.bat`.
- dotnet 9, Angular CLI 22, node 25 are on PATH.
- Database: SQL Server `DESKTOP-SBF2I7A`, database `Wanes` (see `appsettings.json`).
- Dev login: phone `+962790000000`, OTP `1234` (fixed while `Otp:IsTesting=true`).
- The `.claude/skills/run-app` skill holds the verified, gotcha-annotated recipes for running
  the app on web / Android emulator / a real phone over LAN. Use it rather than re-deriving
  the commands.

## Commands

```bash
# backend (from backend/)
dotnet build Wanes.sln
dotnet run --project Wanes --launch-profile Wases
dotnet test
dotnet test --filter "FullyQualifiedName~BookingServiceTests"
dotnet ef migrations add <Name> --project Wanes --startup-project Wanes
```

Profile `Wases` serves `http://0.0.0.0:5000` + `https://0.0.0.0:5001` (Swagger at `/swagger`);
profile `https` serves `:5256`/`:7034` on localhost only. `dotnet run` from the **Bash** tool
exits 127 — start it detached from PowerShell (`Start-Process dotnet -ArgumentList "run",...`).
Migrations are applied and the admin account seeded automatically on startup.

```bash
# CMS (from cms/wanes-cp/)
npm start
npm run build
npm test
```

```bash
# app (from app/wanes_app/)
C:/flutter/bin/flutter.bat analyze
C:/flutter/bin/flutter.bat test
C:/flutter/bin/flutter.bat test test/bookings_test.dart
C:/flutter/bin/flutter.bat run -d web-server --web-port 8090 --dart-define=API_BASE_URL=http://localhost:5000/api/v1/
```

`flutter analyze` is expected to stay at 0 issues.

### The API base URL is target-specific — the #1 gotcha

`lib/core/environment.dart` reads `API_BASE_URL` from a compile-time `--dart-define`,
defaulting to the host's **LAN** IP so a phone, an emulator and the host browser can all reach
one backend. Pass the URL the *target* can resolve: `localhost` (host browser), `10.0.2.2`
(Android emulator only), the LAN IP (real device). Prefer plain HTTP `:5000` — the self-signed
cert on `:5001` gives browsers a timeout and Dart a `HandshakeException`. When the DHCP LAN IP
moves, update **three** places together: `environment.dart`,
`app/wanes_app/android/app/src/main/res/xml/network_security_config.xml`, and
`cms/wanes-cp/src/app/environment.ts`.

## Backend architecture

`Controllers → Services → DataAccess → Domain`, cross-cutting in `Shareds/`. Feature-grouped:
every feature has a folder under each of `Areas/Domain`, `Areas/Services`, `Areas/Controllers`.
Adding a feature means adding a slice to all three, not a new layer.

- **Response envelope.** Every action returns `BaseResponse` / `BaseResponse<T>` — never a raw
  DTO, never `IActionResult`. Failures carry a typed `ErrorCode`
  (`Shareds/Models/ErrorCode.cs`); callers switch on the code, they never parse messages.
- **Errors are HTTP 200.** `ExceptionMiddleware` turns a thrown `AppException` into 200 + a
  failed `BaseResponse`; only unhandled exceptions become 500. `ValidateModelAttribute` owns
  invalid-model responses (`SuppressModelStateInvalidFilter` is on) so validation failures use
  the same envelope.
- **Controllers are thin.** Inherit `BaseApiController` (`/api/v1/[controller]`), one service
  call per action, no logic. Admin controllers override the route
  (`[Route("api/v1/admin/users")]`).
- **DI by convention.** Put `[ScopedInjectable]` / `[TransientInjectable]` /
  `[SingletonInjectable]` on the service **interface**; `AppServiceExtension.RegisterTypes()`
  scans the assembly and wires the single implementation. No manual registration in
  `Program.cs`.
- **Data access.** `IRepository<T>` + `IUnitOfWork`. Multi-row invariants (seat reservation,
  first-wins request accept) run inside `unitOfWork.BeginTransactionAsync()` with a rollback
  helper that returns the error envelope — `BookingService.Create` is the model to copy.
- **Soft delete is not automatic.** `BaseEntity.IsDeleted` exists but there is **no global query
  filter** — every query must add `.Where(x => !x.IsDeleted)` itself.
- **Audit is explicit, by design.** Each mutating service calls
  `auditService.LogAsync(AuditActions.X, nameof(Entity), id)` after commit. A generic request
  interceptor was considered and rejected: the per-service call captures domain context a filter
  cannot. Keep adding the call.
- **Auth.** JWT bearer plus a `UserLogin` session row per device, re-validated on every request
  in `OnTokenValidated`, so logout revokes immediately; `ClockSkew = TimeSpan.Zero` is
  deliberate. Access tokens are short-lived and refresh tokens rotate. Authorize with
  `[AppAuthorize]` / `[AppAuthorize(Roles.Admin)]`, not `[Authorize]`. Read the acting user via
  `securityManager.RequireUserId()`.
- **Spatial.** Origin/destination are `geography` (SRID 4326) via NetTopologySuite; matching is
  a distance + time-window candidate filter, then ranking. Build points with `GeoFactory`.
- **Realtime.** `NotificationService` fans out three ways at once: a stored inbox row, an SSE
  push (`GET /sse`, `SseConnectionManager`), and FCM. FCM is credential-gated — with no
  service-account JSON configured, storage + SSE still work and push is skipped. Notifications
  are stored **bilingually** (both en and ar text) because clients send a fixed
  `Accept-Language`.
- **Error copy is localized on the client**, not the server: the API returns numeric codes and
  each client maps them (`app/wanes_app/lib/core/error_messages.dart`, CMS `i18n/*.ts`).
- **Every `/api/` request is logged** to a row by `ApiLoggerMiddleware`, placed ahead of auth so
  401/403 are captured too. Surfaced in the CMS and in the app's debug API-log screen.

## CMS architecture

Standalone components + signals, lazy `loadComponent` routes, SSR enabled.

- **Routes are language-first**: `/:languageCode/...` guarded by `languageGuard`. `appAuthGuard`
  and `loggedInGuard` return `true` on the server (localStorage is unreadable there) and let the
  browser decide, so deep links aren't bounced to login during SSR.
- **One typed gateway**, `core/api/api.service.ts`: named methods for the fixed screens plus a
  generic `/admin/<resource>` CRUD surface.
- **Config-driven admin grids.** `features/data/resource-config.ts` declares columns, form
  fields, filters and permissions per entity; `resource.component` renders any of them at
  `/:lang/data/:resource`. Adding an admin CRUD screen is normally a config entry, not a new
  component. Enum option lists are derived from the TS enums in `core/api/models.ts` and
  labelled through i18n keys. Notifications and driver verification are deliberately bespoke
  screens rather than grid entries.
- **Interceptor order matters on the way back**: `[auth, error, refresh]` — responses unwind in
  reverse, so `refreshInterceptor` sees a 401 first and can renew before `errorInterceptor` ends
  the session. Calls that must opt out (config bootstrap, the refresh call itself) set
  `SKIP_AUTH_HANDLING` in the `HttpContext`.
- **i18n**: flat `Record<string, string>` maps in `src/app/i18n/{en,ar}.ts`, read through the
  impure `| translate` pipe. Add every key to **both** files.
- The admin-set brand colour is applied in `provideAppInitializer` before first paint (cache
  first, never rejects) so screens don't flash the default palette.

## Flutter app architecture

Lean stack on purpose — no state-management package. `http` + `shared_preferences` + `intl`,
plus `flutter_map`, `firebase_messaging`, `geolocator` and `google_fonts`.

- **Layers**: `core/` (infrastructure) → `services/` (one class per API area, all in
  `services/services.dart`) → `features/` (screens) → `widgets/` (the UI kit).
  `models/models.dart` holds every DTO.
- **`ApiClient`** is a singleton gateway that never throws: transport, timeout and parse
  failures become typed `AppResponse` errors, so screens only ever check `success` /
  `errorMessage`. It also owns the 401 → refresh → replay-once flow, with a shared in-flight
  refresh future because refresh tokens rotate and two concurrent refreshes would race.
- **Theming**: `core/theme.dart` is the single source of truth, ported 1:1 from the design
  prototype's CSS custom properties. `WanesColors` for brand constants; the full
  brightness-aware token set comes from the `WanesTokens` `ThemeExtension` —
  `WanesTokens.of(context)`. Don't hardcode colours in screens.
- **UI kit**: `widgets/wanes_ui.dart` (components), `wanes_motion.dart` (the prototype's
  animations), `wanes_alerts.dart` (toasts / error panel / inline banner). **Never use
  `SnackBar`** — go through `WanesAlerts` — and **never `CircularProgressIndicator`**: the
  app's loader is `WanesSpinner` (`WanesSpinner.mono(colour)` inside a filled button), with
  `WanesOrbitLoader` ringing the mark on the splash.
- **Location**: `core/device_location.dart` wraps `geolocator` and never throws — every
  outcome is a `LocationFix` carrying either coordinates or a typed
  `LocationFailure` with its own copy. A good fix is cached for two minutes and shared
  between concurrent callers, so two taps can't raise two OS prompts. Nothing prompts
  on its own: only the picker's explicit "use my current location" row asks. Search is
  geocoded through `core/geocoding.dart` (Nominatim `/search` + `/reverse`, the reverse
  URL derived from `GEOCODER_URL`), biased to a viewbox around the rider and re-ranked
  by distance when a fix is known.
- **i18n**: flat key→text maps in `lib/l10n/strings_{en,ar}.dart`, looked up with
  `context.tr('some.key')` and `{placeholder}` interpolation; counted nouns go through
  `AppLocalizations.plural` (full CLDR categories for Arabic). Add every key to **both** maps —
  a miss falls back to English, then to the raw key, and `debugPrint`s in debug builds.

## Tests

- **Backend** — `backend/Wanes.Tests`, xUnit with hand-rolled doubles in `TestDoubles/`
  (`InMemoryRepository<T>`, `FakeUnitOfWork`, `FakeSecurityManager`, `FakeAuditService`,
  `FakeNotificationService`) plus an async-query shim so EF's async operators run over lists.
  No database, no mocking framework — extend the fakes rather than introducing one.
- **App** — `app/wanes_app/test/*`, widget tests per feature.
- **CMS** — vitest; coverage is sparse today.

## Cross-stack conventions

- Enums cross all three stacks (`Shareds/Enums` → `core/api/models.ts` →
  `models/models.dart`). Changing or reordering one means updating all three plus the CMS enum
  label keys.
- A new failure mode means: add an `ErrorCode`, then map it in the app's `error_messages.dart`
  and in the CMS i18n files.
- `docs/DESIGN.md`, `backend/BACKEND_PLAN.md` and `app/APP_STRUCTURE.md` are useful for intent
  but have drifted from the code (`APP_STRUCTURE.md` still claims "no real map SDK" — the app
  now uses `flutter_map`; `BACKEND_PLAN.md` predates the Configuration, Logging, Support and
  Management slices). Trust the code, and refresh these docs when you touch their area.
