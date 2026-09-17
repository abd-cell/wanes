// Whole-series commitments (`api/v1/series`): a driver taking a rider's
// recurring request, a rider booking every day of a driver's recurring trip.
//
// A series never replaces the days: each one is still an ordinary trip with
// ordinary seats. These models describe the promise that decides who drives a
// day, and who sits in it, as the server writes each one.

import 'package:intl/intl.dart';

import '../core/l10n.dart';
import 'models.dart';

/// Which side of a schedule a commitment is on (`SeriesSide`).
enum SeriesSide {
  /// A driver drives a rider's recurring request.
  driverServes(1),

  /// A rider books every day of a driver's recurring trip.
  riderJoins(2);

  const SeriesSide(this.value);
  final int value;

  static SeriesSide fromValue(int? v) =>
      SeriesSide.values.firstWhere((s) => s.value == v, orElse: () => SeriesSide.driverServes);
}

/// `SeriesStatus`.
enum SeriesStatus {
  proposed(1, 'series.status.proposed'),
  active(2, 'series.status.active'),
  declined(3, 'series.status.declined'),
  withdrawn(4, 'series.status.withdrawn'),
  ended(5, 'series.status.ended');

  const SeriesStatus(this.value, this.labelKey);
  final int value;
  final String labelKey;

  static SeriesStatus? fromValue(int? v) {
    for (final s in SeriesStatus.values) {
      if (s.value == v) return s;
    }
    return null;
  }
}

/// The days a repeat runs on, in words: "Sun–Thu", "Every day", "Mon, Wed",
/// "Monthly on the 5th". One place, so every card says it the same way.
String repeatDaysLabel(
  String localeName, {
  required Recurrence recurrence,
  WeekDaySet days = WeekDaySet.none,
  WeekDaySet subset = WeekDaySet.none,
  int? dayOfMonth,
}) {
  final l10n = AppLocalizations.current;
  final set = !subset.isEmpty ? subset : (recurrence == Recurrence.weekly ? days : WeekDaySet.none);
  if (set.isEmpty) {
    if (recurrence == Recurrence.monthly && dayOfMonth != null) {
      return l10n.t('repeat.monthlyOn', {'day': '$dayOfMonth'});
    }
    return l10n.t('repeat.everyDay');
  }

  // Sunday-first, the region's week. Dart weekday 7 is Sunday.
  const order = [7, 1, 2, 3, 4, 5, 6];
  final on = [for (var i = 0; i < 7; i++) if (set.has(order[i])) i];
  if (on.length == 7) return l10n.t('repeat.everyDay');

  String name(int index) => DateFormat('EEE', localeName)
      .format(DateTime(2026, 9, 6).add(Duration(days: index)));

  final consecutive = on.length >= 3 &&
      [for (var i = 1; i < on.length; i++) on[i] == on[i - 1] + 1].every((x) => x);
  if (consecutive) return '${name(on.first)}–${name(on.last)}';
  return on.map(name).join(localeName.startsWith('ar') ? '، ' : ', ');
}

/// The recurrence behind a card — "Repeats Sun–Thu · until 31 Dec" — and
/// whether the viewer can commit to the whole of it.
class SeriesInfo {
  SeriesInfo({
    required this.scheduleId,
    required this.ownerRole,
    required this.recurrence,
    this.daysOfWeek = WeekDaySet.none,
    this.dayOfMonth,
    this.timeOfDay = const TimeOfDayValue(8, 0),
    this.startDate,
    this.endDate,
    this.isPaused = false,
    this.upcomingDays = 0,
    this.hasDriver = false,
    this.driverName,
    this.proposalCount = 0,
    this.mySeriesId,
    this.mySeriesStatus,
    this.commitmentId,
  });

  final int scheduleId;

  /// 1 Rider · 2 Driver.
  final int ownerRole;
  final Recurrence recurrence;
  final WeekDaySet daysOfWeek;
  final int? dayOfMonth;
  final TimeOfDayValue timeOfDay;
  final DateTime? startDate;
  final DateTime? endDate;
  final bool isPaused;

  /// Generated days still ahead — open requests or bookable trips.
  final int upcomingDays;

  /// A rider's series already has a driver.
  final bool hasDriver;
  final String? driverName;

  /// Drivers waiting on the rider's answer.
  final int proposalCount;

  /// The viewer's own live commitment or offer on it.
  final int? mySeriesId;
  final SeriesStatus? mySeriesStatus;

  /// The commitment the row itself was made under.
  final int? commitmentId;

  bool get isRiderSeries => ownerRole == 1;
  bool get isDriverSeries => ownerRole == 2;

  /// Somebody can still take or book the whole of it.
  bool get isOpenForSeries => !isPaused && (isDriverSeries || !hasDriver);

  String daysLabel(String localeName) => repeatDaysLabel(localeName,
      recurrence: recurrence, days: daysOfWeek, dayOfMonth: dayOfMonth);

  /// "Repeats Sun–Thu · until 31 Dec".
  String label(String localeName) {
    final l10n = AppLocalizations.current;
    final days = daysLabel(localeName);
    final end = endDate;
    return end == null
        ? l10n.t('repeat.badge', {'days': days})
        : l10n.t('repeat.badgeUntil',
            {'days': days, 'date': DateFormat('d MMM', localeName).format(end)});
  }

  factory SeriesInfo.fromJson(Map<String, dynamic> j) => SeriesInfo(
        scheduleId: (j['scheduleId'] as num?)?.toInt() ?? 0,
        ownerRole: (j['ownerRole'] as num?)?.toInt() ?? 1,
        recurrence: Recurrence.fromValue((j['recurrence'] as num?)?.toInt()),
        daysOfWeek: WeekDaySet((j['daysOfWeek'] as num?)?.toInt() ?? 0),
        dayOfMonth: (j['dayOfMonth'] as num?)?.toInt(),
        timeOfDay: TimeOfDayValue.parse(j['timeOfDay'] as String?),
        startDate: parseDateOnly(j['startDate'] as String?),
        endDate: parseDateOnly(j['endDate'] as String?),
        isPaused: j['isPaused'] as bool? ?? false,
        upcomingDays: (j['upcomingDays'] as num?)?.toInt() ?? 0,
        hasDriver: j['hasDriver'] as bool? ?? false,
        driverName: j['driverName'] as String?,
        proposalCount: (j['proposalCount'] as num?)?.toInt() ?? 0,
        mySeriesId: (j['mySeriesId'] as num?)?.toInt(),
        mySeriesStatus: SeriesStatus.fromValue((j['mySeriesStatus'] as num?)?.toInt()),
        commitmentId: (j['commitmentId'] as num?)?.toInt(),
      );

  static SeriesInfo? tryParse(Object? raw) =>
      raw is Map<String, dynamic> ? SeriesInfo.fromJson(raw) : null;
}

/// A commitment, as either side reads it.
class SeriesCommitment {
  SeriesCommitment({
    required this.id,
    required this.scheduleId,
    required this.side,
    required this.status,
    required this.driverId,
    required this.riderId,
    this.driverName,
    this.driverRating = 0,
    this.driverTrips = 0,
    this.driverCompletionRate,
    this.riderName,
    this.vehicleLabel,
    this.vehicleColor,
    this.vehicleSeats,
    this.pricePerSeat,
    this.seats,
    this.daysOfWeek = WeekDaySet.none,
    this.until,
    this.message,
    this.decideAt,
    this.acceptedAt,
    this.endRequestedAt,
    this.endedAt,
    this.originAddress = '',
    this.destinationAddress = '',
    this.recurrence = Recurrence.weekly,
    this.scheduleDaysOfWeek = WeekDaySet.none,
    this.dayOfMonth,
    this.timeOfDay = const TimeOfDayValue(8, 0),
    this.scheduleEndDate,
    this.schedulePaused = false,
    this.upcomingCount = 0,
    this.nextDeparture,
    this.isMine = false,
  });

  final int id;
  final int scheduleId;
  final SeriesSide side;
  final SeriesStatus status;
  final int driverId;
  final String? driverName;
  final double driverRating;
  final int driverTrips;
  final double? driverCompletionRate;
  final int riderId;
  final String? riderName;
  final String? vehicleLabel;
  final String? vehicleColor;
  final int? vehicleSeats;
  final double? pricePerSeat;
  final int? seats;

  /// The days covered — a subset of the schedule's; empty means all of them.
  final WeekDaySet daysOfWeek;
  final DateTime? until;
  final String? message;
  final DateTime? decideAt;
  final DateTime? acceptedAt;
  final DateTime? endRequestedAt;
  final DateTime? endedAt;

  final String originAddress;
  final String destinationAddress;
  final Recurrence recurrence;
  final WeekDaySet scheduleDaysOfWeek;
  final int? dayOfMonth;
  final TimeOfDayValue timeOfDay;
  final DateTime? scheduleEndDate;
  final bool schedulePaused;

  final int upcomingCount;
  final DateTime? nextDeparture;

  /// The viewer made this commitment (rather than owning the schedule it is on).
  final bool isMine;

  bool get isProposed => status == SeriesStatus.proposed;
  bool get isActive => status == SeriesStatus.active;

  /// Active, but set to stop after its notice period.
  bool get isEnding => isActive && endRequestedAt != null;

  String daysLabel(String localeName) => repeatDaysLabel(localeName,
      recurrence: recurrence,
      days: scheduleDaysOfWeek,
      subset: daysOfWeek,
      dayOfMonth: dayOfMonth);

  factory SeriesCommitment.fromJson(Map<String, dynamic> j) => SeriesCommitment(
        id: (j['id'] as num).toInt(),
        scheduleId: (j['scheduleId'] as num?)?.toInt() ?? 0,
        side: SeriesSide.fromValue((j['side'] as num?)?.toInt()),
        status: SeriesStatus.fromValue((j['status'] as num?)?.toInt()) ?? SeriesStatus.proposed,
        driverId: (j['driverId'] as num?)?.toInt() ?? 0,
        driverName: j['driverName'] as String?,
        driverRating: (j['driverRating'] as num?)?.toDouble() ?? 0,
        driverTrips: (j['driverTrips'] as num?)?.toInt() ?? 0,
        driverCompletionRate: (j['driverCompletionRate'] as num?)?.toDouble(),
        riderId: (j['riderId'] as num?)?.toInt() ?? 0,
        riderName: j['riderName'] as String?,
        vehicleLabel: j['vehicleLabel'] as String?,
        vehicleColor: j['vehicleColor'] as String?,
        vehicleSeats: (j['vehicleSeats'] as num?)?.toInt(),
        pricePerSeat: (j['pricePerSeat'] as num?)?.toDouble(),
        seats: (j['seats'] as num?)?.toInt(),
        daysOfWeek: WeekDaySet((j['daysOfWeek'] as num?)?.toInt() ?? 0),
        until: parseDateOnly(j['until'] as String?),
        message: j['message'] as String?,
        decideAt: parseServerDate(j['decideAt'] as String?)?.toLocal(),
        acceptedAt: parseServerDate(j['acceptedAt'] as String?)?.toLocal(),
        endRequestedAt: parseServerDate(j['endRequestedAt'] as String?)?.toLocal(),
        endedAt: parseServerDate(j['endedAt'] as String?)?.toLocal(),
        originAddress: j['originAddress'] as String? ?? '',
        destinationAddress: j['destinationAddress'] as String? ?? '',
        recurrence: Recurrence.fromValue((j['recurrence'] as num?)?.toInt()),
        scheduleDaysOfWeek: WeekDaySet((j['scheduleDaysOfWeek'] as num?)?.toInt() ?? 0),
        dayOfMonth: (j['dayOfMonth'] as num?)?.toInt(),
        timeOfDay: TimeOfDayValue.parse(j['timeOfDay'] as String?),
        scheduleEndDate: parseDateOnly(j['scheduleEndDate'] as String?),
        schedulePaused: j['schedulePaused'] as bool? ?? false,
        upcomingCount: (j['upcomingCount'] as num?)?.toInt() ?? 0,
        nextDeparture: parseServerDate(j['nextDeparture'] as String?)?.toLocal(),
        isMine: j['isMine'] as bool? ?? false,
      );
}

/// What one day of a series came to.
class SeriesDay {
  SeriesDay({required this.date, this.departAt, this.tripId, this.bookingId, this.refusalCode});

  final DateTime date;
  final DateTime? departAt;
  final int? tripId;
  final int? bookingId;

  /// The server's `ErrorCode` value when the day could not be taken or booked.
  final int? refusalCode;

  bool get ok => tripId != null && (refusalCode == null);

  factory SeriesDay.fromJson(Map<String, dynamic> j) => SeriesDay(
        date: parseDateOnly(j['date'] as String?) ?? DateTime.now(),
        departAt: parseServerDate(j['departAt'] as String?)?.toLocal(),
        tripId: (j['tripId'] as num?)?.toInt(),
        bookingId: (j['bookingId'] as num?)?.toInt(),
        refusalCode: ((j['refusal'] as Map<String, dynamic>?)?['code'] as num?)?.toInt(),
      );
}

class SeriesResult {
  SeriesResult({required this.series, this.days = const []});

  final SeriesCommitment series;
  final List<SeriesDay> days;

  int get okCount => days.where((d) => d.refusalCode == null).length;
  List<SeriesDay> get missed => days.where((d) => d.refusalCode != null).toList();

  factory SeriesResult.fromJson(Map<String, dynamic> j) => SeriesResult(
        series: SeriesCommitment.fromJson(j['series'] as Map<String, dynamic>),
        days: (j['days'] as List<dynamic>? ?? [])
            .map((e) => SeriesDay.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

/// What ending a series would do, both ways.
class SeriesEndPreview {
  SeriesEndPreview({
    required this.noticeDays,
    required this.noticeEnd,
    this.daysKept = 0,
    this.daysDroppedWithNotice = 0,
    this.daysDroppedNow = 0,
    this.pointsNow = 0,
    this.lateCancelsNow = 0,
    this.pointsAfter = 0,
    this.suspendPoints = 0,
    this.wouldSuspend = false,
    this.free = false,
  });

  final int noticeDays;
  final DateTime noticeEnd;
  final int daysKept;
  final int daysDroppedWithNotice;
  final int daysDroppedNow;
  final int pointsNow;
  final int lateCancelsNow;
  final int pointsAfter;
  final int suspendPoints;
  final bool wouldSuspend;

  /// Ending costs the viewer nothing either way.
  final bool free;

  factory SeriesEndPreview.fromJson(Map<String, dynamic> j) => SeriesEndPreview(
        noticeDays: (j['noticeDays'] as num?)?.toInt() ?? 0,
        noticeEnd: parseDateOnly(j['noticeEnd'] as String?) ?? DateTime.now(),
        daysKept: (j['daysKept'] as num?)?.toInt() ?? 0,
        daysDroppedWithNotice: (j['daysDroppedWithNotice'] as num?)?.toInt() ?? 0,
        daysDroppedNow: (j['daysDroppedNow'] as num?)?.toInt() ?? 0,
        pointsNow: (j['pointsNow'] as num?)?.toInt() ?? 0,
        lateCancelsNow: (j['lateCancelsNow'] as num?)?.toInt() ?? 0,
        pointsAfter: (j['pointsAfter'] as num?)?.toInt() ?? 0,
        suspendPoints: (j['suspendPoints'] as num?)?.toInt() ?? 0,
        wouldSuspend: j['wouldSuspend'] as bool? ?? false,
        free: j['free'] as bool? ?? false,
      );
}
