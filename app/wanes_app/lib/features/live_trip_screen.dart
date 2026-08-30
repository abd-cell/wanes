import 'dart:async';
import 'package:flutter/material.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../widgets/map_backdrop.dart';
import '../widgets/wanes_motion.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';
import 'rate_screen.dart';

/// Active trip — prototype screen 06. The route on the map with the car
/// approaching and an ETA card, over a sheet holding the four-stage progress
/// rail, the driver row and Cancel trip.
class LiveTripScreen extends StatefulWidget {
  const LiveTripScreen({super.key, required this.trip, required this.bookingId});
  final Trip trip;
  final int bookingId;

  @override
  State<LiveTripScreen> createState() => _LiveTripScreenState();
}

class _LiveTripScreenState extends State<LiveTripScreen> with SingleTickerProviderStateMixin {
  static const _stepKeys = [
    'trip.stepOnTheWay',
    'trip.stepArrived',
    'trip.stepInTrip',
    'trip.stepDone',
  ];

  int _step = 0;
  int _eta = 4; // minutes — simulated, see the note on _timer below
  Timer? _timer;

  /// `@keyframes carbob` — the car marker rocks gently as it moves. The
  /// keyframe rises and falls within its 2.4s, so a reversing controller runs
  /// half of it per pass.
  late final AnimationController _bob =
      AnimationController(vsync: this, duration: WanesMotion.bob ~/ 2)
        ..repeat(reverse: true);

  @override
  void initState() {
    super.initState();
    // NOTE: trip progress is still simulated on the client. The driver app is
    // what actually advances a trip server-side; wiring this to booking/trip
    // status over SSE is the real fix.
    _timer = Timer.periodic(const Duration(seconds: 4), (_) {
      if (!mounted) return;
      setState(() {
        if (_step < _stepKeys.length - 1) _step++;
        if (_eta > 0) _eta--;
      });
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    _bob.dispose();
    super.dispose();
  }

  bool get _completed => _step >= _stepKeys.length - 1;

  static String initialsOf(String name) {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
    return (parts.first[0] + parts.last[0]).toUpperCase();
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final trip = widget.trip;
    final car = [trip.vehicleLabel, trip.vehiclePlate].where((s) => s.isNotEmpty).join(' · ');

    return Scaffold(
      backgroundColor: t.bg,
      body: Column(children: [
        Expanded(
          child: MapBackdrop(
            route: MapRoutes.activeTrip,
            routeColor: t.teal,
            startMarker: const MapDot(),
            endMarker: AnimatedBuilder(
              animation: _bob,
              builder: (_, child) => Transform.translate(
                offset: Offset(0, -3.2 * Curves.easeInOut.transform(_bob.value)),
                child: child,
              ),
              child: Container(
                width: 40,
                height: 40,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: t.ink,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: t.bg, width: 2),
                  boxShadow: const [
                    BoxShadow(color: Color(0x66000000), blurRadius: 12, offset: Offset(0, 4)),
                  ],
                ),
                child: Icon(Icons.directions_car_rounded, size: 22, color: t.teal),
              ),
            ),
            children: [
              PositionedDirectional(
                end: 16,
                top: MediaQuery.of(context).padding.top + 10,
                child: _etaCard(t),
              ),
            ],
          ),
        ),
        MapSheet(
          padding: const EdgeInsets.fromLTRB(20, 14, 20, 18),
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            TripStepper(
                steps: _stepKeys.map(context.tr).toList(growable: false), current: _step),
            const SizedBox(height: 14),
            Divider(height: 1, thickness: 1, color: t.border),
            const SizedBox(height: 14),
            Row(children: [
              AvatarBadge(initialsOf(trip.driverName), size: 50, tint: t.tealTint, fg: t.tealInk),
              const SizedBox(width: 13),
              Expanded(
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(trip.driverName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15, color: t.ink)),
                  const SizedBox(height: 2),
                  Text(car.isEmpty ? trip.statusLabel : car,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: WanesTheme.mono(
                          size: 11.5, weight: FontWeight.w500, color: t.ink2, spacing: 0.4)),
                ]),
              ),
              const SizedBox(width: 8),
              RoundAction(
                  icon: Icons.call_outlined,
                  onTap: () => _notAvailable(context.tr('trip.callingSoon'))),
              const SizedBox(width: 10),
              RoundAction(
                  icon: Icons.chat_bubble_outline_rounded,
                  filled: true,
                  onTap: () => _notAvailable(context.tr('trip.messagingSoon'))),
            ]),
            const SizedBox(height: 14),
            if (_completed)
              PrimaryButton(
                label: context.tr('rate.rateYourTrip'),
                arrow: false,
                onPressed: () => Navigator.pushReplacement(
                  context,
                  MaterialPageRoute(
                    builder: (_) => RateScreen(
                      bookingId: widget.bookingId,
                      driverName: trip.driverName,
                      trip: trip,
                    ),
                  ),
                ),
              )
            else
              SizedBox(
                width: double.infinity,
                height: 46,
                child: OutlinedButton(
                  onPressed: () => Navigator.pop(context),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: t.amberInk,
                    side: BorderSide(color: t.border),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                  ),
                  child: Text(context.tr('trip.cancelTrip'),
                      style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14)),
                ),
              ),
          ]),
        ),
      ]),
    );
  }

  void _notAvailable(String message) => WanesAlerts.info(
        context,
        context.tr('common.notAvailableYet'),
        message: message,
      );

  Widget _etaCard(WanesTokens t) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: t.border),
        boxShadow: [
          BoxShadow(color: t.shadow, blurRadius: 14, offset: const Offset(0, 4), spreadRadius: -6),
        ],
      ),
      child: Column(mainAxisSize: MainAxisSize.min, children: [
        Text(_completed ? context.tr('trip.stepDone') : context.tr('units.minutes', {'value': _eta}),
            style: WanesTheme.mono(size: 18, weight: FontWeight.w800, color: t.tealInk, spacing: 0)),
        Text(_completed ? context.tr('trip.complete') : context.tr(_stageCaptionKey),
            style: TextStyle(fontSize: 10, color: t.ink2)),
      ]),
    );
  }

  String get _stageCaptionKey => switch (_step) {
        0 => 'trip.toPickup',
        1 => 'trip.atPickup',
        _ => 'trip.toDropoff',
      };
}
