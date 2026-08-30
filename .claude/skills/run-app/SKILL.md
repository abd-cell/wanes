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
compile-time `--dart-define=API_BASE_URL=...` (default:
`http://10.0.2.2:5000/api/v1/`, the Android-emulator→host mapping over plain
HTTP). Pass the URL that the *target* can actually reach:

```bash
# web / desktop browser
C:/flutter/bin/flutter.bat run -d web-server --web-port 8090 --dart-define=API_BASE_URL=http://localhost:5000/api/v1/
# Android emulator (this is the default, no define needed)
C:/flutter/bin/flutter.bat run -d emulator-5554 --dart-define=API_BASE_URL=http://10.0.2.2:5000/api/v1/
# real phone on same Wi-Fi (find <PC-LAN-IP> via ipconfig)
C:/flutter/bin/flutter.bat run -d <device-id> --dart-define=API_BASE_URL=http://<PC-LAN-IP>:5000/api/v1/
```

- `10.0.2.2` is the **Android-emulator** alias for the host — it does NOT
  resolve from a browser or a real device.
- Prefer the backend's **plain-HTTP** endpoint (`:5000`). The HTTPS endpoint
  (`:5001`) uses a self-signed dev cert: a browser times out
  (`ERR_CONNECTION_TIMED_OUT`), and a device/emulator throws a Dart
  `HandshakeException`. Use https only against a trusted cert
  (`dotnet dev-certs https --trust` on the host). The backend must also allow
  the client origin via CORS.

## Bring up the backend

```bash
cd /c/Git/claude/wanes/backend
dotnet run --project Wanes
```

Serves `https://localhost:5001` and `http://localhost:5000` (profile "Wases"),
Swagger at `/swagger`. Needs SQL Server `DESKTOP-SBF2I7A` reachable.

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
