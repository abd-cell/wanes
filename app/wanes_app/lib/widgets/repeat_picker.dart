import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../models/series_models.dart';
import 'wanes_ui.dart';

/// Sunday to Thursday — the working week where this ships.
const WeekDaySet workWeek = WeekDaySet(0x1F);

/// Every day of the week.
const WeekDaySet allWeek = WeekDaySet(0x7F);

/// The quick choices a repeat opens on.
enum RepeatPreset { workWeek, everyDay, custom, monthly }

/// "Once" or "Repeat", and — when repeating — on which days and until when.
///
/// The first question on both posting forms, because it changes what the
/// form makes: a single ride, or a schedule that writes one for each day.
class RepeatChoice {
  const RepeatChoice({
    this.repeat = false,
    this.preset = RepeatPreset.workWeek,
    this.days = workWeek,
    this.until,
  });

  final bool repeat;
  final RepeatPreset preset;

  /// The days, when [preset] is [RepeatPreset.custom].
  final WeekDaySet days;
  final DateTime? until;

  Recurrence get recurrence => switch (preset) {
        RepeatPreset.everyDay => Recurrence.daily,
        RepeatPreset.monthly => Recurrence.monthly,
        _ => Recurrence.weekly,
      };

  WeekDaySet get effectiveDays => switch (preset) {
        RepeatPreset.workWeek => workWeek,
        RepeatPreset.custom => days,
        _ => WeekDaySet.none,
      };

  /// A weekly repeat with no day chosen would never run.
  bool get isValid => !repeat || recurrence != Recurrence.weekly || !effectiveDays.isEmpty;

  RepeatChoice copyWith({
    bool? repeat,
    RepeatPreset? preset,
    WeekDaySet? days,
    DateTime? until,
    bool clearUntil = false,
  }) =>
      RepeatChoice(
        repeat: repeat ?? this.repeat,
        preset: preset ?? this.preset,
        days: days ?? this.days,
        until: clearUntil ? null : (until ?? this.until),
      );
}

/// Once / Repeat, the presets, the day chips and the end date.
class RepeatPicker extends StatelessWidget {
  const RepeatPicker({
    super.key,
    required this.value,
    required this.onChanged,
    required this.firstDate,
    this.onOpenMine,
  });

  final RepeatChoice value;
  final ValueChanged<RepeatChoice> onChanged;

  /// The first day it runs — the "until" calendar opens no earlier.
  final DateTime firstDate;

  /// "My repeats", when the host screen offers the list.
  final VoidCallback? onOpenMine;

  Future<void> _pickUntil(BuildContext context) async {
    final floor = DateTime(firstDate.year, firstDate.month, firstDate.day);
    final initial = value.until ?? floor.add(const Duration(days: 30));
    final picked = await showDatePicker(
      context: context,
      initialDate: initial.isBefore(floor) ? floor : initial,
      firstDate: floor,
      lastDate: floor.add(const Duration(days: 365 * 2)),
    );
    if (picked != null) onChanged(value.copyWith(until: picked));
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final locale = context.l10n.localeName;

    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      SegmentedToggle(
        labels: [context.tr('repeat.once'), context.tr('repeat.repeat')],
        index: value.repeat ? 1 : 0,
        onSelect: (i) => onChanged(value.copyWith(repeat: i == 1)),
      ),
      if (value.repeat) ...[
        const SizedBox(height: 10),
        Wrap(spacing: 8, runSpacing: 8, children: [
          for (final preset in RepeatPreset.values)
            _PresetChip(
              label: context.tr(switch (preset) {
                RepeatPreset.workWeek => 'repeat.presetWorkWeek',
                RepeatPreset.everyDay => 'repeat.presetEveryDay',
                RepeatPreset.custom => 'repeat.presetCustom',
                RepeatPreset.monthly => 'repeat.presetMonthly',
              }),
              selected: value.preset == preset,
              onTap: () => onChanged(value.copyWith(preset: preset)),
            ),
        ]),
        if (value.preset == RepeatPreset.custom) ...[
          const SizedBox(height: 10),
          DayChips(
            days: value.days,
            onToggle: (weekday) => onChanged(value.copyWith(days: value.days.toggle(weekday))),
          ),
        ],
        const SizedBox(height: 10),
        GroupedCard(children: [
          GroupedRow(
            icon: Icons.repeat_rounded,
            title: context.tr('repeat.runs'),
            subtitle: value.preset == RepeatPreset.monthly
                ? context.tr('repeat.monthlyOn', {'day': '${firstDate.day}'})
                : value.isValid
                    ? repeatDaysLabel(locale, recurrence: value.recurrence, days: value.effectiveDays)
                    : context.tr('schedule.pickDays'),
            trailing: const SizedBox.shrink(),
          ),
          GroupedRow(
            icon: Icons.event_busy_outlined,
            title: context.tr('repeat.until'),
            subtitle: value.until == null
                ? context.tr('schedule.noEnd')
                : DateFormat('EEE d MMM yyyy', locale).format(value.until!),
            trailing: value.until == null
                ? Icon(Icons.chevron_right_rounded, size: 18, color: t.ink2)
                : IconButton(
                    icon: Icon(Icons.close_rounded, size: 18, color: t.ink2),
                    onPressed: () => onChanged(value.copyWith(clearUntil: true)),
                  ),
            onTap: () => _pickUntil(context),
          ),
        ]),
        const SizedBox(height: 6),
        Row(children: [
          Expanded(
            child: Text(context.tr('repeat.hint'),
                style: TextStyle(fontSize: 11.5, height: 1.4, color: t.ink2)),
          ),
          if (onOpenMine != null)
            TextButton(
              onPressed: onOpenMine,
              child: Text(context.tr('schedule.mine'),
                  style: TextStyle(fontWeight: FontWeight.w600, fontSize: 12.5, color: t.tealInk)),
            ),
        ]),
      ],
    ]);
  }
}

class _PresetChip extends StatelessWidget {
  const _PresetChip({required this.label, required this.selected, required this.onTap});

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 140),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
        decoration: BoxDecoration(
          color: selected ? t.tealTint : t.surface,
          borderRadius: BorderRadius.circular(20),
          border: Border.all(color: selected ? t.teal : t.border),
        ),
        child: Text(label,
            style: TextStyle(
                fontSize: 12.5,
                fontWeight: FontWeight.w700,
                color: selected ? t.tealInk : t.ink2)),
      ),
    );
  }
}

/// Sun–Sat as seven toggles, Sunday first. [allowed] greys out the days a
/// subset may not include (the ones the schedule itself does not run on).
class DayChips extends StatelessWidget {
  const DayChips({super.key, required this.days, required this.onToggle, this.allowed});

  final WeekDaySet days;
  final ValueChanged<int> onToggle;
  final WeekDaySet? allowed;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final locale = context.l10n.localeName;
    const order = [7, 1, 2, 3, 4, 5, 6];

    return Row(
      children: order.map((weekday) {
        final enabled = allowed == null || allowed!.has(weekday);
        final on = enabled && days.has(weekday);
        final label = DateFormat('EEE', locale)
            .format(DateTime(2026, 9, 6).add(Duration(days: weekday % 7)));
        return Expanded(
          child: Padding(
            padding: const EdgeInsetsDirectional.only(end: 5),
            child: Semantics(
              button: true,
              selected: on,
              label: label,
              child: GestureDetector(
                onTap: enabled ? () => onToggle(weekday) : null,
                child: Container(
                  height: 40,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: on ? t.tealTint : t.surface,
                    borderRadius: BorderRadius.circular(10),
                    border: Border.all(color: on ? t.teal : t.border),
                  ),
                  child: Text(label.substring(0, label.length.clamp(0, 3)),
                      style: TextStyle(
                          fontWeight: FontWeight.w700,
                          fontSize: 11,
                          color: !enabled
                              ? t.ink2.withValues(alpha: .35)
                              : on
                                  ? t.tealInk
                                  : t.ink2)),
                ),
              ),
            ),
          ),
        );
      }).toList(),
    );
  }
}

/// "↻ Repeats Sun–Thu · until 31 Dec" — on any card that is one day of a series.
class RepeatBadge extends StatelessWidget {
  const RepeatBadge({super.key, required this.series, this.compact = false});

  final SeriesInfo series;

  /// Days only, for tight rows.
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final locale = context.l10n.localeName;
    final text = compact ? series.daysLabel(locale) : series.label(locale);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 4),
      decoration: BoxDecoration(
        color: t.amberTint,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(mainAxisSize: MainAxisSize.min, children: [
        Icon(Icons.repeat_rounded, size: 13, color: t.amberInk),
        const SizedBox(width: 4),
        Flexible(
          child: Text(text,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontSize: 11.5, fontWeight: FontWeight.w700, color: t.amberInk)),
        ),
      ]),
    );
  }
}
