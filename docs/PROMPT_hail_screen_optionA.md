# Wanes — "Pinging Nearby Drivers" screen · Option A (free, no map)

Paste the block below into Claude (claude.ai / Artifacts) to build the screen as
a live, animated HTML/CSS prototype. No maps API, no cost — a pure CSS/Canvas
radar animation.

---

```
Build a single mobile screen as a self-contained HTML file (inline CSS + a little
JS, no external libraries, no image/font/CDN links). Show it inside a realistic
phone frame (375×812). This is the "searching for a driver" waiting screen for a
ride app called Wanes.

── THE CONCEPT (important) ──
There is NO real map. Instead, the top ~55% of the screen is an abstract "radar /
sonar" search animation that reassures the rider we're looking for a driver
nearby. It must look calm, premium, and alive — not busy.

── LAYOUT (top to bottom) ──
1. Status bar row: time "9:41" on the left, small signal/wifi/battery hints right.
2. RADAR AREA (the hero, ~55% height):
   - A soft, faint background suggesting streets: a few very light grey diagonal
     lines / blocks at low opacity (pure CSS, abstract — NOT a real map).
   - Centered "you are here" marker: a solid dot with a warm amber glow.
   - From that center, 2–3 concentric rings continuously pulse OUTWARD and fade
     (expanding radius = actively searching). Smooth, ~2.5s loop, staggered.
   - 3–4 small teal dots scattered around at different distances = nearby drivers.
     They can gently blink/appear so it feels live.
3. CONTENT CARD (bottom ~45%, on a solid surface):
   - Small pill label: a pulsing amber dot + "LIVE · SEARCHING" (uppercase, letter-spaced, mono).
   - Big heading: "No trips on this route yet"
   - Subtext (muted): "We're pinging nearby drivers to accept your request —
     hang tight, this usually takes under a minute."
   - A status row styled as a rounded chip: a small car/bell icon, the text
     "3 drivers notified", and on the right a live countdown "0:18" that ticks
     DOWN once per second (mono, tabular numbers).
   - A full-width "Cancel request" button (subtle / outline style, not alarming).

── BRAND TOKENS ──
- Ink / text: #0e1726 (light mode), near-white in dark mode.
- Primary accent (drivers, "go"): teal #0fae9e.
- Signal accent (the searching pulse, the LIVE pill, the center glow): amber #ff7a2f.
  Use amber ONLY for the live/searching elements — everything else stays calm.
- Cool near-white surfaces in light mode; deep navy (#0a111d / #111b2b) in dark.
- Rounded corners (14–18px), soft shadows, generous spacing.
- Font: system sans (system-ui) for text; a monospace stack for the pill label,
  the countdown, and "drivers notified" number.

── MOTION RULES ──
- The radar rings pulse forever, smoothly, and respect prefers-reduced-motion
  (if reduced, show static rings, no expansion).
- The amber center dot has a soft breathing glow.
- The "0:18" counter actually counts down in JS.
- Keep it 60fps-cheap: animate transform/opacity only (no layout thrash).

── RULES ──
- Design BOTH light and dark themes (use CSS variables; adapt via
  prefers-color-scheme). Text must stay readable on both.
- Real content, exactly the strings above — no lorem ipsum.
- Everything inline and offline: no external URLs of any kind.
- Accessible: visible focus state on the Cancel button, sufficient contrast.

Deliver the finished HTML as one artifact I can preview immediately.
```

---

### Short version (quick pass)
```
Make a self-contained animated HTML phone screen (375×812, no libraries, no map,
no external URLs) for a ride app's "searching for a driver" state. Top half = an
abstract radar/sonar animation: a centered amber "you" dot with a soft glow, 2–3
concentric rings pulsing outward and fading forever, and a few teal driver dots
scattered around; a faint abstract street texture behind it (not a real map).
Bottom card: a pulsing "LIVE · SEARCHING" pill, heading "No trips on this route
yet", muted subtext about pinging nearby drivers, a chip reading "3 drivers
notified" with a live "0:18" countdown ticking down, and a full-width "Cancel
request" button. Colors: ink #0e1726, teal #0fae9e, amber #ff7a2f (amber only for
the live/searching parts). Light + dark themes, respect prefers-reduced-motion,
animate transform/opacity only.
```

### Notes
- Colors match [DESIGN.md](DESIGN.md) so it fits the rest of the app.
- This screen is **frontend-only** — no backend/maps needed to build or demo it.
- When wiring it up later: replace the fake "3 drivers notified / 0:18" with live
  values from the WebSocket channel (see the HAIL flow in DESIGN.md).
