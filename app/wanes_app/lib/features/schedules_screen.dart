import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;
import '../core/fare.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_motion.dart';
import '../widgets/wanes_ui.dart';
import 'schedule_form_screen.dart';

/// The user's repeating trips — the commute they make every week, stated once.
///
/// Both sides live in one list because a schedule is one thing with an owner:
/// the driver's produce trips, the rider's produce postings, and somebody who
/// does both should see their week in one place rather than in two tabs that
/// each hide half of it. Which side a row is on is a badge, not a screen.
class SchedulesScreen extends StatefulWidget {
  const SchedulesScreen({super.key, this.asDriver = false});

  /// Which side a new schedule defaults to, taken from wherever the user opened
  /// this from.
  final bool asDriver;

  @override
  State<SchedulesScreen> createState() => _SchedulesScreenState();
}

class _SchedulesScreenState extends State<SchedulesScreen> {
  final _schedules = ScheduleService();

  List<TripSchedule> _list = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    if (mounted) setState(() => _loading = _list.isEmpty);
    final res = await _schedules.mine();
    if (!mounted) return;
    setState(() {
      _loading = false;
      _list = res.data ?? _list;
      _error = res.success ? null : res.errorMessage;
    });
  }

  Future<void> _open([TripSchedule? schedule]) async {
    final changed = await Navigator.push<bool>(
      context,
      MaterialPageRoute(
        builder: (_) => ScheduleFormScreen(schedule: schedule, asDriver: widget.asDriver),
      ),
    );
    if (changed == true) await _load();
  }

  Future<void> _remove(TripSchedule schedule) async {
    final res = await _schedules.remove(schedule.id);
    if (!mounted) return;
    if (!res.success) {
      WanesAlerts.failure(context, res, title: context.tr('schedule.saveFailed'));
      return;
    }
    // Says what actually happened to the trips it had already made: the ones
    // nobody booked are off, and the booked ones still run.
    WanesAlerts.info(context, context.tr('schedule.deleted'),
        message: context.tr('schedule.deletedBody'));
    await _load();
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);

    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        bottom: false,
        child: Column(children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 8, 20, 0),
            child: ScreenHeader(title: context.tr('schedule.title')),
          ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
                children: [
                  if (_loading && _list.isEmpty)
                    const Padding(
                      padding: EdgeInsets.only(top: 60),
                      child: Center(child: WanesSpinner()),
                    )
                  else if (_error != null && _list.isEmpty)
                    WanesInlineAlert(_error!, onTap: _load)
                  else if (_list.isEmpty)
                    _empty(t)
                  else
                    ..._list.map((s) => Padding(
                          padding: const EdgeInsets.only(bottom: 10),
                          child: _card(t, s),
                        )),
                ],
              ),
            ),
          ),
          Container(
            padding: EdgeInsets.fromLTRB(20, 12, 20, 12 + MediaQuery.of(context).padding.bottom),
            decoration: BoxDecoration(
              color: t.surface,
              border: Border(top: BorderSide(color: t.border)),
            ),
            child: PrimaryButton(label: context.tr('schedule.add'), onPressed: () => _open()),
          ),
        ]),
      ),
    );
  }

  Widget _card(WanesTokens t, TripSchedule schedule) {
    final locale = context.l10n.localeName;
    final next = schedule.nextDepartures.isEmpty ? null : schedule.nextDepartures.first.toLocal();

    return WanesCard(
      radius: 16,
      padding: const EdgeInsets.all(14),
      onTap: () => _open(schedule),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          StatusPill(
            label: context.tr(schedule.isDriverSchedule ? 'schedule.asDriver' : 'schedule.asRider'),
            color: schedule.isDriverSchedule ? t.teal : t.amber,
            dot: false,
          ),
          const SizedBox(width: 8),
          if (schedule.isPaused)
            StatusPill(label: context.tr('schedule.paused'), color: t.ink2, dot: false),
          const Spacer(),
          Text(schedule.timeOfDay.toString(),
              style: WanesTheme.mono(size: 13, weight: FontWeight.w700, color: t.ink, spacing: 0)),
        ]),
        const SizedBox(height: 12),
        Row(children: [
          const RouteDot(),
          const SizedBox(width: 8),
          Expanded(
            child: Text(schedule.originAddress,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
          ),
        ]),
        Padding(
          padding: const EdgeInsetsDirectional.only(start: 5, top: 4, bottom: 4),
          child: Container(width: 2, height: 14, color: t.border),
        ),
        Row(children: [
          const RouteDot(destination: true),
          const SizedBox(width: 8),
          Expanded(
            child: Text(schedule.destinationAddress,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
          ),
        ]),
        const SizedBox(height: 12),
        Row(children: [
          MetaChip(schedule.recurrence.label),
          const SizedBox(width: 7),
          Flexible(child: MetaChip(context.trPlural('vehicle.seatCount', schedule.seats))),
          if (schedule.pricePerSeat != null) ...[
            const SizedBox(width: 7),
            MetaChip(Fare.format(schedule.pricePerSeat!)),
          ],
        ]),
        // The next departure it will actually produce, which is the only proof
        // a schedule is doing anything. A paused one, or one whose recurrence
        // never comes round, shows nothing here — and that is the tell.
        if (next != null) ...[
          const SizedBox(height: 10),
          Row(children: [
            Icon(Icons.event_rounded, size: 14, color: t.ink2),
            const SizedBox(width: 6),
            Expanded(
              child: Text(
                '${context.tr('schedule.next')} · '
                '${DateFormat('EEE d MMM · HH:mm', locale).format(next)}',
                style: WanesTheme.mono(
                    size: 11, weight: FontWeight.w500, color: t.ink2, spacing: 0),
              ),
            ),
            TextButton(
              onPressed: () => _remove(schedule),
              child: Text(context.tr('schedule.delete'),
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 12, color: t.alert)),
            ),
          ]),
        ],
      ]),
    );
  }

  Widget _empty(WanesTokens t) => Padding(
        padding: const EdgeInsets.only(top: 40),
        child: Column(children: [
          Icon(Icons.repeat_rounded, size: 40, color: t.ink2),
          const SizedBox(height: 12),
          Text(context.tr('schedule.none'),
              style: TextStyle(fontWeight: FontWeight.w700, color: t.ink)),
          const SizedBox(height: 6),
          Text(context.tr('schedule.noneBody'),
              textAlign: TextAlign.center,
              style: TextStyle(color: t.ink2, fontSize: 13, height: 1.45)),
        ]),
      );
}
