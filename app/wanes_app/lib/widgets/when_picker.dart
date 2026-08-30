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

/// Departure-time picker — one sheet, one tap.
///
/// Replaces the stock calendar+clock dialog pair: the common cases (now, or a
/// slot in the next hour) are a single tap, and picking any other time is
/// Today/Tomorrow plus a time chip. Returns null if dismissed.
Future<WhenSelection?> showWhenPicker(BuildContext context, DateTime? current) {
  return showModalBottomSheet<WhenSelection>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (_) => _WhenPickerSheet(initial: current),
  );
}

class _WhenPickerSheet extends StatefulWidget {
  const _WhenPickerSheet({this.initial});
  final DateTime? initial;

  @override
  State<_WhenPickerSheet> createState() => _WhenPickerSheetState();
}

class _WhenPickerSheetState extends State<_WhenPickerSheet> {
  /// 0 = today, 1 = tomorrow.
  late int _day = _initialDay();

  int _initialDay() {
    final d = widget.initial;
    if (d == null) return 0;
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    final picked = DateTime(d.year, d.month, d.day);
    return picked.isAfter(today) ? 1 : 0;
  }

  void _pick(DateTime? when) =>
      Navigator.pop(context, WhenSelection(when));

  /// 30-minute slots across the chosen day, skipping ones already past.
  List<DateTime> _slots() {
    final now = DateTime.now();
    final base = DateTime(now.year, now.month, now.day).add(Duration(days: _day));
    final cutoff = now.add(const Duration(minutes: 10));
    final out = <DateTime>[];
    for (var m = 0; m < 24 * 60; m += 30) {
      final t = base.add(Duration(minutes: m));
      if (t.isAfter(cutoff)) out.add(t);
    }
    return out;
  }

  bool _isSelected(DateTime t) {
    final d = widget.initial;
    return d != null && d.year == t.year && d.month == t.month && d.day == t.day &&
        d.hour == t.hour && d.minute == t.minute;
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final slots = _slots();

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
                Expanded(
                    child: _quick(t, context.tr('home.now'), null, highlight: true)),
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
            const SizedBox(height: 14),
            // ── time slots ──
            Flexible(
              child: slots.isEmpty
                  ? Padding(
                      padding: const EdgeInsets.fromLTRB(20, 24, 20, 24),
                      child: Text(context.tr('when.noTimesLeft'),
                          style: TextStyle(color: t.ink2)),
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
    return GestureDetector(
      onTap: () => _pick(offset == null ? null : DateTime.now().add(offset)),
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 14),
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: highlight ? t.tealTint : t.surface2,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: highlight ? Colors.transparent : t.border),
        ),
        child: Text(label,
            style: TextStyle(
                fontWeight: FontWeight.w700, fontSize: 14,
                color: highlight ? t.tealInk : t.ink)),
      ),
    );
  }

  Widget _dayTab(WanesTokens t, String label, int value) {
    final active = _day == value;
    return GestureDetector(
      onTap: () => setState(() => _day = value),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 10),
        decoration: BoxDecoration(
          color: active ? t.ink : t.surface2,
          borderRadius: BorderRadius.circular(999),
          border: Border.all(color: active ? t.ink : t.border),
        ),
        child: Text(label,
            style: TextStyle(
                fontWeight: FontWeight.w700, fontSize: 13.5,
                color: active ? t.surface : t.ink2)),
      ),
    );
  }

  Widget _slotChip(WanesTokens t, DateTime slot) {
    final selected = _isSelected(slot);
    return GestureDetector(
      onTap: () => _pick(slot),
      child: Container(
        width: 78,
        padding: const EdgeInsets.symmetric(vertical: 12),
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: selected ? t.teal : t.surface2,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: selected ? t.teal : t.border),
        ),
        child: Text(DateFormat('HH:mm', context.l10n.localeName).format(slot),
            style: WanesTheme.mono(
                size: 14, weight: FontWeight.w700,
                color: selected ? t.onTeal : t.ink)),
      ),
    );
  }
}
