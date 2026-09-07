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
posted ──► en_route ──► arrived ──► active ──► completed
   │                                   │
   ├──► full ──────────────────────────┘   (seats_left = 0; still runs to completion)
   │
   └──► cancelled                          (by driver, before departure)
```

**The trip's status is not a field the driver sets — it is a summary of its
bookings** (Section 8). One trip carries several riders, so:

| Its bookings | The trip is |
|---|---|
| any `in_progress` | `active` — somebody is aboard |
| else any `arrived` | `arrived` — the driver is at a pickup |
| else all settled and ≥1 `completed` | `completed` — the last rider was dropped off |
| else, and it was `en_route` | `en_route` — see below |
| else | `posted`, or `full` when `seats_left = 0` |

**`en_route` is the one status a driver sets outright**, and the exception to
derivation: setting off moves *nobody's* seat — every rider is still at their
kerb — so the seats would say `posted` while the car is already moving. Derivation
preserves it instead of falling back, because falling back would put a departed
trip back in search.

`completed` and `cancelled` are terminal and never re-derived. A journey whose
last rider cancels or no-shows mid-ride stays where it was — the driver is still
on the road — and their own trip-wide move is what finishes it.

**Rules**
- `posted` — created, in the future, discoverable in search. Published straight
  away, with no admin review.
- `full` — no seats left; hidden from search but existing bookings stand.
- `en_route` — the driver has set off for the first pickup (`POST
  trips/{id}/depart`). **This is where a trip leaves search.** Before it existed a
  trip stayed discoverable all the way to the first kerb and dropped out on
  arrival, which is backwards. Optional for the driver: `posted → arrived` is
  still legal, because refusing it would only teach drivers to tap a button that
  means nothing to them.
- `arrived` — the driver has reached at least one rider's pickup point.
- `active` — trip is under way; at least one rider is aboard.
- `completed` — trip finished; unlocks rating for everyone on it. Bumps the
  driver's trip count once.
- `cancelled` — driver cancels; **all live bookings are auto-cancelled** and riders notified.
- The driver's trip-wide **Arrived / Start / Complete** buttons are the same
  per-seat moves applied to every rider the move is legal for (Section 8); the
  status is then derived from the result. **On my way** is not one of these — it
  moves no seat at all. The order enforced is
  `posted|full → en_route → arrived → active → completed` (`Conflict` otherwise),
  with `en_route` skippable.
- A `posted` trip with **no bookings** can be **edited** by its driver (vehicle,
  route, departure, seats, price); `seats_left` is reset to the new `seats_total`.
  Once any non-cancelled booking exists — or the trip has left `posted` — editing
  is refused (`TripNotEditable`) and the driver must cancel instead.
- A trip in the **past** with no `active` transition auto-expires (`cancelled`/system).

### One driver, one car — availability

A driver is **engaged** while any trip of theirs is `en_route`, `arrived` or
`active`: they are on the way to a pickup, at one, or carrying riders. A trip that still holds a slot in
their day (`posted`, `full`, `en_route`, `arrived`, `active`) **holds their
schedule**.

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
     • status = posted            (not full, not departed, not cancelled)
     • seats_left >= requested seats
     • depart_at has not gone     (> now − BoardingGrace)
     • origin is near the trip's origin
     • destination is near the trip's destination
       (both ends inside the radius — this is also the direction check:
        a trip going the other way has its origin near the rider's
        destination, so it cannot match)
     • AND, if the driver has a live fix, they are near the rider too
3. IF candidates exist:
     → RANK them (Section 6) → return list → rider books a seat.   [CARPOOL]
   ELSE (no candidate):
     → create a RideRequest (status = open), stamped with `when`
     → notify nearby online drivers free at `when`                  [HAIL]
```

**The rider's `when` is not in that filter.** It ranks (Section 6) and never
excludes. A trip with free seats that has not departed is discoverable *whenever*
it leaves — six hours out, four days out. Used as a ±30 min window this hid a
perfectly usable trip and dropped the rider into a hail with a good match sitting
unshown; the discoverability rule is "seats left and not departed", full stop.

**Rules**
- The **direction check** is mandatory: a trip going the opposite way must never match.
- Only **seats-available, posted** trips are matchable, and only those that have
  not yet gone: `depart_at > now - MatchRules.BoardingGrace` (15 min). The floor
  is a grace rather than "now" because a Posted trip a few minutes past its
  departure has not left — it is boarding, or the driver is late. The grace stays
  short so the case it was written for — a trip from this morning that was never
  started — is still excluded. A driver who has actually set off is excluded by
  `status`, not by the clock (`en_route`, Section 4).
- **The driver has to actually be around here.** Matching the rider's pickup
  against the trip's *planned* origin alone makes a notional match: the pickup
  point can be next door while the driver is an hour away. So when the driver has
  a **live fix** — `User.LastLocation`, reported within `MatchRules.LiveFixWindow`
  (30 min) — that position must be inside the radius of the rider's origin as
  well. Both halves are required: a driver standing next to the rider, on a trip
  leaving from another region, is no more a match than the reverse.
- **A missing or stale fix is ignored, not disqualifying.** A driver only reports
  while the app is running, so most trips posted for a future day carry an old fix
  or none; requiring one would empty search of every scheduled trip. Those are
  judged on the planned origin exactly as before. `MatchRules.IsLiveFix` is the
  one place that decides, and a position with no timestamp is not live — an unaged
  position cannot be called current, so it does not get to exclude a trip.
- **A trip already collecting riders is closed**, even with seats free. Only
  `posted` is searchable, so `en_route`, `arrived` and `active` are all out: a
  rider added to a pickup run already under way has no way to tell the driver to
  turn around.
- If the rider requests more seats than any single trip offers, no carpool match →
  falls through to a request (or we suggest splitting — future).

---

## 6. Ranking rule (which trips show first)

The default order (`SearchSort.Best`) is **walk plus wait**, normalised so the two
can be added:

```
score = (walk_km_at_both_ends / match_radius_km)
      + (minutes_from_wanted_departure / MatchRules.RankTimeScale)   # 60 min
```

A full match radius of walking costs the same as an hour off the wanted hour.
That is what lets a trip leaving tomorrow stay in the results while a trip
leaving soon sits above it — the window became a ranking factor here when it
stopped being a filter (Section 5).

- Computed **in memory over a candidate pool** (`MatchRules.CandidatePool`, 200),
  not in the query: the geography operators only answer in metres *inside* a
  query, so a materialised entity's `Distance` returns degrees. Distances go
  through `GeoDistance.Km`. The pool comes back ordered by proximity, which the
  database *can* do, and is re-ranked properly before the 20-row cap.
- The rider may override with an explicit sort (soonest, cheapest, best-rated,
  shortest walk, most seats); those run in the query. Every sort tie-breaks on
  proximity then `depart_at`, so two identical searches cannot disagree.
- **Price is NOT part of the default score** (payments out of scope) — it is only
  an explicit sort the rider can choose.
- MVP uses **straight-line** distances; upgrade to true route geometry later.

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
  trip (`Posted`, seats left, not departed, both ends inside the radius).
- It runs the **reverse match** as a posted trip does: riders already sitting on
  an open hail along the same route are told a trip they can take now exists.
  Skipped when the hail filled the car (`SeatsLeft == 0`) — there is nothing to
  offer them.
- It carries a **price**, derived rather than quoted. See below.

### One trip type — the invariant to protect

The two paths differ **only in who created the trip row**. After creation there
is one trip type, one search index, one join rule, and `Trip` deliberately has
**no `Source` / `OfferedBy` column** — nothing downstream can ask which path a
trip came from, so nothing can start behaving differently for one of them.

Keep it that way. Adding such a flag is how "one trip type" quietly becomes two
sets of rules that have to be kept in step.

Three consequences follow from a hail-accepted trip being an ordinary trip, and
each needed an answer:

- **Seat capacity** comes from the driver's vehicle, not from the request. The
  request only says what one rider needs; `SeatsTotal = vehicle.SeatCapacity`
  and the remainder is carpool seats. The driver's vehicle is therefore part of
  trip creation, not optional metadata — `Accept` refuses with `VehicleNotFound`
  if they have none.
- **Price per seat is set by the driver, at accept time.** A hail carries no
  price — the rider asked for a ride, not a quote — so `POST
  requests/{id}/accept` takes a `pricePerSeat` and the trip lists at that figure,
  verbatim, exactly as a posted trip lists at its driver's own price. The app
  requires it: tapping Accept opens a price sheet, and **backing out of that
  sheet is declining, never accepting at the default**.

  The sheet opens on the **distance estimate** — the same figure already shown on
  the request card, from the admin-set `AppConfiguration.FareBaseAmount` /
  `FarePerKm` — so the driver confirms a number they have been looking at rather
  than inventing one against the countdown. Those rates are also the server's
  **fallback** for a call that carries no price (an older build, a retry whose
  body was lost): they are not a second pricing model, they are the guarantee
  that this never produces the one kind of trip in search with a blank where
  every other row shows a figure. Zero is kept as a real answer — a free seat is
  a favour, not a missing field. Still display-only: there are no payments.
- **Route deviation** for a joining rider is bounded by the **search radius and
  nothing else**: both of the joiner's ends must sit within `MatchRules.RadiusFor`
  (5 km Nearby / 50 km Anywhere) of the trip's own ends. A joiner can only arrive
  before the trip departs at all (`en_route` closes it, Section 4), so there is no
  such thing as adding a detour to a run already in progress. That is the rider's own
  Nearby/Anywhere choice, so a rider who opted into a wide match accepted the
  longer ride. A real added-detour figure was considered and declined — it needs
  route geometry the MVP does not have, and a straight-line approximation would
  refuse joins a driver would happily take.

**Whose filters govern a joiner** is not a live question: every filter is
logistical (from/to, time, seats, radius), so a later joiner cannot violate the
first rider's constraint. Should a *social* filter ever be added — gender
preference is the obvious one — this becomes a real decision, and the choice is
between making filters matching-time only or copying the requester's constraints
onto the trip as hard limits. Do not add one without settling that.

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
- **Joining is instant — there is no driver approval and no `pending` step.** A
  booking is created `confirmed` and `seats_left` drops in the same commit. The
  `pending` value still exists on `BookingStatus` (and the transition rules still
  accept it, so an admin-created row behaves) but no rider-facing path produces
  one.
- A booking is refused if `seats_left < requested seats`, and the decrement is
  race-safe — see *Concurrency* below for how, because a transaction alone is not
  enough.
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

### Concurrency — the two races

Two operations are genuine races, and a transaction does **not** settle either:
SQL Server reads at READ COMMITTED, so two callers see the same row and neither
blocks the other's read.

| The race | What used to happen |
|---|---|
| Two riders take the last seat | Both read `seats_left = 1`, both pass the check, both write `0` — one seat sold twice |
| Two drivers accept the same hail | Both read `open`, both create a trip — the rider gets two drivers |

Both rows therefore carry a **row version** (`Trip.RowVersion`,
`RideRequest.RowVersion`, mapped `IsRowVersion()`), which makes every `UPDATE`
conditional on the value that was read. The loser changes no rows, gets a
`DbUpdateConcurrencyException`, and its whole transaction rolls back — including,
for a hail, the trip and booking it had already staged.

**Losing is not the same as being refused.** Two riders booking a four-seat trip
at the same instant both deserve a seat; the loser only needs to look again. So
the seat paths retry from a fresh read (`ConcurrencyRules.MaxAttempts`, 3), and
the refusal — when there is one — comes out of the ordinary checks on that fresh
read (`TripNotBookable` for a trip that filled, `NoSeatsLeft` for one that has
too few). Only sustained contention across every attempt returns `Conflict`,
which is honest: there may well be a seat, we simply never got to take it.

**Accepting a hail is idempotent.** A double tap, or a client retrying a request
whose response it never saw, is answered from the trip the driver already has —
one trip, success both times. Deliberately scoped to *that* driver: another
driver asking about a hail that is already matched is a loser, not a repeat
caller, and gets `RequestNotOpen`.

Retries need the change tracker cleared between attempts (`IUnitOfWork.Detach()`),
or the stale entity is handed straight back and the retry fails forever on the
same version.

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
- Concurrency: see the section below — the seat decrement and the hail claim are
  both guarded, and neither is a plain read-then-write.
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
