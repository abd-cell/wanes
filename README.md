# Wanes

Ride-matching platform. A rider asks for a trip **from → to**; Wanes shows trips
drivers already posted (carpool) or notifies nearby drivers to accept (hail).
One trip can carry several riders. **No payments in the current scope.**

## Monorepo layout

```
wanes/
├── backend/     ASP.NET Core 9 Web API        (architecture mirrors "Sheel")
├── cms/         Angular 21 admin panel        (structure mirrors "Parkaway CP")
├── app/         Flutter mobile app            (design imported from Claude Design)
└── docs/        BUSINESS_LOGIC · DESIGN · DFD · design prompts
```

## Documents

- [docs/BUSINESS_LOGIC.md](docs/BUSINESS_LOGIC.md) — the rules (states, matching, booking).
- [docs/DESIGN.md](docs/DESIGN.md) — data model, APIs, stack, roadmap.
- [backend/BACKEND_PLAN.md](backend/BACKEND_PLAN.md) — Wanes domain mapped to the .NET architecture.

## Build order

1. **Backend** — the API contract every client depends on (build first).
2. **CMS** — admin panel (drivers verification, audit log, dashboards).
3. **App** — Flutter rider/driver app using the imported design.

## Principles

Clean Code + SOLID throughout. Layered, feature-grouped. `AppResponse<T>` envelope,
Repository + Unit of Work, DI by convention, soft-delete, JWT sessions, i18n (en/ar, RTL).
Every mutating action is written to an immutable **audit log**.
