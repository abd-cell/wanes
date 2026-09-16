import 'package:flutter/material.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import 'wanes_ui.dart';

/// Who you will share a car with, as one card.
///
/// The same editor serves three screens — a rider posting a trip, a driver
/// posting one, and either side writing a schedule — because it is the same
/// question pointed in different directions. A driver says who may take their
/// seats; a rider says who may drive them and who else may be aboard. Passing
/// the direction in rather than building two cards is what keeps the wording,
/// the ordering and the "any" default identical on both sides, which matters:
/// these are the fields people read most carefully.
class ConditionsCard extends StatelessWidget {
  const ConditionsCard({
    super.key,
    required this.coRiderPolicy,
    required this.onCoRiderPolicy,
    this.driverPolicy,
    this.onDriverPolicy,
    this.minAge,
    this.maxAge,
    this.onAges,
    this.coRiderTitleKey = 'conditions.passengers',
  });

  /// Who may take a seat (driver's side) or share the car (rider's side).
  final GenderPolicy coRiderPolicy;
  final ValueChanged<GenderPolicy> onCoRiderPolicy;

  /// Who may drive. Rider-side only — a driver does not set a condition on
  /// themselves — so it is absent rather than disabled on the driver's screen.
  final GenderPolicy? driverPolicy;
  final ValueChanged<GenderPolicy>? onDriverPolicy;

  final int? minAge;
  final int? maxAge;

  /// Null hides the age row: a schedule editor with no room for it, say.
  final void Function(int? minAge, int? maxAge)? onAges;

  /// "Passengers" on a driver's trip, "Co-riders" on a rider's posting.
  final String coRiderTitleKey;

  static const _ageBands = <(String, int?, int?)>[
    ('conditions.anyAge', null, null),
    ('conditions.age18to30', 18, 30),
    ('conditions.age25to45', 25, 45),
    ('conditions.age30plus', 30, null),
  ];

  String _ageLabel(BuildContext context) {
    for (final (key, from, to) in _ageBands) {
      if (from == minAge && to == maxAge) return context.tr(key);
    }
    if (minAge != null && maxAge != null) return '$minAge–$maxAge';
    if (minAge != null) return context.tr('conditions.ageFrom', {'age': minAge!});
    return context.tr('conditions.ageTo', {'age': maxAge!});
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final driver = driverPolicy;

    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      SectionHeader(context.tr('conditions.title')),
      const SizedBox(height: 8),
      GroupedCard(children: [
        if (driver != null && onDriverPolicy != null)
          GroupedRow(
            icon: Icons.drive_eta_outlined,
            title: context.tr('conditions.driver'),
            subtitle: driver.label,
            trailing: Icon(Icons.expand_more_rounded, size: 18, color: t.ink2),
            onTap: () => _pickPolicy(context, driver, onDriverPolicy!),
          ),
        GroupedRow(
          icon: Icons.groups_outlined,
          title: context.tr(coRiderTitleKey),
          subtitle: coRiderPolicy.label,
          trailing: Icon(Icons.expand_more_rounded, size: 18, color: t.ink2),
          onTap: () => _pickPolicy(context, coRiderPolicy, onCoRiderPolicy),
        ),
        if (onAges != null)
          GroupedRow(
            icon: Icons.cake_outlined,
            title: context.tr('conditions.age'),
            subtitle: _ageLabel(context),
            trailing: Icon(Icons.expand_more_rounded, size: 18, color: t.ink2),
            onTap: () => _pickAge(context),
          ),
      ]),
      const SizedBox(height: 8),
      // Said once, plainly: a condition is only checked against a profile that
      // answers it, and somebody who has not said is refused rather than
      // guessed at. People set these expecting them to hold.
      Text(context.tr('conditions.hint'),
          style: TextStyle(fontSize: 11.5, height: 1.45, color: t.ink2)),
    ]);
  }

  Future<void> _pickPolicy(
      BuildContext context, GenderPolicy current, ValueChanged<GenderPolicy> onPick) async {
    final picked = await _showOptions<GenderPolicy>(
      context,
      title: context.tr('conditions.title'),
      options: GenderPolicy.values
          .map((p) => (value: p, label: p.label, selected: p == current))
          .toList(),
    );
    if (picked != null) onPick(picked);
  }

  Future<void> _pickAge(BuildContext context) async {
    final picked = await _showOptions<int>(
      context,
      title: context.tr('conditions.age'),
      options: [
        for (var i = 0; i < _ageBands.length; i++)
          (
            value: i,
            label: context.tr(_ageBands[i].$1),
            selected: _ageBands[i].$2 == minAge && _ageBands[i].$3 == maxAge,
          ),
      ],
    );
    if (picked == null) return;
    onAges?.call(_ageBands[picked].$2, _ageBands[picked].$3);
  }
}

/// A plain option sheet, in the kit's own idiom — the pickers here are short
/// closed lists, and a full picker screen for three choices is a screen the
/// rider has to come back out of.
Future<T?> _showOptions<T>(
  BuildContext context, {
  required String title,
  required List<({T value, String label, bool selected})> options,
}) {
  final t = WanesTokens.of(context);
  return showModalBottomSheet<T>(
    context: context,
    backgroundColor: t.surface,
    shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(22))),
    builder: (sheetContext) => SafeArea(
      child: Column(mainAxisSize: MainAxisSize.min, children: [
        const SizedBox(height: 10),
        Container(
          width: 38,
          height: 4,
          decoration: BoxDecoration(color: t.border, borderRadius: BorderRadius.circular(2)),
        ),
        const SizedBox(height: 14),
        Text(title, style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15, color: t.ink)),
        const SizedBox(height: 8),
        for (final option in options)
          ListTile(
            title: Text(option.label,
                style: TextStyle(
                    fontWeight: option.selected ? FontWeight.w700 : FontWeight.w500,
                    fontSize: 14,
                    color: t.ink)),
            trailing: option.selected
                ? Icon(Icons.check_rounded, size: 18, color: t.teal)
                : null,
            onTap: () => Navigator.pop(sheetContext, option.value),
          ),
        const SizedBox(height: 8),
      ]),
    ),
  );
}
