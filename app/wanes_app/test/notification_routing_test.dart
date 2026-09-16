import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:wanes_app/core/notification_router.dart';
import 'package:wanes_app/core/session.dart';
import 'package:wanes_app/features/driver/driver_apply_screen.dart';
import 'package:wanes_app/features/driver/requests_screen.dart';
import 'package:wanes_app/features/trip_details_screen.dart';
import 'package:wanes_app/models/models.dart';

/// Where a tapped notification lands.
///
/// Only the branches that decide from the payload alone are asserted here —
/// the ones that first have to ask the API whose booking it is need a server,
/// and are covered by the e2e pass instead.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  AppNotification notification(String type, {Map<String, dynamic>? data}) =>
      AppNotification(
        id: 1,
        kind: NotificationKind.fromWire(type),
        title: 'x',
        body: 'y',
        isRead: false,
        data: data,
      );

  /// The widget a destination builder produces, or null when the notification
  /// named nothing to open.
  Future<Widget?> destination(AppNotification n) async {
    final builder = await NotificationRouter.destinationFor(n);
    if (builder == null) return null;
    return builder(_FakeContext());
  }

  group('the payload the API sends', () {
    test('every wire type the API can send maps to a kind of its own', () {
      // NotificationType on the backend: 1..17 plus General. A type with no
      // case here would fall through to `general` and route nowhere — which is
      // exactly how DriverArrived used to get lost.
      const wire = [
        'RiderTripNearby',
        'BookingConfirmed',
        'TripCancelled',
        'DriverAccepted',
        'TripCompleted',
        'BookingCancelled',
        'TripStarted',
        'TripMatched',
        'DriverVerified',
        'DriverRejected',
        'RatingReceived',
        'DriverArrived',
        'FeedbackReplied',
        'TripConfirmed',
        'TripNotEnoughRiders',
        'ConfirmDecision',
      ];
      for (final value in wire) {
        expect(NotificationKind.fromWire(value), isNot(NotificationKind.general),
            reason: '$value has no NotificationKind');
      }
    });

    test('the ids are read out of data whether they arrive as int or string', () {
      final n = notification('BookingConfirmed', data: {'bookingId': 42, 'tripId': '7'});
      expect(n.bookingId, 42);
      expect(n.tripId, 7);
    });
  });

  group('routing', () {
    test('a nearby posting opens the driver board', () async {
      final target =
          await destination(notification('RiderTripNearby', data: {'riderTripId': 3}));
      expect(target, isA<RequestsScreen>());
    });

    test('a matched trip opens trip details, still bookable', () async {
      final target = await destination(notification('TripMatched', data: {'tripId': 9}));
      expect(target, isA<TripDetailsScreen>());
      expect((target as TripDetailsScreen).tripId, 9);
      expect(target.canBook, isTrue);
    });

    test('a cancelled trip opens the same screen with booking off', () async {
      final target = await destination(notification('TripCancelled', data: {'tripId': 9}));
      expect((target as TripDetailsScreen).canBook, isFalse);
    });

    test('a declined driver application opens the form to resubmit', () async {
      final target = await destination(notification('DriverRejected'));
      expect(target, isA<DriverApplyScreen>());
    });

    test('an admin broadcast has nothing to open, so it falls back', () async {
      expect(await destination(notification('General')), isNull);
    });

    test('a trip notification with no trip id falls back too', () async {
      expect(await destination(notification('TripMatched')), isNull);
    });
  });

  group('opening one', () {
    setUp(() async {
      SharedPreferences.setMockInitialValues({});
      await Session.instance.save('token', 'refresh',
          Profile(id: 1, phone: '+962790000000', firstName: 'A', lastName: 'B'));
    });

    tearDown(() async {
      await Session.instance.clear();
      NotificationRouter.detach();
    });

    testWidgets('a tap pushes the destination onto the root navigator',
        (tester) async {
      final pushed = <Route<dynamic>>[];
      await tester.pumpWidget(MaterialApp(
        navigatorKey: NotificationRouter.navigatorKey,
        navigatorObservers: [_Watcher(pushed)],
        home: const Scaffold(),
      ));

      // A nearby posting is the one kind that decides from the payload alone,
      // so the push happens without a server to ask.
      unawaited(NotificationRouter.open(
          notification('RiderTripNearby', data: {'riderTripId': 3})));
      await tester.pump();
      await tester.pump();

      expect(pushed, isNotEmpty, reason: 'the tap pushed nothing');
      expect(find.byType(RequestsScreen), findsOneWidget);

      // The screen polls on a timer; drop it before the test ends.
      NotificationRouter.navigatorKey.currentState!.pop();
      await tester.pumpAndSettle();
    });

    testWidgets('a tap with no navigator yet is held, not dropped',
        (tester) async {
      await tester.pumpWidget(const SizedBox());
      final held = notification('TripMatched', data: {'tripId': 4});
      await NotificationRouter.open(held);

      final pushed = <Route<dynamic>>[];
      await tester.pumpWidget(MaterialApp(
        navigatorKey: NotificationRouter.navigatorKey,
        navigatorObservers: [_Watcher(pushed)],
        home: const Scaffold(),
      ));
      pushed.clear();

      NotificationRouter.drainPending();
      await tester.pump();
      await tester.pump();

      expect(pushed, isNotEmpty, reason: 'the held tap was dropped');
      NotificationRouter.navigatorKey.currentState!.pop();
      await tester.pumpAndSettle();
    });
  });
}

/// Records what gets pushed, so a route can be asserted on without depending on
/// what the destination screen manages to paint without a server.
class _Watcher extends NavigatorObserver {
  _Watcher(this.pushed);
  final List<Route<dynamic>> pushed;

  @override
  void didPush(Route<dynamic> route, Route<dynamic>? previous) => pushed.add(route);
}

/// The destination builders here ignore their context — they only construct.
class _FakeContext extends StatelessElement {
  _FakeContext() : super(const _Nothing());
}

class _Nothing extends StatelessWidget {
  const _Nothing();

  @override
  Widget build(BuildContext context) => const SizedBox.shrink();
}
