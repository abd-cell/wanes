import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/intl.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/widgets/when_picker.dart';

/// The departure picker, and what it does with a time the caller cannot use.
///
/// Every screen that opens it has a real constraint — a rider's posting needs a
/// gathering lead, a driver cannot promise two departures at once, and a rider
/// searching cannot take a seat at an hour they are already riding — and they
/// used to be enforced only *after* the tap: the rider's choice snapped forward
/// under a warning toast, and the driver's was refused by the server. These
/// tests pin the replacement: such a slot is on screen, struck through and
/// inert, with a line saying why.
void main() {
  setUp(() => AppLocalizations.current = const AppLocalizations(Locale('en')));

  /// A time far enough into tomorrow that "now" can never overtake it mid-test.
  DateTime tomorrowAt(int hour) {
    final now = DateTime.now();
    final day = DateTime(now.year, now.month, now.day).add(const Duration(days: 1));
    return day.add(Duration(hours: hour));
  }

  String clock(DateTime t) => DateFormat('HH:mm', 'en').format(t);

  /// Opens the sheet and returns whatever it was popped with. [onResult] runs
  /// once the sheet closes.
  Future<void> pumpPicker(
    WidgetTester tester, {
    DateTime? earliest,
    List<DateTime> committed = const [],
    Duration clashWindow = Duration.zero,
    bool isEngaged = false,
    WhenAudience audience = WhenAudience.driver,
    void Function(WhenSelection?)? onResult,
  }) async {
    await tester.pumpWidget(MaterialApp(
      theme: WanesTheme.light(),
      supportedLocales: AppLocalizations.supportedLocales,
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      home: Builder(
        builder: (context) => Scaffold(
          body: Center(
            child: ElevatedButton(
              onPressed: () async {
                final result = await showWhenPicker(
                  context,
                  null,
                  earliest: earliest,
                  committed: committed,
                  clashWindow: clashWindow,
                  isEngaged: isEngaged,
                  audience: audience,
                );
                onResult?.call(result);
              },
              child: const Text('open'),
            ),
          ),
        ),
      ),
    ));
    // The localisation delegates resolve a frame later, so the host is not on
    // screen until this settles.
    await tester.pumpAndSettle();

    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();
  }

  /// The sheet opens on the first usable day, so reaching tomorrow's chips may
  /// need a tap on the day tab.
  Future<void> showTomorrow(WidgetTester tester) async {
    await tester.tap(find.text('Tomorrow'));
    await tester.pumpAndSettle();
  }

  Text chipText(WidgetTester tester, String label) =>
      tester.widget<Text>(find.text(label).first);

  bool isStruckThrough(Text text) =>
      text.style?.decoration == TextDecoration.lineThrough;

  group('a lead time', () {
    testWidgets('strikes out the slots before it and says why', (tester) async {
      final earliest = tomorrowAt(9);
      await pumpPicker(tester, earliest: earliest);
      await showTomorrow(tester);

      // Shown, not hidden: a slot you can see is unavailable teaches the rule.
      expect(find.text(clock(tomorrowAt(7))), findsOneWidget);
      expect(isStruckThrough(chipText(tester, clock(tomorrowAt(7)))), isTrue);

      expect(isStruckThrough(chipText(tester, clock(earliest))), isFalse);
      expect(
          find.text('Times before ${clock(earliest)} are too soon for the seats you asked for.'),
          findsOneWidget);
    });

    testWidgets('a struck-out slot cannot be chosen', (tester) async {
      WhenSelection? result;
      var closed = false;
      await pumpPicker(
        tester,
        earliest: tomorrowAt(9),
        onResult: (r) {
          result = r;
          closed = true;
        },
      );
      await showTomorrow(tester);

      await tester.tap(find.text(clock(tomorrowAt(7))).first);
      await tester.pumpAndSettle();

      // The sheet is still open and nothing was returned.
      expect(closed, isFalse);
      expect(result, isNull);
      expect(find.text('When are you leaving?'), findsOneWidget);
    });

    testWidgets('a free slot still returns its time', (tester) async {
      WhenSelection? result;
      final earliest = tomorrowAt(9);
      await pumpPicker(tester, earliest: earliest, onResult: (r) => result = r);
      await showTomorrow(tester);

      await tester.tap(find.text(clock(tomorrowAt(10))).first);
      await tester.pumpAndSettle();

      expect(result?.dateTime, tomorrowAt(10));
    });
  });

  group("a driver's own diary", () {
    testWidgets('strikes out departures inside the clash window', (tester) async {
      // One trip already promised at 08:00. A 45-minute window reaches the
      // slots either side of it, which a 30-minute one cannot with a 30-minute
      // grid.
      await pumpPicker(
        tester,
        committed: [tomorrowAt(8)],
        clashWindow: const Duration(minutes: 45),
      );
      await showTomorrow(tester);

      expect(isStruckThrough(chipText(tester, clock(tomorrowAt(8)))), isTrue);
      expect(isStruckThrough(chipText(tester, clock(tomorrowAt(7).add(const Duration(minutes: 30))))), isTrue);
      expect(isStruckThrough(chipText(tester, clock(tomorrowAt(8).add(const Duration(minutes: 30))))), isTrue);

      // A full window away is fine.
      expect(isStruckThrough(chipText(tester, clock(tomorrowAt(7)))), isFalse);
      expect(isStruckThrough(chipText(tester, clock(tomorrowAt(9)))), isFalse);

      expect(find.text('Crossed-out times clash with a trip you are already driving.'),
          findsOneWidget);
    });

    testWidgets('the window is exclusive, exactly as the server has it', (tester) async {
      // DriverAvailabilityRules.Clashes is a strict `<`, so a departure exactly
      // one window away is allowed. Greying it out here would hide a slot the
      // API would happily take.
      await pumpPicker(
        tester,
        committed: [tomorrowAt(8)],
        clashWindow: const Duration(minutes: 30),
      );
      await showTomorrow(tester);

      expect(isStruckThrough(chipText(tester, clock(tomorrowAt(8)))), isTrue);
      expect(isStruckThrough(chipText(tester, clock(tomorrowAt(7).add(const Duration(minutes: 30))))), isFalse);
      expect(isStruckThrough(chipText(tester, clock(tomorrowAt(8).add(const Duration(minutes: 30))))), isFalse);
    });

    testWidgets('leaves every slot open when nothing is promised', (tester) async {
      await pumpPicker(tester, clashWindow: const Duration(minutes: 30));
      await showTomorrow(tester);

      expect(isStruckThrough(chipText(tester, clock(tomorrowAt(8)))), isFalse);
      expect(find.text('Crossed-out times clash with a trip you are already driving.'),
          findsNothing);
    });

    testWidgets('a driver out on a trip can pick nothing at all', (tester) async {
      WhenSelection? result;
      await pumpPicker(tester, isEngaged: true, onResult: (r) => result = r);

      // Not "not then" but "not yet" — a different sentence, so its own line.
      expect(find.text('You are out on a trip. Finish it before promising another departure.'),
          findsOneWidget);

      await tester.tap(find.text('Now'));
      await tester.pumpAndSettle();
      expect(result, isNull);
    });
  });

  group("a rider's own diary, in search", () {
    testWidgets('strikes out hours the rider already has a seat at', (tester) async {
      // The same rule as the driver's, from the rider's side: one rider rides in
      // one car, so a seat held at 08:00 rules out taking another beside it.
      await pumpPicker(
        tester,
        committed: [tomorrowAt(8)],
        clashWindow: const Duration(minutes: 45),
        audience: WhenAudience.rider,
      );
      await showTomorrow(tester);

      expect(isStruckThrough(chipText(tester, clock(tomorrowAt(8)))), isTrue);
      expect(isStruckThrough(chipText(tester, clock(tomorrowAt(9)))), isFalse);
    });

    testWidgets('says booked, not driving', (tester) async {
      // Same rule, different sentence. A rider told their search time clashes
      // with "a trip you are already driving" would be reading about somebody
      // else's problem.
      await pumpPicker(
        tester,
        committed: [tomorrowAt(8)],
        clashWindow: const Duration(minutes: 45),
        audience: WhenAudience.rider,
      );
      await showTomorrow(tester);

      expect(find.text('Crossed-out times clash with a ride you have already booked.'),
          findsOneWidget);
      expect(find.text('Crossed-out times clash with a trip you are already driving.'),
          findsNothing);
    });

    testWidgets('a rider mid-ride can pick nothing at all', (tester) async {
      WhenSelection? result;
      await pumpPicker(
        tester,
        isEngaged: true,
        audience: WhenAudience.rider,
        onResult: (r) => result = r,
      );

      expect(find.text('You are on a ride now. Finish it before booking another.'),
          findsOneWidget);

      await tester.tap(find.text('Now'));
      await tester.pumpAndSettle();
      expect(result, isNull);
    });

    testWidgets('an empty diary leaves search exactly as it was', (tester) async {
      // The common case, and the one that must not regress: a rider with
      // nothing booked — or whose availability call failed, which arrives here
      // as the same empty value — can still pick any time.
      WhenSelection? result;
      await pumpPicker(
        tester,
        audience: WhenAudience.rider,
        onResult: (r) => result = r,
      );
      await showTomorrow(tester);

      expect(isStruckThrough(chipText(tester, clock(tomorrowAt(8)))), isFalse);
      expect(find.text('Crossed-out times clash with a ride you have already booked.'),
          findsNothing);

      await tester.tap(find.text(clock(tomorrowAt(8))).first);
      await tester.pumpAndSettle();
      expect(result?.dateTime, tomorrowAt(8));
    });
  });

  testWidgets('with no constraints it behaves as it always did', (tester) async {
    WhenSelection? result;
    var closed = false;
    await pumpPicker(tester, onResult: (r) {
      result = r;
      closed = true;
    });

    await tester.tap(find.text('Now'));
    await tester.pumpAndSettle();

    // "Now" is the one choice that returns no instant — the caller reads it as
    // "leave immediately".
    expect(closed, isTrue);
    expect(result, isNotNull);
    expect(result!.dateTime, isNull);
  });
}
