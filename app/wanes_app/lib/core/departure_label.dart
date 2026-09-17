import 'package:flutter/widgets.dart';
import 'package:intl/intl.dart';

import 'l10n.dart';

/// How long until a departure, written for a glance.
///
/// The request cards used to print total minutes, so a request for tomorrow
/// read "1642:25" — which looks like an error, not a time. Minutes and seconds
/// are only worth showing inside the hour, where they are the thing moving.
String untilLabel(BuildContext context, Duration d) {
  if (d.isNegative || d.inSeconds <= 0) {
    return context.tr('units.secondsShort', {'value': 0});
  }
  if (d.inSeconds < 60) return context.tr('units.secondsShort', {'value': d.inSeconds});
  if (d.inMinutes < 60) {
    return '${d.inMinutes}:${(d.inSeconds % 60).toString().padLeft(2, '0')}';
  }
  if (d.inHours < 24) {
    return context.tr('units.hoursMinutesShort', {'h': d.inHours, 'm': d.inMinutes % 60});
  }
  return context.tr('units.daysHoursShort', {'d': d.inDays, 'h': d.inHours % 24});
}

/// "Leaving now", "Today 18:30", "Tomorrow 07:15" or "Fri 12 Sep 22:12".
String departureLabel(BuildContext context, DateTime at) {
  final local = at.toLocal();
  final now = DateTime.now();
  if (local.difference(now).inMinutes < 15) return context.tr('driver.leavingNow');

  final locale = context.l10n.localeName;
  final time = DateFormat('HH:mm', locale).format(local);
  final days = DateTime(local.year, local.month, local.day)
      .difference(DateTime(now.year, now.month, now.day))
      .inDays;
  return switch (days) {
    0 => '${context.tr('common.today')} $time',
    1 => '${context.tr('common.tomorrow')} $time',
    _ => '${DateFormat('EEE d MMM', locale).format(local)} $time',
  };
}

/// Requests leaving inside this window are "now": they interrupt drivers and
/// sit on the dashboard. Anything later is scheduled work for the marketplace.
const Duration instantHorizon = Duration(hours: 1);

bool isInstant(DateTime departAt, {DateTime? now}) =>
    departAt.toLocal().difference(now ?? DateTime.now()) <= instantHorizon;
