import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import 'wanes_ui.dart';

/// Result of the departure-time picker.
/// [dateTime] is null when the rider chose to leave "Now".
class WhenSelection {
  const WhenSelection(this.dateTime);
  final DateTime? dateTime;
}

/// Why a slot cannot be chosen. Drives both the chip's appearance and the line
/// of copy under the day tabs — a greyed time with no explanation reads as a
/// bug.
enum _Blocked {
  /// Choosable.
  none,

  /// Already gone.
  past,

  /// Sooner than the posting needs: a rider asking for four seats is asking a
  /// driver to gather four people first, and that takes time.
  tooSoon,

  /// Too close to a departure the caller has already promised.
  clash,

  /// The caller is out on a trip, so nothing today is available.
  engaged,
}

/// Who is being asked, which decides only the wording of the greyed-slot hint.
///
/// The rule behind [_Blocked.clash] and [_Blocked.engaged] is the same on both
/// sides — one person, one car — but the sentence is not: a driver reads "a trip
/// you are already driving", a rider "a ride you have already booked". One
/// picker, two vocabularies, rather than two pickers.
enum WhenAudience { driver, rider }

/// Departure-time picker — one sheet, one tap.
///
/// Replaces the stock calendar+clock dialog pair: the common cases (now, or a
/// slot in the next hour) are a single tap, and picking any other time is
/// Today/Tomorrow plus a time chip.
///
/// Times the caller cannot actually use are **shown and disabled**, not hidden.
/// Every caller has a real constraint — a rider's posting needs a gathering
/// lead, a driver cannot promise two departures at once, and a rider searching
/// cannot take a seat at an hour they are already riding — and they used to be
/// enforced only after the tap, by a warning that moved the time or by a
/// refusal from the server. A slot you can see is unavailable teaches the rule;
/// a slot that silently vanishes or snaps back does not.
/// Times that have simply passed stay hidden: those are not unavailable, they
/// no longer exist.
///
/// Returns null if dismissed.
Future<WhenSelection?> showWhenPicker(
  BuildContext context,
  DateTime? current, {
  DateTime? earliest,
  List<DateTime> committed = const [],
  Duration clashWindow = Duration.zero,
  bool isEngaged = false,
  WhenAudience audience = WhenAudience.driver,
}) {
  return showModalBottomSheet<WhenSelection>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (_) => _WhenPickerSheet(
      initial: current,
      earliest: earliest,
      committed: committed,
      clashWindow: clashWindow,
      isEngaged: isEngaged,
      audience: audience,
    ),
  );
}

class _WhenPickerSheet extends StatefulWidget {
  const _WhenPickerSheet({
    this.initial,
    this.earliest,
    this.committed = const [],
    this.clashWindow = Duration.zero,
    this.isEngaged = false,
    this.audience = WhenAudience.driver,
  });

  final DateTime? initial;
  final DateTime? earliest;
  final List<DateTime> committed;
  final Duration clashWindow;
  final bool isEngaged;
  final WhenAudience audience;

  @override
  State<_WhenPickerSheet> createState() => _WhenPickerSheetState();
}

class _WhenPickerSheetState extends State<_WhenPickerSheet> {
  /// 0 = today, 1 = tomorrow.
  late int _day = _initialDay();

  int _initialDay() {
    final d = widget.initial;
    final today = _dayStart(0);
    if (d != null) {
      final picked = DateTime(d.year, d.month, d.day);
      if (picked.isAfter(today)) return 1;
    }
    // Open on the first day that has something to offer, so a rider whose lead
    // pushes past midnight does not land on a page of grey.
    return _slots(0).any((s) => _blockedReason(s) == _Blocked.none) ? 0 : 1;
  }

  DateTime _dayStart(int day) {
    final now = DateTime.now();
    return DateTime(now.year, now.month, now.day).add(Duration(days: day));
  }

  void _pick(DateTime? when) => Navigator.pop(context, WhenSelection(when));

  /// 30-minute slots across a day, dropping only the ones already gone.
  List<DateTime> _slots(int day) {
    final base = _dayStart(day);
    final now = DateTime.now();
    final out = <DateTime>[];
    for (var m = 0; m < 24 * 60; m += 30) {
      final t = base.add(Duration(minutes: m));
      if (t.isAfter(now)) out.add(t);
    }
    return out;
  }

  /// Why [slot] cannot be chosen, or [_Blocked.none].
  ///
  /// [fromNow] marks an instant derived from this moment — the Now / in 15 /
  /// in 30 chips. Such a slot can never be genuinely past, and judging it
  /// against a clock read microseconds later would grey out "Now" every time.
  _Blocked _blockedReason(DateTime slot, {bool fromNow = false}) {
    if (widget.isEngaged) return _Blocked.engaged;
    if (!fromNow && !slot.isAfter(DateTime.now())) return _Blocked.past;

    final earliest = widget.earliest;
    if (earliest != null && slot.isBefore(earliest)) return _Blocked.tooSoon;

    if (widget.clashWindow > Duration.zero) {
      for (final promised in widget.committed) {
        if (promised.difference(slot).abs() < widget.clashWindow) return _Blocked.clash;
      }
    }
    return _Blocked.none;
  }

  bool _isSelected(DateTime t) {
    final d = widget.initial;
    return d != null &&
        d.year == t.year &&
        d.month == t.month &&
        d.day == t.day &&
        d.hour == t.hour &&
        d.minute == t.minute;
  }

  /// The one sentence that explains the greyed chips on screen. Ordered by how
  /// much it stops the caller: being out on a trip beats everything, then a
  /// lead that has not elapsed, then a clash further down the day.
  String? _hint(List<DateTime> slots) {
    final rider = widget.audience == WhenAudience.rider;
    if (widget.isEngaged) {
      return context.tr(rider ? 'when.riderEngagedHint' : 'when.engagedHint');
    }

    final reasons = slots.map(_blockedReason).toSet();
    if (reasons.contains(_Blocked.tooSoon)) {
      final earliest = widget.earliest!;
      return context.tr('when.tooSoonHint', {'time': _clock(earliest)});
    }
    if (reasons.contains(_Blocked.clash)) {
      return context.tr(rider ? 'when.riderClashHint' : 'when.clashHint');
    }
    return null;
  }

  String _clock(DateTime t) => DateFormat('HH:mm', context.l10n.localeName).format(t);

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final slots = _slots(_day);
    final hint = _hint(slots);

    return Container(
      constraints: BoxConstraints(maxHeight: MediaQuery.of(context).size.height * 0.78),
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: const BorderRadius.vertical(top: Radius.circular(24)),
      ),
      child: SafeArea(
        top: false,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SizedBox(height: 10),
            Center(
              child: Container(
                width: 40, height: 4,
                decoration: BoxDecoration(color: t.border, borderRadius: BorderRadius.circular(2)),
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 16, 20, 0),
              child: Text(context.tr('when.title'),
                  style: TextStyle(fontSize: 19, fontWeight: FontWeight.w800, letterSpacing: -0.3, color: t.ink)),
            ),
            // ── one-tap common choices ──
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 16, 20, 0),
              child: Row(children: [
                Expanded(child: _quick(t, context.tr('home.now'), null, highlight: true)),
                const SizedBox(width: 10),
                Expanded(
                    child: _quick(t, context.tr('when.inMinutes', {'value': 15}),
                        const Duration(minutes: 15))),
                const SizedBox(width: 10),
                Expanded(
                    child: _quick(t, context.tr('when.inMinutes', {'value': 30}),
                        const Duration(minutes: 30))),
              ]),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 20, 20, 12),
              child: Row(children: [
                Expanded(child: Divider(color: t.border)),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 12),
                  child: MonoLabel(context.tr('when.orPickTime'), spacing: 0.8),
                ),
                Expanded(child: Divider(color: t.border)),
              ]),
            ),
            // ── day switch ──
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Row(children: [
                _dayTab(t, context.tr('common.today'), 0),
                const SizedBox(width: 10),
                _dayTab(t, context.tr('common.tomorrow'), 1),
              ]),
            ),
            if (hint != null)
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 12, 20, 0),
                child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Icon(Icons.info_outline, size: 15, color: t.ink2),
                  const SizedBox(width: 7),
                  Expanded(
                    child: Text(hint,
                        style: TextStyle(fontSize: 12.5, height: 1.4, color: t.ink2)),
                  ),
                ]),
              ),
            const SizedBox(height: 14),
            // ── time slots ──
            Flexible(
              child: slots.isEmpty
                  ? Padding(
                      padding: const EdgeInsets.fromLTRB(20, 24, 20, 24),
                      child: Text(context.tr('when.noTimesLeft'), style: TextStyle(color: t.ink2)),
                    )
                  : SingleChildScrollView(
                      padding: const EdgeInsets.fromLTRB(20, 0, 20, 20),
                      child: Wrap(
                        spacing: 10,
                        runSpacing: 10,
                        children: slots.map((s) => _slotChip(t, s)).toList(),
                      ),
                    ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _quick(WanesTokens t, String label, Duration? offset, {bool highlight = false}) {
    // "Now" means this instant; the others mean a few minutes from it. Both are
    // real departures and are held to the same rule as any slot.
    final when = DateTime.now().add(offset ?? Duration.zero);
    final enabled = _blockedReason(when, fromNow: true) == _Blocked.none;

    return Semantics(
      enabled: enabled,
      button: true,
      child: GestureDetector(
        onTap: enabled ? () => _pick(offset == null ? null : when) : null,
        child: Opacity(
          opacity: enabled ? 1 : 0.45,
          child: Container(
            padding: const EdgeInsets.symmetric(vertical: 14),
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: !enabled
                  ? t.surface2
                  : highlight
                      ? t.tealTint
                      : t.surface2,
              borderRadius: BorderRadius.circular(14),
              border: Border.all(
                  color: highlight && enabled ? Colors.transparent : t.border),
            ),
            child: Text(label,
                style: TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 14,
                    color: !enabled ? t.ink2 : (highlight ? t.tealInk : t.ink))),
          ),
        ),
      ),
    );
  }

  Widget _dayTab(WanesTokens t, String label, int value) {
    final active = _day == value;
    // A day with nothing choosable on it is a dead tab; say so rather than let
    // it open onto a wall of grey.
    final enabled = _slots(value).any((s) => _blockedReason(s) == _Blocked.none);

    return Semantics(
      enabled: enabled,
      button: true,
      selected: active,
      child: GestureDetector(
        onTap: enabled ? () => setState(() => _day = value) : null,
        child: Opacity(
          opacity: enabled ? 1 : 0.45,
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 10),
            decoration: BoxDecoration(
              color: active ? t.ink : t.surface2,
              borderRadius: BorderRadius.circular(999),
              border: Border.all(color: active ? t.ink : t.border),
            ),
            child: Text(label,
                style: TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 13.5,
                    color: active ? t.surface : t.ink2)),
          ),
        ),
      ),
    );
  }

  Widget _slotChip(WanesTokens t, DateTime slot) {
    final blocked = _blockedReason(slot);
    final enabled = blocked == _Blocked.none;
    final selected = enabled && _isSelected(slot);

    return Semantics(
      enabled: enabled,
      button: true,
      selected: selected,
      label: enabled ? null : '${_clock(slot)}, ${context.tr('when.unavailable')}',
      child: GestureDetector(
        onTap: enabled ? () => _pick(slot) : null,
        child: Opacity(
          opacity: enabled ? 1 : 0.4,
          child: Container(
            width: 78,
            padding: const EdgeInsets.symmetric(vertical: 12),
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: selected ? t.teal : t.surface2,
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: selected ? t.teal : t.border),
            ),
            child: Text(
              _clock(slot),
              style: WanesTheme.mono(
                size: 14,
                weight: FontWeight.w700,
                color: selected ? t.onTeal : t.ink,
              ).copyWith(
                // A struck-through time is unavailable at a glance, without
                // relying on the grey alone — which does not survive a bright
                // screen outdoors.
                decoration: enabled ? null : TextDecoration.lineThrough,
                decorationColor: t.ink2,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
