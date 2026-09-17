import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

import '../../core/app_config.dart';
import '../../core/l10n.dart';
import '../../core/theme.dart';
import '../../services/services.dart';
import '../../widgets/wanes_alerts.dart';
import '../../widgets/wanes_motion.dart';
import '../../widgets/wanes_ui.dart';

/// The driver's own reliability record, and the rules behind it, in plain
/// words. A driver who knows what a late cancellation costs is a driver who
/// does not accept the ride they are not sure about.
class ReliabilityScreen extends StatefulWidget {
  const ReliabilityScreen({super.key});

  @override
  State<ReliabilityScreen> createState() => _ReliabilityScreenState();
}

class _ReliabilityScreenState extends State<ReliabilityScreen> {
  final _service = MarketplaceService();
  ReliabilityRecord? _record;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    final res = await _service.reliability();
    if (!mounted) return;
    setState(() {
      _loading = false;
      if (res.success) _record = res.data;
    });
    if (!res.success) WanesAlerts.failure(context, res, title: context.tr('reliability.loadFailed'));
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final r = _record;
    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        bottom: false,
        child: Column(children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 8, 20, 0),
            child: ScreenHeader(title: context.tr('reliability.title')),
          ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
                children: [
                  if (_loading && r == null)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 40),
                      child: Center(child: WanesSpinner()),
                    )
                  else if (r != null) ...[
                    if (r.isSuspended) ...[
                      _banner(t, r),
                      const SizedBox(height: 12),
                    ],
                    _scoreCard(t, r),
                    const SizedBox(height: 12),
                    _rulesCard(t, r),
                    const SizedBox(height: 16),
                    Text(context.tr('reliability.recent'),
                        style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15, color: t.ink)),
                    const SizedBox(height: 8),
                    if (r.recent.isEmpty)
                      WanesCard(child: Text(context.tr('reliability.clean'), style: TextStyle(color: t.ink2)))
                    else
                      for (final e in r.recent)
                        Padding(
                          padding: const EdgeInsets.only(bottom: 8),
                          child: WanesCard(
                            child: Row(children: [
                              Icon(e.points > 0 ? Icons.remove_circle_outline : Icons.check_circle_outline,
                                  color: e.points > 0 && !e.isWaived ? t.alert : t.tealInk),
                              const SizedBox(width: 10),
                              Expanded(
                                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                                  Text(context.tr(e.kind.labelKey),
                                      style: TextStyle(fontWeight: FontWeight.w700, color: t.ink)),
                                  if (e.createdAt != null)
                                    Text(
                                        DateFormat('d MMM, HH:mm', context.l10n.localeName)
                                            .format(e.createdAt!.toLocal()),
                                        style: TextStyle(fontSize: 11.5, color: t.ink2)),
                                ]),
                              ),
                              Text(
                                  e.isWaived
                                      ? context.tr('reliability.waived')
                                      : e.needsReview
                                          ? context.tr('reliability.inReview')
                                          : context.trPlural('reliability.points', e.points),
                                  style: WanesTheme.mono(
                                      size: 11, weight: FontWeight.w700, color: t.ink2, spacing: 0)),
                            ]),
                          ),
                        ),
                  ],
                ],
              ),
            ),
          ),
        ]),
      ),
    );
  }

  Widget _banner(WanesTokens t, ReliabilityRecord r) => Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(color: t.amberTint, borderRadius: BorderRadius.circular(14)),
        child: Row(children: [
          Icon(Icons.pause_circle_outline_rounded, color: t.amberInk),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
                context.tr('reliability.suspended', {
                  'date': DateFormat('d MMM', context.l10n.localeName).format(r.suspendedUntil!.toLocal()),
                }),
                style: TextStyle(fontSize: 13, height: 1.4, color: t.amberInk, fontWeight: FontWeight.w600)),
          ),
        ]),
      );

  Widget _scoreCard(WanesTokens t, ReliabilityRecord r) {
    final rate = r.completionRate;
    return WanesCard(
      child: Row(children: [
        Expanded(
          child: MetricTile(
            value: rate == null ? '—' : '${(rate * 100).round()}%',
            label: context.tr('reliability.completion'),
            valueColor: t.tealInk,
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: MetricTile(
            value: '${r.pointsInWindow}/${r.suspendPoints}',
            label: context.tr('reliability.pointsWindow', {'days': r.windowDays}),
            valueColor: r.pointsInWindow >= r.warnPoints ? t.amberInk : null,
          ),
        ),
        const SizedBox(width: 10),
        Expanded(child: MetricTile(value: '${r.tripsAsDriver}', label: context.tr('reliability.trips'))),
      ]),
    );
  }

  Widget _rulesCard(WanesTokens t, ReliabilityRecord r) {
    final c = AppConfigController.value;
    final lines = [
      context.tr('reliability.ruleFree', {'min': c.freeCancelGraceMinutes}),
      context.tr('reliability.ruleCounted'),
      context.tr('reliability.ruleLate', {'hours': (c.lateCancelLeadMinutes / 60).round()}),
      context.tr('reliability.ruleSuspend', {'points': r.suspendPoints, 'days': r.windowDays}),
      context.tr('reliability.ruleReview'),
    ];
    return WanesCard(
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(context.tr('reliability.rulesTitle'),
            style: TextStyle(fontWeight: FontWeight.w800, fontSize: 14, color: t.ink)),
        const SizedBox(height: 8),
        for (final line in lines)
          Padding(
            padding: const EdgeInsets.only(bottom: 6),
            child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text('•  ', style: TextStyle(color: t.ink2)),
              Expanded(child: Text(line, style: TextStyle(fontSize: 12.5, height: 1.4, color: t.ink2))),
            ]),
          ),
      ]),
    );
  }
}
