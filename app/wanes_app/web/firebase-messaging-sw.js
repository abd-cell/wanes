/*
 * Service worker for web push (firebase_messaging on Flutter web).
 *
 * A service worker cannot read Dart `--dart-define`s or import the generated
 * firebase_options.dart, so the project keys have to be repeated here by hand.
 * Copy them from the `web` block of lib/core/firebase_options.dart after
 * running `flutterfire configure`.
 *
 * Until the placeholders below are replaced this file does nothing useful, and
 * PushService skips web registration anyway (it also needs
 * --dart-define=FIREBASE_VAPID_KEY). The keys are not secrets — they identify
 * the project and are visible in any shipped web build.
 */
importScripts('https://www.gstatic.com/firebasejs/10.12.2/firebase-app-compat.js');
importScripts('https://www.gstatic.com/firebasejs/10.12.2/firebase-messaging-compat.js');

firebase.initializeApp({
  // Only these two are still missing: register a *web* app in the Firebase
  // console (Project settings -> Your apps -> Add app -> Web) and paste its
  // browser apiKey and its `1:267148136691:web:...` appId here. The Android and
  // iOS keys will not work -- each is restricted to its own platform.
  apiKey: 'REPLACE_WITH_FIREBASE_VALUE',
  appId: 'REPLACE_WITH_FIREBASE_VALUE',
  messagingSenderId: '267148136691',
  projectId: 'wanees-9c31c',
  storageBucket: 'wanees-9c31c.firebasestorage.app',
  authDomain: 'wanees-9c31c.firebaseapp.com',
});

firebase.messaging();
