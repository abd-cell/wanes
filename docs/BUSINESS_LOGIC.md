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

## 4. Trip lifecycle (driver side) — derived from the seats

```
posted ──► arrived ──► active ──► completed
   │                     │
   ├──► full ────────────┘        (seats_left = 0; can still become active/completed)
   │
   └──► cancelled                 (by driver, before departure)
```

**The trip's status is not a field the driver sets — it is a summary of its
bookings** (Section 8). One trip carries several riders, so:

| Its bookings | The trip is |
|---|---|
| any `in_progress` | `active` — somebody is aboard |
| else any `arrived` | `arrived` — the driver is at a pickup |
| else all settled and ≥1 `completed` | `completed` — the last rider was dropped off |
| else | `posted`, or `full` when `seats_left = 0` |

`completed` and `cancelled` are terminal and never re-derived. A journey whose
last rider cancels or no-shows mid-ride stays where it was — the driver is still
on the road — and their own trip-wide move is what finishes it.

**Rules**
- `posted` — created, in the future, discoverable in search. Published straight
  away, with no admin review.
- `full` — no seats left; hidden from search but existing bookings stand.
- `arrived` — the driver has reached at least one rider's pickup point.
- `active` — trip is under way; at least one rider is aboard.
- `completed` — trip finished; unlocks rating for everyone on it. Bumps the
  driver's trip count once.
- `cancelled` — driver cancels; **all live bookings are auto-cancelled** and riders notified.
- The driver's trip-wide **Arrived / Start / Complete** buttons are the same
  per-seat moves applied to every rider the move is legal for (Section 8); the
  status is then derived from the result. They still enforce the order
  `posted|full → arrived → active → completed` (`Conflict` otherwise).
- A `posted` trip with **no bookings** can be **edited** by its driver (vehicle,
  route, departure, seats, price); `seats_left` is reset to the new `seats_total`.
  Once any non-cancelled booking exists — or the trip has left `posted` — editing
  is refused (`TripNotEditable`) and the driver must cancel instead.
- A trip in the **past** with no `active` transition auto-expires (`cancelled`/system).

### One driver, one car — availability

A driver is **engaged** while any trip of theirs is `arrived` or `active`: they
are at a pickup point or carrying riders. A trip that still holds a slot in
their day (`posted`, `full`, `arrived`, `active`) **holds their schedule**.

While engaged, the driver:
- **cannot be marked online.** `POST me/location` still stores the location —
  riders in the car are tracking it — but the online flag stays down however
  often the app reports itself available. Setting out flips it down by itself,
  the moment the trip becomes `arrived`/`active`; going back online afterwards is
  the driver's own choice.
- **is not notified** about nearby hails, and the hail list (`GET
  ride-requests/nearby`) comes back empty — the list and the push agree.
- **cannot accept** a hail (`DriverOnActiveTrip`).
- **cannot post or re-time a trip** (`DriverOnActiveTrip`).

Independently of that, two of one driver's departures may not fall within
**30 minutes** of each other (`DriverTripTimeConflict`) — the same envelope
matching uses, because two trips that close together compete for the same driver
at the same moment. This is checked when posting a trip, when re-timing one
(against every trip but itself), and when accepting a hail — against **that
hail's own departure** (Section 7), not against "now". Finished and cancelled
trips are ignored.

That distinction is why the clash is filtered **per hail** rather than per
driver: a driver with a trip at nine is refused the hail leaving at nine and
still offered the one leaving at six, and `GET ride-requests/nearby` drops only
the rows that actually clash. Being *engaged* is the per-driver half — it empties
the list outright, because a driver in the car can take nothing at all. The rules live in one place,
`Areas/Domain/Trips/DriverAvailabilityRules`, read through
`IDriverAvailabilityService`.

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
     → create a RideRequest (status = open), stamped with `when`
     → notify nearby online drivers free at `when`                  [HAIL]
```

**Rules**
- The **direction check** is mandatory: a trip going the opposite way must never match.
- Only **seats-available, posted** trips are matchable, and only those that have
  not yet gone: `depart_at > now - MatchRules.BoardingGrace` (15 min). The floor
  is a grace rather than "now" because a Posted trip a few minutes past its
  departure has not left — it is boarding, the driver is late, or it is a
  hail-accepted driver still on the way to the first pickup. The grace stays
  short so the case it was written for — a trip from this morning that was never
  started — is still excluded.
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
- On `open`: find **online, free drivers** with free seats within `radius_m` of the
  origin, push them a notification. "Free" is Section 4's availability rule, asked
  about the hail's **wanted departure** — a driver out on a trip, or with one
  leaving inside the clash window of that hour, is skipped.
- **First driver to accept wins**; the request becomes `matched` and closes for others.
- If nobody accepts, **expand `radius_m`** over time; after a max radius/time → `expired`.
- Rider may `cancel` any time while `open`.
- A driver already out on a trip is **not** notified, does **not** see the request
  in their nearby list, and cannot accept it (Section 4).

### A hail is for the hour the rider searched for

A hail is **not** always "leave now". The rider's `when` travels onto the request
as `WantedDepartAt` (normalised through `MatchRules.HailDepartureFor`, so an
immediate hail still carries a reachable departure rather than the instant of the
tap). Everything downstream reads that field rather than `RequestedAt`:

- the trip a driver creates by accepting **departs then**;
- the **availability check** on accept, and the driver's nearby list, ask whether
  the driver is free *at that hour* (Section 4);
- the **fan-out** skips drivers busy at that hour, not drivers busy right now;
- the **reverse match** compares it against the new trip's departure — while
  `RequestedAt` stood in, a rider hailing for this evening was never told about
  the evening trip a driver had just posted, because the only hails the reverse
  match could reach were those opened in the last half hour.

The **TTL is unchanged and independent of it**: it is how long drivers have to
answer, not how far ahead the ride is. A hail for tomorrow evening still expires
in the configured window if nobody takes it, and the rider hails again.

### What accepting produces — a normal trip, with spare seats

Accepting is not a private arrangement between one driver and one rider. It
creates a real `Trip` with `SeatsTotal = vehicle capacity` and
`SeatsLeft = capacity - request.seats`, and those spare seats are **carpool seats
like any other**:

- It departs at the rider's **wanted departure** (`RideRequest.WantedDepartAt`),
  floored at `MatchRules.HailPickupLead` (5 min) from now — the driver still has
  to reach the pickup, and a departure stamped in the past is one search can
  never offer, which is what used to make this trip unjoinable by anyone except
  the hailing rider.
- It is offered in search, and bookable, on exactly the same rules as a posted
  trip (`Posted`, seats left, inside the radius and time window).
- It runs the **reverse match** as a posted trip does: riders already sitting on
  an open hail along the same route are told a trip they can take now exists.
  Skipped when the hail filled the car (`SeatsLeft == 0`) — there is nothing to
  offer them.

### How long a request stays open

- The window is **configuration, not a constant**: `AppConfiguration.HailRequestTtlMinutes`,
  set by an admin from the CMS, clamped to 1–240 minutes
  (`MatchRules.Min/MaxHailTtlMinutes`; the shipped default is 10). It is stamped onto
  `ExpiresAt` when the request opens, so **changing the setting does not move the
  deadline of a request that is already open** — every countdown runs on the row's own
  `ExpiresAt`, which is why both the search response and the driver's request rows carry it.
- A **background sweep** (`RideRequestExpiryWorker`, every 30 s) moves `open` requests
  past their `ExpiresAt` to `expired`. Filtering them out of list queries is not enough:
  a request has to reach a terminal status, and the drivers already holding its card
  have to be told.
- Between the deadline and the next sweep the row still reads `open`, so **`accept`
  honours the clock, not the column** — an accept after `ExpiresAt` is refused with
  `RequestNotOpen`.

### Leaving the drivers' screens

Every exit from `open` — `cancelled`, `matched`, `expired`, and an admin closing or
deleting the request from the CMS — also **broadcasts a close** to every connected
client: a small SSE control frame `{event: "rideRequestClosed", requestId, reason}`.

- It is **not a notification**: no inbox row, no push. A driver does not need to be
  told out-of-app that a request they never answered went away; they need the card gone.
- It goes to **every open connection**, not a computed audience. The audience for the
  original push was "drivers standing near the origin *at that moment*", and by the
  time the request closes they have moved, gone offline, or come online. The frame
  carries only an id, and a client that is not holding it ignores it.
- The clients drop the card on receipt; a client that misses the frame still loses it
  on its own countdown or the next list refresh.

---

## 8. Booking lifecycle — the driver tracks each seat

```
pending ──► confirmed ──► arrived ──► in_progress ──► completed
   │            │            │        (aboard)        (dropped off)
   │            │            │
   │            └────────────┴──► no_show      (driver waited, rider never came)
   └─────────────────────────┴──► cancelled    (rider gave the seat back)
```

A carpool collects its riders one at a time, so **the driver moves each booking
separately** — reached this rider, picked them up, dropped them off, or gave up
on them (`PUT Trips/{id}/bookings/{bookingId}/status`). The trip's own status is
then derived from all of its seats (Section 4).

**Rules**
- Creating a booking **reserves** seats (`pending`); confirming **decrements** `seats_left`.
- A booking cannot be confirmed if `seats_left < requested seats` (race-safe: check-and-decrement atomically).
- Only the trip's **own driver** may move a seat, and only along the order above
  (`BookingStatusNotAllowed` otherwise): a rider can be dropped off only once
  aboard, and marked a no-show only while they are not.
- **Live** seats are `pending`, `confirmed`, `arrived`, `in_progress`. A live seat
  is what entitles the two parties to each other's phone number and the rider to
  the driver's live position; `completed`, `cancelled` and `no_show` are settled.
- **Cancellation windows:**
  - Rider cancels before departure → seats returned to the trip.
  - Driver cancels the trip → all its live bookings auto-cancel.
  - `no_show` before the trip leaves → the seat goes back on the trip, exactly as
    a rider's own cancel does. Once it has left, the seat is spent.
- Each move notifies **only the rider whose seat moved** — telling a rider still
  waiting at the curb that "your trip has started" because someone else boarded
  would be a lie, and their tracking rail reads these events.
- Rating unlocks on `completed` only; a `no_show` rates nobody.
- No fees/charges on cancel or no-show (no payments yet).

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

### Signing out ends more than the token

Revoking the `UserLogin` row stops the API calls, and for a while that was all it
did. Three other things outlive a session unless they are ended explicitly:

- **Presence.** A driver stays `IsOnline`, so hail fan-out goes on counting and
  targeting a handset that has signed out, and the CMS shows a phantom available
  driver. Logout clears it — but only when this was the account's **last** live
  session, so a driver signed in on a second handset is not knocked offline
  mid-shift.
- **The SSE stream.** It authenticates once, at connect, and never again, so a
  revoked session's socket keeps delivering that account's notifications.
  Connections carry the session key they were opened with, and revoking a session
  closes exactly its own streams.
- **Anything cached on the device.** The client tears down in one place
  (`AuthService.logout`) rather than per screen, and clears the session
  regardless of what the API answered — an unreachable server must not leave
  someone signed in after they tapped Log out.

Every revocation path runs this, not just the Log out button: refresh-token reuse
detection, an expired refresh token, and a disabled account all revoke the same
way.

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
- A driver **cannot hold two rides at once**: none while out on a trip, and never
  two departures within 30 minutes of each other (Section 4).
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
