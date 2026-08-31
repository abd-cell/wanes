import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/foundation.dart';

/// Firebase project keys, one set per platform.
///
/// ─────────────────────────────────────────────────────────────────────────
/// THIS FILE SHIPS WITH PLACEHOLDERS. Push stays off until it is filled in.
///
/// The supported way to fill it is to let the FlutterFire CLI overwrite it:
///
///   dart pub global activate flutterfire_cli
///   flutterfire configure --project=<your-firebase-project-id>
///
/// That regenerates this file with the real values (and drops
/// `android/app/google-services.json` + `ios/Runner/GoogleService-Info.plist`
/// in place). Everything here matches the CLI's output shape, so the
/// regeneration is a clean replacement — only [isConfigured] and [webVapidKey]
/// below are ours, and the CLI leaves them alone because it rewrites the whole
/// file: re-add them from git after running it.
///
/// The values are *not* secrets — they identify the project to Google and are
/// visible in any shipped binary. The real secret is the service-account JSON
/// on the backend (see `Fcm:CredentialsPath` in the API's appsettings).
/// ─────────────────────────────────────────────────────────────────────────
class DefaultFirebaseOptions {
  /// Sentinel written into every unset field below.
  static const String _unset = 'REPLACE_WITH_FIREBASE_VALUE';

  /// Web push additionally needs the "Web Push certificate" key pair from
  /// Firebase console → Project settings → Cloud Messaging → Web configuration.
  /// Pass it at build time so the checked-in source stays project-agnostic:
  ///
  ///   flutter run -d chrome --dart-define=FIREBASE_VAPID_KEY=BB...
  static const String webVapidKey =
      String.fromEnvironment('FIREBASE_VAPID_KEY', defaultValue: '');

  /// False while the placeholders are still in place — the app then skips
  /// Firebase entirely and runs with in-app (SSE) notifications only, instead
  /// of crashing on `Firebase.initializeApp`.
  static bool get isConfigured {
    try {
      final options = currentPlatform;
      return !options.apiKey.contains(_unset) &&
          !options.appId.contains(_unset) &&
          !options.projectId.contains(_unset);
    } on UnsupportedError {
      // Platform the project has no Firebase app for (e.g. desktop).
      return false;
    }
  }

  static FirebaseOptions get currentPlatform {
    if (kIsWeb) return web;
    return switch (defaultTargetPlatform) {
      TargetPlatform.android => android,
      TargetPlatform.iOS => ios,
      TargetPlatform.macOS => macos,
      _ => throw UnsupportedError(
          'No Firebase options for $defaultTargetPlatform — run `flutterfire configure`.'),
    };
  }

  static const FirebaseOptions android = FirebaseOptions(
    apiKey: 'AIzaSyBm-B6wn9XOMXGVmzifsHhMNFSaQhHPlEE',
    appId: '1:267148136691:android:19cf709dc8ca0ebb4ea786',
    messagingSenderId: '267148136691',
    projectId: 'wanees-9c31c',
    storageBucket: 'wanees-9c31c.firebasestorage.app',
  );

  static const FirebaseOptions ios = FirebaseOptions(
    apiKey: 'AIzaSyAXGql8pUHjuHJ9qDr6wkl3c6EQGn4HCvw',
    appId: '1:267148136691:ios:19745402860b31e54ea786',
    messagingSenderId: '267148136691',
    projectId: 'wanees-9c31c',
    storageBucket: 'wanees-9c31c.firebasestorage.app',
    iosBundleId: 'com.wanes.wanesApp',
  );

  static const FirebaseOptions macos = FirebaseOptions(
    apiKey: _unset,
    appId: _unset,
    messagingSenderId: _unset,
    projectId: _unset,
    storageBucket: _unset,
    iosBundleId: 'com.wanes.wanesApp',
  );

  /// Web is the one platform still waiting on the console: a Firebase *web*
  /// app has to be registered (Project settings -> Your apps -> Add app -> Web),
  /// which mints a browser `apiKey` and a `1:267148136691:web:...` app id. The
  /// Android and iOS keys cannot stand in — each is restricted to its platform.
  ///
  /// The four values below are the same for every app in the project, so they
  /// are filled in already. [isConfigured] tests apiKey/appId/projectId, so web
  /// push stays off (SSE only) until the two real values land here and in
  /// `web/firebase-messaging-sw.js`.
  static const FirebaseOptions web = FirebaseOptions(
    apiKey: _unset,
    appId: _unset,
    messagingSenderId: '267148136691',
    projectId: 'wanees-9c31c',
    storageBucket: 'wanees-9c31c.firebasestorage.app',
    authDomain: 'wanees-9c31c.firebaseapp.com',
  );
}
