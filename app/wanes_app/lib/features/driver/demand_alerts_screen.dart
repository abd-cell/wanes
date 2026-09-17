import 'package:flutter/material.dart';

import '../../core/l10n.dart';
import '../../core/places.dart';
import '../../core/theme.dart';
import '../../services/services.dart';
import '../../widgets/place_picker.dart';
import '../../widgets/wanes_alerts.dart';
import '../../widgets/wanes_motion.dart';
import '../../widgets/wanes_ui.dart';

/// A driver's route alerts: "tell me when N people want my route".
///
/// Planned, shared trips are the product, and a driver cannot watch the board
/// all day. An alert watches it for them and fires once per request, when the
/// pool reaches the seats that make the run worth planning.
class DemandAlertsScreen extends StatefulWidget {
  const DemandAlertsScreen({super.key});

  @override
  State<DemandAlertsScreen> createState() => _DemandAlertsScreenState();
}

class _DemandAlertsScreenState extends State<DemandAlertsScreen> {
  final _service = MarketplaceService();

  List<DemandAlert> _alerts = [];
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    final res = await _service.alerts();
    if (!mounted) return;
    setState(() {
      _loading = false;
      if (res.success) _alerts = res.data ?? [];
    });
    if (!res.success) WanesAlerts.failure(context, res, title: context.tr('alerts.loadFailed'));
  }

  Future<void> _add() async {
    final created = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => const _NewAlertSheet(),
    );
    if (created == true) _load();
  }

  Future<void> _delete(DemandAlert alert) async {
    final res = await _service.deleteAlert(alert.id);
    if (!mounted) return;
    if (!res.success) {
      WanesAlerts.failure(context, res, title: context.tr('alerts.deleteFailed'));
      return;
    }
    setState(() => _alerts.removeWhere((a) => a.id == alert.id));
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Scaffold(
      backgroundColor: t.bg,
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _add,
        backgroundColor: t.tealInk,
        foregroundColor: Colors.white,
        icon: const Icon(Icons.add_alert_outlined),
        label: Text(context.tr('alerts.add')),
      ),
      body: SafeArea(
        bottom: false,
        child: Column(children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 8, 20, 0),
            child: ScreenHeader(title: context.tr('alerts.title')),
          ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.fromLTRB(20, 12, 20, 96),
                children: [
                  Text(context.tr('alerts.body'), style: TextStyle(fontSize: 13, height: 1.4, color: t.ink2)),
                  const SizedBox(height: 14),
                  if (_loading && _alerts.isEmpty)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 40),
                      child: Center(child: WanesSpinner()),
                    )
                  else if (_alerts.isEmpty)
                    WanesCard(child: Text(context.tr('alerts.empty'), style: TextStyle(color: t.ink2)))
                  else
                    for (final a in _alerts)
                      Padding(
                        padding: const EdgeInsets.only(bottom: 10),
                        child: WanesCard(
                          child: Row(children: [
                            Icon(a.isWatch ? Icons.visibility_outlined : Icons.alt_route_rounded,
                                color: t.tealInk),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                                Text(
                                    context.tr('common.routeSummary', {
                                      'from': a.originAddress.split(',').first,
                                      'to': a.destinationAddress.split(',').first,
                                    }),
                                    style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
                                const SizedBox(height: 2),
                                Text(
                                    context.tr(a.isWatch ? 'alerts.watchLine' : 'alerts.routeLine', {
                                      'seats': a.minSeats,
                                      'km': (a.radiusMeters / 1000).toStringAsFixed(1),
                                      'count': a.notifiedCount,
                                    }),
                                    style: TextStyle(fontSize: 12, color: t.ink2)),
                              ]),
                            ),
                            IconButton(
                              tooltip: context.tr('common.remove'),
                              onPressed: () => _delete(a),
                              icon: Icon(Icons.delete_outline_rounded, color: t.ink2),
                            ),
                          ]),
                        ),
                      ),
                ],
              ),
            ),
          ),
        ]),
      ),
    );
  }
}

class _NewAlertSheet extends StatefulWidget {
  const _NewAlertSheet();

  @override
  State<_NewAlertSheet> createState() => _NewAlertSheetState();
}

class _NewAlertSheetState extends State<_NewAlertSheet> {
  final _service = MarketplaceService();
  Place? _from;
  Place? _to;
  int _minSeats = 3;
  int _radiusKm = 3;
  bool _busy = false;

  Future<void> _pick(bool from) async {
    final picked = await showPlacePicker(
      context,
      title: context.tr(from ? 'home.pickup' : 'home.destination'),
      hint: context.tr(from ? 'home.searchPickupHint' : 'home.searchDestinationHint'),
    );
    if (picked == null || !mounted) return;
    setState(() => from ? _from = picked : _to = picked);
  }

  Future<void> _save() async {
    final from = _from, to = _to;
    if (from == null || to == null) {
      WanesAlerts.warning(context, context.tr(from == null ? 'home.pickOrigin' : 'home.pickDestination'));
      return;
    }
    setState(() => _busy = true);
    final res = await _service.addRouteAlert(
        from: from, to: to, minSeats: _minSeats, radiusMeters: _radiusKm * 1000);
    if (!mounted) return;
    setState(() => _busy = false);
    if (!res.success) {
      WanesAlerts.failure(context, res, title: context.tr('alerts.saveFailed'));
      return;
    }
    WanesAlerts.success(context, context.tr('alerts.saved'));
    Navigator.pop(context, true);
  }

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
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 18),
          child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Text(context.tr('alerts.newTitle'),
                style: TextStyle(fontSize: 19, fontWeight: FontWeight.w800, color: t.ink)),
            const SizedBox(height: 12),
            GroupedCard(children: [
              GroupedRow(
                icon: Icons.trip_origin,
                title: context.tr('home.pickup'),
                subtitle: _from?.name ?? context.tr('home.searchPickupHint'),
                iconColor: t.teal,
                onTap: () => _pick(true),
              ),
              GroupedRow(
                icon: Icons.place_outlined,
                title: context.tr('home.destination'),
                subtitle: _to?.name ?? context.tr('home.searchDestinationHint'),
                iconColor: t.amberInk,
                onTap: () => _pick(false),
              ),
              GroupedRow(
                icon: Icons.groups_2_outlined,
                title: context.tr('alerts.minSeats'),
                subtitle: context.tr('alerts.minSeatsBody'),
                trailing: SeatStepper(value: _minSeats, onChanged: (v) => setState(() => _minSeats = v), max: 8),
              ),
              GroupedRow(
                icon: Icons.radar_rounded,
                title: context.tr('alerts.radius'),
                subtitle: context.tr('units.km', {'value': _radiusKm}),
                trailing: SeatStepper(value: _radiusKm, onChanged: (v) => setState(() => _radiusKm = v), max: 20),
              ),
            ]),
            const SizedBox(height: 14),
            PrimaryButton(
              label: context.tr('alerts.save'),
              arrow: false,
              busy: _busy,
              onPressed: _busy ? null : _save,
            ),
          ]),
        ),
      ),
    );
  }
}
