import 'package:flutter/material.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';

/// A seat that is held but not yet committed, because the trip is short of the
/// seats its driver asked for.
///
/// It reports rather than asks. Nothing is owed by this rider — the trip is
/// waiting on other people — and the one thing they need to know is that it
/// might not run, which is exactly what a status pill reading "Pending" fails
/// to say.
class GatheringChip extends StatelessWidget {
  const GatheringChip({super.key, required this.booking});

  final Booking booking;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final missing = booking.minSeatsToConfirm - booking.seatsHeld;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
      decoration: BoxDecoration(
        color: t.surface2,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: t.border),
      ),
      child: Row(children: [
        Icon(Icons.groups_2_outlined, size: 14, color: t.ink2),
        const SizedBox(width: 7),
        Expanded(
          child: Text(
            missing > 0
                ? context.tr('trip.gathering', {'left': missing})
                : context.tr('trip.confirmsAt', {'min': booking.minSeatsToConfirm}),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(fontWeight: FontWeight.w600, fontSize: 12, color: t.ink2),
          ),
        ),
      ]),
    );
  }
}
