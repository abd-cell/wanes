# Wanes — Business Logic

> The rules that govern how Wanes behaves. This is the **what/why** (business
> rules, states, decisions), separate from `DESIGN.md` (the **how**: stack,
> schema, APIs). Money/payments are **out of scope for now**.

---

## 1. In one sentence

A **rider** asks for a trip **from → to**. Wanes first **shows matching trips
drivers already posted** (carpool); if none match, it **opens a request and
notifies nearby drivers** to accept (hail). One trip can carry **several riders**.

---

## 2. Actors & roles

| Actor | Can do |
|---|---|
| **Rider** | Search a trip, book a seat, open a ride request, rate the driver |
| **Driver** | Post trips, receive & accept nearby requests, carry multiple riders, rate riders |
| **User** | One account can be **both** rider and driver; switches an *active role* |
| **Admin** | Verify drivers (badge + request acceptance), resolve disputes, read the audit log |

**Rules**
- A user must **verify their phone (OTP)** before any action.
- A driver needs `is_driver = true` and **at least one vehicle** to post a trip.
  There is **no admin sign-off on trips** — a trip is `posted` (live and
  searchable) the moment the driver creates it. `driver_status = verified` is a
  trust badge only; it is still required to **accept a nearby ride request**.
- A user acts as rider **or** driver at a time (`active_role`), but history keeps both.

---

## 3. Seats — capacity vs offered (the multi-rider rule)

- **Vehicle** has `seat_capacity` = the max passenger seats the car physically has (fixed when the car is added).
- **Trip** has `seats_total` = seats the driver **offers on this trip**.
  - Default `seats_total = vehicle.seat_capacity`.
  - Driver may lower it; it **must never exceed** `seat_capacity`.
- `seats_left` starts equal to `seats_total` and **decreases** with each confirmed booking.
- When `seats_left = 0` → trip becomes **`full`** and drops out of search results.
- **Multiple riders per trip:** each booking takes 1+ seats; a 4-seat trip can serve
  up to 4 riders (or fewer bookings each taking more than one seat).

---

## 4. Trip lifecycle (driver side)

```
posted ──► active ──► completed
   │          │
   ├──► full ─┘            (seats_left = 0; can still become active/completed)
   │
   └──► cancelled          (by driver, before departure)
```

**Rules**
- `posted` — created, in the future, discoverable in search. Published straight
  away, with no admin review.
- `full` — no seats left; hidden from search but existing bookings stand.
- `active` — trip is under way (started at/after `depart_at`).
- `completed` — trip finished; unlocks rating for everyone on it.
- `cancelled` — driver cancels; **all confirmed bookings are auto-cancelled** and riders notified.
- A `posted` trip with **no bookings** can be **edited** by its driver (vehicle,
  route, departure, seats, price); `seats_left` is reset to the new `seats_total`.
  Once any non-cancelled booking exists — or the trip has left `posted` — editing
  is refused (`TripNotEditable`) and the driver must cancel instead.
- A trip in the **past** with no `active` transition auto-expires (`cancelled`/system).

---

## 5. The search & match decision (rider side) — core logic

Input: `origin`, `destination`, `when`, `seats`.

```
1. Geocode origin + destination → coordinates.
2. Find CANDIDATE trips where ALL hold:
     • status = posted (not full/cancelled/active-past)
     • depart_at within the time window (e.g. ±30 min of `when`)
     • seats_left >= requested seats
     • origin is near the trip's route
     • destination is near the trip's route AND further along it than origin
       (direction check — no wrong-way matches)
3. IF candidates exist:
     → RANK them (Section 6) → return list → rider books a seat.   [CARPOOL]
   ELSE (no candidate):
     → create a RideRequest (status = open)
     → notify nearby online drivers                                [HAIL]
```

**Rules**
- The **direction check** is mandatory: a trip going the opposite way must never match.
- Only **future, seats-available, posted** trips are matchable.
- If the rider requests more seats than any single trip offers, no carpool match →
  falls through to a request (or we suggest splitting — future).

---

## 6. Ranking rule (which trips show first)

```
score =  w1 · 1/detour_distance     (least detour for the driver — best)
       + w2 · 1/pickup_distance     (route passes closest to the rider)
       + w3 · 1/time_gap            (closest to requested time)
       + w4 · driver_rating         (more trusted driver)
```

- **Price is NOT a ranking factor** (payments out of scope; price is display-only if shown).
- Ties broken by earliest `depart_at`, then highest `driver_rating`.
- MVP may use **straight-line** distances; upgrade to true route geometry later.

---

## 7. Ride request lifecycle (the HAIL fallback)

```
open ──► matched ──► (becomes a Booking on the accepted trip)
  │
  ├──► expired      (no driver accepted within the time limit)
  └──► cancelled    (rider cancels)
```

**Rules**
- On `open`: find **online drivers** with free seats within `radius_m` of the origin,
  push them a notification.
- **First driver to accept wins**; the request becomes `matched` and closes for others.
- If nobody accepts, **expand `radius_m`** over time; after a max radius/time → `expired`.
- Rider may `cancel` any time while `open`.
- A driver already on an active trip with no free seats is **not** notified.

---

## 8. Booking lifecycle

```
pending ──► confirmed ──► in_progress ──► completed
   │            │
   └────────────┴──► cancelled
```

**Rules**
- Creating a booking **reserves** seats (`pending`); confirming **decrements** `seats_left`.
- A booking cannot be confirmed if `seats_left < requested seats` (race-safe: check-and-decrement atomically).
- **Cancellation windows:**
  - Rider cancels before departure → seats returned to the trip.
  - Driver cancels the trip → all its bookings auto-cancel.
- `in_progress` begins when the trip goes `active`; `completed` when the trip completes.
- No fees/charges on cancel (no payments yet).

---

## 9. Ratings

- Unlocked **only after** a booking reaches `completed`.
- **Two-way:** rider rates driver, driver rates rider (1–5 stars + optional comment).
- Each side rates **once** per booking.
- A user's `rating_avg` / `rating_count` update on each new rating.
- Ratings feed the ranking (Section 6, `w4`).

---

## 10. Profile rules

- Phone is the **identity** — unique and verified; changing it requires re-verification.
- **Role switch** (rider ↔ driver) is allowed anytime, verified or not; posting a
  trip only needs a vehicle, while accepting a ride request still requires
  `verified` status.
- **Saved places** (Home / Work / favourites) pre-fill search for speed.
- **Preferences:** language (Arabic / English), push/SMS notification toggles.
- **Safety:** an emergency contact can be stored.
- **Delete/deactivate:** account goes `deleted`/`disabled`; audit history is retained (immutable).
- **No payment methods / wallet** anywhere in the profile (out of scope).

---

## 11. Audit rule (track the whole journey)

- **Every mutating action + auth event** writes one immutable `AuditLog` row:
  who (actor), what (action), which entity, `before`/`after`, IP, time.
- Examples: `auth.login`, `trip.create`, `trip.cancel`, `booking.confirm`,
  `request.accept`, `rating.create`, `profile.update`, `vehicle.add`, admin actions.
- The log is **append-only** — never updated or deleted.
- **Reads are not audited** (only changes + auth), to keep it meaningful.
- Sensitive values (OTP codes, document contents) are **redacted**, not stored raw.
- Powers: admin activity timeline, dispute resolution, and the user's "activity" view.

---

## 12. Key validation & edge-case rules

- Origin and destination **must differ** and both must geocode successfully.
- `depart_at` must be in the **future** when posting a trip.
- `seats_total ≤ vehicle.seat_capacity`; requested seats `≥ 1`.
- A rider **cannot book their own** trip.
- A rider **cannot double-book** the same trip.
- Concurrency: seat decrement is **atomic** so two riders can't take the last seat.
- A cancelled/expired entity cannot transition back to an active state.
- All state changes are recorded in the audit log (Section 11).

---

## 13. Out of scope (now)

- **Payments / wallet / fares / payouts** — deferred to a later phase.
- Automated pricing — price is display-only if shown at all.
- (Future) trip splitting across multiple drivers, scheduled recurring trips, promo codes.

---

*See `DESIGN.md` for the data model, APIs, tech stack, and the Arabic DFD
(`wanes-dfd-ar.html`) for the visual data flow.*
