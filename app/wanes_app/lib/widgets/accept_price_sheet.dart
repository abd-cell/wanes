import 'package:flutter/material.dart';

import '../core/fare.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import 'safety_notes.dart';
import 'wanes_ui.dart';

/// What the driver is charging per seat, asked before they take a request —
/// and the moment they agree that the trip is shared.
///
/// A request carries no price — the riders asked for a ride, not for a quote —
/// so this is the driver's only chance to say what the seat costs, and the trip
/// created by accepting lists at whatever comes back from here.
///
/// It opens on the distance estimate rather than empty. That figure is the one
/// already shown on the request card, off the admin's configured rates, so the
/// driver is confirming a number they have been looking at instead of inventing
/// one against a countdown.
///
/// Accepting makes a trip whose free seats go on sale. The sheet spells out the
/// split — riders now, seats in the car, seats left open — and the driver ticks
/// that they understand more riders may join before the button arms. Saying so
/// here is what makes a later "I didn't agree to more passengers" cancellation
/// a broken promise rather than a surprise.
///
/// Returns the chosen terms, or null if the driver backed out — which must be
/// treated as "did not accept", never as "accept at the default".
Future<AcceptTerms?> showAcceptPriceSheet(
  BuildContext context, {
  required double suggestion,
  required int seats,
  int riders = 1,
  int? vehicleSeats,
}) {
  return showModalBottomSheet<AcceptTerms>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (_) => AcceptPriceSheet(
      suggestion: suggestion,
      seats: seats,
      riders: riders,
      vehicleSeats: vehicleSeats,
    ),
  );
}

/// What a driver accepts a request on.
class AcceptTerms {
  const AcceptTerms({required this.pricePerSeat, this.seatsOffered, this.minPassengers});

  final double pricePerSeat;

  /// Seats the trip will carry — the riders' own plus any opened to others.
  final int? seatsOffered;

  /// The conditional accept: the trip runs once this many seats are held.
  final int? minPassengers;
}

class AcceptPriceSheet extends StatefulWidget {
  const AcceptPriceSheet({
    super.key,
    required this.suggestion,
    required this.seats,
    this.riders = 1,
    this.vehicleSeats,
  });

  final double suggestion;

  /// Seats the riders asked for, across all of them.
  final int seats;

  /// How many riders are on the request.
  final int riders;

  /// The car's passenger seats, when known. Null leaves the open-seat rows off
  /// rather than guessing.
  final int? vehicleSeats;

  @override
  State<AcceptPriceSheet> createState() => _AcceptPriceSheetState();
}

class _AcceptPriceSheetState extends State<AcceptPriceSheet> {
  /// Matches the post-a-trip screen's stepper, so a driver meets one idiom for
  /// pricing a seat whichever way they came to it.
  static const _step = 0.25;
  static const _min = 0.0;
  static const _max = 50.0;

  late double _price = widget.suggestion.clamp(_min, _max).toDouble();
  bool _agreed = false;

  /// Seats on the trip. Opens on the whole car — the shared default — and the
  /// driver may close some, never below the riders already on it.
  late int _seatsOnTrip = widget.vehicleSeats ?? widget.seats;

  /// Off unless the driver asks for it: "only if it reaches N".
  bool _conditional = false;
  late int _minPassengers = (widget.seats + 1).clamp(widget.seats, _seatsOnTrip);

  void _nudge(double by) =>
      setState(() => _price = ((_price + by).clamp(_min, _max)).toDouble());

  int? get _openSeats {
    if (widget.vehicleSeats == null) return null;
    return (_seatsOnTrip - widget.seats).clamp(0, _seatsOnTrip);
  }

  /// A condition only means something when the trip can hold more than the
  /// riders already on it.
  bool get _canCondition => _seatsOnTrip > widget.seats;

  void _setSeats(int next) => setState(() {
        _seatsOnTrip = next.clamp(widget.seats, widget.vehicleSeats ?? widget.seats);
        if (_minPassengers > _seatsOnTrip) _minPassengers = _seatsOnTrip;
        if (!_canCondition) _conditional = false;
      });

  void _submit() => Navigator.pop(
        context,
        AcceptTerms(
          pricePerSeat: _price,
          seatsOffered: widget.vehicleSeats == null ? null : _seatsOnTrip,
          minPassengers: _conditional && _minPassengers > widget.seats ? _minPassengers : null,
        ),
      );

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final open = _openSeats;

    return ConstrainedBox(
      constraints: BoxConstraints(maxHeight: MediaQuery.of(context).size.height * .92),
      child: Container(
        decoration: BoxDecoration(
          color: t.surface,
          borderRadius: const BorderRadius.vertical(top: Radius.circular(24)),
        ),
        child: SafeArea(
          top: false,
          child: SingleChildScrollView(
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
                Text(context.tr('accept.title'),
                    style: TextStyle(
                        fontSize: 19, fontWeight: FontWeight.w800,
                        letterSpacing: -0.3, color: t.ink)),
                const SizedBox(height: 5),
                Text(context.tr('driver.setPriceBody'),
                    style: TextStyle(fontSize: 13, height: 1.4, color: t.ink2)),
                const SizedBox(height: 16),

                // Who is in the car, and who still might be.
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
                  decoration: BoxDecoration(
                    color: t.surface2,
                    borderRadius: BorderRadius.circular(16),
                    border: Border.all(color: t.border),
                  ),
                  child: Column(children: [
                    _row(t, context.tr('accept.passengersNow'),
                        '${context.trPlural('market.passengers', widget.riders)} · '
                        '${context.trPlural('vehicle.seatCount', widget.seats)}'),
                    if (widget.vehicleSeats != null) ...[
                      _row(t, context.tr('accept.seatsInCar'),
                          context.trPlural('vehicle.seatCount', widget.vehicleSeats!)),
                      // How many seats this trip carries — the riders' own and
                      // any the driver opens to others.
                      Padding(
                        padding: const EdgeInsets.symmetric(vertical: 4),
                        child: Row(children: [
                          Expanded(
                            child: Text(context.tr('accept.seatsOnTrip'),
                                style: TextStyle(fontSize: 13, color: t.ink2)),
                          ),
                          _stepButton(t, '–', _seatsOnTrip > widget.seats,
                              () => _setSeats(_seatsOnTrip - 1)),
                          Padding(
                            padding: const EdgeInsets.symmetric(horizontal: 12),
                            child: Text('$_seatsOnTrip',
                                style: WanesTheme.mono(
                                    size: 16, weight: FontWeight.w800, color: t.ink, spacing: 0)),
                          ),
                          _stepButton(t, '+', _seatsOnTrip < widget.vehicleSeats!,
                              () => _setSeats(_seatsOnTrip + 1), accent: true),
                        ]),
                      ),
                      _row(t, context.tr('accept.openSeats'),
                          context.trPlural('vehicle.seatCount', open!),
                          valueColor: open > 0 ? t.amberInk : t.ink),
                    ],
                  ]),
                ),
                if (_canCondition) ...[
                  const SizedBox(height: 10),
                  _conditionCard(t),
                ],
                const SizedBox(height: 12),

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
                      padding: const EdgeInsets.symmetric(horizontal: 14),
                      child: Text(Fare.format(_price),
                          style: WanesTheme.mono(
                              size: 19, weight: FontWeight.w800, color: t.ink, spacing: 0)),
                    ),
                    _stepButton(t, '+', _price < _max, () => _nudge(_step), accent: true),
                  ]),
                ),
                const SizedBox(height: 6),
                _row(t, context.tr('accept.totalNow'), Fare.format(_price * widget.seats),
                    valueColor: t.tealInk),
                if (open != null && open > 0)
                  _row(t, context.tr('accept.totalIfFull'), Fare.format(_price * _seatsOnTrip)),

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

                SharedRideNotice(
                  body: open == null
                      ? context.tr('accept.sharedNoteUnknown')
                      : open == 0
                          ? context.tr('accept.sharedNoteFull')
                          : context.trPlural('accept.sharedNote', open),
                  value: _agreed,
                  onChanged: (v) => setState(() => _agreed = v),
                ),
                const SafetyReminder(audience: SafetyAudience.driver),
                const SizedBox(height: 8),
                SizedBox(
                  width: double.infinity,
                  child: FilledButton(
                    onPressed: _agreed ? _submit : null,
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
      ),
    );
  }

  /// "Only if it reaches N passengers" — the conditional accept.
  Widget _conditionCard(WanesTokens t) => Container(
        padding: const EdgeInsets.fromLTRB(14, 8, 10, 8),
        decoration: BoxDecoration(
          color: _conditional ? t.amberTint : t.surface2,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: _conditional ? t.amber : t.border),
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(children: [
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(context.tr('accept.conditionTitle'),
                    style: TextStyle(fontWeight: FontWeight.w700, fontSize: 13.5, color: t.ink)),
                const SizedBox(height: 2),
                Text(context.tr('accept.conditionBody'),
                    style: TextStyle(fontSize: 11.5, height: 1.35, color: t.ink2)),
              ]),
            ),
            Switch(
              value: _conditional,
              onChanged: (v) => setState(() => _conditional = v),
            ),
          ]),
          if (_conditional)
            Row(children: [
              Expanded(
                child: Text(context.tr('accept.conditionAtLeast'),
                    style: TextStyle(fontSize: 13, color: t.ink)),
              ),
              _stepButton(t, '–', _minPassengers > widget.seats + 1,
                  () => setState(() => _minPassengers--)),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 12),
                child: Text('$_minPassengers',
                    style: WanesTheme.mono(size: 16, weight: FontWeight.w800, color: t.ink, spacing: 0)),
              ),
              _stepButton(t, '+', _minPassengers < _seatsOnTrip,
                  () => setState(() => _minPassengers++), accent: true),
            ]),
        ]),
      );

  Widget _row(WanesTokens t, String label, String value, {Color? valueColor}) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 6),
        child: Row(children: [
          Expanded(child: Text(label, style: TextStyle(fontSize: 13, color: t.ink2))),
          const SizedBox(width: 8),
          Text(value,
              style: WanesTheme.mono(
                  size: 13, weight: FontWeight.w700, color: valueColor ?? t.ink, spacing: 0)),
        ]),
      );

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
