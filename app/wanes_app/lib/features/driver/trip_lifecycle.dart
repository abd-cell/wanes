import 'package:flutter/material.dart';
import '../../core/app_config.dart';
import '../../core/app_response.dart';
import '../../core/l10n.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/trip_safety.dart';
import '../../widgets/wanes_alerts.dart';
import '../../widgets/wanes_motion.dart';

/// The single move a driver may make on the **whole trip** from where it
/// stands — every rider at once.
///
/// The server enforces the same order in `TripService.CanTransition`, so this
/// is the UI half of one rule, not a second one: Posted/Full → EnRoute →
/// Arrived → Active → Completed, and nothing at all once the trip is finished
/// or cancelled. Per-rider moves live in [SeatStep]; a trip's status is derived
/// from its seats either way — EnRoute excepted, which is the driver saying they
/// have set off and moves nobody's seat.
class DriverTripStep {
  const DriverTripStep(this.labelKey, this.doneKey, this.icon, this.call);

  final String labelKey;
  final String doneKey;
  final IconData icon;
  final Future<AppResponse<Trip>> Function(int id) call;

  static DriverTripStep? forTrip(Trip trip, TripService trips) => switch (trip.status) {
        // Setting off is the offered move on a trip still waiting to go: it is
        // what takes the trip out of search, so it comes before the first kerb.
        // A driver who skips it and goes straight to arriving is still allowed
        // — the server permits Posted → Arrived — they just lose the step.
        1 || 2 => DriverTripStep('driver.departTrip', 'driver.tripEnRoute',
            Icons.navigation_outlined, trips.depart),
        7 => DriverTripStep('driver.arriveTrip', 'driver.tripArrived',
            Icons.location_on_outlined, trips.arrive),
        // Boarding everyone at once would skip the riders' codes, so with codes
        // on the driver boards each rider from their own card instead.
        6 when AppConfigController.value.boardingCodeRequired => null,
        6 => DriverTripStep(
            'driver.startTrip', 'driver.tripStarted', Icons.play_arrow_rounded, trips.start),
        3 => DriverTripStep(
            'driver.completeTrip', 'driver.tripCompleted', Icons.flag_outlined, trips.complete),
        _ => null,
      };
}

/// Renders the trip's next move, or nothing when there is none. Owns the
/// in-flight state so a caller only has to say what to do afterwards.
class TripStepButton extends StatefulWidget {
  const TripStepButton({
    super.key,
    required this.trip,
    required this.onChanged,
    this.expand = false,
  });

  final Trip trip;

  /// Called after any attempt that may have moved the trip — including a
  /// refusal, which means the server's view differs from ours and the caller
  /// should reload rather than keep offering a button that cannot work.
  final VoidCallback onChanged;

  /// Full-width (the details screen) rather than sized to its label (a card).
  final bool expand;

  @override
  State<TripStepButton> createState() => _TripStepButtonState();
}

class _TripStepButtonState extends State<TripStepButton> {
  final _trips = TripService();
  bool _busy = false;

  Future<void> _advance(DriverTripStep step) async {
    if (_busy) return;
    setState(() => _busy = true);
    final res = await step.call(widget.trip.id);
    if (!mounted) return;
    setState(() => _busy = false);

    if (res.success) {
      WanesAlerts.success(context, context.tr(step.doneKey));
    } else {
      WanesAlerts.failure(context, res, title: context.tr('driver.tripUpdateFailed'));
    }
    widget.onChanged();
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final step = DriverTripStep.forTrip(widget.trip, _trips);
    if (step == null) return const SizedBox.shrink();

    final button = FilledButton(
      onPressed: _busy ? null : () => _advance(step),
      style: FilledButton.styleFrom(
        backgroundColor: t.teal,
        foregroundColor: t.onTeal,
        padding: const EdgeInsets.symmetric(horizontal: 14),
        minimumSize: Size(0, widget.expand ? 48 : 38),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(widget.expand ? 12 : 11)),
      ),
      child: _busy
          ? WanesSpinner.mono(t.onTeal, size: 18)
          : Row(mainAxisSize: MainAxisSize.min, children: [
              Icon(step.icon, size: widget.expand ? 18 : 16),
              const SizedBox(width: 6),
              Text(context.tr(step.labelKey),
                  style: TextStyle(
                      fontWeight: FontWeight.w700, fontSize: widget.expand ? 14.5 : 13)),
            ]),
    );

    return widget.expand ? SizedBox(width: double.infinity, child: button) : button;
  }
}

/// One move a driver may make on **one rider's seat**.
///
/// A carpool collects its riders one at a time, so this — not the trip-wide
/// button — is the driver's ordinary tool: reach a rider, pick them up, drop
/// them off, or give up on them. The order mirrors the server's
/// `BookingStatusRules.CanDriverSet`, and the trip's own status is derived from
/// the seats afterwards, so nothing here sets it directly.
class SeatStep {
  const SeatStep(this.status, this.labelKey, this.doneKey, this.icon, {this.confirm = false});

  /// The server's `BookingStatus` this move puts the seat at.
  final int status;
  final String labelKey;
  final String doneKey;
  final IconData icon;

  /// Ask first. A no-show costs the rider their seat and cannot be undone.
  final bool confirm;

  static const arrive =
      SeatStep(6, 'driver.seatArrive', 'driver.seatArriveDone', Icons.location_on_outlined);
  static const pickUp =
      SeatStep(3, 'driver.seatPickUp', 'driver.seatPickUpDone', Icons.person_add_alt_1_outlined);
  static const dropOff =
      SeatStep(4, 'driver.seatDropOff', 'driver.seatDropOffDone', Icons.flag_outlined);
  static const noShow = SeatStep(7, 'driver.seatNoShow', 'driver.seatNoShowDone',
      Icons.person_off_outlined, confirm: true);

  /// What the driver can do to this seat right now, in the order the journey
  /// takes: the next step first, then giving up on the rider where that is
  /// still possible. Empty once the seat is settled.
  static List<SeatStep> forSeat(TripBooking seat) => switch (seat.status) {
        1 || 2 => [arrive, pickUp, noShow],
        6 => [pickUp, noShow],
        3 => [dropOff],
        _ => [],
      };
}

/// The seat's moves as a row of buttons — the first one filled, the rest quiet.
/// Owns the in-flight state and the no-show confirmation, so the manifest only
/// has to say what to do afterwards.
class SeatStepButtons extends StatefulWidget {
  const SeatStepButtons({
    super.key,
    required this.tripId,
    required this.seat,
    required this.onChanged,
  });

  final int tripId;
  final TripBooking seat;

  /// Called after any attempt that may have moved the seat — including a
  /// refusal, which means the server's view differs from ours and the caller
  /// should reload rather than keep offering a button that cannot work.
  final VoidCallback onChanged;

  @override
  State<SeatStepButtons> createState() => _SeatStepButtonsState();
}

class _SeatStepButtonsState extends State<SeatStepButtons> {
  final _trips = TripService();
  bool _busy = false;

  Future<void> _advance(SeatStep step) async {
    if (_busy) return;
    if (step.confirm && !await _confirm(step)) return;
    if (!mounted) return;

    // Boarding: the rider reads their code, the driver types it.
    String? code;
    if (step == SeatStep.pickUp && AppConfigController.value.boardingCodeRequired) {
      code = await showBoardingCodeEntry(context, widget.seat.riderName);
      if (code == null || !mounted) return;
    }

    setState(() => _busy = true);
    final res = await _trips.setBookingStatus(widget.tripId, widget.seat.id, step.status,
        boardingCode: code);
    if (!mounted) return;
    setState(() => _busy = false);

    if (res.success) {
      WanesAlerts.success(context, context.tr(step.doneKey));
    } else {
      WanesAlerts.failure(context, res, title: context.tr('driver.seatUpdateFailed'));
    }
    widget.onChanged();
  }

  Future<bool> _confirm(SeatStep step) async {
    final t = WanesTokens.of(context);
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(context.tr('driver.seatNoShowTitle', {'name': widget.seat.riderName})),
        content: Text(context.tr('driver.seatNoShowBody')),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: Text(context.tr('common.cancel'))),
          TextButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: Text(context.tr(step.labelKey), style: TextStyle(color: t.alert)),
          ),
        ],
      ),
    );
    return ok == true;
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final steps = SeatStep.forSeat(widget.seat);
    if (steps.isEmpty) return const SizedBox.shrink();

    return Wrap(
      spacing: 8,
      runSpacing: 8,
      children: [
        for (final (index, step) in steps.indexed)
          index == 0 ? _primary(t, step) : _secondary(t, step),
      ],
    );
  }

  Widget _primary(WanesTokens t, SeatStep step) => FilledButton(
        onPressed: _busy ? null : () => _advance(step),
        style: FilledButton.styleFrom(
          backgroundColor: t.teal,
          foregroundColor: t.onTeal,
          padding: const EdgeInsets.symmetric(horizontal: 12),
          minimumSize: const Size(0, 36),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(11)),
        ),
        child: _busy
            ? WanesSpinner.mono(t.onTeal, size: 16)
            : _label(step, 15),
      );

  Widget _secondary(WanesTokens t, SeatStep step) {
    // A no-show reads as the destructive move it is; anything else is just the
    // step after next, offered quietly.
    final tint = step.confirm ? t.alert : t.ink2;
    return OutlinedButton(
      onPressed: _busy ? null : () => _advance(step),
      style: OutlinedButton.styleFrom(
        foregroundColor: tint,
        side: BorderSide(color: t.border),
        padding: const EdgeInsets.symmetric(horizontal: 12),
        minimumSize: const Size(0, 36),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(11)),
      ),
      child: _label(step, 14),
    );
  }

  Widget _label(SeatStep step, double iconSize) =>
      Row(mainAxisSize: MainAxisSize.min, children: [
        Icon(step.icon, size: iconSize),
        const SizedBox(width: 6),
        Text(context.tr(step.labelKey),
            style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 12.5)),
      ]);
}
