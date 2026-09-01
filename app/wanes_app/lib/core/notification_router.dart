import 'dart:async';

import 'package:flutter/material.dart';

import '../features/booking_details_screen.dart';
import '../features/driver/driver_apply_screen.dart';
import '../features/driver/driver_home_screen.dart';
import '../features/driver/driver_trip_details_screen.dart';
import '../features/driver/requests_screen.dart';
import '../features/live_trip_screen.dart';
import '../features/notifications_screen.dart';
import '../features/trip_details_screen.dart';
import '../models/models.dart';
import '../services/services.dart';
import 'push_service.dart';
import 'session.dart';
import '../widgets/wanes_motion.dart';

/// Turns a tapped notification into the screen it is about.
///
/// A tap arrives with no `BuildContext` to push from: FCM delivers one while
/// the app is backgrounded, and `getInitialMessage` replays one that happened
/// before `runApp` ever ran. So routing goes through [navigatorKey], and a tap
/// that lands before the shell is on screen is held until [drainPending].
///
/// What it routes on is the `data` object the API attaches to every
/// notification — `tripId`, `bookingId`, `requestId` (see the `Notify` call
/// sites in the backend's services). A notification carrying nothing usable
/// falls back to the inbox rather than swallowing the tap: opening the app and
/// landing nowhere reads as a broken notification.
///
/// **Web is the exception.** `onMessageOpenedApp` is not implemented there, so a
/// click on a background push only reaches the service worker
/// (`web/firebase-messaging-sw.js`), which focuses the app but cannot hand the
/// payload to Dart. Foreground clicks route normally. Closing that gap means the
/// worker passing `event.notification.data` through the URL for `main` to read.
class NotificationRouter {
  NotificationRouter._();

  /// The app's root navigator. Set on `MaterialApp` in `main.dart`.
  static final GlobalKey<NavigatorState> navigatorKey = GlobalKey<NavigatorState>();

  static final _notifications = NotificationsService();
  static final _bookings = BookingService();
  static final _trips = TripService();
  static final _auth = AuthService();

  static StreamSubscription<AppNotification>? _sub;

  /// A tap that could not be acted on yet — see [drainPending].
  static AppNotification? _held;

  /// True while a destination is being resolved. Two quick taps on the same
  /// tray entry would otherwise stack two copies of the same screen.
  static bool _resolving = false;

  /// Starts listening for taps. Call once, from `main`.
  static void attach() {
    _sub ??= PushService.instance.opened.listen(open);
  }

  /// Replays a tap that had nowhere to go when it arrived — a cold start out of
  /// the tray, or a push handled while the splash still owned the screen.
  /// Called by the splash once it has handed off to the shell.
  static void drainPending() {
    final pending = PushService.instance.takePendingOpen() ?? _held;
    _held = null;
    if (pending == null) return;
    // The shell's first frame has to land before anything can sit on top of it.
    WidgetsBinding.instance.addPostFrameCallback((_) => open(pending));
  }

  /// Opens what [notification] is about.
  ///
  /// [fromInbox] marks a tap on an inbox row rather than on the tray. The inbox
  /// marks the row read itself as part of drawing it read, and a notification
  /// with nothing to open is already being looked at there — so both of this
  /// method's fallbacks are the caller's job in that case.
  static Future<void> open(AppNotification notification, {bool fromInbox = false}) async {
    final navigator = navigatorKey.currentState;
    // Splash still owns the screen, or the session went away between the push
    // and the tap. Hold it; drainPending replays it once the shell is up.
    if (navigator == null || !Session.instance.isLoggedIn) {
      _held = notification;
      return;
    }
    if (_resolving) return;
    _resolving = true;

    if (!fromInbox) _markRead(notification);

    // Resolving can take a round trip or two. Block the UI for it, or the tap
    // looks ignored and the user taps something else meanwhile.
    final busy = OverlayEntry(builder: (_) => const _ResolvingBarrier());
    navigator.overlay?.insert(busy);

    WidgetBuilder? destination;
    try {
      destination = await destinationFor(notification);
    } finally {
      busy.remove();
      _resolving = false;
    }

    // Nothing to open. From the tray that still has to lead somewhere — the
    // inbox, where the notification is at least readable in full.
    destination ??= fromInbox ? null : _inbox;
    if (destination == null) return;

    navigator.push(MaterialPageRoute(builder: destination));
  }

  static Widget _inbox(BuildContext _) => const NotificationsScreen();

  /// A tap counts as reading it. Fire-and-forget — the destination matters more
  /// than the badge, and the next feed load reconciles it either way.
  static void _markRead(AppNotification notification) {
    // A broadcast's push copy carries no per-user row id; there is nothing to
    // mark, and the inbox will pick it up unread on the next load.
    if (notification.id == 0 || notification.isRead) return;
    final unread = PushService.instance.unreadCount;
    if (unread.value > 0) unread.value -= 1;
    unawaited(_notifications.markRead(notification.id));
  }

  /// The screen a notification opens, or null when it names nothing the app can
  /// show — [open] falls back to the inbox for those.
  ///
  /// Not private so the mapping can be asserted without a navigator.
  @visibleForTesting
  static Future<WidgetBuilder?> destinationFor(AppNotification n) async {
    final tripId = n.tripId;
    final bookingId = n.bookingId;

    switch (n.kind) {
      // A hail is worth something only while it is still open, so the driver
      // lands on the accept sheet rather than on a summary of it.
      case NotificationKind.rideRequestNearby:
        return (_) => const RequestsScreen();

      // Approval is the moment the driver side becomes usable, so step into it.
      case NotificationKind.driverVerified:
        final switched = await _auth.switchRole(2);
        if (!switched.success) return null;
        return (_) => const DriverHomeScreen();

      // A rejection lands on the form the driver would have to resubmit.
      case NotificationKind.driverRejected:
        return (_) => const DriverApplyScreen();

      // Seat-level news. Both sides are told about most of it and the type
      // alone doesn't say which side this device is on — whether the booking is
      // in the rider's own list settles it.
      case NotificationKind.bookingConfirmed:
      case NotificationKind.bookingCancelled:
      case NotificationKind.tripStarted:
      case NotificationKind.driverArrived:
      case NotificationKind.tripCompleted:
      case NotificationKind.ratingReceived:
        final mine = await _myBooking(bookingId: bookingId, tripId: tripId);
        if (mine != null) return _riderBooking(mine, n.kind);
        if (tripId != null) return _driverTrip(tripId);
        return null;

      // The rider's hail was taken: a trip and a seat on it now exist, but the
      // payload names only the trip, so the booking is looked up by it.
      case NotificationKind.driverAccepted:
        if (tripId == null) return null;
        final seat = await _myBooking(tripId: tripId);
        if (seat != null) return _riderBooking(seat, n.kind);
        return (_) => TripDetailsScreen(tripId: tripId, canBook: false);

      // Rider-only, and all either carries is the trip.
      case NotificationKind.tripCancelled:
      case NotificationKind.tripMatched:
        if (tripId == null) return null;
        return (_) => TripDetailsScreen(
              tripId: tripId,
              // A fresh match is something the rider can still take; a trip that
              // was just cancelled under them is not.
              canBook: n.kind == NotificationKind.tripMatched,
            );

      // Admin-composed: there is no entity behind it, so show it in full.
      case NotificationKind.general:
        return null;
    }
  }

  /// The rider's own seat, if this notification is about one.
  ///
  /// Matches on both ids when both are known: a driver who also rides could
  /// hold a booking whose id happens to equal the rider booking named in a
  /// driver-side notification, and the trip tells them apart.
  static Future<Booking?> _myBooking({int? bookingId, int? tripId}) async {
    if (bookingId == null && tripId == null) return null;

    final response = await _bookings.myBookings();
    if (!response.success) return null;

    for (final booking in response.data ?? const <Booking>[]) {
      if (bookingId != null && booking.id != bookingId) continue;
      if (tripId != null && booking.tripId != tripId) continue;
      return booking;
    }
    return null;
  }

  /// Where a rider's seat opens. A trip actually under way goes to the live
  /// rail — that is what the notification is about — and everything else to the
  /// booking, which is the hub for track, rate, cancel and the trip itself.
  static Future<WidgetBuilder?> _riderBooking(Booking booking, NotificationKind kind) async {
    const tracking = {
      NotificationKind.driverAccepted,
      NotificationKind.tripStarted,
      NotificationKind.driverArrived,
    };
    if (tracking.contains(kind) && booking.isLive) {
      final response = await _trips.get(booking.tripId);
      final trip = response.data;
      if (trip != null) return (_) => LiveTripScreen(trip: trip, booking: booking);
    }
    return (_) => BookingDetailsScreen(booking: booking);
  }

  /// The driver's own trip. Fetched rather than reconstructed from the payload:
  /// the screen wants the seat counts and status the server has now, not what
  /// they were when the notification was written.
  static Future<WidgetBuilder?> _driverTrip(int tripId) async {
    final response = await _trips.get(tripId);
    final trip = response.data;
    if (trip == null) return null;
    return (_) => DriverTripDetailsScreen(trip: trip);
  }

  /// Test/teardown hook — the router lives for the app's lifetime otherwise.
  @visibleForTesting
  static void detach() {
    _sub?.cancel();
    _sub = null;
    _held = null;
    _resolving = false;
  }
}

/// Swallows taps and shows a spinner while the destination is being read.
class _ResolvingBarrier extends StatelessWidget {
  const _ResolvingBarrier();

  @override
  Widget build(BuildContext context) => const ColoredBox(
        color: Color(0x33000000),
        child: Center(child: WanesSpinner()),
      );
}
