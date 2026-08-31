# Push notifications

How notifications travel through Wanes, and how to obtain the Firebase
credentials that switch out-of-app delivery on.

**Current state (31 Aug 2026):** live end-to-end on **Android** against Firebase
project **`wanees-9c31c`**. Verified on an emulator: the FCM token registers at
sign-in, a backgrounded app receives an OS-drawn push, a foregrounded app draws
its own copy without double-counting, and a broadcast to all 49 dev accounts went
out as a single multicast.

**iOS** is configured as far as Windows allows — `GoogleService-Info.plist` is in
the Xcode target and the keys are in `firebase_options.dart` — but it has never
been build-run, and it will not deliver until an **APNs key** is uploaded (§2c).
iOS needs a Mac.

**Web** is the one platform still unfinished: it needs a Firebase *web* app
registered in the console (§2d).

---

## 1. What is already wired

There are two delivery paths, and every notification uses both:

| Path | Reaches | Works today |
|---|---|---|
| **SSE** (`GET /api/v1/sse`) | app in the foreground | ✅ yes |
| **FCM** (Firebase Cloud Messaging) | app backgrounded or killed | ✅ Android · ⚠️ iOS needs §2c · ⚠️ web needs §2d |

Both are fed from one place — `NotificationService.Notify` — which writes the
row to `UserNotifications`, pushes it, and streams it. The stored row is the
source of truth; delivery is best-effort and can never fail the booking or trip
action that triggered it.

### Events that raise a notification

| Type | Fired by | Recipient |
|---|---|---|
| `RideRequestNearby` (1) | `SearchService` opens a hail | verified, online drivers in radius |
| `BookingConfirmed` (2) | `BookingService.Create` | rider **and** driver |
| `TripCancelled` (3) | `TripService.Cancel` | every rider holding a seat |
| `DriverAccepted` (4) | `RideRequestService` accept | the rider |
| `TripCompleted` (5) | `TripService.Complete` | every rider on the trip |
| `BookingCancelled` (6) | `BookingService.Cancel` | the driver |
| `TripStarted` (7) | `TripService.Start` | every rider on the trip |
| `DriverVerified` (9) | `AdminService.VerifyDriver` approve | the driver |
| `DriverRejected` (10) | `AdminService.VerifyDriver` reject | the driver |
| `RatingReceived` (11) | `RatingService.Rate` | the rated party |
| `General` (100) | admin console — one user, or a whole audience | the chosen user(s) |

Adding a type means five edits, not one: the `NotificationType` enum, the call
site, `NotificationKind` in the app's `models.dart`, a label + icon arm in
`notifications_screen.dart`, and the `notifType.*` key in **both**
`strings_en.dart` and `strings_ar.dart`. The Dart switches are exhaustive, so the
app fails to compile if you forget the middle three — but a missing l10n key only
shows up at runtime as the raw key.

### Language (English + Arabic)

Every system notification is written in **both** languages and both are stored on
the row, so the inbox follows the reader's current language with no round trip
and stays right if they switch language later — the same reasoning as `FaqItem`.
The Arabic columns are nullable, which is where this differs from FAQ's mandatory
pair: an admin composing a notification by hand may only have typed English, and
readers then fall back to the default.

**Push is the exception.** An FCM payload carries one title and one body, so the
sender has to commit to a language. It uses the recipient's `User.Language` —
which means that value has to be accurate:

> The app **must** sync its locale to the account. `AuthService.updatePreferences`
> sends `language`, and it is called both from the language picker and once after
> every sign-in (so users who never open the picker are still covered). Before
> this existed the column was permanently `En` and every Arabic user got English
> pushes while the in-app UI was Arabic.

Wording lives in `Shareds/Notifications/NotificationTexts.cs`, keyed by
`NotificationTemplate` — which is deliberately *not* `NotificationType`, because
one type can carry two messages (a confirmed booking says different things to the
rider and the driver). Call sites pass a template plus args, never a formatted
string:

```csharp
await notificationService.Notify(riderId, NotificationTemplate.BookingConfirmedRider,
    args: new { origin = trip.OriginAddress, destination = trip.DestinationAddress },
    data: new { tripId = trip.Id, bookingId = booking.Id });
```

Placeholders are `{name}`-style and filled from the args object's properties. An
argument that is missing leaves the placeholder visible rather than throwing — one
ugly notification is a better failure than a failed booking.

Arabic is phrased naturally, not transliterated: routes read `من X إلى Y` rather
than borrowing the English `X -> Y`, whose arrow points the wrong way in an RTL
line.

For **admin-composed** notifications there is nothing to template, so
`NotificationInput` and `BroadcastInput` take optional `titleAr`/`bodyAr` (two
extra fields in the CMS, rendered `dir="rtl"`). Blank means Arabic readers see the
default. A broadcast groups its multicast by recipient language, so it is still
one send per language present rather than one per recipient.

Adding a template means: the enum, the `NotificationTexts` entry, the call site,
and — if it introduces a new `NotificationType` — the five app-side edits listed
above.

> **Testing gotcha:** do not put Arabic directly on a Git Bash command line. The
> shell encodes it in the ANSI codepage and every character reaches the API as
> `?`. Write the JSON body to a UTF-8 file and use `curl --data-binary @file`.

### Who actually gets a push

`User.NotifPush` is a user-facing preference (Profile → notifications) and it
gates **push only**: the inbox row is always written and the SSE copy always goes
out, so opting out costs the user nothing in-app. Both delivery paths honour it.
Disabled accounts (`IsDisabled`) are excluded from broadcasts entirely.

### API surface

```
GET    /api/v1/Notifications/mine           inbox page + unreadCount
POST   /api/v1/Notifications/{id}/read      mark one read
POST   /api/v1/Notifications/read-all       mark all read
POST   /api/v1/Notifications/device-token   register / refresh this device's FCM token
DELETE /api/v1/Notifications/device-token   stop push for this device
GET    /api/v1/sse                          live stream for the signed-in user

POST   /api/v1/admin/notifications          admin: compose one, for one user
POST   /api/v1/admin/notifications/broadcast  admin: one notification -> a whole audience
```

### Broadcast

`POST /admin/notifications/broadcast` takes an audience rather than a user id:

```jsonc
{ "audience": 1,          // 1 All · 2 Riders · 3 Drivers · 4 VerifiedDrivers
  "type": 100,
  "title": "Service update",
  "body": "Wanes now sends trip updates to your phone.",
  "dataJson": "{\"kind\":\"announcement\"}" }   // optional, must parse
```

It replies with the recipient count. In the CMS it is the **Broadcast** button on
the Notifications screen, behind a confirm — it cannot be recalled.

Two things make it different from calling `Notify` in a loop, and both matter:

- **One multicast for the whole audience**, not one send per recipient, so the
  cost does not scale with the user count (`FcmSender` chunks at FCM's 500-token
  limit). The recipient set is passed to EF as a subquery, never as thousands of
  parameters — that would blow SQL Server's 2100-parameter ceiling.
- **A shared `dedupe` key rides both transports.** A broadcast's push copy
  carries no `notificationId` (one payload, and every recipient's row id
  differs), so the app cannot pair the FCM and SSE copies by id. Both carry the
  same `dedupe` value instead, and `PushService._ingest` prefers it over the id.
  Remove it and a foregrounded app counts every broadcast twice.

Tokens are stored per device session on `UserLogin.DeviceToken`. They are
registered at sign-in, refreshed on every app launch and whenever Firebase
rotates them, detached on sign-out, and pruned automatically when Firebase
reports a token as unregistered.

---

## 2. Getting the credentials

Three separate things, commonly confused. Only the first is a real secret, and
it is already in place.

### 2a. Backend — service-account JSON (the real secret)

This is what lets the API send push. Anyone holding it can notify every user of
the project, so treat it like a database password.

1. Go to the [Firebase console](https://console.firebase.google.com) and sign in
   with a Google account.
2. **Add project** → name it (e.g. `wanes`) → accept or skip Analytics → create.
   *If a Wanes project already exists, use it instead of making a second one —
   tokens issued by one project are meaningless to another.*
3. Open **⚙ Project settings → Service accounts**.
4. Click **Generate new private key** → **Generate key**. A JSON file downloads
   once and cannot be re-downloaded — save it somewhere safe.
5. Put it next to the API (it is already git-ignored) and point the config at it.

**This is already done** — the key lives at `backend/Wanes/wanees-notification.json`
and `appsettings.json` reads:

```jsonc
// backend/Wanes/appsettings.json
"Fcm": {
  "Enabled": true,
  "CredentialsPath": "wanees-notification.json"
}
```

The path resolves against the content root, and `Wanes.csproj` copies the file to
the build/publish output (guarded by `Exists(...)`, so a machine without the key
still builds — push just stays off there).

On a host that injects secrets as environment variables rather than files, paste
the file's contents into `Fcm__CredentialsJson` instead and leave
`CredentialsPath` empty. `GOOGLE_APPLICATION_CREDENTIALS` also works with no
config at all.

Restart the API. The log tells you which state you are in:

```
FCM ready (project wanees-9c31c).                 ← working (what you should see now)
FCM has no service account (set Fcm:Credentials…) ← still off, notifications stored only
```

### 2b. App — per-platform project keys (not secret)

These identify the project to Google and are visible in any shipped binary.
**Android and iOS are done** — the values are in `lib/core/firebase_options.dart`
and the two config files are in place. Web is not (§2d).

Both config files are **git-ignored**, so every machine and CI runner needs them
placed by hand:

| File | Goes in | Registered as |
|---|---|---|
| `google-services.json` | `android/app/` | `com.wanes.wanes_app` |
| `GoogleService-Info.plist` | `ios/Runner/` | `com.wanes.wanesApp` |

The ids differ on purpose — Apple rejects underscores in a bundle id, so Flutter
camel-cased the iOS one. Firebase treats them as two apps in one project.

Two placement traps, both already handled in the repo:

- **iOS: the plist must be in the Xcode target, not just the folder.** A plist
  sitting in `ios/Runner/` that is not in the Runner target's *Resources* build
  phase never reaches the app bundle, and Firebase reports its config as missing
  at runtime. Four `project.pbxproj` entries are required: `PBXFileReference`,
  `PBXBuildFile`, the Runner group's `children`, and the resources build phase.
- **Android: the google-services Gradle plugin is applied conditionally.**

  ```kotlin
  // android/app/build.gradle.kts
  if (file("google-services.json").exists()) {
      apply(plugin = "com.google.gms.google-services")
  }
  ```

  Putting it in `plugins {}` unconditionally hard-fails the build whenever the
  git-ignored JSON is absent, which would break the credentials gate below.
  Verified both ways: with the JSON, `processDebugGoogleServices` emits
  `google_app_id` and the APK builds; with it moved away, no GoogleServices task
  is configured and the build still succeeds.

To regenerate everything from scratch instead, the supported route is:

```bash
dart pub global activate flutterfire_cli
cd app/wanes_app
flutterfire configure --project=wanees-9c31c
```

> **After running it**, re-add the two Wanes-specific members the CLI overwrites
> — `isConfigured` and `webVapidKey`. Recover them with
> `git diff lib/core/firebase_options.dart`. Everything else in the file is the
> CLI's to own. Note the CLI does *not* add the conditional above; it applies the
> plugin unconditionally.

`isConfigured` is what gates the whole client: while the placeholders are in
place `PushService` skips Firebase entirely, so the app builds and runs normally
(verified — `flutter build apk` succeeds today with no Firebase project at all).

### 2c. iOS only — APNs key

Apple, not Google, delivers to iPhones, so Firebase needs a key to talk to APNs.
This requires a **paid Apple Developer Program** membership.

1. [Apple Developer](https://developer.apple.com/account) → **Certificates,
   Identifiers & Profiles → Keys → +**.
2. Name it, tick **Apple Push Notifications service (APNs)**, continue,
   register, then **Download** the `.p8`. It downloads once.
3. Note the **Key ID** shown on that page, and your **Team ID** (top-right of
   the developer account, or Membership details).
4. Firebase console → **Project settings → Cloud Messaging → Apple app
   configuration → APNs Authentication Key → Upload**, and supply the `.p8`,
   Key ID and Team ID.
5. In Xcode, under **Signing & Capabilities**, confirm **Push Notifications**
   is listed. `ios/Runner/Runner.entitlements` is already committed and wired
   into all three build configurations, so it should be. Switch
   `aps-environment` to `production` for App Store builds.

Note: **iOS push cannot be tested on Windows** — it needs a Mac with Xcode and a
physical device (the simulator does not receive remote push).

### 2d. Web only — register a web app, then the VAPID key

**This is the remaining platform.** A web build needs its own Firebase app: the
Android and iOS API keys are each restricted to their platform and will not work
in a browser.

1. Firebase console → **Project settings → Your apps → Add app → Web**. That
   mints a browser `apiKey` and an app id shaped `1:267148136691:web:…`.
2. Paste those **two** values into the `web` block of
   `lib/core/firebase_options.dart` *and* into `web/firebase-messaging-sw.js`.
   The other four (`messagingSenderId`, `projectId`, `storageBucket`,
   `authDomain`) are shared across the project and are already filled in.
   `isConfigured` tests apiKey/appId/projectId, so until then web runs SSE-only.
3. Firebase console → **Project settings → Cloud Messaging → Web configuration
   → Web Push certificates → Generate key pair**.
4. Pass it at build time:

```bash
flutter run -d chrome --dart-define=FIREBASE_VAPID_KEY=BB…
```

A service worker cannot read Dart defines or import the generated
`firebase_options.dart`, which is why step 2 writes the keys in two places.

---

## 3. Verifying it works

With credentials in place:

```bash
# 1. API log on startup should read "FCM ready (project …)".
# 2. Sign in on a device, then confirm the token was stored:
#    the app calls POST /Notifications/device-token automatically.
# 3. Send yourself one from the admin console (or the API):
curl -X POST http://localhost:5000/api/v1/admin/notifications \
  -H "Authorization: Bearer <admin-token>" -H "Content-Type: application/json" \
  -d '{"userId":1,"type":100,"title":"Test","body":"Hello","isRead":false}'
```

Background the app first — a foreground push is drawn by the app itself, so it
does not prove FCM delivery. Watch the API log for
`FCM → 1 device(s): Test — Hello`.

The **channel id must stay `wanes_default`** in all three places or Android 8+
drops the notification silently:

- `FcmSender.AndroidChannelId` (API)
- `kAndroidChannelId` in `lib/core/push_service.dart`
- `default_notification_channel_id` meta-data in `AndroidManifest.xml`

---

## 4. Cost

Firebase Cloud Messaging is **free and unmetered** — there is no push cost at
any volume, and no billing account is required for FCM alone. The only paid
item in this document is the Apple Developer Program membership (~$99/year),
and only if you ship to iOS.
