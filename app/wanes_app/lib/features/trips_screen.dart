import 'package:flutter/material.dart';
import '../core/l10n.dart';
import '../core/theme.dart';

/// The rider "Trips" tab. A booking-history feed will populate this once the
/// backend exposes a rider bookings list; for now it presents the prototype's
/// Upcoming / Past framing with a clean empty state.
class TripsScreen extends StatefulWidget {
  const TripsScreen({super.key});

  @override
  State<TripsScreen> createState() => _TripsScreenState();
}

class _TripsScreenState extends State<TripsScreen> {
  int _tab = 0; // 0 upcoming · 1 past

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return SafeArea(
      bottom: false,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
        children: [
          Text(context.tr('trips.title'),
              style: TextStyle(
                  fontSize: 23, fontWeight: FontWeight.w800, letterSpacing: -0.4, color: t.ink)),
          const SizedBox(height: 16),
          _segmented(t),
          const SizedBox(height: 20),
          _empty(t),
        ],
      ),
    );
  }

  Widget _segmented(WanesTokens t) {
    Widget seg(String label, int i) {
      final active = _tab == i;
      return Expanded(
        child: GestureDetector(
          onTap: () => setState(() => _tab = i),
          child: Container(
            padding: const EdgeInsets.symmetric(vertical: 10),
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: active ? t.surface : Colors.transparent,
              borderRadius: BorderRadius.circular(10),
              boxShadow: active ? [BoxShadow(color: t.shadow, blurRadius: 12, offset: const Offset(0, 4), spreadRadius: -8)] : null,
            ),
            child: Text(label, style: TextStyle(fontWeight: FontWeight.w700, fontSize: 13.5, color: active ? t.ink : t.ink2)),
          ),
        ),
      );
    }

    return Container(
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(color: t.surface2, borderRadius: BorderRadius.circular(12), border: Border.all(color: t.border)),
      child: Row(children: [
        seg(context.tr('trips.upcoming'), 0),
        seg(context.tr('trips.past'), 1),
      ]),
    );
  }

  Widget _empty(WanesTokens t) {
    return Padding(
      padding: const EdgeInsets.only(top: 48),
      child: Column(children: [
        Container(
          width: 64, height: 64,
          decoration: BoxDecoration(color: t.tealTint, borderRadius: BorderRadius.circular(20)),
          child: Icon(_tab == 0 ? Icons.route_rounded : Icons.history_rounded, color: t.tealInk, size: 28),
        ),
        const SizedBox(height: 16),
        Text(context.tr(_tab == 0 ? 'trips.noUpcoming' : 'trips.noPast'),
            style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: t.ink)),
        const SizedBox(height: 6),
        Text(context.tr(_tab == 0 ? 'trips.noUpcomingBody' : 'trips.noPastBody'),
            textAlign: TextAlign.center, style: TextStyle(color: t.ink2, fontSize: 13.5)),
      ]),
    );
  }
}
