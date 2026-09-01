import 'package:flutter/material.dart';

import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';

/// Sort picker for the carpool results — one sheet, one tap.
///
/// Pass [allowPickup] as false when the rider's start point is unknown, since
/// "shortest walk" cannot be worked out without it and a dead option is worse
/// than a missing one. Returns null if dismissed.
Future<TripSort?> showSortPicker(
  BuildContext context,
  TripSort current, {
  bool allowPickup = true,
}) {
  return showModalBottomSheet<TripSort>(
    context: context,
    // Six options plus a title overrun the default 9/16 sheet on a short handset,
    // and overrun any handset once the reader turns text size up.
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (_) => _SortPickerSheet(current: current, allowPickup: allowPickup),
  );
}

class _SortPickerSheet extends StatelessWidget {
  const _SortPickerSheet({required this.current, required this.allowPickup});

  final TripSort current;
  final bool allowPickup;

  static const _icons = {
    TripSort.best: Icons.auto_awesome_outlined,
    TripSort.departure: Icons.schedule_rounded,
    TripSort.price: Icons.payments_outlined,
    TripSort.rating: Icons.star_outline_rounded,
    TripSort.pickup: Icons.directions_walk_rounded,
    TripSort.seats: Icons.airline_seat_recline_normal_outlined,
  };

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final options = TripSort.values
        .where((s) => allowPickup || s != TripSort.pickup)
        .toList();

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
                width: 40,
                height: 4,
                decoration: BoxDecoration(color: t.border, borderRadius: BorderRadius.circular(2)),
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 16, 20, 4),
              child: Text(context.tr('sort.title'),
                  style: TextStyle(
                      fontSize: 19, fontWeight: FontWeight.w800, letterSpacing: -0.3, color: t.ink)),
            ),
            const SizedBox(height: 8),
            Flexible(
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [for (final option in options) _row(context, t, option)],
                ),
              ),
            ),
            const SizedBox(height: 12),
          ],
        ),
      ),
    );
  }

  Widget _row(BuildContext context, WanesTokens t, TripSort option) {
    final selected = option == current;
    return InkWell(
      onTap: () => Navigator.pop(context, option),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 13),
        child: Row(children: [
          Container(
            width: 34,
            height: 34,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: selected ? t.tealTint : t.surface2,
              borderRadius: BorderRadius.circular(10),
            ),
            child: Icon(_icons[option], size: 18, color: selected ? t.tealInk : t.ink2),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Text(context.tr(option.labelKey),
                style: TextStyle(
                    fontSize: 14.5,
                    fontWeight: selected ? FontWeight.w800 : FontWeight.w600,
                    color: selected ? t.ink : t.ink2)),
          ),
          if (selected) Icon(Icons.check_rounded, size: 19, color: t.tealInk),
        ]),
      ),
    );
  }
}
