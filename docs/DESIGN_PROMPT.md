# Wanes — UI Design Prompt

Copy everything in the block below and paste it into Claude (or v0 / Figma AI / any
design tool) to generate the app's visual design. A shorter version follows at the end.

---

## ▶ Full prompt (paste this)

```
You are a senior product designer. Design the UI for "Wanes", a ride-matching
mobile app. Deliver clean, modern, production-ready screen designs (mobile-first,
375×812). Output as a self-contained HTML/CSS prototype with each screen as a
phone frame, so I can see them side by side.

── WHAT WANES IS ──
A two-sided ride app. A rider asks for a trip From address → To address. Wanes
does two things at once:
  1. CARPOOL mode — shows trips drivers already posted along that route (pick a
     seat, like BlaBlaCar).
  2. HAIL mode — if nothing matches, it notifies nearby drivers to accept the
     request (like inDrive / Uber).
One user can be both a rider and a driver.

── BRAND & MOOD ──
Modern, trustworthy, fast, friendly. Think "movement and wayfinding": routes,
map pins, a moving dot from A to B.
- Palette: deep navy ink (#0e1726), a teal "route/go" accent (#0fae9e), and a
  warm amber "pickup ping" accent (#ff7a2f) used sparingly for the on-demand /
  call-to-action moments. Cool near-white surfaces in light mode; deep navy
  surfaces in dark mode. Design BOTH light and dark.
- Typography: a clean geometric sans (e.g. Inter / SF) with a strong type scale;
  a monospace touch for labels, prices, and route codes.
- Rounded cards (12–16px radius), soft shadows, generous spacing, real map
  imagery where a map belongs.

── SCREENS TO DESIGN (rider + driver) ──
RIDER
 1. Onboarding / phone-OTP login.
 2. Home / Search — "From" and "To" address fields, a date/time picker, seats
    selector, and a big search button. Recent places below.
 3. Search results (CARPOOL) — a map at top showing the route, then a ranked
    list of matching driver trips as cards: driver name + rating + photo,
    departure time, seats left, price/seat, small detour indicator. A clear
    "Book seat" button.
 4. No-match / HAIL state — friendly empty state: "No trips yet — we're pinging
    drivers near you." Show a pulsing radius animation on the map and a live
    "3 drivers notified" status, with Cancel.
 5. Trip detail / booking confirmation — driver + vehicle, pickup point, price
    breakdown, confirm button.
 6. Active trip — live map, driver location, ETA, contact/cancel, and status
    steps (Driver on the way → Arrived → In trip → Completed).
 7. Rate & pay screen — star rating, tip, receipt.

DRIVER
 8. Driver home — toggle "Online / Offline", today's earnings, and two actions:
    "Post a trip" and incoming request cards.
 9. Post a trip — From → To, departure time, seats, price/seat, review & publish.
10. Incoming ride request (HAIL) — a card that slides up with route, distance to
    pickup, seats, fare estimate, and Accept / Decline with a countdown ring.

── COMPONENTS TO INCLUDE ──
Address input with map-pin icon, route line visual, trip card, driver mini-card
with rating, seats stepper, price pill, status stepper/timeline, bottom nav
(Home · Trips · Profile), floating action button, toast/notification.

── RULES ──
- Mobile-first, thumb-reachable primary actions at the bottom.
- The teal accent is for "go/route/confirm"; the amber accent is only for the
  on-demand HAIL / live-notify moments — do not overuse it.
- Show real, believable content (real street names, times, prices in the local
  currency) — no lorem ipsum.
- Accessible contrast in both themes; clear focus states.
- Keep it one cohesive system: same spacing scale, same corner radius, same
  type ramp across every screen.

Deliver: (a) a one-line description of the design direction, (b) the color +
type tokens, then (c) the HTML/CSS with all screens in phone frames.
```

---

## ▶ Short version (if you want a quick pass)

```
Design a mobile UI for "Wanes", a ride-matching app where a rider searches a trip
From → To and either books a seat on a driver's already-posted trip (carpool) or
gets matched to a nearby driver on demand (hail). Modern, trustworthy, map-centric.
Palette: navy ink #0e1726, teal route accent #0fae9e, amber ping accent #ff7a2f;
design light + dark. Screens: phone login, search home, carpool results with map +
trip cards, no-match "pinging nearby drivers" state, booking confirm, live trip
with ETA, rate & pay, driver home with online toggle, post-a-trip, and an incoming
ride-request card with Accept/Decline. Output a self-contained HTML/CSS prototype
with each screen in a phone frame, light and dark, no lorem ipsum.
```

---

## Tips for using it
- **In Claude Code / claude.ai:** paste the full prompt — it can produce an HTML
  prototype artifact you can preview instantly.
- **In v0 (Vercel):** paste the short version; iterate screen-by-screen.
- **In Figma (with an AI plugin):** feed the "SCREENS" and "COMPONENTS" sections.
- **To match our real plan:** the palette and the two modes (carpool + hail) come
  straight from [DESIGN.md](DESIGN.md), so the design will line up with the code.
- Swap the currency / street names in the prompt to your target city (see the
  open decisions in DESIGN.md).
```
