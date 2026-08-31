import 'package:flutter/material.dart';
import '../../core/app_response.dart';
import '../../core/l10n.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/wanes_alerts.dart';

/// The single move a driver may make on a trip from where it stands.
///
/// The server enforces the same order in `TripService.CanTransition`, so this
/// is the UI half of one rule, not a second one: Posted/Full → Arrived →
/// Active → Completed, and nothing at all once the trip is finished or
/// cancelled. Each move is what advances the riders' tracking rail.
class DriverTripStep {
  const DriverTripStep(this.labelKey, this.doneKey, this.icon, this.call);

  final String labelKey;
  final String doneKey;
  final IconData icon;
  final Future<AppResponse<Trip>> Function(int id) call;

  static DriverTripStep? forTrip(Trip trip, TripService trips) => switch (trip.status) {
        1 || 2 => DriverTripStep('driver.arriveTrip', 'driver.tripArrived',
            Icons.location_on_outlined, trips.arrive),
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
          ? SizedBox(
              height: 18,
              width: 18,
              child: CircularProgressIndicator(
                  strokeWidth: 2.2, valueColor: AlwaysStoppedAnimation(t.onTeal)))
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
