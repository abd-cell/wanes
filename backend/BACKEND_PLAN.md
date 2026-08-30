# Wanes Backend — Plan & Progress

ASP.NET Core 9 Web API. Architecture mirrors the **Sheel** backend:
`Controllers → Services → DataAccess → Domain`, cross-cutting in `Shareds/`.
Clean Code + SOLID. `AppResponse<T>` envelope · Repository + Unit of Work ·
convention-based DI · soft-delete · JWT sessions in DB · immutable audit log.

## Stack

| Concern | Choice |
|---|---|
| Framework | ASP.NET Core 9 (`net9.0`), nullable + implicit usings |
| ORM | EF Core 9 (SQL Server + **NetTopologySuite** spatial) |
| Auth | JWT Bearer, **Argon2id** hashing, session row per device (`UserLogin`) |
| Spatial | `geography` columns (SRID 4326) for origin/dest/route + `ST_Distance` matching |
| Docs | Swagger |

## Layout (built)

```
Wanes/
├── Program.cs                     ✅ composition root (DI, JWT, EF, pipeline, migrate)
├── AppHttpContext.cs              ✅ static HttpContext accessor
├── Areas/
│   ├── Domain/                    ✅ entities (no logic)
│   │   ├── Users/     User, UserRole, UserLogin, OtpCode, SavedPlace
│   │   ├── Vehicles/  Vehicle
│   │   ├── Trips/     Trip, TripStatusHistory
│   │   ├── Bookings/  Booking
│   │   ├── Requests/  RideRequest
│   │   ├── Ratings/   Rating
│   │   └── Audit/     AuditLog
│   ├── Services/
│   │   ├── Audit/     ✅ AuditService (append-only)
│   │   └── Users/Accounts/  ✅ AccountService (+ Models/)
│   └── Controllers/
│       ├── BaseApiController.cs         ✅ /api/v1/[controller]
│       └── Users/AccountsController.cs  ✅
├── DataAccess/
│   ├── DatabaseService.cs         ✅ DbContext
│   ├── Repositories/              ✅ IRepository<T> / Repository<T>
│   ├── UnitOfWorks/               ✅ IUnitOfWork / UnitOfWork (+ transactions)
│   ├── Config/                    ✅ IEntityTypeConfiguration<T> (spatial, indexes, delete behavior)
│   └── DesignTimeDbContextFactory.cs
├── Migrations/                    ✅ InitialCreate
└── Shareds/
    ├── Models/   ✅ AppResponse, ErrorCode, AppException, Paging, Base/, Config/
    ├── Enums/    ✅ all domain enums
    ├── Attributes/ ✅ [ScopedInjectable]/[Transient]/[Singleton], [AppAuthorize]
    ├── Security/ ✅ SecurityManager, TokenGenerator (JWT), PasswordHasher (Argon2)
    ├── Extensions/ ✅ AppServiceExtension (DI scan), IQueryable, Phone
    ├── Middlewares/ ✅ ExceptionMiddleware (AppException → 200 + AppResponse)
    └── Notifications/Sms/ ✅ ISmsSender (dev logger; swap for Twilio)
```

## Endpoints (live)

```
POST /api/v1/Accounts/request-otp        send OTP (fixed 1234 in testing)
POST /api/v1/Accounts/verify-otp         verify → JWT + profile (registers on first login)
POST /api/v1/Accounts/logout             [auth] revoke session
GET  /api/v1/Accounts/me                 [auth] full profile
PATCH/api/v1/Accounts/me                 [auth] edit profile
PATCH/api/v1/Accounts/me/preferences     [auth] language + notifications
PATCH/api/v1/Accounts/me/role            [auth] switch rider ↔ driver
```

## Slices — status

✅ **SavedPlaces** — CRUD (`/me/places`).
✅ **Vehicles** — add/edit/delete, set default, capacity (`/me/vehicles`).
✅ **Driver onboarding** — `POST /me/driver/apply`.
✅ **Trips** — create/get/mine/cancel/start/complete; `seats_total ≤ capacity`, future-departure, origin≠dest.
✅ **Search / Match Engine** — `POST /search`: geo + time-window candidate filter → rank → carpool results, else opens a RideRequest (hail).
✅ **Bookings** — atomic seat reserve/confirm/cancel (transaction), can't book own/double, seats returned on cancel.
✅ **RideRequests** — mine, cancel, `nearby` (driver), `accept` (first wins → creates trip + booking).
✅ **Ratings** — after completion, two-way, recomputes `rating_avg`.
✅ **Admin** — pending drivers, verify driver/vehicle, `GET /admin/audit` (filterable), `[AppAuthorize(Roles.Admin)]`.
✅ **Seeder** — default admin account.

✅ **Realtime notifications** — driver presence (`POST /me/location`), hail →
   notify nearby online drivers (FCM + SSE + stored inbox), accept → "driver
   accepted" to the rider via SSE. Endpoints: `GET /sse` (live stream),
   `GET /notifications/mine`, `POST /notifications/{id}/read`. Migration added.

✅ **Tests** — `Wanes.Tests` (xUnit) with hand-rolled in-memory fakes
   (`InMemoryRepository<T>`, `FakeUnitOfWork`, `FakeSecurityManager`,
   `FakeAuditService`, `FakeNotificationService`) + an async-query shim so EF's
   async operators run over lists. **15 tests, all green** — booking (atomic
   seats, own-trip, double-book, no-seats, cancel-returns-seats), trip creation
   (verify/past/capacity), search (carpool vs hail), rating (gated + average),
   vehicle onboarding.

### Still to do
1. **Real FCM** — `FcmSender` currently logs; swap for FirebaseAdmin + a service-account key.
2. **Route-aware matching** — replace straight-line with route geometry + detour scoring + direction check.
3. **File uploads** — avatar, vehicle photo, license/id documents.
4. **Localization** — `Resources/General/General.{en,ar}.json` for error messages.
5. **Dashboard** — admin stats/aggregations.

> On the "global audit interceptor" idea: kept the **explicit per-service audit**
> instead. It captures richer, domain-specific context (action names,
> before/after) that a generic request interceptor cannot — the per-service call
> is a feature, not boilerplate to remove.

## Run

```bash
# from backend/
dotnet build Wanes.sln
dotnet ef database update --project Wanes --startup-project Wanes   # needs SQL Server
dotnet run --project Wanes/Wanes.csproj                             # Swagger at /swagger
```

> Testing mode: `Otp.IsTesting=true` in appsettings fixes the OTP to `1234` and skips SMS.
