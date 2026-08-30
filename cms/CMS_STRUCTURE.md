# Wanes CMS (Control Panel) — Structure

Angular 22 (standalone + SSR) admin panel for Wanes. Structure mirrors the
**Parkaway** control panel. Consumes the backend admin/audit/verification APIs.

## Stack

| Concern | Choice |
|---|---|
| Framework | Angular 22, standalone components, `inject()` |
| Rendering | SSR (`RenderMode.Server`, all routes) |
| State | Services + **signals** (no NgRx) |
| HTTP | Single typed gateway `ApiService` + functional interceptors |
| i18n | TS label maps + impure `translate` pipe (en / ar, RTL) |
| Styling | Custom CSS design tokens (`--app-*`), no UI framework |

## Layout

```
wanes-cp/src/app/
├── environment.ts                 APP_NAME + apiBaseUrl (https://localhost:5001/api/v1/)
├── app.ts                         root (router-outlet + toast host)
├── app.config.ts                  providers: router, hydration, HttpClient + interceptors
├── app.routes.ts                  /:languageCode → login | shell(dashboard, drivers, audit)
├── app.routes.server.ts           RenderMode.Server
├── i18n/  en.ts · ar.ts           label maps (same keys)
├── components/
│   └── lang-wrapper.component.ts   sets dir/lang from :languageCode
├── core/
│   ├── api/
│   │   ├── api.service.ts          typed HTTP gateway (all endpoints)
│   │   └── models.ts               AppResponse<T>, AdminPage<T>, DTOs, enums
│   ├── guard/guards.ts             appAuthGuard · loggedInGuard · languageGuard
│   ├── interceptors/               auth (Bearer + Accept-Language) · error (401→login, toasts)
│   ├── pipes/translate.pipe.ts     impure translate pipe
│   └── services/
│       ├── global.service.ts        session/localStorage, language, toasts (SSR-safe)
│       └── translation.service.ts   label lookup
└── features/
    ├── auth/login                  phone-OTP sign in
    ├── layout/main-layout          authenticated sidebar shell (lang + logout)
    ├── dashboard                   landing
    ├── drivers                     pending-driver verification (approve/reject)  ← key screen
    └── audit                       audit log (filter by action)

src/styles/
├── main.css                        design tokens + base (light/dark, RTL)
└── admin.css                       shared classes (admin-page, card, data-table, badge, btn…)
```

## Screens (live)

- **Login** — phone + OTP (admin seeded phone `+962790000000`, OTP `1234` in testing mode).
- **Dashboard** — welcome + quick tiles.
- **Driver Verification** — lists pending drivers from `GET /admin/drivers/pending`, approve/reject via `POST /admin/drivers/{id}/verify`.
- **Audit Log** — `GET /admin/audit`, filter by action.

Every string exists in **en + ar**; the panel is fully RTL-aware.

## Run

```bash
cd cms/wanes-cp
npm start                 # dev server (http://localhost:4200)
npm run build             # SSR production build
```

> Point `environment.apiBaseUrl` at the running Wanes backend. Sign in with the
> seeded admin phone to reach the admin screens.
```
