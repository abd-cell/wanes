import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

import '../core/departure_label.dart';
import '../core/fare.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/safety_notes.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_motion.dart';
import '../widgets/wanes_ui.dart';

/// The drivers who offered to take a planned request, side by side.
///
/// A scheduled request collects offers for a while before the marketplace
/// picks one. While it does, its riders can compare — price, rating, how
/// reliable the driver has been, the car, and whether the offer is conditional
/// — and pick the driver themselves. Anything they leave is decided by the
/// ranking when the window closes.
class RideOffersScreen extends StatefulWidget {
  const RideOffersScreen({super.key, required this.rideRequestId});

  final int rideRequestId;

  @override
  State<RideOffersScreen> createState() => _RideOffersScreenState();
}

class _RideOffersScreenState extends State<RideOffersScreen> {
  final _requests = RiderTripService();

  RiderTrip? _request;
  List<RideOffer> _offers = [];
  bool _loading = true;
  int? _choosing;
  Timer? _tick;

  @override
  void initState() {
    super.initState();
    _load();
    _tick = Timer.periodic(const Duration(seconds: 30), (_) {
      if (mounted) _load(quiet: true);
    });
  }

  @override
  void dispose() {
    _tick?.cancel();
    super.dispose();
  }

  Future<void> _load({bool quiet = false}) async {
    if (!quiet) setState(() => _loading = true);
    final request = await _requests.get(widget.rideRequestId);
    final offers = await _requests.offers(widget.rideRequestId);
    if (!mounted) return;
    setState(() {
      _loading = false;
      if (request.success) _request = request.data;
      if (offers.success) _offers = offers.data ?? [];
    });
    if (!quiet && !offers.success) {
      WanesAlerts.failure(context, offers, title: context.tr('offers.loadFailed'));
    }
  }

  Future<void> _choose(RideOffer offer) async {
    if (_choosing != null) return;
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(ctx.tr('offers.chooseTitle', {'name': offer.driverName})),
        content: Text(ctx.tr('offers.chooseBody', {'price': Fare.format(offer.pricePerSeat)})),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: Text(ctx.tr('common.cancel'))),
          FilledButton(onPressed: () => Navigator.pop(ctx, true), child: Text(ctx.tr('offers.choose'))),
        ],
      ),
    );
    if (ok != true || !mounted) return;

    setState(() => _choosing = offer.interestId);
    final res = await _requests.chooseOffer(widget.rideRequestId, offer.interestId);
    if (!mounted) return;
    setState(() => _choosing = null);

    if (!res.success) {
      WanesAlerts.failure(context, res, title: context.tr('offers.chooseFailed'));
      _load(quiet: true);
      return;
    }
    WanesAlerts.success(context, context.tr('offers.chosen'),
        message: context.tr('offers.chosenBody', {'name': offer.driverName}));
    // The seat now lives in Bookings, which the shell's tab reloads on return.
    Navigator.popUntil(context, (route) => route.isFirst);
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final request = _request;
    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        bottom: false,
        child: Column(children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 8, 20, 0),
            child: ScreenHeader(title: context.tr('offers.title')),
          ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
                children: [
                  if (request != null) _summary(t, request),
                  const SizedBox(height: 14),
                  if (_loading && _offers.isEmpty)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 40),
                      child: Center(child: WanesSpinner()),
                    )
                  else if (_offers.isEmpty)
                    WanesCard(
                      child: Text(
                          context.tr(request != null && !request.isOpen
                              ? 'offers.closed'
                              : 'offers.none'),
                          style: TextStyle(color: t.ink2)),
                    )
                  else
                    for (final offer in _offers)
                      Padding(
                        padding: const EdgeInsets.only(bottom: 12),
                        child: _offerCard(t, offer),
                      ),
                  const SafetyReminder(audience: SafetyAudience.rider),
                ],
              ),
            ),
          ),
        ]),
      ),
    );
  }

  Widget _summary(WanesTokens t, RiderTrip r) {
    final decideAt = r.decideAt;
    return WanesCard(
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(context.tr('common.routeSummary', {'from': r.originAddress, 'to': r.destinationAddress}),
            style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15, color: t.ink)),
        const SizedBox(height: 4),
        Text(
            '${departureLabel(context, r.departAt)} · '
            '${context.trPlural('vehicle.seatCount', r.seatsWanted)}',
            style: TextStyle(fontSize: 12.5, color: t.ink2)),
        if (decideAt != null && r.isOpen) ...[
          const SizedBox(height: 10),
          Row(children: [
            Icon(Icons.timer_outlined, size: 16, color: t.amberInk),
            const SizedBox(width: 6),
            Expanded(
              child: Text(
                  decideAt.isAfter(DateTime.now())
                      ? context.tr('offers.decidesAt', {
                          'time': DateFormat('HH:mm', context.l10n.localeName).format(decideAt.toLocal()),
                          'left': untilLabel(context, decideAt.difference(DateTime.now())),
                        })
                      : context.tr('offers.decidingNow'),
                  style: TextStyle(fontSize: 12.5, fontWeight: FontWeight.w600, color: t.amberInk)),
            ),
          ]),
        ],
      ]),
    );
  }

  Widget _offerCard(WanesTokens t, RideOffer o) {
    final busy = _choosing == o.interestId;
    final completion = o.driverCompletionRate;
    return WanesCard(
      radius: 16,
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Row(children: [
          AvatarBadge(AvatarBadge.initialsOf(o.driverName), size: 44, tint: t.tealTint, fg: t.tealInk),
          const SizedBox(width: 12),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(children: [
                Flexible(
                  child: Text(o.driverName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15, color: t.ink)),
                ),
                if (o.driverVerified) ...[
                  const SizedBox(width: 4),
                  Icon(Icons.verified_rounded, size: 15, color: t.tealInk),
                ],
              ]),
              const SizedBox(height: 3),
              Wrap(spacing: 10, children: [
                if (o.driverRating > 0) InlineRating(o.driverRating.toStringAsFixed(1), size: 11),
                Text(context.trPlural('offers.trips', o.driverTrips),
                    style: TextStyle(fontSize: 11.5, color: t.ink2)),
                if (completion != null)
                  Text(context.tr('offers.completion', {'value': (completion * 100).round()}),
                      style: TextStyle(fontSize: 11.5, color: t.ink2)),
              ]),
            ]),
          ),
          Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
            Text(Fare.format(o.pricePerSeat),
                style: WanesTheme.mono(size: 17, weight: FontWeight.w800, color: t.tealInk, spacing: 0)),
            Text(context.tr('offers.perSeat'),
                style: WanesTheme.mono(size: 10, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
          ]),
        ]),
        const SizedBox(height: 10),
        Wrap(spacing: 8, runSpacing: 6, children: [
          if (o.vehicleLabel.isNotEmpty)
            _chip(t, Icons.directions_car_outlined,
                [o.vehicleLabel, o.vehicleColor].where((s) => s.isNotEmpty).join(' · ')),
          if (o.seatsOffered != null)
            _chip(t, Icons.event_seat_outlined, context.trPlural('vehicle.seatCount', o.seatsOffered!)),
          if (o.minPassengers != null)
            _chip(t, Icons.groups_2_outlined, context.tr('offers.conditional', {'min': o.minPassengers}),
                warn: true),
        ]),
        if ((o.message ?? '').trim().isNotEmpty) ...[
          const SizedBox(height: 8),
          Text('“${o.message!.trim()}”', style: TextStyle(fontSize: 12.5, color: t.ink2)),
        ],
        const SizedBox(height: 12),
        PrimaryButton(
          label: context.tr('offers.choose'),
          arrow: false,
          busy: busy,
          onPressed: _choosing != null ? null : () => _choose(o),
        ),
      ]),
    );
  }

  Widget _chip(WanesTokens t, IconData icon, String label, {bool warn = false}) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
        decoration: BoxDecoration(
          color: warn ? t.amberTint : t.surface2,
          borderRadius: BorderRadius.circular(999),
        ),
        child: Row(mainAxisSize: MainAxisSize.min, children: [
          Icon(icon, size: 13, color: warn ? t.amberInk : t.ink2),
          const SizedBox(width: 4),
          Text(label, style: TextStyle(fontSize: 11.5, color: warn ? t.amberInk : t.ink)),
        ]),
      );
}
