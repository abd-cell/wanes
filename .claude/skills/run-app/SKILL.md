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

## Run the Flutter app on web (fastest; drivable in a browser)

```bash
cd /c/Git/claude/wanes/app/wanes_app
C:/flutter/bin/flutter.bat run -d web-server --web-port 8090 --web-hostname 127.0.0.1
```

First compile ~30s. Wait for `lib\main.dart is being served at http://127.0.0.1:8090`,
then open that URL in the browser pane (`preview_start` with the url). Front the
tab and screenshot — you should see the **Sign in** screen: ink `#0e1726`
background, teal `#0fae9e` brand dot + "Send code" button, phone field.

Drive it: click the phone field, type `+962790000000`, click **Send code**.
The button shows a teal spinner while it calls the API.

### The API base URL is target-specific — this is the #1 gotcha

`app/wanes_app/lib/core/environment.dart` reads the base URL from a
compile-time `--dart-define=API_BASE_URL=...`. The default is the host PC's
**LAN** address (`http://192.168.1.160:5000/api/v1/`) so a real phone, an
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

Smoke-test the LAN path:
```bash
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://192.168.1.160:5000/api/v1/accounts/request-otp -H "Content-Type: application/json" -d '{"phone":"+962790000000"}'
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
