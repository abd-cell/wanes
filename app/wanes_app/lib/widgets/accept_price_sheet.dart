import 'package:flutter/material.dart';

import '../core/fare.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import 'wanes_ui.dart';

/// What the driver is charging per seat, asked before they take a hail.
///
/// A hail carries no price — the rider asked for a ride, not for a quote — so
/// this is the driver's only chance to say what the seat costs, and the trip
/// created by accepting lists at whatever comes back from here.
///
/// It opens on the distance estimate rather than empty. That figure is the one
/// already shown on the request card, off the admin's configured rates, so the
/// driver is confirming a number they have been looking at instead of inventing
/// one against a countdown. Adjusting it is a tap; accepting it is a tap.
///
/// Returns the chosen price, or null if the driver backed out — which must be
/// treated as "did not accept", never as "accept at the default".
Future<double?> showAcceptPriceSheet(
  BuildContext context, {
  required double suggestion,
  required int seats,
}) {
  return showModalBottomSheet<double>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    // The hail is on a clock. Dismissing by tapping away is fine — it is the
    // same as declining — but the sheet must not be dismissed by accident while
    // the driver is reaching for the + button.
    builder: (_) => _AcceptPriceSheet(suggestion: suggestion, seats: seats),
  );
}

class _AcceptPriceSheet extends StatefulWidget {
  const _AcceptPriceSheet({required this.suggestion, required this.seats});

  final double suggestion;
  final int seats;

  @override
  State<_AcceptPriceSheet> createState() => _AcceptPriceSheetState();
}

class _AcceptPriceSheetState extends State<_AcceptPriceSheet> {
  /// Matches the post-a-trip screen's stepper, so a driver meets one idiom for
  /// pricing a seat whichever way they came to it.
  static const _step = 0.5;
  static const _min = 0.0;
  static const _max = 50.0;

  late double _price = widget.suggestion.clamp(_min, _max).toDouble();

  void _nudge(double by) =>
      setState(() => _price = ((_price + by).clamp(_min, _max)).toDouble());

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);

    return Container(
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: const BorderRadius.vertical(top: Radius.circular(24)),
      ),
      child: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 14, 20, 18),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Center(
                child: Container(
                  width: 38, height: 4,
                  decoration: BoxDecoration(
                    color: t.border, borderRadius: BorderRadius.circular(2)),
                ),
              ),
              const SizedBox(height: 16),
              Text(context.tr('driver.setPriceTitle'),
                  style: TextStyle(
                      fontSize: 19, fontWeight: FontWeight.w800,
                      letterSpacing: -0.3, color: t.ink)),
              const SizedBox(height: 5),
              Text(context.tr('driver.setPriceBody'),
                  style: TextStyle(fontSize: 13, height: 1.4, color: t.ink2)),
              const SizedBox(height: 18),

              // The figure, with the same −/+ pair the post-a-trip screen uses.
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
                decoration: BoxDecoration(
                  color: t.surface2,
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: t.border),
                ),
                child: Row(children: [
                  MonoLabel(context.tr('driver.pricePerSeat'), spacing: 1.0),
                  const Spacer(),
                  _stepButton(t, '–', _price > _min, () => _nudge(-_step)),
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 16),
                    child: Text(Fare.format(_price),
                        style: WanesTheme.mono(
                            size: 20, weight: FontWeight.w800, color: t.ink, spacing: 0)),
                  ),
                  _stepButton(t, '+', _price < _max, () => _nudge(_step), accent: true),
                ]),
              ),

              // What the rider will actually pay, because the seat price is not
              // the whole story when they asked for more than one.
              if (widget.seats > 1) ...[
                const SizedBox(height: 10),
                Row(children: [
                  Text(context.trPlural('vehicle.seatCount', widget.seats),
                      style: TextStyle(fontSize: 12, color: t.ink2)),
                  const Spacer(),
                  Text(Fare.format(_price * widget.seats),
                      style: WanesTheme.mono(
                          size: 13, weight: FontWeight.w700, color: t.tealInk, spacing: 0)),
                ]),
              ],

              const SizedBox(height: 8),
              // A way back to the figure the platform suggested, for a driver who
              // stepped away from it and changed their mind.
              if ((_price - widget.suggestion).abs() > 0.001)
                Align(
                  alignment: AlignmentDirectional.centerStart,
                  child: TextButton(
                    onPressed: () => setState(
                        () => _price = widget.suggestion.clamp(_min, _max).toDouble()),
                    child: Text(context.tr('driver.priceSuggested',
                        {'value': Fare.format(widget.suggestion)})),
                  ),
                ),

              const SizedBox(height: 10),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: () => Navigator.pop(context, _price),
                  style: FilledButton.styleFrom(
                    backgroundColor: t.tealInk,
                    padding: const EdgeInsets.symmetric(vertical: 15),
                    shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(14)),
                  ),
                  child: Text(context.tr('driver.acceptAtPrice',
                      {'value': Fare.format(_price)})),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _stepButton(WanesTokens t, String glyph, bool enabled, VoidCallback onTap,
          {bool accent = false}) =>
      GestureDetector(
        onTap: enabled ? onTap : null,
        child: Container(
          width: 34, height: 34,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: accent ? t.tealTint : t.surface,
            borderRadius: BorderRadius.circular(9),
            border: accent ? null : Border.all(color: t.border),
          ),
          child: Text(glyph,
              style: TextStyle(
                  fontWeight: FontWeight.w800, fontSize: 19,
                  color: enabled ? (accent ? t.tealInk : t.ink2) : t.ink2.withValues(alpha: .4))),
        ),
      );
}
