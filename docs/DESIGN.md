# Wanes — Design & Architecture Plan

> Ride-matching platform. A rider asks for a trip **from → to**. Wanes shows
> matching trips drivers already posted, **and** notifies nearby drivers when
> nothing matches. Two-sided marketplace: **Riders** and **Drivers**.

---

## 1. Product vision

**One line:** "Ask for a ride from A to B — grab a driver's already-planned trip, or let nearby drivers come to you."

Wanes blends two proven models:

| Model | Example | What we borrow |
|-------|---------|----------------|
| Carpooling (posted trips) | BlaBlaCar | Drivers pre-post trips; riders browse & join |
| On-demand hailing | Uber / inDrive | Rider posts request; nearby drivers get notified & accept |

**Wanes = both at once.** When a rider searches, we:
1. **Show** existing driver trips whose route matches (instant).
2. If nothing good matches, **create a ride request** and **notify nearby drivers** to accept.

---

## 2. Actors & roles

- **Rider** — requests a trip (from → to, time, seats).
- **Driver** — posts planned trips *and/or* receives & accepts nearby ride requests. Has a vehicle.
- **Admin** — verifies drivers, handles disputes, monitors the platform.

A single user account can be **both** a rider and a driver (role flags).

---

## 3. Core user flows

### Flow A — Rider searches (the main flow)
```
Rider enters:  From (address) → To (address) → When → Seats
        │
        ▼
Geocode both addresses to (lat, lng)
        │
        ▼
MATCH ENGINE
   ├─ Query driver trips near route + time window  ──► results found ──► show list, rider picks & books
   └─ No good match ──► create RideRequest ──► notify drivers within radius ──► first to accept wins
```

### Flow B — Driver posts a trip
```
Driver enters: From → To → Departure time → Seats available → Price/seat
        │
        ▼
Trip stored with route geometry ──► becomes discoverable in Flow A
```

### Flow C — Driver receives a request (on-demand)
```
Push notification: "Ride request nearby: A → B, 2 seats, ~$X"
        │
        ▼
Accept / Decline ──► on accept, request becomes a booked Trip, rider notified
```

---

## 4. Data model (entities)

```
User                 (account + full profile)
  -- identity
  id, phone (unique, verified), phone_verified (bool),
  email, email_verified (bool),
  first_name, last_name, display_name,
  avatar_url, gender, date_of_birth, bio,
  -- roles
  is_rider (bool), is_driver (bool), active_role,   -- rider | driver (current mode)
  -- driver onboarding / trust
  driver_status,        -- none | pending | verified | rejected | suspended
  license_number, license_photo_url, id_document_url,
  -- reputation
  rating_avg, rating_count, trips_as_rider, trips_as_driver,
  -- preferences
  language,             -- ar | en
  notif_push (bool), notif_sms (bool),
  -- safety
  emergency_contact_name, emergency_contact_phone,
  -- account
  status,               -- active | disabled | deleted
  last_seen_at, created_at, updated_at

SavedPlace           (rider's home / work / favourites)
  id, user_id, label,   -- home | work | custom
  name, address, lat, lng, created_at

Vehicle              (belongs to driver User)
  id, user_id, make, model, plate, color, year,
  seat_capacity,        -- MAX passenger seats the car physically has (fixed)
  photo_url, is_default, created_at

Trip                 (a driver's planned/active trip)
  id, driver_id, vehicle_id,
  origin_lat, origin_lng, origin_address,
  dest_lat, dest_lng, dest_address,
  route_geom (LineString),          -- PostGIS
  depart_at,
  seats_total,          -- seats OFFERED on this trip; default = vehicle.seat_capacity, must be <= it
  seats_left,           -- seats_total minus confirmed bookings; 0 => status = full
  price_per_seat, status            -- posted | active | full | completed | cancelled

RideRequest          (a rider's ask, when no trip matched)
  id, rider_id,
  origin_lat/lng/address, dest_lat/lng/address,
  requested_at, seats,
  status                            -- open | matched | expired | cancelled
  radius_m                          -- driver notification radius

Booking              (links a rider to a Trip)
  id, trip_id, rider_id,
  seats,
  status                            -- pending | confirmed | in_progress | completed | cancelled
  created_at

Rating
  id, booking_id, from_user, to_user, stars, comment

AuditLog             (immutable — records every meaningful action)
  id, actor_user_id,   -- who did it (null for system/cron)
  action,              -- e.g. trip.create, booking.confirm, request.accept, profile.update, auth.login
  entity_type, entity_id,
  before (jsonb), after (jsonb),   -- state change snapshot
  ip, user_agent, created_at       -- append-only, never updated/deleted
```

**Multi-rider trips:** one trip carries several riders. Each `Booking` takes 1+
seats and decrements `Trip.seats_left`; when it hits 0 the trip is `full`. So a
4-seat trip can serve up to 4 riders (or fewer bookings each taking >1 seat).

> **Money:** the price fields (`price_per_seat`, `price_total`) and any payment
> processing are **out of scope for now** — no wallet, no gateway. Trips are
> arranged and (if the driver wishes) settled offline/cash. Price stays as an
> optional display field only; we add real payments in a later phase.

**Geo columns** (`_lat`, `_lng`, `route_geom`) are the heart of matching — indexed with PostGIS `GEOGRAPHY`/`GEOMETRY` + GiST index.

---

## 5. The Match Engine (the core IP)

Given a rider search `(origin, dest, when, seats)`:

**Step 1 — Candidate filter (cheap, indexed):**
- Driver trips where `depart_at` within time window (e.g. ±30 min).
- `seats_left >= requested seats`.
- `status = posted | active`.
- Origin within `Rmax` of the trip's route line (PostGIS `ST_DWithin(route_geom, origin_point, R)`).
- Destination also within `Rmax` of the route, **and** further along the route than the origin (direction check).

**Step 2 — Score & rank:**
```
score =  w1 * (1/detour_distance)      -- how little the driver detours to pick up
       + w2 * (1/pickup_distance)      -- how close driver route passes to rider origin
       + w3 * (1/time_gap)             -- closeness to requested departure
       + w4 * driver_rating
```
*(price is not a ranking factor for now — payments are out of scope.)*
Return top N.

**Step 3 — Fallback (no match):**
- Create `RideRequest`, status `open`.
- Find drivers currently near the origin (`ST_DWithin(driver_location, origin, radius)`), online, seats available.
- Push notification to them; expand radius over time if no acceptance.
- First accept → create Booking, close request.

> **MVP simplification:** start with straight-line origin↔dest matching + time window
> before adding true route-geometry detour scoring. Ship the simple version, then upgrade.

---

## 5b. Audit & activity tracking (cross-cutting)

Every meaningful action in the system is recorded, so the whole journey is
traceable — who did what, when, and what changed.

- **How:** a global NestJS **interceptor** writes an `AuditLog` row after every
  successful mutating request (and key events: login, OTP verify, role switch,
  trip create/cancel, booking, request accept, rating, profile/vehicle edit,
  admin actions).
- **What's stored:** actor, action, entity, `before`/`after` JSON snapshot, IP,
  user-agent, timestamp.
- **Rules:** the table is **append-only** — never updated or deleted (immutable
  trail). Sensitive fields (OTP codes, documents) are redacted, not stored raw.
- **Use:** admin activity timeline, dispute resolution, security review, and a
  per-user "activity" view in the profile.

> Reads are not audited by default (only mutations + auth events), to keep the
> log meaningful and the volume manageable.

---

## 6. Recommended tech stack

| Layer | Choice | Why |
|-------|--------|-----|
| **Backend** | Node.js + TypeScript (NestJS) | Structured, testable, great for real-time later |
| **DB** | PostgreSQL + **PostGIS** | Geo queries (`ST_DWithin`, route distance) are first-class |
| **Geocoding / routes** | Mapbox or Google Maps API | Address → coords, route geometry, ETA |
| **Realtime** | WebSockets (Socket.IO) | Live driver location, request notifications |
| **Push** | Firebase Cloud Messaging | Notify drivers of nearby requests |
| **Cache/queue** | Redis | Online-driver location index, request fan-out |
| **Frontend (web)** | React + Vite + Tailwind | Fast dev, map via Mapbox GL |
| **Mobile (later)** | React Native / Flutter | Reuse API |
| **Auth** | JWT + phone OTP (Twilio) | Standard for ride apps |
| **Hosting** | Docker → Railway/Render/Fly.io → AWS at scale | Cheap start, clear upgrade path |

> Alternative if the team is Python-first: **FastAPI + PostGIS + SQLAlchemy** — same architecture, swap the language.

---

## 7. System architecture

```
 ┌──────────┐     ┌──────────┐
 │ Rider    │     │ Driver   │      (Web / Mobile clients)
 │ client   │     │ client   │
 └────┬─────┘     └────┬─────┘
      │ REST + WebSocket │
      ▼                  ▼
 ┌─────────────────────────────┐
 │        Wanes API (NestJS)   │
 │  Auth · Trips · Requests ·  │
 │  Bookings · Match Engine    │
 └───┬───────────┬─────────┬───┘
     ▼           ▼         ▼
 ┌────────┐  ┌───────┐  ┌──────────────┐
 │Postgres│  │ Redis │  │ External APIs │
 │+PostGIS│  │(geo   │  │ Maps · FCM ·  │
 │        │  │ index)│  │ Twilio OTP    │
 └────────┘  └───────┘  └──────────────┘
```

---

## 8. API surface (MVP)

```
# auth
POST /auth/request-otp         # send OTP to phone
POST /auth/verify-otp          # verify -> issue JWT (registers on first login)

# profile & account
GET   /me                      # full profile
PATCH /me                      # edit name, avatar, bio, gender, dob, language
PATCH /me/preferences          # notifications (push/sms), language
POST  /me/avatar               # upload profile photo
PATCH /me/role                 # switch active role rider <-> driver
POST  /me/emergency-contact    # set safety contact
DELETE/me                      # delete / deactivate account
GET   /me/history              # my trips (as rider + as driver)
GET   /me/ratings              # ratings I received

# saved places
GET   /me/places               # home, work, favourites
POST  /me/places
DELETE/me/places/:id

# driver onboarding
POST  /me/driver/apply         # submit license + id for verification
GET   /me/vehicles             # list
POST  /me/vehicles             # add vehicle
PATCH /me/vehicles/:id         # edit / set default
DELETE/me/vehicles/:id

# trips
POST /trips                    # driver posts a trip {origin,dest,depart_at,vehicle_id,
                               #   seats_total(<= vehicle.seat_capacity, default=capacity), price?}
GET  /trips/:id
PATCH/trips/:id                # update seats/status

# admin
GET  /admin/audit              # activity log, filter by actor/action/entity/date

POST /search                   # rider: {origin, dest, when, seats}
                               #   -> returns matching trips OR opens a request
POST /requests                 # explicit ride request
POST /requests/:id/accept      # driver accepts (on-demand)

POST /bookings                 # rider books a matched trip seat
PATCH/bookings/:id             # confirm / cancel / complete

POST /ratings
WS   /driver/location          # driver streams live location
WS   /notifications            # push channel
```

---

## 9. Build phases (roadmap)

**Phase 0 — Foundations (week 1)**
- Repo, Docker (Postgres+PostGIS+Redis), NestJS skeleton, User + auth + **profile**.

**Phase 1 — Posted trips + search (weeks 2–3)**
- Driver posts trips. Rider `/search` returns matching trips (simple straight-line + time window). Booking a seat. *← first demoable product.*

**Phase 2 — On-demand fallback (weeks 4–5)**
- RideRequest + notify nearby drivers + accept flow + live driver location (Redis + WebSocket).

**Phase 3 — Route-aware matching (weeks 6–7)**
- PostGIS route geometry, detour scoring, ranking.

**Phase 4 — Polish**
- Ratings, driver verification, admin dashboard, mobile app.

**Later — Payments (deferred, not now)**
- Wallet / gateway (Stripe or local), fares, driver payouts. Explicitly out of the current scope.

---

## 10. User profile — what it holds

The profile is a first-class part of the MVP (no payments in it). Screen layout:

- **Header** — avatar, display name, star rating + trips count, verified badge.
- **Personal info** — first/last name, phone (verified ✓), email, gender, date of birth, short bio. *Editable.*
- **Role switch** — a toggle between **Rider** and **Driver** mode (one account, both roles).
- **Driver section** (shown when driver) — verification status, license & ID upload, **My vehicles** (add/edit, set default).
- **Saved places** — Home, Work, and custom favourites for fast search.
- **Preferences** — language (Arabic / English), notification toggles (push / SMS).
- **Safety** — emergency contact name + phone.
- **Activity** — trip history (as rider and as driver), ratings received.
- **Account** — change phone (re-verify), log out, delete/deactivate account.

> No payment methods, wallet, or cards anywhere in the profile for now.

---

## 11. Open questions to decide next

1. **Geo/maps provider** — Mapbox (cheaper, generous free tier) vs Google (best coverage)?
2. **Market** — which city/country first? (affects maps, regulations)
3. **Pricing display** — show a driver-set price as info only, or hide price entirely for now?
4. **Driver verification** — manual admin review, or automated document check?
5. **Language** — confirmed Arabic + English / RTL?

*(Payments intentionally removed from this list — deferred.)*

---

*Next step: confirm the stack (Section 6) and Phase 1 scope, then scaffold the repo.*
