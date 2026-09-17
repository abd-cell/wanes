import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

import '../../core/fare.dart';
import '../../core/l10n.dart';
import '../../core/theme.dart';
import '../../core/app_response.dart';
import '../../services/services.dart';
import '../../widgets/wanes_alerts.dart';
import '../../widgets/wanes_motion.dart';
import '../../widgets/wanes_ui.dart';
import 'series_flow.dart';

/// Whole-series commitments, from either side.
///
/// Two lists in one screen because they are one question — "what am I on, every
/// week?" — and the answer differs only in who promised what: a driver's series
/// they drive, a rider's seat they hold, and the offers waiting on an answer.
///
/// Pass [scheduleId] to narrow it to the offers on one of the caller's own
/// repeating postings, which is what the schedules list links to.
class MySeriesScreen extends StatefulWidget {
  const MySeriesScreen({super.key, this.scheduleId});

  final int? scheduleId;

  @override
  State<MySeriesScreen> createState() => _MySeriesScreenState();
}

class _MySeriesScreenState extends State<MySeriesScreen> {
  final _api = SeriesApi();

  List<SeriesCommitment> _rows = const [];
  bool _loading = true;
  int? _busyId;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final res = widget.scheduleId == null
        ? await _api.mine()
        : await _api.offersFor(widget.scheduleId!);
    if (!mounted) return;
    setState(() {
      _loading = false;
      _rows = res.data ?? const [];
    });
    if (!res.success) WanesAlerts.failure(context, res, title: context.tr('series.loadFailed'));
  }

  Future<void> _run(int id, Future<AppResponse<Object?>> Function() call, {String? okKey}) async {
    setState(() => _busyId = id);
    final res = await call();
    if (!mounted) return;
    setState(() => _busyId = null);
    if (!res.success) {
      WanesAlerts.failure(context, res, title: context.tr('series.actionFailed'));
      return;
    }
    if (okKey != null) WanesAlerts.success(context, context.tr(okKey));
    await _load();
  }

  Future<void> _accept(SeriesCommitment s) async {
    setState(() => _busyId = s.id);
    final res = await _api.accept(s.id);
    if (!mounted) return;
    setState(() => _busyId = null);
    if (!res.success || res.data == null) {
      WanesAlerts.failure(context, res, title: context.tr('series.actionFailed'));
      return;
    }
    await showSeriesResult(context, res.data!, titleKey: 'series.acceptedTitle');
    if (mounted) await _load();
  }

  Future<void> _end(SeriesCommitment s) async {
    if (await endSeries(context, s) && mounted) await _load();
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final proposals = _rows.where((s) => s.isProposed).toList();
    final active = _rows.where((s) => s.isActive).toList();
    final past = _rows.where((s) => !s.isProposed && !s.isActive).toList();

    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        bottom: false,
        child: Column(children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 8, 20, 0),
            child: ScreenHeader(
                title: context.tr(widget.scheduleId == null ? 'series.title' : 'series.offersTitle')),
          ),
          Expanded(
            child: _loading
                ? const Center(child: WanesSpinner())
                : _rows.isEmpty
                    ? _empty(t)
                    : RefreshIndicator(
                        onRefresh: _load,
                        child: ListView(
                          padding: const EdgeInsets.fromLTRB(20, 12, 20, 28),
                          children: [
                            if (proposals.isNotEmpty) ...[
                              SectionHeader(context.tr('series.waiting')),
                              const SizedBox(height: 8),
                              for (final s in proposals) _card(t, s),
                            ],
                            if (active.isNotEmpty) ...[
                              const SizedBox(height: 8),
                              SectionHeader(context.tr('series.running')),
                              const SizedBox(height: 8),
                              for (final s in active) _card(t, s),
                            ],
                            if (past.isNotEmpty) ...[
                              const SizedBox(height: 8),
                              SectionHeader(context.tr('series.finished')),
                              const SizedBox(height: 8),
                              for (final s in past) _card(t, s),
                            ],
                          ],
                        ),
                      ),
          ),
        ]),
      ),
    );
  }

  Widget _empty(WanesTokens t) => ListView(
        padding: const EdgeInsets.fromLTRB(28, 60, 28, 28),
        children: [
          Icon(Icons.repeat_rounded, size: 40, color: t.ink2),
          const SizedBox(height: 14),
          Text(context.tr('series.none'),
              textAlign: TextAlign.center,
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: t.ink)),
          const SizedBox(height: 6),
          Text(context.tr('series.noneBody'),
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 13, height: 1.5, color: t.ink2)),
        ],
      );

  Widget _card(WanesTokens t, SeriesCommitment s) {
    final locale = context.l10n.localeName;
    final busy = _busyId == s.id;
    final counterpart = s.side == SeriesSide.driverServes
        ? (s.isMine ? s.riderName : s.driverName)
        : (s.isMine ? s.driverName : s.riderName);

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: t.surface,
          borderRadius: BorderRadius.circular(18),
          border: Border.all(color: t.border),
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(children: [
            Expanded(
              child: Text('${s.originAddress} → ${s.destinationAddress}',
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontWeight: FontWeight.w800, fontSize: 14.5, color: t.ink)),
            ),
            const SizedBox(width: 8),
            TintChip(context.tr(s.status.labelKey),
                tint: s.isActive ? t.tealTint : t.surface2,
                color: s.isActive ? t.tealInk : t.ink2),
          ]),
          const SizedBox(height: 8),
          Wrap(spacing: 8, runSpacing: 6, children: [
            MetaChip('${s.daysLabel(locale)} · ${s.timeOfDay}'),
            if (counterpart != null && counterpart.isNotEmpty) MetaChip(counterpart),
            if (s.pricePerSeat != null) MetaChip(Fare.format(s.pricePerSeat!)),
            if (s.seats != null) MetaChip(context.trPlural('vehicle.seatCount', s.seats!)),
            if (s.side == SeriesSide.driverServes && s.driverCompletionRate != null && !s.isMine)
              MetaChip(context.tr('series.completion',
                  {'percent': (s.driverCompletionRate! * 100).round()})),
          ]),
          if (s.isActive) ...[
            const SizedBox(height: 8),
            Text(
              s.nextDeparture == null
                  ? context.tr('series.noUpcoming')
                  : context.tr('series.upcoming', {
                      'count': s.upcomingCount,
                      'next': DateFormat('EEE d MMM · HH:mm', locale).format(s.nextDeparture!),
                    }),
              style: TextStyle(fontSize: 12.5, color: t.ink2),
            ),
          ],
          if (s.isEnding)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text(
                  context.tr('series.endsOn', {
                    'date': s.until == null
                        ? ''
                        : DateFormat('EEE d MMM', locale).format(s.until!),
                  }),
                  style: TextStyle(fontSize: 12.5, fontWeight: FontWeight.w700, color: t.amberInk)),
            ),
          if (s.isProposed && s.decideAt != null)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Text(
                  context.tr('series.decidesAt',
                      {'time': DateFormat('EEE d MMM · HH:mm', locale).format(s.decideAt!)}),
                  style: TextStyle(fontSize: 12.5, color: t.ink2)),
            ),
          if (s.message != null && s.message!.isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Text('"${s.message}"',
                  style: TextStyle(fontSize: 12.5, fontStyle: FontStyle.italic, color: t.ink2)),
            ),
          const SizedBox(height: 10),
          if (busy)
            const Center(child: Padding(padding: EdgeInsets.all(6), child: WanesSpinner(size: 20)))
          else
            _actions(t, s),
        ]),
      ),
    );
  }

  Widget _actions(WanesTokens t, SeriesCommitment s) {
    // A proposal is the driver's to withdraw and the rider's to answer.
    if (s.isProposed) {
      if (s.isMine) {
        return Align(
          alignment: AlignmentDirectional.centerEnd,
          child: TextButton(
            onPressed: () => _run(s.id, () => _api.withdraw(s.id), okKey: 'series.withdrawn'),
            child: Text(context.tr('series.withdraw'),
                style: TextStyle(fontWeight: FontWeight.w700, color: t.ink2)),
          ),
        );
      }
      return Row(children: [
        Expanded(
          child: OutlinedButton(
            onPressed: () => _run(s.id, () => _api.decline(s.id), okKey: 'series.declined'),
            child: Text(context.tr('common.decline')),
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: FilledButton(
            onPressed: () => _accept(s),
            style: FilledButton.styleFrom(backgroundColor: t.tealInk, foregroundColor: Colors.white),
            child: Text(context.tr('series.acceptCta')),
          ),
        ),
      ]);
    }

    if (!s.isActive) return const SizedBox.shrink();

    // A driver ends the series they drive; a rider ends their seat, or releases
    // the driver of their own. A driver cannot end a rider's seat — that is the
    // rider's to give back.
    final canEnd = s.side == SeriesSide.driverServes || s.isMine;
    if (!canEnd) return const SizedBox.shrink();

    return Align(
      alignment: AlignmentDirectional.centerEnd,
      child: TextButton(
        onPressed: () => _end(s),
        child: Text(context.tr(s.isMine ? 'series.end' : 'series.release'),
            style: TextStyle(fontWeight: FontWeight.w700, color: t.alert)),
      ),
    );
  }
}
