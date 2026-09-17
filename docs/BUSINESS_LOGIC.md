# Wanes — Business Logic (v2)

> The rules that govern how Wanes behaves. This is the **what/why** (actors,
> concepts, states, matching, confirmation, recurrence, cancellation,
> concurrency), separate from `DESIGN.md` (the **how**: stack, schema, APIs).
> Money/payments are **out of scope** — every price here is agreed and
> displayed, never charged.
>
> **This is v2, and it is implemented.** The v2 change is one idea with
> consequences everywhere: **a rider's unmet demand is not a trip without a
> driver — it is a Ride Request, a first-class marketplace object of its own**,
> which becomes a Trip only when a driver is matched to it.
>
> §22 maps each rule to the code that owns it, records how the split was
> migrated, and names the two settings that decide how much of v2 a marketplace
> actually feels — both shipped at the value that preserves v1's behaviour.

---

## 1. In one sentence

Wanes is a **two-sided marketplace**, not a booking app. Drivers create **supply**
by publishing trips they are driving; riders create **demand** by publishing the
journeys they need. Riders may **join another rider's demand** rather than
duplicating it, drivers **discover demand** on their route and offer to serve it,
and the marketplace **forms a trip** when the two meet. One trip carries several
riders, and a trip may need a **minimum number of passengers** before it is
confirmed.

Search must never dead-end at "no trips found". It escalates instead:

```
Search → direct supply → on-the-way supply → existing rider demand
       → create demand → other riders join → a driver offers
       → trip formed → minimum passengers reached → confirmed → ride
```

---

## 2. Supply and demand are two different things

```
SUPPLY                                  DEMAND
Trip                                    RideRequest
  driver → vehicle → route → time         riders → route → time window
  → seats → price                         → seats → preferences
  → minimum passengers                    → driver interest
  → lifecycle + confirmation              → lifecycle
```

**A Trip always has a driver.** It is transportation that exists, whether or not
it has gathered its passengers yet.

**A Ride Request never has a driver.** It is transportation that does *not* exist
yet, and the marketplace's job is to make it exist.

The two are related by **matching**, and by exactly one transition — **trip
formation** (§8.4) — and nothing else. Do not model demand as a Trip whose driver
column happens to be null: every reader downstream then has to ask "is this real
supply or a wish?", and the answer has to be right in search, booking,
availability, notifications, ranking and reporting all at once. The null-driver
row is how v1 lost that distinction; see §22.

**What is shared, and must stay shared:** route geometry, the eligibility
predicates (§10), the corridor maths (§9), the availability rules (§6.4), the
configuration (§20). Two objects, one set of rules about roads, people and time.

---

## 3. Actors & roles

| Actor | Can do |
|---|---|
| **Rider** | Search, book a seat, **create a ride request**, **join another rider's request**, leave either, rate the driver |
| **Driver** | Publish trips (single or recurring) with a price and conditions, **discover demand**, **express interest / make an offer**, carry multiple riders, rate riders |
| **User** | One account is **both** rider and driver; switches an *active role*, history keeps both |
| **Admin** | Verify drivers, configure marketplace rules (§20), resolve disputes, read the audit log |

**Rules**
- A user must **verify their phone (OTP)** before any action.
- A driver needs `is_driver = true` and **at least one vehicle** to publish a
  trip. There is **no admin sign-off on trips** — a trip is live the moment it is
  created. `driver_status = verified` is a trust badge, and it is required to
  **be selected against a ride request** (§8).
- **A rider needs no verification to create a request.** It commits them to
  nothing: there is no driver and no price, and nothing is owed until a trip is
  formed and they stay on it (§8.5).
- Role switching erases no history.

---

## 4. Seats — capacity vs offered

- **Vehicle** has `seat_capacity` — the passenger seats the car physically has,
  fixed when the car is added.
- **Trip** has `seats_offered` — what the driver offers on *this* trip. Defaults
  to `seat_capacity`, may be lowered, **never exceeds** it.
- `seats_left` starts at `seats_offered` and decreases with each booking that
  holds seats — **including a `pending` one** (§13): a held seat is not for sale
  twice.
- `seats_left = 0` makes the trip **full**. Full is a **capacity condition, not a
  lifecycle state** (§6.3): the trip still runs, still confirms, still completes;
  it simply accepts nobody else and drops out of search.
- **Multiple riders per trip:** each booking takes 1+ seats, so a 4-seat trip
  serves up to four riders, or fewer riders taking more than one seat each.
- A **Ride Request** has `seats_requested`, the sum of its participants' seats
  (§7.3). It has no capacity of its own — capacity arrives with the vehicle.

---

## 5. What a driver publishes

Route, departure, seats and **price per seat** — plus the conditions they drive
under:

```
Trip
  PricePerSeat      : decimal      the driver's own figure, per seat, display-only
  MinPassengers     : int          secured SEATS required to confirm; 1 = no condition
  GenderPolicy      : enum { Any, MaleOnly, FemaleOnly }
  MinAge / MaxAge   : int?         null = no bound
```

### 5.1 Minimum passengers — the threshold, and the clock on it

A driver willing to make the run only if it is worth making. `MinPassengers`
**seats** must be held before anybody is confirmed.

- **The threshold counts secured seats, not booking rows.** Two riders taking two
  seats and one seat is three secured seats: a threshold of 3 is met. Counting
  rows would confirm one trip and not another for no reason a rider could see.
- **The default is platform policy, not a per-driver invention.**
  `MinimumPassengersDefault` (§20) seeds the field — recommended **3** — and the
  driver may lower it to 1 or raise it to at most `seats_offered`. It is admin-set
  per marketplace because what makes a run worth driving is a city-level fact, not
  a constant.
- They may not **raise** it once any seat is held.

```
 depart_at − ConfirmCutoff − ConfirmDecisionLead     depart_at − ConfirmCutoff      depart_at
              │                                              │                          │
   "2 of 3 seats — run, or cancel?"  ─── decision window ────►│ unanswered ⇒ cancelled   │
```

- Below the threshold the trip is **gathering**: its bookings sit `pending` and
  nobody is committed.
- **Threshold met** at any point → the trip confirms and every pending booking
  flips to `confirmed` **in one atomic commit**, each rider is notified **once**,
  and the decision never happens (§6.2, §13).
- **Still short**, and the driver is pushed a decision
  `ConfirmationDecisionLeadMinutes` before the cutoff (default 15) carrying the
  seat count and two answers:
  - **Run with N** → the pending bookings confirm; free seats stay on sale.
  - **Cancel** → the trip cancels and every held rider is told why: not enough
    passengers.
- **Unanswered at the cutoff, the trip cancels and the riders are told.** Silence
  cannot mean "run": the driver said they needed N, and leaving riders `pending`
  until departure strands them with no time to find another ride. That is why the
  cutoff is `depart_at − ConfirmationCutoffMinutes` (default 60) and not
  departure itself.
- The driver may confirm early, or cancel, whenever they like. The prompt gives
  them no new power — it forces the question at the last moment where the riders
  can still act on the answer.
- **A gathering trip is searchable and labelled as such** — "confirms at 3 of 4
  seats". Hiding it would starve it of the very bookings it needs. A rider booking
  one is told it may not run (§12).

### 5.2 Gender and age — the driver's conditions on their riders

- Checked against the booking rider's own profile — `User.Gender`,
  `User.DateOfBirth`.
- **A restriction requires the data.** A `FemaleOnly` trip refuses a rider whose
  gender is `Unspecified` (`RiderProfileIncomplete`) rather than guessing. The app
  asks for the missing field at that moment; it does not hide the trip forever.
- Ineligible trips are **filtered out of search**, not shown-then-refused (§12).
- These conditions bind **riders**, and are symmetric between co-riders by
  construction. The driver is covered by the rider's own conditions instead (§7.2).
- Age is evaluated at `depart_at`, so a birthday inside the booking window cannot
  make a confirmed rider retroactively ineligible.
- Part of the `trip.create` / `trip.update` audit before/after.

---

## 6. Trip state — three dimensions, not one enum

A single status enum containing every combination explodes. Three independent
dimensions do not:

```
Lifecycle       posted → en_route → arrived → active → completed
                                                     ↘ cancelled

Confirmation    gathering → confirmed
                          ↘ cancelled

Capacity        available ↔ full
```

A gathering trip that has not left is `lifecycle = posted, confirmation =
gathering, capacity = available`. Reaching the threshold changes **one** of the
three. This is the v2 correction to v1's flat ladder, where `full`, `gathering`
and `awaiting_driver` sat in the same column as `en_route`.

### 6.1 Lifecycle — derived from the seats

**Lifecycle is not a field the driver sets; it is a summary of the trip's
bookings** (§13). One trip carries several riders, so:

| Its bookings | The trip is |
|---|---|
| any `in_progress` | `active` — somebody is aboard |
| else any `arrived` | `arrived` — the driver is at a pickup |
| else all settled and ≥1 `completed` | `completed` — the last rider was dropped off |
| else, and it was `en_route` | `en_route` — preserved, see below |
| else | `posted` |

**`en_route` is the one lifecycle value a driver sets outright**, and the
exception to derivation: setting off moves *nobody's* seat — every rider is still
at their kerb — so derivation would say `posted` while the car is already moving,
and put a departed trip back in search. Optional: `posted → arrived` is still
legal, because refusing it would only teach drivers to tap a button that means
nothing to them.

`completed` and `cancelled` are **terminal and never re-derived**. A journey whose
last rider cancels or no-shows mid-ride stays where it was — the driver is still
on the road — and the driver's own trip-wide move finishes it.

**Rules**
- `posted` — created, in the future, discoverable. Published straight away, no
  admin review.
- `en_route` — the driver has set off for the first pickup
  (`POST trips/{id}/depart`). **This is where a trip leaves search.**
- `arrived` — the driver has reached at least one rider's pickup point.
- `active` — under way; at least one rider aboard.
- `completed` — finished; unlocks rating for everyone on it, bumps the driver's
  trip count once.
- `cancelled` — driver cancels, or the confirmation decision went unanswered
  (§5.1): **all live bookings auto-cancel and every rider is notified**.
- The driver's trip-wide **Arrived / Start / Complete** buttons are the same
  per-seat moves applied to every rider the move is legal for (§13); lifecycle is
  then derived. **On my way** is not one of these — it moves no seat. The order
  enforced is `posted → en_route → arrived → active → completed` (`Conflict`
  otherwise), with `en_route` skippable.
- A `posted` trip with **no bookings** may be edited by its driver (vehicle,
  route, departure, seats, price, conditions); `seats_left` resets to the new
  `seats_offered`. Once any non-cancelled booking exists — or it has left `posted`
  — editing is refused (`TripNotEditable`) and the driver must cancel instead
  (§6.5).
- A trip in the past with no `active` transition auto-expires (`cancelled`,
  system).

### 6.2 Confirmation — its own dimension

`gathering → confirmed` is decided only by §5.1's threshold against **secured
seats**, and the transition is **atomic and once**: confirm the trip, confirm
every eligible pending booking, write the audit row, notify the riders and the
driver, all in one commit (§13). Two bookings that cross the threshold together
must not confirm it twice.

**Once confirmed, a trip stays confirmed.** A rider cancelling back below the
threshold does **not** return it to gathering. Re-deriving would oscillate —

```
2 → 3 → confirmed → 2 → gathering → 3 → confirmed
```

— and a rider told their ride is confirmed must not be told otherwise because a
stranger changed their mind. Falling below the threshold is a signal to the driver
(who may cancel, §6.1) and to search (free seats are back on sale), not an
automatic downgrade.

### 6.3 Capacity

`full` when `seats_left = 0`. Hidden from search, existing bookings stand, the
trip runs and confirms normally. A seat returned by a cancellation makes it
available again — capacity is the one dimension that moves both ways.

### 6.4 Driver availability — one driver, one car

A driver is **engaged** while any trip of theirs is `en_route`, `arrived` or
`active`. A trip holding a slot in their day (`posted`, `en_route`, `arrived`,
`active`) **holds their schedule**. Cancelled and completed trips block nothing.

While engaged, the driver:
- **cannot be marked online.** `POST me/location` still stores the position —
  riders in the car are tracking it — but the online flag stays down however often
  the app reports itself available. It flips down by itself the moment the trip
  becomes `arrived`/`active`; coming back online afterwards is the driver's choice.
- **is not notified** about demand, and their demand board comes back empty — the
  board and the push agree.
- **cannot express interest in, or be selected for, a ride request**
  (`DriverOnActiveTrip`).
- **cannot publish or re-time a trip** (`DriverOnActiveTrip`).

Independently, two of one driver's departures may not fall within
`DriverTripConflictMinutes` (default **30**) of each other
(`DriverTripTimeConflict`) — the same envelope matching uses, because two trips
that close compete for the same driver at the same moment. Checked when
publishing, when re-timing (against every trip but itself), when materialising a
recurring occurrence (§11), and when a driver is selected for a request —
against **that request's own departure**, not against "now".

That distinction is why the clash is filtered **per request** rather than per
driver: a driver busy at nine is refused the nine o'clock request and still
offered the six o'clock one. Being *engaged* is the per-driver half — it empties
the board outright.

Availability is checked against single trips, generated recurring occurrences,
matched demand and active trips, in one place
(`Areas/Domain/Trips/DriverAvailabilityRules`, read through
`IDriverAvailabilityService`).

### 6.5 Editing a trip people depend on

A driver may edit a future trip only while **no bookings exist, it has not
started, and it is not matched/locked to a request**. Once passengers depend on
it, route/time/capacity changes require cancel-and-recreate or a controlled change
flow that re-asks every rider. **Never silently move a passenger's confirmed
trip** — a rider who agreed to 08:00 from Abdali did not agree to 09:00 from
Sweifieh.

---

## 7. Demand — the ride request

A **RideRequest** is a journey riders want and nobody is driving yet.

```
RideRequest
  Origin / Destination (+ addresses, route line)
  DepartAt          : datetime     the wanted departure
  TimeWindow        : minutes      how far either side is acceptable
  SeatsRequested    : int          sum of its participants' seats
  Conditions                       §7.2
  Status            : enum { Open, Matched, Cancelled, Expired }
  MatchedTripId     : id?          set exactly once, at formation (§8.4)

RideRequestParticipant             one per rider on the request
  RiderId, Seats, JoinedAt, Status : { Active, Left }
```

It has **no driver, no vehicle, no price, and no bookings** — a booking is a seat
on a trip, and there is no trip yet. Participation is its own record, and it
becomes a booking exactly once, at formation.

### 7.1 Creating one — and the earliest departure it may ask for

The rider gives origin, destination, wanted departure (plus an acceptable window)
and seats. The departure must be far enough out that a driver can realistically
serve it:

```
estimated_minutes  = distance_km(origin, destination) / AverageSpeedKmh * 60
minimum_lead       = estimated_minutes * seats                 (clamped)
earliest_departure = now + minimum_lead
```

A `depart_at` earlier than that is refused (`DepartureTooSoon`).

**Why seats multiply it.** A rider asking for four seats is not asking for a taxi
— they are asking a driver to *gather* four people and run the leg. The gathering
costs roughly one leg-time per seat, and a departure that leaves no room for it
produces a request no driver can actually serve, which is worse than no request at
all. One seat therefore asks only for a few minutes' lead; four seats push the
earliest departure hours out — deliberately.

- `AverageSpeedKmh` is admin-set (§20) so the estimate is tunable per city.
- Clamped to `[MinimumRiderTripLead, MaximumRiderTripLead]` — a floor so a 300 m
  hop is not instant, a ceiling so eight seats over a long intercity route do not
  demand next week.
- The same estimate produces the **arrival estimate** the rider sees and the
  **suggested price** an interested driver is shown (§8.2).

### 7.2 Conditions — who the riders will travel with

The same three fields as the driver's, pointed the other way:

```
RiderConditions                    on a request, and on the profile as a default
  DriverGenderPolicy : enum { Any, MaleOnly, FemaleOnly }
  CoRiderGender      : enum { Any, MaleOnly, FemaleOnly }
  MinAge / MaxAge    : int?                              on co-riders
```

- `DriverGenderPolicy` is a filter on search and a hard check at booking
  (`DriverNotEligible`). Held as a profile default so it is stated once, and
  overridable per request.
- On a request it also governs **who may serve it**: a driver who does not satisfy
  it never sees the request on their board and cannot be selected for it. The check
  runs at selection time as well as in the list query — a board is a cache.

### 7.3 Joining, and demand aggregation

**Riders join demand rather than duplicating it.** A second rider whose search
turns up a compatible open request joins it (§9, tier 3) instead of creating a
fourth near-identical row. A request that three riders have joined is a far more
attractive thing for a driver to answer than three requests for one seat each.

A rider may join if, and only if (`CanJoinRequest`, §10):
- route matches and direction agrees,
- the departure times are compatible within the aggregation tolerance,
- their own conditions are satisfied by the request,
- **every rider already on it still satisfies the joiner's conditions** —
  otherwise `RideRequestConditionsConflict`,
- the resulting `SeatsRequested` stays within `MaximumTripSeats`, the largest
  vehicle expected to serve it,
- they are not already a participant, and they are not the request's author
  joining twice.

Conditions therefore **intersect as the pool grows and never retroactively exclude
somebody already in it**. A pool whose membership can change under its members is
the one outcome to rule out.

`SeatsRequested` growth **does not** re-run §7.1 against the original departure:
the lead-time rule is a check at creation, and re-checking it would let a late
joiner invalidate a request everyone else already planned around.

**Automatic aggregation.** Separate requests that are compatible on origin,
destination, date, time window, seats and conditions *may* be presented to drivers
as one pool of N passengers (`AggregateDemand`), with the tolerance configurable
(§20). Requests whose requirements conflict are never merged. V1 behaviour: riders
aggregate themselves by joining, and the driver's board shows each request with its
participant count; automatic cross-request pooling is a later addition that must
not change any rule above.

**The author has no special power.** If they leave and other participants remain,
the request lives on without them; it closes only when the last one goes. There is
no author column at all — "who wrote it" is the participant who joined first, and
it buys them nothing. An author who could dissolve a pool other people planned
around is a trap, and authorship stops meaning anything once a trip is formed.

### 7.4 Demand lifecycle, expiry and close

```
open ──► matched      a driver was selected and a trip was formed (§8.4)
  │
  ├────► cancelled    the last participant left, or an admin closed it
  └────► expired      its own departure passed with no driver
```

- **A request stays open until its own departure.** It is not expired early for
  failing to attract a driver — giving the marketplace time to find supply is the
  entire point of creating it. Swept every 30 s; between the deadline and the next
  sweep the row still reads `open`, so **selection honours the clock, not the
  column** (`RideRequestNotOpen`).
- A request departing inside the notify window is **pushed immediately** to nearby,
  free, online drivers who satisfy its conditions. One departing later sits on the
  board **silently** and is pushed when it enters the notify horizon
  (`RiderTripNotifyLead`, e.g. 60 min before departure) — or never, if a driver
  takes it off the board first. A ride needed in twenty minutes has to wake drivers
  up; a request for Thursday must not ping every driver in town on Monday.
- **Every exit from `open` broadcasts a close** to every connected client: a small
  SSE control frame `{event: "rideRequestClosed", rideRequestId, reason}`.
  - It is **not a notification**: no inbox row, no push. A driver does not need to
    be told out-of-app that a request they never answered went away; they need the
    card gone.
  - It goes to **every open connection**, not a computed audience. The audience for
    the original push was "drivers near the origin *at that moment*", and by the
    time it closes they have moved, gone offline, or come online. The frame carries
    only an id, and a client not holding it ignores it.
  - Clients drop the card on receipt; a client that misses the frame loses it on
    the next list refresh.
- **A terminal request never reopens.** If a formed trip is later cancelled, the
  riders get a new request to create or trips to search; the matched one stays
  matched (§16.20).

### 7.5 A rider leaves demand

Leaving a request cancels their participation, returns their seats to
`SeatsRequested`, and closes the request if they were the last one. It is always
free — there are no payments, so there is nothing to forfeit. Interested drivers
are told the pool shrank; a request that drops below a driver's stated `minSeats`
simply leaves their board.

---

## 8. From demand to supply — interest, selection, formation

```
Open demand ──► driver interested ──► driver selected ──► trip formed ──► gathering/confirmed
```

### 8.1 Driver discovers demand

Drivers get a board and a search (§9.3) of open requests whose origin is inside
their radius, whose conditions they satisfy, and whose departure they are free for
(§6.4, asked about **that request's own departure**).

Ranking for the driver (§9.4) prioritises direct route match, departure fit,
**passenger count**, route efficiency, compatibility, distance to pickup, and
reliability where known — and the card says *why*: "3 passengers · directly on your
route · 2 km from pickup · departs in 45 minutes". An engaged driver must never be
shown demand they cannot realistically serve.

### 8.2 Interest and offers

```
POST ride-requests/{id}/interest   { vehicleId, pricePerSeat, message? }
```

**Interest is a marketplace signal, not a trip.** It means: *I am willing to serve
this demand, subject to the platform's matching and confirmation rules.* It
carries everything an offer needs — driver, vehicle, price per seat, departure
time, seats available, an optional message — so that the day riders choose between
drivers, nothing in the domain has to change.

```
DriverInterest status: interested → withdrawn | selected | rejected | expired
```

The driver's price sheet opens on the distance estimate from `FareBaseAmount` /
`FarePerKm` — the same figure already on the card — so they confirm a number they
have been looking at rather than inventing one. Backing out of the sheet is
**declining**, never offering at the default. Zero stays a real answer: a free seat
is a favour, not a missing field.

A driver may **withdraw** interest while the request is still open and they have
not been selected.

### 8.3 Selection — deterministic, and never "first click wins" by design

Exactly one interest becomes `selected`; every other live interest becomes
`rejected` and its driver is told.

Selection runs when the **selection window** closes:
`DriverSelectionWindowMinutes` (§20), measured from the first interest.

- **At 0 — the V1 default — the window is empty and the first interest is selected
  immediately.** That is today's first-come-first-served behaviour, unchanged in
  feel.
- **Above 0**, interests accumulate for that long and the winner is chosen by a
  deterministic ranking: eligibility → availability → route quality →
  departure-time fit → vehicle capacity → driver rating → price → completion
  history → a deterministic tie-break (earliest interest, then lowest id). Same
  inputs, same winner, every time.

First-come-first-served is therefore a **configuration value, not an architectural
invariant** — the thing v2 exists to avoid baking in. Riders choosing between
competing offers is the next step up from the same data, and needs no new domain.

**Concurrency.** Two drivers must never both become the active driver. Selection is
a conditional update on the request's row version; the loser changes no rows and is
refused `RideRequestNotOpen` (§13).

### 8.4 Trip formation

```
RideRequest + selected driver + vehicle  ──►  Trip
```

In one transaction:

1. create the `Trip` with the driver, vehicle, route, departure and offered price,
   `seats_offered = vehicle.seat_capacity`;
2. convert **each active participant into exactly one `Booking`** on that trip,
   carrying their seats — no duplicates, no second pass, nobody dropped;
3. carry the request's conditions onto the trip;
4. set `MatchedTripId` and move the request `open → matched`;
5. set the trip's confirmation: the participants are already secured seats, so a
   trip formed from demand whose secured seats meet `MinPassengers` is
   **confirmed at formation** (§8.5);
6. notify every participant — *a driver took your request, here is the price, here
   is the trip* — and broadcast the close frame (§7.4);
7. write `driver.select`, `trip.create` and one `booking.create` per participant to
   the audit log.

The remaining seats are **ordinary carpool seats**, searchable and bookable by
anyone (§9), because after formation this is an ordinary trip in every respect.
Nothing downstream may ask whether a trip came from demand or was published
directly — see §16.

### 8.5 There is no price handshake

A formed trip is the driver's in every respect, including its seats: the
participants become `confirmed` bookings outright.

The rider's answer to a price they dislike is to **leave** (§13). That is the whole
mechanism, and it is enough:

- Leaving is free and instant, and it hands the seat straight back to the trip.
- An accept step would ask riders to re-confirm something they already asked for.
  They created the demand; a driver is now serving it; the only new fact is the
  number, and a rider who will not pay it walks away.
- It removes a whole class of dead state — an unanswered hold, a countdown to floor
  against `depart_at`, a sweeper to release it, and a rider stuck holding a seat
  they never look at.

**If every rider leaves, the trip does not die.** It stays `posted` with a full car
of free seats: the driver has a real route and a real price, so search can offer
it. Deleting it would punish the driver for the riders' choice.

`pending` therefore has exactly one producer: a trip short of the passengers its
driver asked for (§5.1). A seat booked from search is `confirmed` immediately, and
so is a seat that came from formation.

### 8.6 You are never your own passenger

| Path | What stops it |
|---|---|
| booking a seat on a trip you drive | `CannotBookOwnTrip` |
| expressing interest in a request you are on | `CannotServeOwnRequest` |
| joining a request you are already on | `AlreadyJoined` |
| being selected for a request you are a participant of | selection skips you |
| seeing any of the above offered | search excludes `t.DriverId == me` and requests you participate in; the driver board excludes them too |

The filter and the refusal are both required and neither is redundant. The filter
is manners — a result you cannot take does not belong in your list. The refusal is
the rule, because a filtered list is a convenience and an API that trusted it would
let a stale screen through.

**The rule applies per occurrence** (§11): a driver with a recurring schedule
cannot ride as a passenger on an occurrence it generated, even if a client
accidentally exposes it.

---

## 9. Search — tiers, not one list

**Both sides search.** A rider states where they want to go and finds trips going
that way (§9.1); a driver states where they are about to drive and finds people who
want that journey (§9.3). Same two points, same corridor maths, opposite side of
the car.

### 9.1 The rider's search

Input: `origin`, `destination`, `when`, `seats`, Nearby/Anywhere.

```
1. Geocode origin + destination → coordinates.
2. TIER 1 — DIRECT.       Trips whose own origin AND destination are both inside
                          the radius of the rider's.
3. TIER 2 — ON THE WAY.   Trips whose ROUTE passes the rider: both of the rider's
                          ends lie within the corridor around the trip's route
                          line, in the right order along it.
4. TIER 3 — OPEN DEMAND.  Open ride requests on the same route the rider may join
                          (§7.3) instead of creating a duplicate. The card reads
                          "2 people are looking for this trip · 1 more needed".
5. TIER 4 — CREATE.       Offer to create a ride request (§7.1).
```

All four come back in **one response, labelled by tier**, and the home page renders
them as sections in that order. The rider is never sent to another screen for tier
3 — "somebody already asked for this, join them" is a search result. **A "no
results" state should be rare**; tier 4 is always available.

**Every tier is filtered by eligibility first, both ways** (§10) — the driver's
conditions on this rider, and this rider's conditions on the trip. **A tier is a
ranking band, never a relaxation**: nothing a rider cannot book appears further
down the list.

**Candidate rules, common to tiers 1 and 2**
- Only trips with `lifecycle = posted` and seats left are matchable: `en_route`,
  `arrived`, `active`, full and cancelled are all out. A rider added to a pickup run
  already under way has no way to tell the driver to turn around.
- `depart_at > now − BoardingGrace` (15 min). A grace rather than "now" because a
  posted trip a few minutes past its departure has not left — it is boarding, or
  the driver is late. Short enough that this morning's never-started trip is still
  excluded. A driver who has actually set off is excluded by lifecycle, not by the
  clock.
- `seats_left >= requested seats`.
- **The driver has to actually be around here.** Matching the rider's pickup against
  a *planned* origin alone makes a notional match: the pickup can be next door while
  the driver is an hour away. So when the driver has a **live fix**
  (`User.LastLocation`, inside `LiveFixWindowMinutes`, 30), that position must be
  inside the radius of the rider's origin too.
- **A missing or stale fix is ignored, not disqualifying.** A driver only reports
  while the app is running, so most trips for a future day carry an old fix or none;
  requiring one would empty search of every scheduled trip. One place decides what
  "live" means, and a position with no timestamp is not live.
- **The rider's `when` ranks; it does not exclude.** See §9.5. A trip with free
  seats that has not departed is discoverable whenever it leaves. Every search still
  states its acceptable window (`TimeMatchWindowMinutes`) for *aggregation and
  demand matching*, and that window is configuration — never an arbitrary ±30
  minutes hard-coded in each caller.
- A **gathering** trip is matchable and labelled (§5.1).

### 9.2 The corridor match (tier 2)

`Trip.Route` — a `LineString`, straight-line in the MVP — is what makes tier 2
possible at all. Route matching must consider **all six** of: pickup proximity,
destination proximity, corridor, direction, pickup-before-destination order, and
maximum detour. Proximity to the same road is **not** a match — that would pair
opposite directions.

- **Distance:** both rider ends within `CorridorMeters` of the route, measured **in
  the query**, where the geography operators answer in metres.
- **Direction and order:** project each end onto the route with an indexed line and
  require `index(origin) < index(destination)`. This is the direction check tier 1
  gets for free by matching both ends against the trip's own ends.
- **Detour bound:** the projections must not add more than `MaxDetourMeters` /
  `MaxDetourFraction` to the run. Without a bound, a rider 4 km off the middle of a
  300 km motorway route counts as "on the way".
- Computed over the same candidate pool in memory, for the same reason ranking is.

Straight-line routes make tier 2 approximate today. It gets strictly better the day
`Route` holds real geometry, and nothing above changes when it does — which is why
the corridor is expressed against `Route` and not against the endpoints.

### 9.3 The driver's search — who is going my way

The mirror of §9.1, and the driver's counterpart to publishing: instead of offering
seats and waiting, they state the journey they are about to make and see who
already wants it.

```
POST ride-requests/search   { origin, destination, when, seats, nearby,
                              minSeats, riderGenderPolicy, sortBy }

TIER 1 — GOING YOUR WAY.  Requests whose two ends are both inside the radius of
                          the driver's. They want the journey being made.
TIER 2 — ON YOUR WAY.     Requests whose two ends lie in the corridor around the
                          DRIVER'S route, in the driver's direction of travel.
                          These ask for a diversion, and the card names it.
```

The bands are the same idea pointed the other way, and they share one
`RouteGeometry`: a rider projects their own ends onto each driver's route, a driver
projects each request's ends onto their own. Two notions of "on the way" would
drift, which is why there is one.

**The direction check is the half that matters.** Two points can sit two hundred
metres from the driver's line while the people at them travel the other way, so the
pickup's fraction along the route must come before the drop-off's.

**Everything it returns, the driver must be able to serve.** A driver who is
unverified or has no car is refused outright rather than shown an empty list —
their application is the thing to fix; an engaged driver gets an empty list; a pool
bigger than their car, one whose riders ruled them out, one clashing with their
diary, and one they are riding on themselves are all absent.

Two filters are the driver's own, stated per search and written onto nothing:
**`minSeats`**, the smallest pool worth a diversion, and **`riderGenderPolicy`**,
who they will carry. A request that has stated the same condition passes on its
column; one that has stated nothing is judged on who is actually on it.

Narrow (5 km) by default, as on the rider's side. Wide was tempting — a driver
drives to the pickup where a rider walks to it — but at 50 km on a 67 km leg every
request in the region has both ends "near" the driver's, so everything becomes a
direct match and the second band stops meaning anything.

This is **not** the driver's board (§8.1), which stays. That one is push-driven and
local: it answers "who needs a lift around me, right now", off the driver's live
position and with no route at all. A driver planning Thursday's run to Irbid cannot
ask that question, which is the gap this fills.

### 9.4 Search must agree with booking

Any trip shown as bookable must pass the **same eligibility predicates** enforced
when booking (§10), and any request shown as joinable the same predicates enforced
when joining. *Search says yes, booking says no* is a bug unless the underlying
state changed in between. Search is a convenience; server-side validation is
authoritative.

### 9.5 Ranking

The default order is **walk plus wait**, normalised so the two can be added, with
the tier in front:

```
score = tier_penalty
      + (walk_km_at_both_ends / match_radius_km)
      + (minutes_from_wanted_departure / RankTimeScale)      # 60 min
```

A full match radius of walking costs the same as an hour off the wanted hour. That
is what lets a trip leaving tomorrow stay in the results while a trip leaving soon
sits above it — the wanted time became a ranking factor when it stopped being a
filter.

- `tier_penalty` is a constant per tier, not a weight to tune: **tiers cannot
  interleave**.
- Walk for a **tier-2** trip is measured to the **projected pickup point on the
  route**, not the driver's origin — that is the point the driver will actually
  pass.
- Computed **in memory over a candidate pool** (`CandidatePoolSize`, 200), not in
  the query: the geography operators only answer in metres *inside* a query, so a
  materialised entity's `Distance` returns degrees. The pool comes back ordered by
  proximity, which the database *can* do, and is re-ranked properly before the
  20-row cap.
- The rider may override with an explicit sort — **soonest, cheapest, best rated,
  shortest walk, most seats** — which runs in the query. Every sort tie-breaks on
  proximity then `depart_at`, so two identical searches cannot disagree.
- **Price is not part of the default score** (payments out of scope, §19) and must
  never dominate it; it is an explicit sort the rider chooses.
- A gathering trip is not penalised.

**Demand ranking for drivers** (§8.1) uses its own order: direct route match →
departure-time fit → passenger count → route efficiency → passenger compatibility →
distance to pickup → reliability where available.

---

## 10. Eligibility, in one place

Every path that puts a rider on a trip, or a rider into a pool, or a driver onto
demand runs the **same predicates** — search filtering, booking, joining,
expressing interest, selection, formation, and each schedule occurrence as it
materialises. Because conditions run both ways (§5.2, §7.2), so does the check:

```
CanRide(rider, trip) =                       # the driver's conditions on the rider
      (trip.GenderPolicy == Any || rider.Gender == required)
  &&  age(rider, trip.DepartAt) within [trip.MinAge, trip.MaxAge]
  &&  rider != trip.Driver
  &&  no live booking by rider on this trip
  &&  trip.SeatsLeft >= seats

WillRideWith(rider, trip) =                  # the rider's conditions on the trip
      (rider.DriverGenderPolicy == Any || trip.Driver.Gender == required)

CanJoinRequest(rider, request) =             # demand, both ways at once
      request.Status == Open
  &&  rider satisfies request.Conditions
  &&  every active participant satisfies the rider's own conditions
  &&  request.SeatsRequested + seats <= MaximumTripSeats
  &&  rider is not already a participant

CanServe(driver, request) =                  # interest and selection
      (request.DriverGenderPolicy == Any || driver.Gender == required)
  &&  driver is verified, has a vehicle, and is free at request.DepartAt
  &&  driver is not a participant of the request
  &&  vehicle.SeatCapacity >= request.SeatsRequested
```

A stated requirement is checked against a real profile value in **both**
directions: `Unspecified` on either side means refused, never assumed
(`ProfileIncomplete`).

These live in one shared domain service beside `DriverAvailabilityRules`. Copies of
these predicates in search, booking, join, interest and selection **will** drift,
and the drift shows up as a rider who can see a trip they cannot book, or a driver
holding a card they cannot answer.

---

## 11. Recurrence — schedules and occurrences

Either side may post a recurrence. Every trip and every request has exactly one
frequency: **single** or **recurring**.

```
Schedule
  OwnerId, OwnerRole            rider | driver
  Origin / Destination (+ addresses)
  Recurrence   : enum { Daily, Weekly, Monthly }
  DaysOfWeek   : flags          Weekly only
  DayOfMonth   : int?           Monthly only
  TimeOfDay    : TimeOnly       local, with the schedule's time zone
  StartDate, EndDate?           (or OccurrenceCount)
  Seats
  Conditions                    §5 for a driver's, §7.2 for a rider's
  PricePerSeat / VehicleId      driver-owned schedules only
  MinPassengers                 driver-owned schedules only
  IsPaused     : bool
```

### 11.1 A schedule is a generator, never a participant

**A schedule must not appear in search, booking, matching, confirmation, or any
status.** A worker materialises it into ordinary rows over a rolling horizon —
`Trip`s for a driver's schedule, `RideRequest`s for a rider's —
`ScheduleHorizonDays` ahead (e.g. 14).

```
Schedule → materializer → concrete occurrence → normal marketplace logic
```

The rest of the system needs no recurrence logic at all, and **a booking never
represents a series** (§16.6).

### 11.2 Occurrences are independent

Each occurrence has its own passengers, confirmation state, lifecycle and result.
One may confirm while another fails its threshold, is cancelled, is skipped, or
completes — without touching the others. Price is **per seat per occurrence**, as
the schedule states: not a subscription, not a per-series total.

### 11.3 Generation rules

- **Idempotent.** An occurrence is keyed `(ScheduleId, OccurrenceDateTime)`, so a
  worker that runs twice, or catches up after a day down, produces exactly one row
  per date.
- **Rolling.** The worker maintains the next `ScheduleHorizonDays` days.
- **Driver availability is checked per occurrence, at materialisation.** A schedule
  cannot pre-book a driver's calendar: an occurrence clashing with something they
  already have (§6.4) is skipped, and the driver is told which date and why.
- **Calendar edges are decided, not discovered.** A `Monthly` schedule on the 31st
  falls to the **last valid day** of shorter months; a `TimeOfDay` a DST shift
  deletes moves **forward** to the next valid local time; timezone changes and the
  end-date boundary are evaluated in the schedule's own zone. Each is written down
  because each otherwise produces a silently missing trip.

### 11.4 Joining and cancelling occurrences

- **V1: a rider chooses the specific occurrences they want.** "Join all upcoming"
  is a later addition; the domain must never make the schedule itself the booking.
- A rider may cancel **one** occurrence without affecting their participation in
  the others.
- A driver may cancel **one** occurrence without deleting the schedule.
- Cancelling the **schedule** affects only future, unstarted occurrences.
  Completed ones are immutable; booked ones follow the schedule-cancellation policy
  and **every affected rider is notified** per occurrence.

### 11.5 Editing a schedule

```
Not generated yet      → the new schedule rules apply
Generated, no bookings → may be updated, on explicit request
Has bookings           → the occurrence is preserved, untouched
```

`IsPaused` stops generation and leaves what exists standing. Deleting a schedule
cancels only its future, unbooked occurrences. A schedule edit must **never**
silently rewrite an occurrence somebody has joined (§6.5).

### 11.6 Recurring notifications

Notifications refer to a **concrete occurrence**, never a series:

> Your Monday, 14 September trip is confirmed.

not

> Your recurring trip is confirmed.

A driver serving demand takes occurrences **one at a time**. Claiming a whole
series would pre-book their calendar and needs break and cancel rules of its own —
explicitly out of scope (§19).

---

## 12. Booking lifecycle — the driver tracks each seat

```
pending ──► confirmed ──► arrived ──► in_progress ──► completed
   │            │            │        (aboard)        (dropped off)
   │            │            │
   │            └────────────┴──► no_show      (driver waited, rider never came)
   └─────────────────────────┴──► cancelled    (rider gave the seat back)
```

A carpool collects its riders one at a time, so **the driver moves each booking
separately** (`PUT trips/{id}/bookings/{bookingId}/status`). The trip's lifecycle is
then derived from all of its seats (§6.1).

**Rules**
- **`pending` means a seat is held but nobody is committed yet**, and it has exactly
  one producer: a trip whose threshold nobody has met (§5.1). It resolves without
  the rider doing anything. Its seats count against `seats_left`, and its rider is
  **not** yet entitled to the driver's phone number or live position — that stays a
  `confirmed`-and-later privilege (§15).
- **Booking a trip whose price is on the table is instant**: created `confirmed`,
  `seats_left` down in the same commit, no driver approval anywhere. A rider who
  books from search read the price before they tapped.
- A booking is refused if `seats_left < requested seats`, and the decrement is
  race-safe — see §13.
- Only the trip's **own driver** may move a seat, and only along the order above
  (`BookingStatusNotAllowed` otherwise): a rider can be dropped off only once
  aboard, and marked a no-show only while they are not. A `pending` seat cannot be
  moved past `confirmed` at all — the trip has not gathered the riders it needs, so
  it is not going anywhere yet.
- **Live** seats are `pending`, `confirmed`, `arrived`, `in_progress`;
  `completed`, `cancelled` and `no_show` are settled.
- **Cancellation windows:**
  - Rider cancels before departure → seats returned to the trip. The trip does not
    fall back to gathering (§6.2).
  - Driver cancels the trip → all its live bookings auto-cancel and every rider is
    notified.
  - `no_show` before the trip leaves → the seat goes back on the trip, exactly as a
    rider's own cancel does. Once it has left, the seat is spent.
  - **After departure a rider cannot cancel a seat as if unused** — the driver
    controls the seat lifecycle from `en_route` onward.
- Each move notifies **only the rider whose seat moved** — telling a rider still at
  the kerb that "your trip has started" because someone else boarded would be a lie,
  and their tracking rail reads these events. The two trip-wide moves that are *not*
  per-seat — the threshold confirming, and the trip cancelling for want of riders —
  notify everybody once.
- Rating unlocks on `completed` only; a `no_show` rates nobody (§14).
- No fees or charges on cancel or no-show (§19).

---

## 13. Concurrency and idempotency

### 13.1 The four races

Genuine races, and a transaction does **not** settle any of them: SQL Server reads
at READ COMMITTED, so two callers see the same row and neither blocks the other's
read.

| The race | What would happen |
|---|---|
| Two riders take the last seat | Both read `seats_left = 1`, both pass the check, both write `0` — one seat sold twice |
| Two riders consume the last demand capacity | Both join, `SeatsRequested` exceeds `MaximumTripSeats`, no car can serve it |
| Two drivers are selected for one request | Both read `open`, both form a trip — the riders get two drivers |
| Two bookings cross the confirm threshold | Both read the same secured-seat count, and either both confirm the trip or neither does |

Trip and RideRequest therefore both carry a **row version** (mapped
`IsRowVersion()`), which makes every `UPDATE` conditional on the value that was
read. The loser changes no rows, gets a `DbUpdateConcurrencyException`, and its
whole transaction rolls back — including, for a selection, the trip and bookings it
had already staged.

**Losing is not the same as being refused.** Two riders booking a four-seat trip at
the same instant both deserve a seat; the loser only needs to look again. So the
seat paths **retry from a fresh read** (`MaxAttempts`, 3), and the refusal — when
there is one — comes out of the ordinary checks on that fresh read
(`TripNotBookable`, `NoSeatsLeft`). Only sustained contention across every attempt
returns `Conflict`, which is honest: there may well be a seat, we simply never got
to take it.

Retries need the change tracker cleared between attempts (`IUnitOfWork.Detach()`),
or the stale entity is handed straight back and the retry fails forever on the same
version.

### 13.2 Idempotency

**A client retry must never create a second marketplace action.** Every retryable
operation carries an idempotency strategy, and a duplicate from the *same actor*
returns the existing result rather than an error:

| Operation | Duplicate from the same actor returns |
|---|---|
| `CreateRideRequest` | the request they already created for that route/time |
| `JoinRideRequest` | their existing participation |
| `ExpressInterest` | their existing interest |
| `SelectDriver` / form trip | the trip that was already formed |
| `BookTrip` | their existing booking on that trip |
| `CancelBooking` / `LeaveRequest` | success, already cancelled |
| `ConfirmTrip` | success, already confirmed — **never a second notification** |
| `MaterializeOccurrence` | the occurrence already keyed to that datetime (§11.3) |

Deliberately scoped to *that* actor: **another** driver asking about a request that
is already matched is a loser, not a repeat caller, and gets `RideRequestNotOpen`.

---

## 14. Notifications

Marketplace events are notified, and every notification is **event-driven,
idempotent, and about a concrete occurrence** (§11.6).

**Rider** — request created · another rider joined · a driver is interested · a
driver was selected · price/offer received · trip confirmed · trip cancelled ·
driver approaching · driver arrived · trip started · trip completed · rating
available.

**Driver** — matching demand found · a rider joined your trip · demand reached the
minimum passengers · confirmation decision needed (§5.1) · trip confirmed · a rider
cancelled · trip cancelled · trip reminder · passenger no-show · your interest was
not selected.

**Rules**
- **Exactly once per event.** Confirmation notifies each rider once, in the same
  commit that confirms (§6.2); a retried confirm notifies nobody again (§13.2).
- Stored **bilingually** (en + ar) because clients send a fixed `Accept-Language`.
- A close frame is **not** a notification (§7.4): no inbox row, no push.
- A push says *that* something happened and carries no free text a lock screen was
  never going to hold.

---

## 15. Contact details and live location

Contact information and live tracking unlock **only on a confirmed trip**. A
`pending` booking and an open ride request do **not** expose the other party's phone
number or position: uncertain demand does not grant ride-level access to anybody.

Once a booking is `confirmed` or later, the rider and the driver may each reach the
other, and the rider sees the driver's live position. The exact mechanics belong in
the security layer; the business rule is the sentence above.

---

## 16. Ratings

- Unlocked **only after a booking reaches `completed`** — per concrete occurrence,
  so a recurring rider rates each completed occurrence separately.
- **Two-way:** rider rates driver, driver rates rider (1–5 stars + optional
  comment). Each side rates **once** per booking.
- A cancelled or `no_show` booking unlocks nothing.
- `rating_avg` / `rating_count` update on each new rating and feed ranking (§9.5)
  through the explicit best-rated sort, and driver selection (§8.3).

---

## 17. Profile rules

- Phone is the **identity** — unique and verified; changing it requires
  re-verification.
- **Role switch** (rider ↔ driver) is allowed anytime; publishing a trip needs a
  vehicle, while being selected against demand requires `verified` status.
- **Gender and date of birth are profile data with rules attached** (§5.2, §7.2):
  they are what conditions are checked against, so a missing one is what refuses a
  restricted trip. Asked for at the point a trip needs them, not demanded at signup.
- **Ride-with preferences** — the rider's default `DriverGenderPolicy` and co-rider
  conditions — live here and pre-fill every request and search.
- **Saved places** (Home / Work / favourites) pre-fill search.
- **Preferences:** language (Arabic / English), push/SMS toggles.
- **Safety:** an emergency contact can be stored.
- **Delete/deactivate:** account goes `deleted`/`disabled`; audit history is
  retained, immutable.
- **No payment methods or wallet** anywhere (§19).

### Signing out ends more than the token

Revoking the `UserLogin` row stops the API calls. Three other things outlive a
session unless they are ended explicitly:

- **Presence.** A driver stays `IsOnline`, so the demand fan-out goes on counting
  and targeting a handset that has signed out, and the CMS shows a phantom available
  driver. Logout clears it — but only when this was the account's **last** live
  session, so a driver signed in on a second handset is not knocked offline
  mid-shift.
- **The SSE stream.** It authenticates once, at connect, so a revoked session's
  socket would keep delivering that account's notifications. Connections carry the
  session key they were opened with, and revoking a session closes exactly its own
  streams.
- **Anything cached on the device.** The client tears down in one place rather than
  per screen, and clears the session regardless of what the API answered — an
  unreachable server must not leave someone signed in after they tapped Log out.

Every revocation path runs this, not just the Log out button: refresh-token reuse
detection, an expired refresh token, and a disabled account all revoke the same way.

---

## 18. Complaints & suggestions

The support desk's one inbox, holding both kinds of message a user might send:
something went wrong (**complaint**) or something could be better (**suggestion**).
One entity carries both — the pipeline is identical, and the kind changes how it is
read, not how it is handled.

**Lifecycle** — `new → in_review → resolved | dismissed`. The user sees the status
verbatim, so it is worded as an answer to "what is happening to my complaint?".
`dismissed` sits next to `resolved` deliberately: "we read it and are not acting on
it" is a real outcome, and calling that resolved would be a lie the user can spot.

**The rules that are not obvious from the CRUD:**

- **One exchange deep.** The user writes, the desk answers, the row closes. A
  back-and-forth belongs in a messaging feature; a single `reply` column pretending
  to be a conversation would let replies overwrite each other.
- **A referenced entity must be the user's own.** Support may reference a Trip, a
  Ride Request, a Booking, a Schedule or an Occurrence — and only one the user
  drives, participates in, or holds a seat on. The id ends up in front of an admin
  as context, so an arbitrary one would attach a complaint to a stranger's ride and
  would answer "does trip N exist?" for anyone who asked.
- **The limit is queue depth, not a rate.** At most five *open* submissions per
  account. A genuine bad day can honestly produce three complaints in ten minutes,
  which a per-minute cap would block while still letting a script file hundreds over
  a day. The desk answers and the slot comes back.
- **No language pair, unlike the FAQ.** FAQ copy is published to everyone and ships
  in both languages; this is one person's own words answered by one person. The
  submission records the language the user was writing in so the desk answers in the
  one they will read.
- **The admin never edits the user's text.** Only `status` and `reply` are writable,
  and there is no create — a submission belongs to whoever wrote it. Deletion is
  soft.
- **Only a real answer notifies.** `new → in_review` is desk bookkeeping; notifying
  on it would train the user to ignore the one notification that carries an actual
  answer.

All support actions are audited.

---

## 19. Audit

- **Every mutating action and every auth event** writes one immutable `AuditLog`
  row: who (actor), what (action), which entity, `before`/`after`, IP, time.
- Actions include: `auth.login`, `trip.create`, `trip.update`, `trip.cancel`,
  `trip.confirm`, `request.create`, `request.join`, `request.leave`,
  `request.cancel`, `request.expire`, `driver.interest`, `driver.withdraw`,
  `driver.offer`, `driver.select`, `booking.create`, `booking.confirm`,
  `booking.cancel`, `schedule.create`, `schedule.update`, `schedule.pause`,
  `occurrence.create`, `rating.create`, `profile.update`, `vehicle.add`, and every
  admin action.
- The log is **append-only** — never updated, never deleted.
- **Reads are not audited** (only changes and auth), to keep it meaningful.
- Sensitive values (OTP codes, document contents) are **redacted**, not stored raw.
- Powers the admin activity timeline, dispute resolution, and the user's activity
  view.

---

## 20. Configuration — one place, no scattered constants

Marketplace rules are admin-set, not compiled in. **Do not hard-code any of these
in application code, and never repeat a "±30 minutes" rule at a call site.**

| Key | What it governs |
|---|---|
| `MatchRadiusMeters` | tier-1 proximity, both directions (§9) |
| `CorridorMeters` | tier-2 corridor width (§9.2) |
| `MaxDetourMeters` / `MaxDetourFraction` | the detour bound (§9.2) |
| `TimeMatchWindowMinutes` | the acceptable departure window for matching and aggregation (§7.3, §9.1) |
| `DemandAggregationToleranceMinutes` | how far apart two requests may be and still pool (§7.3) |
| `MinimumPassengersDefault` | the seeded threshold, recommended 3 (§5.1) |
| `MaximumTripSeats` | the largest pool a single vehicle is expected to serve (§7.3) |
| `MinimumRiderTripLead` / `MaximumRiderTripLead` | the clamp on the lead-time rule (§7.1) |
| `AverageSpeedKmh` | the leg-time estimate behind lead time, ETA and suggested price (§7.1) |
| `ScheduleHorizonDays` | the rolling materialisation horizon (§11.3) |
| `DemandExpiryGraceMinutes` | how long past departure a request is swept (§7.4) |
| `RiderTripNotifyLead` | when a future request starts pushing to drivers (§7.4) |
| `ConfirmationCutoffMinutes` | the final decision point before departure (§5.1) |
| `ConfirmationDecisionLeadMinutes` | how much warning the driver gets (§5.1) |
| `DriverSelectionWindowMinutes` | 0 = first interest wins; above 0 = competing offers (§8.3) |
| `DriverTripConflictMinutes` | the driver's departure envelope (§6.4) |
| `BoardingGraceMinutes` | how long a departed-but-unstarted trip stays matchable (§9.1) |
| `LiveFixWindowMinutes` | how fresh a driver position must be to count (§9.1) |
| `CandidatePoolSize` | how many rows are re-ranked in memory (§9.5) |
| `RankTimeScale` | minutes that cost as much as one match radius of walking (§9.5) |
| `FareBaseAmount` / `FarePerKm` | the suggested-price estimate (§8.2) — shipped at 0.50 + 0.10/km per seat, a cost-share, not a taxi fare |
| `ScheduledSelectionWindowMinutes` | how long planned requests collect offers (§26.2) |
| `RiderOfferChoice` | whether riders may pick an offer themselves (§26.2) |
| `RequireSharedTermsAcceptance` | an offer must agree the trip is shared (§26.1) |
| `FreeCancelGraceMinutes` / `LateCancelLeadMinutes` | what a cancellation costs (§26.4) |
| `ReliabilityWarnPoints` / `ReliabilitySuspendPoints` / `ReliabilityWindowDays` / `SuspensionDays` | when a record warns, and pauses instant work (§26.4) |
| `BoardingCodeRequired` | boarding needs the rider's code (§26.6) |
| `EmergencyNumber` / `ShareBaseUrl` | the SOS button and trip links (§26.6) |

Every value is clamped to a sane range on write, and the ones the clients draw
clocks from are on the wire to the app and the CMS.

---

## 21. Invariants, validation and edge cases

### 21.1 The twenty rules that must never be violated

1. A trip with a driver is **supply**. A rider request without a driver is
   **demand**.
2. Demand may be joined by multiple compatible riders.
3. Demand must not be duplicated unnecessarily — joining beats creating (§7.3).
4. A concrete trip may carry multiple riders.
5. A recurring schedule is **not** a booking, and never matches anything.
6. Every recurring occurrence is independent.
7. A rider can cancel one occurrence without cancelling the rest.
8. A driver can cancel one occurrence without deleting the schedule.
9. Minimum-passenger confirmation is based on **secured seats**, not booking rows.
10. Gathering trips stay visible in search.
11. Search and booking use the **same** eligibility predicates.
12. Driver availability prevents impossible overlapping commitments.
13. Self-booking and self-matching are forbidden, on every path and every
    occurrence.
14. Seat allocation is concurrency-safe.
15. Driver selection is concurrency-safe — exactly one active driver per request.
16. Threshold confirmation is concurrency-safe, atomic, and happens once.
17. Every important mutation is audited.
18. Client retries are idempotent.
19. A cancelled or expired entity never returns to an active state.
20. **Nothing downstream may branch on how a trip came about.** After formation a
    trip from demand and a published trip are the same thing: one search index, one
    booking rule, one ranking. There is no `Source` / `OfferedBy` column on `Trip`,
    deliberately — the only record is a status-history row that reporting reads and
    the product never reads. Adding such a flag is how one trip type quietly becomes
    two sets of rules that must be kept in step.

### 21.2 Validation

- Origin and destination **must differ** and both must geocode.
- `depart_at` must be in the future when publishing a trip, and beyond the
  lead-time floor when creating a request (§7.1, `DepartureTooSoon`).
- `seats_offered <= vehicle.seat_capacity`; requested seats `>= 1`;
  `MinPassengers <= seats_offered`.
- A rider cannot book their own trip; a driver cannot serve their own request
  (§8.6).
- A driver cannot hold two rides at once: none while engaged, and never two
  departures inside `DriverTripConflictMinutes` (§6.4).
- A rider cannot double-book the same trip, or hold two participations on the same
  request.
- A condition without the profile data it checks refuses the action
  (`RiderProfileIncomplete`, `DriverNotEligible`) — it never assumes.
- A request cannot grow past `MaximumTripSeats`, and a join that would exclude an
  existing participant is refused (`RideRequestConditionsConflict`).
- A trip that is matched or has bookings cannot be edited (§6.5).

### 21.3 Acceptance criteria

**Search** — direct trips first; related trips in their own band; compatible demand
in its own band; create-demand always available; no impossible or self matches.

**Demand** — a rider can create it; multiple compatible riders can join;
incompatible riders cannot; it does not disappear early; it attracts drivers;
duplicate driver selection is impossible.

**Supply** — a driver can publish multiple valid trips; cannot publish conflicting
ones; cannot exceed vehicle capacity; a trip is searchable according to its
lifecycle.

**Confirmation** — a trip can stay gathering; secured seats are displayed; reaching
the threshold confirms atomically; notifications fire exactly once; confirmation
never happens twice.

**Recurrence** — single and recurring both work; selected days/times generate
occurrences; occurrences are independent; materialisation is idempotent; schedule
changes never rewrite booked occurrences; one occurrence cancels alone; one can
complete while another gathers.

**Booking** — seat allocation is race-safe; no double-booking; cancellation releases
seats where the rules permit; the driver controls the ride-stage transitions.

**Reliability** — every mutation audited; retries do not duplicate; invalid
transitions rejected; terminal entities never reactivate.

### 21.4 Test scenarios

| # | Scenario | Expected |
|---|---|---|
| 1 | Driver publishes A→B; rider searches A→B and books | in tier 1; booking created; seats decremented |
| 2 | Rider searches A→B, nothing suitable | tier 4: create-request CTA |
| 3 | Rider A creates a request, rider B joins | one request, two participants — not two requests |
| 4 | Driver searches demand and expresses interest | interest recorded; **no trip created** |
| 5 | A suitable driver is selected | one trip formed; every participant attached as one booking |
| 6 | `MinPassengers` 3, secured 2, then +1 | gathering, then confirmed |
| 7 | Two riders take the last seat | exactly one allocation succeeds |
| 8 | Two drivers are selected at once | exactly one succeeds; the other gets `RideRequestNotOpen` |
| 9 | Sun+Mon+Wed 08:00 schedule, materialised twice | one schedule, three weekly occurrences, no duplicates |
| 10 | Rider on three occurrences cancels one | one cancelled, two unchanged |
| 11 | Schedule time changed | ungenerated occurrences use the new time; booked ones unchanged |
| 12 | Driver of trip A books trip A | rejected (`CannotBookOwnTrip`) |
| 13 | Confirmed trip drops below the threshold | stays confirmed; seats back on sale (§6.2) |
| 14 | `BookTrip` retried with the same idempotency key | one booking, success both times |

---

## 22. Where these rules live

v2 is implemented. This is the map from a rule to the file that owns it — the
place to change it, and the place a change will break a test.

| Rule | Owner |
|---|---|
| Who may share a car (§5.2, §7.2, §10) | `Areas/Domain/Trips/RiderEligibilityRules` — one predicate, read by search, booking, joining and interest |
| The passenger threshold and its clock (§5.1) | `Areas/Domain/Trips/TripConfirmationRules` + `Services/Trips/TripConfirmationService` (+ `TripConfirmationWorker`) |
| Confirmation as its own dimension (§6.2) | `Trip.ConfirmedAt`, stamped once in `TripConfirmationService.Commit` |
| Lifecycle from the seats (§6.1) | `Areas/Domain/Trips/TripStatusRules` — `Derive` never returns a capacity |
| Capacity (§6.3) | `Trip.IsFull`, derived from `SeatsLeft`; `TripStatusRules.IsOpenForSeats` is the lifecycle half |
| One driver, one car (§6.4) | `Areas/Domain/Trips/DriverAvailabilityRules` |
| The lead-time rule (§7.1) | `Areas/Domain/RiderTrips/RiderTripRules` — mirrored in the app so the form warns before the server refuses |
| Demand: create, join, leave, board, sweeps (§7) | `Services/RideRequests/RideRequestService` (+ `RideRequestSweepWorker`) |
| Interest, selection and trip formation (§8) | `Services/RideRequests/DriverInterestService` + `Areas/Domain/RideRequests/DriverSelectionRules` |
| The rider's search, tiers 1–3 (§9.1–9.2) | `Services/Search/SearchService` + `Shareds/Extensions/RouteGeometry` |
| The driver's search (§9.3) | `Services/Search/DemandSearchService` — same `RouteGeometry`, roles swapped |
| The matching envelope (§9, §20) | `Shareds/Constants/MatchRules` + `AppConfiguration` |
| Recurrence (§11) | `Areas/Domain/Schedules/RecurrenceRules` + `Services/Schedules/TripScheduleService` (+ `ScheduleMaterialiserWorker`) |
| Seat moves (§12) | `Areas/Domain/Bookings/BookingStatusRules` |
| Races and retries (§13) | `Shareds/Constants/ConcurrencyRules`, row versions on `Trip` **and** `RideRequest` |
| Instant vs scheduled selection, riders choosing (§26.2) | `DriverSelectionRules.DecideAt` + `DriverInterestService.ChooseOffer` / `SelectDue` |
| Seats opened, conditional accept (§26.1, §26.3) | `DriverInterestService.Form` |
| Route alerts and request watches (§26.5) | `Services/Marketplace/DemandAlertService` — called from `RideRequestService.Create` / `Join` |
| Cancellation cost, pauses, waivers (§26.4) | `Areas/Domain/Marketplace/ReliabilityRules` + `Services/Marketplace/ReliabilityService` |
| Riders back on the market (§26.4) | `Services/Marketplace/DemandRecoveryService` — called from `TripService.Cancel` |
| Agreements (§26.1) | `Services/Marketplace/AcknowledgementService` |
| Boarding codes, SOS, trip links (§26.6) | `BoardingCodes` + `TripService.SetBookingStatus` + `Services/Safety/SafetyService` |

**The sweepers.** Hosted services share `Shareds/Hosting/PeriodicWorker`, which owns
the timer, the per-pass DI scope, the catch-up pass on startup and the rule that a
failed pass costs one pass rather than the worker. Each subclass is then only an
interval and one method. `RideRequestSweepWorker` runs notify → select → expire in
that order, so a request that entered the push horizon and departed inside one
interval still reaches somebody, and a window that closed in the same tick is
decided before the departure clock voids it.

### 22.1 How the demand split was migrated

Two migrations carry v2's schema, and both do real work beyond adding columns:

- **`TripConfirmationDimension`** adds `Trips.ConfirmedAt` and backfills it from
  each trip's earliest committed booking — not from `now`, which would date every
  historical confirmation to deploy day — then rewrites `Full → Posted` in both
  `Trips` and `TripStatusHistories`.
- **`RideRequestsAsTheirOwnObject`** creates the three demand tables and moves the
  live rows: every `Trips` row still at `AwaitingDriver` becomes a `RideRequest`,
  its `Pending` bookings become participations, and the trip, its bookings and its
  history rows are deleted. A temporary `LegacyTripId` column carries the mapping
  through the move and is dropped at the end.

**The id changes at that boundary, and that is the price of v2.** A request and the
trip it becomes are two rows. `RideRequest.MatchedTripId` is the only thread across
it, and three places follow it: the matched notification carries `tripId`, the
rider's waiting screen hands over on that id, and `RideRequestRow.MatchedTripId`
carries it to every client list. The down-migration deliberately does **not**
reconstruct the old trips — rebuilding them would mint new `Trips` ids, so the rows
coming back would not be the rows that left.

`TripStatus.AwaitingDriver` and `TripStatus.Full` are both retired but still
present, marked obsolete, because historical `TripStatusHistories` rows carry them.
Nothing writes either.

### 22.2 The shipped configuration is v1's behaviour

Two settings decide how much of v2 a marketplace actually feels, and both ship at
the value that preserves what drivers already understand:

- `DriverSelectionWindowMinutes = 0` — the first driver to offer is selected
  immediately and gets a trip in the same round-trip, exactly as the old claim did.
- `MinimumPassengersDefault = 3` — the one deliberate behaviour change, because a
  driver who names no threshold was previously taken to mean "one passenger will
  do", which they had not said.

Raising the window turns the same data into competing offers ranked by
`DriverSelectionRules.Best`. Nothing else changes: not the domain, not the clients,
not a single other rule in this document.

### 22.3 Domain ownership

```
Domain/Trips           Trip · TripStatusRules · TripConfirmationRules
                       DriverAvailabilityRules · RiderEligibilityRules
Domain/RideRequests    RideRequest · RideRequestParticipant · DriverInterest
                       DriverSelectionRules
Domain/Schedules       TripSchedule · RecurrenceRules
Domain/Bookings        Booking · BookingStatusRules
Services/RideRequests  RideRequestService · DriverInterestService
Services/Search        SearchService (rider) · DemandSearchService (driver)
Workers                RideRequestSweepWorker · ScheduleMaterialiserWorker
                       TripConfirmationWorker
```

**API surface.** `api/v1/ride-requests` carries the whole demand marketplace:
`POST /` · `GET mine` · `GET {id}` · `POST {id}/join` · `POST {id}/leave` ·
`POST search` · `GET nearby` · `POST {id}/interest` · `DELETE {id}/interest`.
Trips, bookings and schedules are unchanged.

---

## 23. UX language

The interface must distinguish supply from demand in words, not only in layout:

| State | Says |
|---|---|
| Supply | *Available trip* |
| Gathering | *2 of 3 passengers secured · 1 more needed* |
| Demand | *2 people are looking for this trip* |
| Joinable demand | *Join their request* |
| Driver interest | *A driver is interested* |
| Confirmed | *Trip confirmed* |

**Do not show "Confirmed" until the confirmation rule is actually satisfied, and do
not hide the uncertainty.** Avoid "Requested" for a rider who already holds a
secured seat — it reads as if nothing has happened.

---

## 24. Out of scope (now)

- **Payments, wallet, fares, payouts, cancellation fees** — the price is display-only.
- Automated or surge pricing, promo codes, subscription billing.
- Multi-driver trips, or splitting one passenger journey across drivers.
- A driver claiming an entire recurring series permanently.
- Full route optimisation and real road geometry (the corridor is straight-line
  today, §9.2).
- Money as a consequence for leaving a trip (a fee). The non-monetary record —
  points, a pause on instant work — is in scope: §26.4.
- A live panic line staffed around the clock: the SOS button dials the public
  emergency number and queues the incident for the admin team (§26.6).

**The architecture must not make these impossible**, and none of them should need a
domain change: dynamic pricing, demand forecasting and heatmaps, corporate and
shared commuting groups. (Rider choice between offers and reliability scores,
listed here before, are now implemented — §26.)

---

## 25. The model in one picture

```
                    Wanes Marketplace
                           │
             ┌─────────────┴─────────────┐
          SUPPLY                       DEMAND
       Driver Trip                 Ride Request
             │                     more riders join
             └─────────────┬─────────────┘
                       MATCHING
                           │
                    Driver Interest
                           │
                    Driver Selection
                           │
                    Trip Formation
                           │
                  Passenger Gathering
                           │
                Minimum Seats Reached
                           │
                       CONFIRMED → EN_ROUTE → ACTIVE → COMPLETED

Single    → one concrete occurrence
Recurring → Schedule → Occurrences → each enters the same lifecycle
```

> **Wanes is not only a ride-booking application. It is a marketplace that
> continuously connects transportation supply with transportation demand.**

---

## 26. The shared, scheduled marketplace

Wanes is not a taxi app. The product is **seats on journeys planned ahead**; a
ride needed within the hour is served, but it is the fallback. Everything in this
section follows from that, and from one fact the driver and the riders must both
hold: **a Wanes trip is shared.**

### 26.1 Agreeing the trip is shared

- **Riders** agree when they post a request, join one, or book a seat; the
  agreement is stamped on the participation / booking (`SharedTermsAcceptedAt`).
  Older clients that send nothing are not refused.
- **Drivers** agree on every offer (`ExpressInterestInput.AcceptSharedTrip`).
  While `RequireSharedTermsAcceptance` is on (the default) an offer without it is
  refused `SharedTermsNotAccepted`. The accept sheet shows the split — riders now,
  seats in the car, seats left open, total now and if full — before the box arms.
- **Seats opened.** The driver chooses how many seats the trip carries
  (`SeatsOffered`), from the riders' own up to the car's capacity. The rest stay on
  sale until departure, like any trip's (§8.4). Fewer than the pool, or more than
  the car, is `InvalidSeatsOffered`.
- **Versioned agreements** (safety notes, shared-ride terms) are recorded per user,
  kind and version (`UserAcknowledgement`); the app asks again when the version
  moves, and a new device picks the old agreements back up.

### 26.2 Instant versus scheduled work

When a request's first offer arrives, its decision time is stamped
(`RideRequest.DecideAt`, from `DriverSelectionRules.DecideAt`):

| Request leaves… | Window | Behaviour |
|---|---|---|
| within `InstantHorizon` (1 h) of the first offer | `DriverSelectionWindowMinutes` (0) | first offer wins on the spot, as in v1 |
| later | `ScheduledSelectionWindowMinutes` (20) | offers collect; riders may compare |

A scheduled decision never lands inside `DecisionMargin` (30 min) of departure.
While the window is open:

- the request's riders see the offers (`GET ride-requests/{id}/offers`) — price,
  rating, completion rate, trips, car, seats offered, and whether the offer is
  conditional — and **may pick one** (`…/offers/{interestId}/choose`) while
  `RiderOfferChoice` is on. The pick forms the trip at once, if the driver is still
  free; the other offers are rejected and told.
- anything they leave is decided by `SelectDue` at `DecideAt`, with the ranking of
  §8.3 — which now weighs the driver's **completion rate** after rating.

### 26.3 The conditional accept

A driver may accept "only if it reaches N" (`MinPassengers`). Above the pool's
seats, the trip forms **gathering**: its threshold is N, its bookings are pending,
and the riders are told a driver will take the ride once N are in
(`RideRequestMatchedGatheringRider`). From there it is an ordinary gathering trip —
§5.1's prompt and cutoff decide it. A condition the pool already meets confirms at
formation.

### 26.4 Cancellations and reliability

There are no fees, so the record is the lever. Each cancellation of a trip is
classified (`ReliabilityRules.ClassifyDriverCancel`):

| Situation | Kind | Points |
|---|---|---|
| nobody holds a live seat | free | 0 |
| within `FreeCancelGraceMinutes` (3) of accepting, and not close to departure | free | 0 |
| within `LateCancelLeadMinutes` (120) of departure, or the trip already under way | late | 2 |
| otherwise | counted | 1 |

- **A reason is required** once riders depend on the trip (`CancelReasonRequired`).
  Vehicle problems, safety concerns and emergencies are **flagged for review**; an
  admin may **waive** an entry, which takes it off the scales without deleting it.
- **The preview** (`GET trips/{id}/cancel-preview`) tells the driver the kind,
  the points, the riders affected and where the record would stand — before they
  confirm.
- **Standing.** Points in the last `ReliabilityWindowDays` (30): at
  `ReliabilityWarnPoints` (3) the driver is warned; at `ReliabilitySuspendPoints` (5)
  **instant requests pause** for `SuspensionDays` (7) — `DriverSuspended` on offers
  leaving within the hour. Scheduled work stays open. A waiver that brings the
  points back under the line lifts the pause.
- **Completion rate** = trips completed ÷ (completed + counted/late
  cancellations), shown on trip cards, offers and the driver's profile; null for a
  driver with no history.
- **Riders** are recorded too: giving a seat back within `LateCancelLeadMinutes`
  (`RiderLateCancel`) and a no-show (`RiderNoShow`). Riders are never paused.
- **Riders are not stranded.** When a driver cancels a trip formed from a request,
  its riders are put back on the market in a **new** request
  (`ReopenedFromRequestId` → the matched one, which never reopens, §7.4), with the
  same journey and conditions — unless departure is within 15 minutes. They hear
  "finding you another driver" instead of a plain cancellation, and nearby drivers
  and route alerts are told as for any new request. A low-seats call-off (§5.1) is
  the platform's decision and is not recorded against anyone.

### 26.5 Route alerts and request watches

A driver can save routes they drive with a minimum seat count
(`DemandAlert`: origin, destination, radius, min seats). When a request is created
or grows by a join and now matches — both ends within the radius, enough seats,
driver eligible for its conditions — the driver is told **once per request**
(`DemandAlertHit`). A **watch** is the same row pointed at one request ("tell me
when this reaches 3"); it retires after it fires.

### 26.6 Safety

- **Safety notes** — five for riders, five for drivers — are agreed once, before
  the first booking, request or offer, and reminded on the screens where they
  matter.
- **Boarding codes.** Every booking gets four random digits. The rider sees theirs
  while the seat is committed and not yet boarded; the driver types it to mark
  them aboard (`BoardingCodeInvalid` otherwise) while `BoardingCodeRequired` is on.
  With codes on, the trip-wide "everybody in" is refused (`BoardingCodeRequired`):
  on a shared ride each rider is boarded by name and code.
- **Trip links.** A rider can share a read-only link
  (`POST safety/bookings/{id}/share`); the public page (`GET share/{token}`, CMS
  `/:lang/share/:token`) shows the journey, the driver's first name, the car and
  plate, and the car's position **only while the ride is under way**. It stops
  working two hours after the seat settles, or when the rider revokes it.
- **SOS.** The button dials `EmergencyNumber` and raises an incident with the
  reporter's location. Admins are notified; if the reporter has an emergency
  contact on their profile, it is messaged with the location and a trip link. The
  admin team works the queue (open → acknowledged → resolved). Only somebody on the
  trip can attach a report to it.

---

*See `DESIGN.md` for the data model, APIs and tech stack, and
`wanes-dfd-ar.html` for the Arabic data-flow diagram.*
