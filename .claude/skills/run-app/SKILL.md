---
name: run-app
description: Launch and drive the Wanes Flutter app (app/wanes_app) on Flutter web, and bring up the ASP.NET backend it talks to. Use when asked to run, start, or screenshot "the app", or to confirm a Flutter/UI change works in the real app.
---

# Run the Wanes app

The monorepo has three runnable stacks. "the app" = the **Flutter** client in
`app/wanes_app/`. The CMS is Angular (`cms/wanes-cp/`); the API is ASP.NET
(`backend/Wanes/`). This skill covers the Flutter app + the backend it calls.

## Toolchain gotchas (verified)

- **Flutter is NOT on PATH.** Always call the full path:
  `C:\flutter\bin\flutter.bat` (Flutter 3.47, Dart 3.13).
- dotnet 9.0.300 is on PATH. Angular CLI 22, node 25.
- Backend DB is SQL Server `DESKTOP-SBF2I7A` (the user's machine).
- Seeded admin login: phone `+962790000000`, OTP `1234`
  (OTP is fixed to 1234 while `Otp.IsTesting=true` in appsettings).
- **Start every long-running server detached via PowerShell `Start-Process`, not
  the Bash tool.** This is not just a `dotnet run` quirk — `ng serve` and
  `flutter run` hit it too: the process exits **127 with no error message**,
  the last log line being a perfectly healthy one. Verified 7 Sep 2026: an
  `ng serve` started from Bash died minutes after printing
  `Application bundle generation complete` + `Page reload sent to client(s)`,
  and a `flutter run` from Bash was torn down the same way. Exit 127 here means
  "the harness reaped it", NOT a build or config error — don't go hunting for
  one. The pattern, for any of the three stacks:
  ```
  Start-Process -FilePath "npm.cmd" -ArgumentList "start","--","--port","4300" `
    -WorkingDirectory "C:\Git\claude\wanes\cms\wanes-cp" `
    -RedirectStandardOutput out.log -RedirectStandardError err.log -WindowStyle Hidden
  ```
  `flutter.bat` takes the same treatment (`-FilePath "C:\flutter\bin\flutter.bat"`).

## Run the Flutter app on web (fastest; drivable in a browser)

```bash
cd /c/Git/claude/wanes/app/wanes_app
C:/flutter/bin/flutter.bat run -d web-server --web-port 8090 --web-hostname 127.0.0.1
```

Open that URL in the browser pane (`preview_start` with the url). Front the
tab and screenshot — you should see the **Sign in** screen: ink `#0e1726`
background, teal `#0fae9e` brand dot + "Send code" button, phone field.

### A 200 on the port does NOT mean the app is ready (verified 7 Sep 2026)

The web server answers `index.html` within a second or two, while the Dart
build behind it keeps going — measured at **67.8s** on this machine
(`Waiting for connection from debug service on Web Server... 67.8s`). So a
readiness poll like `curl -o /dev/null -w "%{http_code}" http://localhost:8096/`
returns `200` long before there is an app to see. Navigate at that point and you
get a **black page** plus a pile of `net::ERR_CONNECTION_TIMED_OUT` console
errors for the `.lib.js` module requests — which reads exactly like a broken
API base URL and sends you debugging the network instead of just waiting.

Poll the **run log** for `is being served at`, not the port:

```bash
for i in $(seq 1 100); do
  grep -q "is being served at" app-out.log && { echo ready; break; }
  grep -qE "Failed to bind|exited with code" app-out.log && { echo FAILED; tail -5 app-out.log; break; }
  sleep 2
done
```

Even after that line appears, first paint takes another ~30-60s (splash →
orbit loader → sign-in). If you already navigated too early, a forced reload
picks it up; no need to restart the server.

Drive it: click the phone field, type `+962790000000`, click **Send code**.
The button shows a teal spinner while it calls the API.

### The API base URL is target-specific — this is the #1 gotcha

`app/wanes_app/lib/core/environment.dart` reads the base URL from a
compile-time `--dart-define=API_BASE_URL=...`. The default is the host PC's
**LAN** address (`http://192.168.10.150:5000/api/v1/`) so a real phone, an
emulator and the host browser all hit the same backend. Pass the URL the
*target* can actually reach:

```bash
# LAN — real phone / another PC on the same Wi-Fi (this is the default)
C:/flutter/bin/flutter.bat run -d web-server --web-port 8090 --web-hostname 0.0.0.0
# host browser only
C:/flutter/bin/flutter.bat run -d web-server --web-port 8090 --dart-define=API_BASE_URL=http://localhost:5000/api/v1/
# Android emulator (10.0.2.2 is the emulator's alias for the host)
C:/flutter/bin/flutter.bat run -d emulator-5554 --dart-define=API_BASE_URL=http://10.0.2.2:5000/api/v1/
```

- `10.0.2.2` is the **Android-emulator** alias for the host — it does NOT
  resolve from a browser or a real device.
- Prefer the backend's **plain-HTTP** endpoint (`:5000`). The HTTPS endpoint
  (`:5001`) uses a self-signed dev cert: a browser times out
  (`ERR_CONNECTION_TIMED_OUT`), and a device/emulator throws a Dart
  `HandshakeException`. Use https only against a trusted cert
  (`dotnet dev-certs https --trust` on the host).

## Serving the backend over the LAN (verified)

Three things must all be true, or a phone gets a connection timeout:

1. **Kestrel binds all interfaces, not just loopback.** `launchSettings.json`
   profile "Wases" is `https://0.0.0.0:5001;http://0.0.0.0:5000`. Confirm after
   startup — the log must say `Now listening on: http://0.0.0.0:5000`, and
   `netstat -ano | grep ":5000.*LISTENING"` must show `0.0.0.0:5000`, **not**
   `127.0.0.1:5000`.
2. **The Windows firewall allows inbound TCP 5000** (all profiles are ON here).
   One-time, from an **elevated** PowerShell:
   ```
   netsh advfirewall firewall add rule name="Wanes API (dev, LAN)" dir=in action=allow protocol=TCP localport=5000 profile=private remoteip=localsubnet
   ```
   Scoped to the private profile + local subnet so it is not exposed beyond the
   Wi-Fi. Testing from the host itself does **not** prove this — loopback and
   same-machine LAN-IP traffic bypass the firewall. Test from the phone.
3. **The client permits cleartext HTTP.** Android 9+ blocks it: the app ships
   `android/app/src/main/res/xml/network_security_config.xml` allowing cleartext
   only for `localhost`, `127.0.0.1`, `10.0.2.2` and the LAN IP. iOS uses
   `NSAllowsLocalNetworking` in `Info.plist`. **Update the LAN IP in that XML
   whenever it changes** or Android silently refuses the connection.

The IP is DHCP-assigned (`ipconfig` → Wi-Fi IPv4). If it moves, update
`environment.dart`, the Android network-security config, and
`cms/wanes-cp/src/app/environment.ts` together — or reserve it on the router.

**This address moves often — re-check `ipconfig` before trusting the files.**
Observed **five** values in seven days, hopping between two subnets:
`192.168.1.43` → `192.168.10.149` → `192.168.1.160` → `192.168.1.127` →
`192.168.10.150` (1-7 Sep 2026). Three of those moved *mid-session*, one of
them within twenty minutes of being verified — so treat the value in these
files as a hint, never as fact. Run `ipconfig` at the start of every session
that touches the API. When the app reports
"Connection lost / request timed out" and the console shows
`net::ERR_CONNECTION_TIMED_OUT`, check `ipconfig` **first** — that symptom was
a moved lease both times, not CORS and not the firewall. Distinguishing test:
`curl` the LAN IP and `localhost` back to back; loopback OK + LAN IP timing out
= the baked-in IP is wrong. (Real CORS failures look different — an explicit
CORS policy error in the console, not a connection timeout.)

There is a fourth place worth updating for your own sanity: the smoke-test
`curl` and the example URL in this skill file.

Smoke-test the LAN path:
```bash
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://192.168.10.150:5000/api/v1/accounts/request-otp -H "Content-Type: application/json" -d '{"phone":"+962790000000"}'
```
`200` means the API is reachable at that address.

## Bring up the backend

```bash
cd /c/Git/claude/wanes/backend
dotnet run --project Wanes
```

Serves `https://0.0.0.0:5001` and `http://0.0.0.0:5000` (profile "Wases"), so it
answers on `localhost` *and* on the PC's LAN IP. Swagger at `/swagger`. Needs
SQL Server `DESKTOP-SBF2I7A` reachable.

`dotnet run` from the Bash tool exits 127; start it detached from PowerShell:

```
Start-Process dotnet -ArgumentList "run","--project","Wanes","--launch-profile","Wases" -WorkingDirectory "C:\Git\claude\wanes\backend" -RedirectStandardOutput out.log -RedirectStandardError err.log -WindowStyle Hidden
```

## Stop the web app

`flutter run` runs in the background as `dart.exe`:

```bash
taskkill //F //IM dart.exe
```

**Check what is already running before you launch anything.** Stale servers
from earlier sessions survive for days on this machine — on 7 Sep 2026 there
were Flutter web servers holding 8090, 8091 *and* 8092 (oldest ~6 days), an
`ng serve` on 4200, and at one point **two** backends on 5000 (one bound
`0.0.0.0`, one loopback-only — only the first is LAN-reachable, and it is easy
to smoke-test the wrong one). A busy port fails the launch with
`SocketException ... errno = 10048`, which is just a port collision, nothing to
debug. Pick a free port instead of killing the user's servers:

```bash
netstat -ano | grep LISTENING | grep -oE ":[0-9]+ " | tr -d ': ' | sort -n | uniq | awk '$1>=8000 && $1<=9100'
netstat -ano | grep ":5000.*LISTENING"   # expect ONE line, on 0.0.0.0:5000
```

Note `taskkill //F //IM dart.exe` is a blunt instrument — there were 13 `dart`
processes running on 7 Sep 2026. Prefer `Stop-Process -Id <pid>` on the PID that
actually holds the port (`netstat -ano` last column).

## Running the CMS alongside (verified 7 Sep 2026)

Only needed when the task touches the admin panel, but the app and CMS share
the backend so they are often brought up together.

```
Start-Process -FilePath "npm.cmd" -ArgumentList "start","--","--port","4300" `
  -WorkingDirectory "C:\Git\claude\wanes\cms\wanes-cp" `
  -RedirectStandardOutput cms-out.log -RedirectStandardError cms-err.log -WindowStyle Hidden
```

- **Do NOT pass `--host 0.0.0.0`.** Angular 22's dev server rejects every
  request with `Header "host" with value "localhost:4300" is not allowed` (its
  SSRF guard) and serves nothing but `ERROR: Bad Request`. Leave it on the
  default localhost binding; use `--allowed-hosts` only if a LAN device really
  must reach the CMS.
- `ng serve` **watches** `environment.ts`, so after an IP edit it rebuilds on
  its own — look for a fresh `Application bundle generation complete` timestamp
  rather than restarting it.
- A bare `GET /` correctly returns **302**, not 200 — `languageGuard`
  redirects to `/en/...`. Poll `/en/login` if you want a 200 as the ready
  signal.
- Don't try to verify the baked-in API URL by grepping the served bundles;
  the value lives in a hashed lazy chunk. Load a screen and watch it fetch.

## Running on an Android emulator (verified end-to-end)

The SDK was installed standalone (no Android Studio) at `C:\Android`, JDK at
`C:\Android\jdk`. If `flutter doctor` shows `[√] Android toolchain`, skip to
"Boot + run". Full setup, and the gotchas that bit us:

- **JDK**: Microsoft OpenJDK 17 → `C:\Android\jdk`. `flutter config --jdk-dir C:\Android\jdk`.
- **SDK**: cmdline-tools → `C:\Android\cmdline-tools\latest\`. Then
  `sdkmanager "platform-tools" "emulator" "platforms;android-36" "build-tools;36.0.0" "system-images;android-35;google_apis;x86_64"`.
  `flutter config --android-sdk C:\Android`.
- **Flutter needs API 36** (not 35) or the Gradle build fails — install
  `platforms;android-36` + `build-tools;36.0.0` even though the emulator image is API 35.
- **`sdkmanager --sdk_root` path bug**: passing `--sdk_root=C:\Android` from bash
  collapsed to `C:Android` (drive-relative) and installed into
  `<cwd>\Android` instead. Fix: **omit `--sdk_root`** and let sdkmanager infer
  the root from the cmdline-tools location (`C:\Android`). Set `JAVA_HOME=C:\Android\jdk`
  for every sdkmanager/avdmanager call.
- **Licenses**: `yes | sdkmanager --licenses` (no `--sdk_root`).
- **`ANDROID_HOME` must be set, or AGP ignores `C:\Android`.** `flutter config
  --android-sdk C:\Android` only steers the *Flutter tool*. The Android Gradle
  Plugin resolves its own SDK, and with no `ANDROID_HOME`/`ANDROID_SDK_ROOT` it
  falls back to the Windows default `%LOCALAPPDATA%\Android\Sdk` — a second,
  half-built SDK on this machine. AGP then **rewrites `sdk.dir` in
  `android/local.properties`** to that path and tries to auto-download the NDK
  there, which aborts into a 177-byte stub
  (`ndk\28.2.13676358\.installer\.installData`, no `source.properties`) and
  fails the build with `[CXX1101] ... did not have a source.properties file`
  plus Flutter's "malformed download of the NDK" advice. Deleting the stub does
  **not** fix it — the next build recreates it. The fix (applied 1 Sep 2026) is
  the user-level env var:
  `[Environment]::SetEnvironmentVariable('ANDROID_HOME','C:\Android','User')`.
  With it set, a clean `flutter build apk --debug` passes, `sdk.dir` stays
  `C:\\Android`, and nothing is written to the AppData SDK.

Create the AVD (once):
```bash
JAVA_HOME='C:\Android\jdk' echo no | C:/Android/cmdline-tools/latest/bin/avdmanager.bat \
  create avd -n wanes -k "system-images;android-35;google_apis;x86_64" -d pixel_6 --force
```

Boot + run:
```bash
# 1) boot emulator (software GPU is the safe default for headless/cold boot)
C:/Android/emulator/emulator.exe -avd wanes -no-snapshot-save -no-boot-anim -gpu swiftshader_indirect &
C:/Android/platform-tools/adb.exe wait-for-device
# poll: adb shell getprop sys.boot_completed  → 1
# 2) start backend (note the actual HTTP port it prints — profile "https" uses :5256)
cd /c/Git/claude/wanes/backend && dotnet run --project Wanes --launch-profile https &
# 3) run the app, pointing at the emulator→host alias on the backend's HTTP port
cd /c/Git/claude/wanes/app/wanes_app
C:/flutter/bin/flutter.bat run -d emulator-5554 --dart-define=API_BASE_URL=http://10.0.2.2:5256/api/v1/
```

- First Android build downloads Gradle + NDK and takes ~10 min; later runs are fast.
- Native Android has **no CORS** and plain HTTP avoids the dev-cert issue, so the
  full login works: `+962790000000` / OTP `1234` → "Hi, Admin" rider home.
- Drive it headlessly with adb: `adb shell input tap X Y`, `adb shell input text 1234`,
  `adb exec-out screencap -p > shot.png`. Watch out — the soft keyboard shifts
  buttons up; tap the button at its *keyboard-open* position.
- Match the `--dart-define` port to whatever port `dotnet run` actually prints
  (profile "Wases" = :5000, profile "https" = :5256).

## Running on a real iPhone (iOS)

**You cannot build/run a Flutter iOS app from Windows** — iOS compilation and
signing require macOS + Xcode. From this Windows machine the only way to see the
app on an iPhone is the **web build in mobile Safari**:

1. Start the app bound to all interfaces so the phone can reach it, with the
   base URL pointed at the PC's LAN IP:
   `C:/flutter/bin/flutter.bat run -d web-server --web-port 8090 --web-hostname 0.0.0.0 --dart-define=API_BASE_URL=http://<host-LAN-IP>:5000/api/v1/`
2. Start the backend; put phone + PC on the same Wi‑Fi.
3. On the iPhone, open `http://<host-LAN-IP>:8090` in Safari.

For a **native** iOS build/install you need a Mac: either a physical Mac with
Xcode (`flutter build ipa` / `flutter run -d <ios-device>`), or a cloud-Mac CI
such as Codemagic or a GitHub Actions `macos` runner.
