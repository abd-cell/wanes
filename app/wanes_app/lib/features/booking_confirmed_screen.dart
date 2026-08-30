import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../core/fare.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../widgets/wanes_ui.dart';
import 'live_trip_screen.dart';

/// Booking confirmed — prototype screen 14. The teal tick, a summary of who is
/// picking you up and when, the booking reference, then View trip.
class BookingConfirmedScreen extends StatefulWidget {
  const BookingConfirmedScreen({
    super.key,
    required this.trip,
    required this.booking,
    this.seats = 1,
    this.total = 0,
  });

  final Trip trip;
  final Booking booking;
  final int seats;
  final double total;

  @override
  State<BookingConfirmedScreen> createState() => _BookingConfirmedScreenState();
}

class _BookingConfirmedScreenState extends State<BookingConfirmedScreen>
    with SingleTickerProviderStateMixin {
  /// `@keyframes spop` — the tick scales in past 1 and settles.
  late final AnimationController _pop =
      AnimationController(vsync: this, duration: const Duration(milliseconds: 620))..forward();

  @override
  void dispose() {
    _pop.dispose();
    super.dispose();
  }

  Trip get trip => widget.trip;

  /// Booking reference — "WNS-" plus the booking id in base-36, which is what
  /// the rider can quote to the driver. Derived, not invented.
  String get _ref => 'WNS-${widget.booking.id.toRadixString(36).toUpperCase().padLeft(4, '0')}';

  static String initialsOf(String name) {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
    return (parts.first[0] + parts.last[0]).toUpperCase();
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final depart = DateFormat('HH:mm', context.l10n.localeName).format(trip.departAt.toLocal());
    final firstName = trip.driverName.trim().split(' ').first;

    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(22, 20, 22, 24),
          child: Column(children: [
            const SizedBox(height: 22),
            ScaleTransition(
              scale: CurvedAnimation(parent: _pop, curve: Curves.easeOutBack),
              child: SizedBox(
                width: 88,
                height: 88,
                child: Stack(alignment: Alignment.center, children: [
                  Container(
                    decoration: BoxDecoration(color: t.tealTint, shape: BoxShape.circle),
                  ),
                  Container(
                    width: 64,
                    height: 64,
                    decoration: BoxDecoration(
                      color: t.teal,
                      shape: BoxShape.circle,
                      boxShadow: [
                        BoxShadow(
                            color: t.teal, blurRadius: 28, offset: const Offset(0, 12), spreadRadius: -8),
                      ],
                    ),
                    child: Icon(Icons.check_rounded, size: 34, color: t.onTeal),
                  ),
                ]),
              ),
            ),
            const SizedBox(height: 20),
            Text(context.tr('booking.youreBooked'),
                style: TextStyle(
                    fontSize: 25, fontWeight: FontWeight.w800, letterSpacing: -0.5, color: t.ink)),
            const SizedBox(height: 8),
            ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 260),
              child: Text.rich(
                TextSpan(children: [
                  TextSpan(text: context.tr('booking.pickYouUpPrefix', {'name': firstName})),
                  TextSpan(
                      text: depart,
                      style: TextStyle(fontWeight: FontWeight.w700, color: t.ink)),
                  TextSpan(text: context.tr('booking.pickYouUpSuffix')),
                ]),
                textAlign: TextAlign.center,
                style: TextStyle(color: t.ink2, fontSize: 14, height: 1.5),
              ),
            ),
            const SizedBox(height: 24),
            _summaryCard(t, depart),
            const SizedBox(height: 16),
            Text.rich(
              TextSpan(children: [
                TextSpan(
                    text: '${context.tr('booking.reference')} · ',
                    style: WanesTheme.mono(size: 11, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
                TextSpan(
                    text: _ref,
                    style: WanesTheme.mono(size: 11, weight: FontWeight.w600, color: t.ink, spacing: 1.0)),
                // The reference is an ASCII code — keep it left-to-right.
                const TextSpan(text: '\u200E'),
              ]),
            ),
            const Spacer(),
            PrimaryButton(
              label: context.tr('booking.viewTrip'),
              arrow: false,
              onPressed: () => Navigator.pushReplacement(
                context,
                MaterialPageRoute(
                  builder: (_) => LiveTripScreen(trip: trip, bookingId: widget.booking.id),
                ),
              ),
            ),
            const SizedBox(height: 11),
            SizedBox(
              width: double.infinity,
              child: OutlinedButton(
                onPressed: () => Navigator.of(context).popUntil((r) => r.isFirst),
                style: OutlinedButton.styleFrom(
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                ),
                child: Text(context.tr('booking.backToHome')),
              ),
            ),
          ]),
        ),
      ),
    );
  }

  Widget _summaryCard(WanesTokens t, String depart) {
    final car = [trip.vehicleLabel, trip.vehiclePlate].where((s) => s.isNotEmpty).join(' · ');
    return WanesCard(
      radius: 16,
      child: Column(children: [
        Row(children: [
          AvatarBadge(initialsOf(trip.driverName), size: 46, tint: t.tealTint, fg: t.tealInk),
          const SizedBox(width: 13),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(trip.driverName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15, color: t.ink)),
              if (car.isNotEmpty) ...[
                const SizedBox(height: 2),
                Text(car,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: WanesTheme.mono(
                        size: 11.5, weight: FontWeight.w500, color: t.ink2, spacing: 0.4)),
              ],
            ]),
          ),
          const SizedBox(width: 8),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
            decoration: BoxDecoration(
              color: t.surface2,
              borderRadius: BorderRadius.circular(8),
              border: Border.all(color: t.border),
            ),
            child: InlineRating(trip.driverRating.toStringAsFixed(1), size: 11),
          ),
        ]),
        const SizedBox(height: 14),
        Divider(height: 1, thickness: 1, color: t.border),
        const SizedBox(height: 14),
        Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
          _fact(t, context.tr('common.seats'), '${widget.seats}', CrossAxisAlignment.start),
          _fact(t, context.tr('common.departs'), depart, CrossAxisAlignment.center),
          _fact(t, context.tr('common.fare'), widget.total > 0 ? Fare.format(widget.total) : '—',
              CrossAxisAlignment.end,
              valueColor: t.tealInk, mono: true),
        ]),
      ]),
    );
  }

  Widget _fact(WanesTokens t, String label, String value, CrossAxisAlignment align,
      {Color? valueColor, bool mono = false}) {
    return Column(crossAxisAlignment: align, mainAxisSize: MainAxisSize.min, children: [
      MonoLabel(label, spacing: 0.8),
      const SizedBox(height: 3),
      Text(value,
          style: mono
              ? WanesTheme.mono(size: 15, weight: FontWeight.w800, color: valueColor ?? t.ink, spacing: 0)
              : TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: valueColor ?? t.ink)),
    ]);
  }
}
