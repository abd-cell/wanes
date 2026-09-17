import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:wanes_app/core/app_config.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/features/series/series_flow.dart';
import 'package:wanes_app/models/models.dart';
import 'package:wanes_app/services/services.dart';
import 'package:wanes_app/widgets/repeat_picker.dart';

/// Phase 4 on the device: choosing to repeat, reading a repeat off a card, and
/// the driver's "this day, or every day?" fork.
void main() {
  setUp(() async {
    await initializeDateFormatting('en');
    SharedPreferences.setMockInitialValues({});
    AppLocalizations.current = const AppLocalizations(Locale('en'));
    AppConfigController.config.value = AppConfig.fallback;
  });

  Widget host(Widget child) => MaterialApp(
        theme: WanesTheme.light(),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: Scaffold(body: child),
      );

  SeriesInfo series({
    int ownerRole = 1,
    Recurrence recurrence = Recurrence.weekly,
    WeekDaySet days = workWeek,
    int upcoming = 10,
    bool hasDriver = false,
    SeriesStatus? mine,
    bool paused = false,
  }) =>
      SeriesInfo(
        scheduleId: 7,
        ownerRole: ownerRole,
        recurrence: recurrence,
        daysOfWeek: days,
        upcomingDays: upcoming,
        hasDriver: hasDriver,
        driverName: hasDriver ? 'Omar' : null,
        mySeriesId: mine == null ? null : 3,
        mySeriesStatus: mine,
        isPaused: paused,
      );

  RiderTrip request({SeriesInfo? info}) => RiderTrip(
        id: 12,
        originAddress: 'A',
        destinationAddress: 'B',
        departAt: DateTime.now().add(const Duration(days: 1)),
        scheduleId: info?.scheduleId,
        series: info,
      );

  group('the repeat choice', () {
    test('a weekly repeat with no day chosen is refused before it is sent', () {
      const custom = RepeatChoice(repeat: true, preset: RepeatPreset.custom, days: WeekDaySet.none);
      expect(custom.isValid, isFalse);
      expect(custom.copyWith(days: const WeekDaySet(0x02)).isValid, isTrue);

      // "Once" never has days to check.
      expect(const RepeatChoice().isValid, isTrue);
    });

    test('the presets say what they run on', () {
      const workweek = RepeatChoice(repeat: true);
      expect(workweek.recurrence, Recurrence.weekly);
      expect(workweek.effectiveDays.mask, workWeek.mask);

      const daily = RepeatChoice(repeat: true, preset: RepeatPreset.everyDay);
      expect(daily.recurrence, Recurrence.daily);
      expect(daily.effectiveDays.isEmpty, isTrue);

      const monthly = RepeatChoice(repeat: true, preset: RepeatPreset.monthly);
      expect(monthly.recurrence, Recurrence.monthly);
    });
  });

  group('reading a repeat', () {
    test('a run of days reads as a range, and every day as "Every day"', () {
      expect(repeatDaysLabel('en', recurrence: Recurrence.weekly, days: workWeek), 'Sun–Thu');
      expect(repeatDaysLabel('en', recurrence: Recurrence.weekly, days: allWeek), 'Every day');
      expect(repeatDaysLabel('en', recurrence: Recurrence.daily), 'Every day');
      expect(
        repeatDaysLabel('en',
            recurrence: Recurrence.weekly, days: const WeekDaySet(0x02 | 0x08)),
        'Mon, Wed',
      );
    });

    test('the badge names the days and the end date', () {
      final info = SeriesInfo(
        scheduleId: 1,
        ownerRole: 1,
        recurrence: Recurrence.weekly,
        daysOfWeek: workWeek,
        endDate: DateTime(2026, 12, 31),
      );
      expect(info.label('en'), 'Repeats Sun–Thu · until 31 Dec');
    });

    test('a card carries the recurrence the server sent', () {
      final row = RiderTrip.fromJson({
        'id': 4,
        'originAddress': 'A',
        'destinationAddress': 'B',
        'departAt': '2026-09-20T05:30:00',
        'scheduleId': 9,
        'occurrenceDate': '2026-09-20T00:00:00',
        'series': {
          'scheduleId': 9,
          'ownerRole': 1,
          'recurrence': 2,
          'daysOfWeek': workWeek.mask,
          'upcomingDays': 12,
          'hasDriver': true,
          'driverName': 'Omar',
          'proposalCount': 2,
        },
      });

      expect(row.isRecurring, isTrue);
      expect(row.occurrenceDate, DateTime(2026, 9, 20));
      expect(row.series!.upcomingDays, 12);
      expect(row.series!.driverName, 'Omar');
      // Somebody already drives it, so the whole series is not on offer.
      expect(row.series!.isOpenForSeries, isFalse);
    });
  });

  group('who may commit', () {
    test('only an open, recurring, rider-owned request offers the whole series', () {
      expect(canTakeSeries(request(info: series())), isTrue);
      expect(canTakeSeries(request()), isFalse);
      expect(canTakeSeries(request(info: series(hasDriver: true))), isFalse);
      expect(canTakeSeries(request(info: series(paused: true))), isFalse);

      // The rider's own schedule, not a driver's run.
      expect(canTakeSeries(request(info: series(ownerRole: 2))), isFalse);
    });

    test('the settings can switch the whole thing off', () {
      AppConfigController.config.value = AppConfig.fromJson({'seriesCommitmentsEnabled': false});
      expect(canTakeSeries(request(info: series())), isFalse);
    });

    test('a rider books every day of a driver\'s run, and only once', () {
      Trip trip({SeriesInfo? info}) => Trip(
            id: 3,
            driverName: 'Omar',
            driverRating: 5,
            originAddress: 'A',
            destinationAddress: 'B',
            departAt: DateTime.now().add(const Duration(days: 1)),
            seatsLeft: 3,
            seatsTotal: 4,
            scheduleId: info?.scheduleId,
            series: info,
          );

      expect(canJoinSeries(trip(info: series(ownerRole: 2))), isTrue);
      expect(canJoinSeries(trip()), isFalse);
      // A rider already committed is not offered it again.
      expect(canJoinSeries(trip(info: series(ownerRole: 2, mine: SeriesStatus.active))), isFalse);
    });
  });

  group('this day, or every day', () {
    testWidgets('a recurring card asks, and a one-off does not', (tester) async {
      await tester.pumpWidget(host(Builder(
        builder: (context) => TextButton(
          onPressed: () => takeRideRequest(context, request(info: series())),
          child: const Text('take'),
        ),
      )));
      await tester.pump();
      await tester.tap(find.text('take'));
      await tester.pumpAndSettle();

      expect(find.text('Take this ride'), findsOneWidget);
      expect(find.text('The whole series'), findsOneWidget);
      // The day it would take is named, not left as "this one".
      expect(find.textContaining('Just '), findsOneWidget);
    });
  });

  group('the terms sheet', () {
    testWidgets('a driver offers on chosen days, with the rules on screen',
        (tester) async {
      SeriesTerms? terms;
      await tester.pumpWidget(host(Builder(
        builder: (context) => TextButton(
          onPressed: () async {
            terms = await showModalBottomSheet<SeriesTerms>(
              context: context,
              isScrollControlled: true,
              backgroundColor: Colors.transparent,
              builder: (_) => SeriesTermsSheet(
                forDriver: true,
                series: series(),
                origin: 'A',
                destination: 'B',
                suggestion: 2,
                minSeats: 1,
                maxSeats: 4,
                initialSeats: 4,
              ),
            );
          },
          child: const Text('open'),
        ),
      )));
      await tester.pump();
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      // What the promise costs to break is read before it is made.
      expect(find.text('What you are promising'), findsOneWidget);
      expect(find.textContaining('24h notice'), findsOneWidget);

      // Drop Sunday from the driver's own subset.
      await tester.tap(find.text('Sun'));
      await tester.pump();

      await tester.ensureVisible(find.textContaining('I understand'));
      await tester.tap(find.textContaining('I understand'));
      await tester.pump();
      await tester.ensureVisible(find.byType(FilledButton).last);
      await tester.tap(find.byType(FilledButton).last);
      await tester.pumpAndSettle();

      expect(terms, isNotNull);
      expect(terms!.seats, 4);
      expect(terms!.pricePerSeat, 2);
      // Mon–Thu: the schedule's days without Sunday.
      expect(terms!.days.has(DateTime.sunday), isFalse);
      expect(terms!.days.has(DateTime.monday), isTrue);
    });

    testWidgets('the agreement gates the button', (tester) async {
      await tester.pumpWidget(host(Builder(
        builder: (context) => TextButton(
          onPressed: () => showModalBottomSheet<SeriesTerms>(
            context: context,
            isScrollControlled: true,
            backgroundColor: Colors.transparent,
            builder: (_) => SeriesTermsSheet(
              forDriver: false,
              series: series(ownerRole: 2),
              origin: 'A',
              destination: 'B',
              pricePerSeat: 2,
              minSeats: 1,
              maxSeats: 4,
              initialSeats: 1,
              driverName: 'Omar',
            ),
          ),
          child: const Text('open'),
        ),
      )));
      await tester.pump();
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      final button = tester.widget<FilledButton>(find.byType(FilledButton).last);
      expect(button.onPressed, isNull);
    });
  });
}
