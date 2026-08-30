import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/widgets/wanes_alerts.dart';

/// Screen 13 ("Alerts & toasts") — the feedback surfaces. These lay out in an
/// unbounded column, which is where the toast's full-height colour edge first
/// went wrong, so every case pumps inside a scrolling list.
void main() {
  Widget host(Widget child, {ThemeData? theme}) => MaterialApp(
        theme: theme ?? WanesTheme.light(),
        home: Scaffold(body: ListView(children: [child])),
      );

  testWidgets('toast renders title, message and the kind glyph',
      (tester) async {
    await tester.pumpWidget(host(const WanesToast(
      kind: WanesAlertKind.error,
      title: 'Booking failed',
      message: 'That seat was just taken. Try another trip.',
    )));

    expect(tester.takeException(), isNull);
    expect(find.text('Booking failed'), findsOneWidget);
    expect(find.text('That seat was just taken. Try another trip.'),
        findsOneWidget);
    expect(find.text('!'), findsOneWidget);
  });

  testWidgets('each kind carries its own glyph', (tester) async {
    for (final (kind, glyph) in const [
      (WanesAlertKind.warning, '△'),
      (WanesAlertKind.success, '✓'),
      (WanesAlertKind.info, 'i'),
    ]) {
      await tester.pumpWidget(host(WanesToast(kind: kind, title: 'x')));
      expect(find.text(glyph), findsOneWidget,
          reason: '$kind should show $glyph');
    }
  });

  testWidgets('toast lays out in dark theme too', (tester) async {
    await tester.pumpWidget(host(
      const WanesToast(
          kind: WanesAlertKind.success,
          title: 'Trip booked',
          message: 'For 08:15.'),
      theme: WanesTheme.dark(),
    ));
    expect(tester.takeException(), isNull);
  });

  testWidgets('error panel shows both actions and reports which was tapped',
      (tester) async {
    var dismissed = false;
    var retried = false;
    await tester.pumpWidget(host(WanesErrorPanel(
      onDismiss: () => dismissed = true,
      onRetry: () => retried = true,
    )));

    expect(find.text('Connection lost'), findsOneWidget);
    await tester.tap(find.text('Retry'));
    await tester.tap(find.text('Dismiss'));
    expect(retried, isTrue);
    expect(dismissed, isTrue);
  });

  testWidgets('error panel hides Retry when there is nothing to retry',
      (tester) async {
    await tester.pumpWidget(host(const WanesErrorPanel()));
    expect(find.text('Dismiss'), findsOneWidget);
    expect(find.text('Retry'), findsNothing);
  });

  testWidgets('inline alert renders its message', (tester) async {
    await tester.pumpWidget(
        host(const WanesInlineAlert('Payment declined — update your card')));
    expect(tester.takeException(), isNull);
    expect(find.text('Payment declined — update your card'), findsOneWidget);
  });

  testWidgets('toasts stack in the overlay, then auto-dismiss', (tester) async {
    await tester.pumpWidget(MaterialApp(
      theme: WanesTheme.light(),
      home: Scaffold(
        body: Builder(
          builder: (context) => TextButton(
            onPressed: () {
              WanesAlerts.error(context, 'Booking failed');
              WanesAlerts.success(context, 'Trip booked');
            },
            child: const Text('go'),
          ),
        ),
      ),
    ));

    await tester.tap(find.text('go'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    expect(tester.takeException(), isNull);
    expect(find.text('Booking failed'), findsOneWidget);
    expect(find.text('Trip booked'), findsOneWidget);

    // Overlay entries live outside the app's Material; without one, text
    // renders in Flutter's missing-Material debug style.
    expect(
      find.ancestor(
          of: find.byType(WanesToast), matching: find.byType(Material)),
      findsWidgets,
    );

    // Default lifetime is 4s; give the exit animation room to finish.
    await tester.pump(const Duration(seconds: 5));
    await tester.pumpAndSettle();
    expect(find.text('Booking failed'), findsNothing);
    expect(find.text('Trip booked'), findsNothing);
  });

  testWidgets('the stack never grows past three cards', (tester) async {
    await tester.pumpWidget(MaterialApp(
      theme: WanesTheme.light(),
      home: Scaffold(
        body: Builder(
          builder: (context) => TextButton(
            onPressed: () {
              for (var i = 1; i <= 5; i++) {
                WanesAlerts.info(context, 'Toast $i');
              }
            },
            child: const Text('go'),
          ),
        ),
      ),
    ));

    await tester.tap(find.text('go'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 400));
    expect(find.text('Toast 1'), findsNothing);
    expect(find.text('Toast 2'), findsNothing);
    expect(find.text('Toast 5'), findsOneWidget);
    expect(find.byType(WanesToast), findsNWidgets(3));
  });

  testWidgets('tapping ✕ closes a toast early', (tester) async {
    await tester.pumpWidget(MaterialApp(
      theme: WanesTheme.light(),
      home: Scaffold(
        body: Builder(
          builder: (context) => TextButton(
            onPressed: () =>
                WanesAlerts.warning(context, 'Driver running late'),
            child: const Text('go'),
          ),
        ),
      ),
    ));

    await tester.tap(find.text('go'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    await tester.tap(find.text('✕'));
    await tester.pumpAndSettle();
    expect(find.text('Driver running late'), findsNothing);
  });
}
