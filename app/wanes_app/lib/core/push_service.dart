import 'dart:async';
import 'dart:convert';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';

import '../models/models.dart';
import '../services/services.dart';
import 'firebase_options.dart';
import 'session.dart';
import 'sse_client.dart';

/// Android channel id. Must match `AndroidChannelId` in the API's `FcmSender`
/// and the `default_notification_channel_id` meta-data in AndroidManifest.xml,
/// or Android 8+ silently drops the notification.
const String kAndroidChannelId = 'wanes_default';

/// Handles a push that arrived while the app was killed or backgrounded.
///
/// Runs in its own isolate, so it shares no state with the UI — it exists only
/// to satisfy Firebase's contract. The system tray already shows the
/// notification; the inbox is re-fetched from the API when the app next opens,
/// so there is nothing to persist here.
@pragma('vm:entry-point')
Future<void> wanesBackgroundMessageHandler(RemoteMessage message) async {
  // Firebase requires initialising in the background isolate too.
  if (!DefaultFirebaseOptions.isConfigured) return;
  await Firebase.initializeApp(options: DefaultFirebaseOptions.currentPlatform);
}

/// Push notifications: permission, token lifecycle, and message routing.
///
/// The whole class is a no-op when [DefaultFirebaseOptions] still holds
/// placeholders, so the app builds and runs against a backend with no Firebase
/// project — in-app notifications still arrive over SSE. See
/// `lib/core/firebase_options.dart` for how to switch it on.
class PushService {
  PushService._();
  static final PushService instance = PushService._();

  final _local = FlutterLocalNotificationsPlugin();
  final _notifications = NotificationsService();

  bool _initialised = false;
  String? _token;
  SseClient? _sse;

  /// Ids already ingested this session. A foregrounded app with Firebase
  /// configured receives every notification twice — once over FCM, once over
  /// SSE — so the badge would double-count without this.
  final _seen = <int>{};

  /// Broadcast dedupe keys already ingested. A broadcast's push copy carries no
  /// per-user row id (one multicast serves the whole audience), so id-based
  /// dedupe cannot pair it with the SSE copy — the shared key can.
  final _seenKeys = <String>{};

  /// Unread badge count. Kept current by [refreshUnreadCount] and bumped
  /// locally when a push lands while the app is open.
  final ValueNotifier<int> unreadCount = ValueNotifier<int>(0);

  /// Fires whenever a notification arrives *or* is tapped, so open screens can
  /// refresh. Broadcast — several widgets may listen.
  final _received = StreamController<AppNotification>.broadcast();
  Stream<AppNotification> get received => _received.stream;

  /// Fires when the user taps a notification, carrying its payload so the app
  /// can route to the trip or booking it refers to.
  final _opened = StreamController<AppNotification>.broadcast();
  Stream<AppNotification> get opened => _opened.stream;

  /// A tap that arrived before anything was listening (app launched from a
  /// notification). Read once by the shell after the first frame.
  AppNotification? _pendingOpen;
  AppNotification? takePendingOpen() {
    final pending = _pendingOpen;
    _pendingOpen = null;
    return pending;
  }

  /// False while the Firebase placeholders are unreplaced — the UI uses this to
  /// explain why push is off instead of pretending it works.
  bool get isAvailable => DefaultFirebaseOptions.isConfigured;

  /// `DeviceType` as the API enums it (Web = 1, Ios = 2, Android = 3).
  static int get deviceType {
    if (kIsWeb) return 1;
    return switch (defaultTargetPlatform) {
      TargetPlatform.iOS || TargetPlatform.macOS => 2,
      _ => 3,
    };
  }

  /// Boots Firebase and the local-notification plugin. Safe to call more than
  /// once; only the first call does work. Never throws — a broken push setup
  /// must not stop the app from starting.
  Future<void> init() async {
    if (_initialised || !isAvailable) return;
    _initialised = true;

    try {
      await Firebase.initializeApp(options: DefaultFirebaseOptions.currentPlatform);
      await _initLocalNotifications();

      FirebaseMessaging.onBackgroundMessage(wanesBackgroundMessageHandler);
      FirebaseMessaging.onMessage.listen(_onForegroundMessage);
      FirebaseMessaging.onMessageOpenedApp.listen(_onOpened);

      // The app may have been launched by tapping a notification.
      final initial = await FirebaseMessaging.instance.getInitialMessage();
      if (initial != null) _pendingOpen = _toNotification(initial);

      FirebaseMessaging.instance.onTokenRefresh.listen(_onTokenRefresh);
    } catch (error, stack) {
      debugPrint('[push] init failed: $error\n$stack');
    }
  }

  Future<void> _initLocalNotifications() async {
    await _local.initialize(
      settings: const InitializationSettings(
        // A dedicated monochrome icon: Android renders the launcher icon as a
        // white blob in the status bar.
        android: AndroidInitializationSettings('@drawable/ic_notification'),
        // Permission is requested explicitly in requestPermission(), so the
        // prompt lands when we choose rather than at startup.
        iOS: DarwinInitializationSettings(
          requestAlertPermission: false,
          requestBadgePermission: false,
          requestSoundPermission: false,
        ),
      ),
      onDidReceiveNotificationResponse: _onLocalTap,
    );

    final android = _local.resolvePlatformSpecificImplementation<
        AndroidFlutterLocalNotificationsPlugin>();
    await android?.createNotificationChannel(const AndroidNotificationChannel(
      kAndroidChannelId,
      'Ride updates',
      description: 'Bookings, driver matches and trip status.',
      importance: Importance.high,
    ));
  }

  /// Asks for notification permission (iOS always, Android 13+, web).
  ///
  /// Call this from a point where the user understands why — after sign-in
  /// rather than on first launch — and register the token afterwards.
  Future<bool> requestPermission() async {
    if (!isAvailable) return false;
    try {
      final settings = await FirebaseMessaging.instance.requestPermission();
      return settings.authorizationStatus == AuthorizationStatus.authorized ||
          settings.authorizationStatus == AuthorizationStatus.provisional;
    } catch (error) {
      debugPrint('[push] permission request failed: $error');
      return false;
    }
  }

  /// Subscribes to the API's SSE stream, which delivers the same notifications
  /// to a foregrounded app.
  ///
  /// This is the path that works *without* Firebase: with the placeholders
  /// still in place the inbox and badge stay live while the app is open, and
  /// only out-of-app delivery is missing. Safe to call repeatedly.
  Future<void> connectStream() async {
    if (_sse != null || !Session.instance.isLoggedIn) return;

    final client = SseClient(autoReconnect: true);
    _sse = client;
    client.events.listen(_onStreamEvent);
    await client.connect();
  }

  /// Drops the SSE subscription (sign-out).
  void disconnectStream() {
    _sse?.dispose();
    _sse = null;
  }

  void _onStreamEvent(Map<String, dynamic> event) {
    _ingest(
      AppNotification(
        id: event['id'] as int? ?? 0,
        kind: NotificationKind.fromWire(event['type'] as String?),
        title: event['title'] as String? ?? '',
        body: event['body'] as String? ?? '',
        titleAr: event['titleAr'] as String?,
        bodyAr: event['bodyAr'] as String?,
        isRead: false,
        // SSE sends the payload as a real object, REST/FCM as a string.
        data: decodeNotificationData(event['data']),
      ),
      dedupeKey: event['dedupe'] as String?,
    );
  }

  /// Single entry point for an incoming notification, whichever transport it
  /// arrived on. Returns false when it was a duplicate and has been dropped.
  bool _ingest(AppNotification notification, {String? dedupeKey}) {
    // A broadcast carries the same key on both transports, and the push copy has
    // no row id at all — so the key wins when present, or the FCM and SSE copies
    // would both be counted.
    if (dedupeKey != null && dedupeKey.isNotEmpty) {
      if (!_seenKeys.add(dedupeKey)) return false;
    } else if (notification.id != 0 && !_seen.add(notification.id)) {
      // Id 0 means the sender omitted it; those can't be deduped, so let them by.
      return false;
    }

    unreadCount.value += 1;
    _received.add(notification);
    return true;
  }

  /// Fetches this device's FCM token and hands it to the API.
  ///
  /// Requires a signed-in session — the token is stored against the caller's
  /// `UserLogin` row, so calling it while logged out would 401.
  Future<void> registerToken() async {
    if (!isAvailable || !Session.instance.isLoggedIn) return;
    try {
      // Web needs the VAPID key pair; without it getToken throws.
      final token = kIsWeb
          ? (DefaultFirebaseOptions.webVapidKey.isEmpty
              ? null
              : await FirebaseMessaging.instance
                  .getToken(vapidKey: DefaultFirebaseOptions.webVapidKey))
          : await FirebaseMessaging.instance.getToken();

      if (token == null || token.isEmpty) return;
      _token = token;
      await _notifications.registerDevice(token, deviceType: deviceType);
    } catch (error) {
      debugPrint('[push] token registration failed: $error');
    }
  }

  Future<void> _onTokenRefresh(String token) async {
    _token = token;
    if (!Session.instance.isLoggedIn) return;
    await _notifications.registerDevice(token, deviceType: deviceType);
  }

  /// Detaches this device from the account. Call *before* clearing the session,
  /// while the auth token is still valid.
  Future<void> clearToken() async {
    unreadCount.value = 0;
    _seen.clear();
    disconnectStream();
    if (!isAvailable) return;
    try {
      if (Session.instance.isLoggedIn) await _notifications.clearDevice();
      // Forces a fresh token for the next account on this handset.
      await FirebaseMessaging.instance.deleteToken();
      _token = null;
    } catch (error) {
      debugPrint('[push] token clear failed: $error');
    }
  }

  /// The current FCM token, for diagnostics (the API log screen shows it).
  String? get token => _token;

  /// Re-reads the badge count from the API.
  Future<void> refreshUnreadCount() async {
    if (!Session.instance.isLoggedIn) return;
    final response = await _notifications.feed();
    if (response.success && response.data != null) {
      unreadCount.value = response.data!.unreadCount;
    }
  }

  /// Foreground pushes are not shown by the OS, so draw one ourselves —
  /// otherwise a message that arrives while the user is on another screen is
  /// invisible until they open the inbox.
  Future<void> _onForegroundMessage(RemoteMessage message) async {
    final notification = _toNotification(message);
    // SSE may have delivered this already; the tray copy is still ours to draw,
    // but the badge must not move twice.
    _ingest(notification, dedupeKey: message.data['dedupe'] as String?);

    final title = message.notification?.title ?? notification.title;
    final body = message.notification?.body ?? notification.body;
    if (title.isEmpty && body.isEmpty) return;

    await _local.show(
      id: notification.id == 0 ? message.hashCode : notification.id,
      title: title,
      body: body,
      payload: jsonEncode(message.data),
      notificationDetails: const NotificationDetails(
        android: AndroidNotificationDetails(
          kAndroidChannelId,
          'Ride updates',
          channelDescription: 'Bookings, driver matches and trip status.',
          importance: Importance.high,
          priority: Priority.high,
          icon: '@drawable/ic_notification',
        ),
        iOS: DarwinNotificationDetails(),
      ),
    );
  }

  void _onOpened(RemoteMessage message) {
    final notification = _toNotification(message);
    if (_opened.hasListener) {
      _opened.add(notification);
    } else {
      _pendingOpen = notification;
    }
  }

  /// Tap on a notification we drew ourselves while in the foreground.
  void _onLocalTap(NotificationResponse response) {
    final raw = response.payload;
    if (raw == null || raw.isEmpty) return;
    try {
      final data = jsonDecode(raw);
      if (data is Map) {
        _onOpened(RemoteMessage(data: data.map((k, v) => MapEntry('$k', v))));
      }
    } catch (_) {
      // malformed payload — the tap still opened the app, which is enough
    }
  }

  /// Normalises an FCM message into the same shape the inbox uses, so tap
  /// handling doesn't care which path a notification arrived by.
  AppNotification _toNotification(RemoteMessage message) => AppNotification(
        id: int.tryParse('${message.data['notificationId']}') ?? 0,
        kind: NotificationKind.fromWire(message.data['type'] as String?),
        title: message.notification?.title ?? '',
        body: message.notification?.body ?? '',
        isRead: false,
        data: decodeNotificationData(message.data['data']),
      );

  /// Test/teardown hook — the singleton lives for the app's lifetime otherwise.
  @visibleForTesting
  void dispose() {
    _received.close();
    _opened.close();
    unreadCount.dispose();
  }
}
