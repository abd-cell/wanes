import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

import '../../core/app_config.dart';
import '../../core/error_messages.dart';
import '../../core/fare.dart';
import '../../core/geo.dart';
import '../../core/l10n.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/repeat_picker.dart';
import '../../widgets/safety_notes.dart';
import '../../widgets/wanes_alerts.dart';
import '../../widgets/wanes_motion.dart';
import '../../widgets/wanes_ui.dart';
import '../driver/accept_flow.dart';

/// Whether the whole-series option applies to this request card.
bool canTakeSeries(RiderTrip request) {
  final series = request.series;
  return AppConfigController.value.seriesEnabled &&
      series != null &&
      series.isRiderSeries &&
      series.isOpenForSeries &&
      request.isOpen;
}

/// Whether a rider can book every day of this trip's series.
bool canJoinSeries(Trip trip) {
  final series = trip.series;
  return AppConfigController.value.seriesEnabled &&
      series != null &&
      series.isDriverSeries &&
      !series.isPaused &&
      series.mySeriesId == null;
}

/// The driver's way into a request card. A one-off goes straight to the
/// ordinary accept; a day of a recurring request first asks "this day, or
/// every day?".
Future<bool> takeRideRequest(
  BuildContext context,
  RiderTrip request, {
  ValueChanged<bool>? onBusy,
}) async {
  if (!canTakeSeries(request)) return acceptRideRequest(context, request, onBusy: onBusy);

  // A dialog rather than a sheet, because the next step is itself a sheet:
  // a sheet that opens another sheet leaves the outgoing one's barrier over
  // the new one on the device, and every tap on its lower half goes nowhere.
  final wholeSeries = await showDialog<bool>(
    context: context,
    builder: (_) => _DayOrSeriesSheet(request: request),
  );
  if (wholeSeries == null || !context.mounted) return false;

  return wholeSeries
      ? offerWholeSeries(context, request, onBusy: onBusy)
      : acceptRideRequest(context, request, onBusy: onBusy);
}

class _DayOrSeriesSheet extends StatelessWidget {
  const _DayOrSeriesSheet({required this.request});

  final RiderTrip request;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final series = request.series!;
    final locale = context.l10n.localeName;
    final offered = series.mySeriesStatus == SeriesStatus.proposed;

    return _ChoiceDialog(
      title: context.tr('series.takeTitle'),
      subtitle: series.label(locale),
      children: [
        _OptionTile(
          icon: Icons.event_rounded,
          title: context.tr('series.takeDay', {
            'date': DateFormat('EEE d MMM', locale).format(request.departAt.toLocal()),
          }),
          body: context.tr('series.takeDayBody'),
          onTap: () => Navigator.pop(context, false),
        ),
        const SizedBox(height: 10),
        _OptionTile(
          icon: Icons.repeat_rounded,
          accent: true,
          title: context.tr(offered ? 'series.offerSentTitle' : 'series.takeAll'),
          body: offered
              ? context.tr('series.offerSentUpdate')
              : context.trPlural('series.takeAllBody', series.upcomingDays),
          onTap: () => Navigator.pop(context, true),
        ),
        if (series.proposalCount > 0 && !offered) ...[
          const SizedBox(height: 8),
          Text(context.trPlural('series.otherOffers', series.proposalCount),
              style: TextStyle(fontSize: 12, color: t.ink2)),
        ],
      ],
    );
  }
}

/// A driver offers to drive every day of the rider's series.
Future<bool> offerWholeSeries(
  BuildContext context,
  RiderTrip request, {
  ValueChanged<bool>? onBusy,
}) async {
  final series = request.series;
  if (series == null) return false;
  if (!await ensureSafetyAcknowledged(context, SafetyAudience.driver)) return false;
  if (!context.mounted) return false;

  final vehicle = await DriverVehicles.primary();
  if (!context.mounted) return false;
  if (vehicle == null) {
    WanesAlerts.warning(context, context.tr('series.needVehicle'));
    return false;
  }

  final suggestion = request.suggestedPricePerSeat > 0
      ? request.suggestedPricePerSeat
      : Fare.perSeat(Geo.distanceKm(request.originLat, request.originLng,
          request.destinationLat, request.destinationLng));

  final terms = await showModalBottomSheet<SeriesTerms>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (_) => SeriesTermsSheet(
      forDriver: true,
      series: series,
      origin: request.originAddress,
      destination: request.destinationAddress,
      suggestion: suggestion,
      minSeats: request.seatsWanted,
      maxSeats: vehicle.seatCapacity,
      initialSeats: vehicle.seatCapacity,
    ),
  );
  if (terms == null || !context.mounted) return false;

  onBusy?.call(true);
  final res = await SeriesApi().propose(
    request.id,
    vehicleId: vehicle.id,
    pricePerSeat: terms.pricePerSeat,
    seatsOffered: terms.seats,
    days: terms.days,
    until: terms.until,
  );
  onBusy?.call(false);
  if (!context.mounted) return res.success;

  final row = res.data;
  if (!res.success || row == null) {
    WanesAlerts.failure(context, res, title: context.tr('series.offerFailed'));
    return false;
  }
  if (row.isActive) {
    WanesAlerts.success(context, context.tr('series.takenTitle'),
        message: context.trPlural('series.takenBody', row.upcomingCount));
  } else {
    WanesAlerts.success(context, context.tr('series.offerSentTitle'),
        message: context.tr('series.offerSentBody', {
          'hours': AppConfigController.value.seriesDecisionHours,
        }));
  }
  return true;
}

/// A rider books every upcoming day of a driver's recurring trip.
Future<bool> joinWholeSeries(BuildContext context, Trip trip) async {
  final series = trip.series;
  if (series == null) return false;
  if (!await ensureSafetyAcknowledged(context, SafetyAudience.rider)) return false;
  if (!context.mounted) return false;

  final terms = await showModalBottomSheet<SeriesTerms>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (_) => SeriesTermsSheet(
      forDriver: false,
      series: series,
      origin: trip.originAddress,
      destination: trip.destinationAddress,
      pricePerSeat: trip.pricePerSeat,
      minSeats: 1,
      maxSeats: trip.seatsTotal > 0 ? trip.seatsTotal : 4,
      initialSeats: 1,
      driverName: trip.driverName,
    ),
  );
  if (terms == null || !context.mounted) return false;

  final res = await SeriesApi().join(trip.id, seats: terms.seats ?? 1, days: terms.days, until: terms.until);
  if (!context.mounted) return res.success;
  final result = res.data;
  if (!res.success || result == null) {
    WanesAlerts.failure(context, res, title: context.tr('series.joinFailed'));
    return false;
  }
  await showSeriesResult(context, result, titleKey: 'series.joinedTitle');
  return true;
}

/// What a series came to, day by day: how many are booked or taken, and which
/// could not be, and why.
Future<void> showSeriesResult(BuildContext context, SeriesResult result, {required String titleKey}) {
  final locale = context.l10n.localeName;
  final missed = result.missed;
  return showDialog<void>(
    context: context,
    builder: (ctx) {
      final t = WanesTokens.of(ctx);
      return AlertDialog(
        title: Text(ctx.tr(titleKey)),
        content: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(ctx.trPlural('series.daysBooked', result.okCount),
              style: TextStyle(fontWeight: FontWeight.w700, color: t.tealInk)),
          if (missed.isNotEmpty) ...[
            const SizedBox(height: 10),
            Text(ctx.trPlural('series.daysMissed', missed.length),
                style: TextStyle(fontWeight: FontWeight.w700, color: t.amberInk)),
            const SizedBox(height: 4),
            for (final day in missed.take(6))
              Padding(
                padding: const EdgeInsets.only(top: 3),
                child: Text(
                  '${DateFormat('EEE d MMM', locale).format(day.date)} — '
                  '${messageForCode(day.refusalCode ?? 0) ?? ctx.tr('errors.tryAgain')}',
                  style: TextStyle(fontSize: 12.5, color: t.ink2),
                ),
              ),
          ],
          const SizedBox(height: 10),
          Text(ctx.tr('series.newDaysNote'), style: TextStyle(fontSize: 12, color: t.ink2)),
        ]),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: Text(ctx.tr('common.done'))),
        ],
      );
    },
  );
}

/// What somebody commits a series on.
class SeriesTerms {
  const SeriesTerms({this.pricePerSeat = 0, this.seats, this.days = WeekDaySet.none, this.until});

  final double pricePerSeat;

  /// Driver: seats on each day's trip. Rider: seats booked each day.
  final int? seats;

  /// A subset of the schedule's days; empty means all of them.
  final WeekDaySet days;
  final DateTime? until;
}

/// The terms sheet, for either side: which days, until when, how many seats —
/// and, for a driver, the price — with the series rules read out before the
/// agreement.
class SeriesTermsSheet extends StatefulWidget {
  const SeriesTermsSheet({
    super.key,
    required this.forDriver,
    required this.series,
    required this.origin,
    required this.destination,
    required this.minSeats,
    required this.maxSeats,
    required this.initialSeats,
    this.suggestion = 0,
    this.pricePerSeat,
    this.driverName,
  });

  final bool forDriver;
  final SeriesInfo series;
  final String origin;
  final String destination;
  final int minSeats;
  final int maxSeats;
  final int initialSeats;

  /// Driver: the price the sheet opens on.
  final double suggestion;

  /// Rider: the price the driver's series lists at.
  final double? pricePerSeat;
  final String? driverName;

  @override
  State<SeriesTermsSheet> createState() => _SeriesTermsSheetState();
}

class _SeriesTermsSheetState extends State<SeriesTermsSheet> {
  static const _step = 0.25;

  late double _price = widget.suggestion.clamp(0, 50).toDouble();
  late int _seats = widget.initialSeats.clamp(widget.minSeats, widget.maxSeats);
  late WeekDaySet _days = _scheduleDays;
  DateTime? _until;
  bool _agreed = false;

  bool get _weekly => widget.series.recurrence == Recurrence.weekly;
  bool get _hasDayChoice => widget.series.recurrence != Recurrence.monthly;

  /// The days the schedule itself runs on.
  WeekDaySet get _scheduleDays => _weekly ? widget.series.daysOfWeek : allWeek;

  /// Every day the schedule runs is the same as "all of them".
  WeekDaySet get _subset => _days.mask == _scheduleDays.mask ? WeekDaySet.none : _days;

  Future<void> _pickUntil() async {
    final now = DateTime.now();
    final floor = DateTime(now.year, now.month, now.day);
    final last = widget.series.endDate ?? floor.add(const Duration(days: 365));
    final picked = await showDatePicker(
      context: context,
      initialDate: _until ?? (last.isBefore(floor.add(const Duration(days: 30))) ? last : floor.add(const Duration(days: 30))),
      firstDate: floor,
      lastDate: last.isBefore(floor) ? floor : last,
    );
    if (picked != null && mounted) setState(() => _until = picked);
  }

  void _submit() {
    if (_hasDayChoice && _days.isEmpty) {
      WanesAlerts.warning(context, context.tr('schedule.pickDays'));
      return;
    }
    Navigator.pop(
      context,
      SeriesTerms(
        pricePerSeat: _price,
        seats: _seats,
        days: _hasDayChoice ? _subset : WeekDaySet.none,
        until: _until,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final locale = context.l10n.localeName;
    final config = AppConfigController.value;
    final series = widget.series;
    final price = widget.forDriver ? _price : (widget.pricePerSeat ?? 0);

    return _SheetFrame(
      title: context.tr(widget.forDriver ? 'series.offerTitle' : 'series.joinTitle'),
      subtitle: '${widget.origin} → ${widget.destination}',
      scrollable: true,
      children: [
        Wrap(spacing: 8, runSpacing: 6, children: [
          RepeatBadge(series: series),
          MetaChip(series.timeOfDay.toString()),
          if (widget.driverName != null) MetaChip(widget.driverName!),
        ]),
        const SizedBox(height: 14),
        if (_hasDayChoice) ...[
          MonoLabel(context.tr('series.whichDays'), spacing: 1.0),
          const SizedBox(height: 8),
          DayChips(
            days: _days,
            allowed: _scheduleDays,
            onToggle: (d) => setState(() => _days = _days.toggle(d)),
          ),
          const SizedBox(height: 12),
        ],
        GroupedCard(children: [
          GroupedRow(
            icon: Icons.event_busy_outlined,
            title: context.tr('repeat.until'),
            subtitle: _until == null
                ? (series.endDate == null
                    ? context.tr('series.untilOpen')
                    : DateFormat('EEE d MMM yyyy', locale).format(series.endDate!))
                : DateFormat('EEE d MMM yyyy', locale).format(_until!),
            trailing: _until == null
                ? Icon(Icons.chevron_right_rounded, size: 18, color: t.ink2)
                : IconButton(
                    icon: Icon(Icons.close_rounded, size: 18, color: t.ink2),
                    onPressed: () => setState(() => _until = null),
                  ),
            onTap: _pickUntil,
          ),
          GroupedRow(
            icon: Icons.event_seat_outlined,
            title: context.tr(widget.forDriver ? 'accept.seatsOnTrip' : 'series.seatsEachDay'),
            subtitle: context.trPlural('vehicle.seatCount', _seats),
            trailing: SeatStepper(
              value: _seats,
              min: widget.minSeats,
              max: widget.maxSeats,
              onChanged: (v) => setState(() => _seats = v),
            ),
          ),
          if (!widget.forDriver && widget.pricePerSeat != null)
            GroupedRow(
              icon: Icons.payments_outlined,
              title: context.tr('driver.pricePerSeat'),
              subtitle: Fare.format(widget.pricePerSeat!),
              trailing: const SizedBox.shrink(),
            ),
        ]),
        if (widget.forDriver) ...[
          const SizedBox(height: 12),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            decoration: BoxDecoration(
              color: t.surface2,
              borderRadius: BorderRadius.circular(16),
              border: Border.all(color: t.border),
            ),
            child: Row(children: [
              MonoLabel(context.tr('driver.pricePerSeat'), spacing: 1.0),
              const Spacer(),
              SeatStepperButton(
                glyph: '–',
                enabled: _price > 0,
                onTap: () => setState(() => _price = (_price - _step).clamp(0, 50).toDouble()),
              ),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 12),
                child: Text(Fare.format(_price),
                    style: WanesTheme.mono(size: 18, weight: FontWeight.w800, color: t.ink, spacing: 0)),
              ),
              SeatStepperButton(
                glyph: '+',
                accent: true,
                enabled: _price < 50,
                onTap: () => setState(() => _price = (_price + _step).clamp(0, 50).toDouble()),
              ),
            ]),
          ),
        ],
        const SizedBox(height: 12),
        // The rules, before the agreement.
        Container(
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(color: t.amberTint, borderRadius: BorderRadius.circular(14)),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(context.tr('series.rulesTitle'),
                style: TextStyle(fontWeight: FontWeight.w800, fontSize: 13, color: t.amberInk)),
            const SizedBox(height: 4),
            Text(
              context.tr(widget.forDriver ? 'series.rulesDriver' : 'series.rulesRider', {
                'hours': config.seriesSkipNoticeHours,
                'days': config.seriesEndNoticeDays,
              }),
              style: TextStyle(fontSize: 12, height: 1.45, color: t.ink),
            ),
          ]),
        ),
        const SizedBox(height: 10),
        // The whole card toggles the agreement, not only the little box: this
        // is the one control between a driver and a week of work.
        GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTap: () => setState(() => _agreed = !_agreed),
          child: SharedRideNotice(
            body: context.tr(widget.forDriver ? 'series.sharedDriver' : 'series.sharedRider'),
            value: _agreed,
            onChanged: (v) => setState(() => _agreed = v),
          ),
        ),
        const SizedBox(height: 8),
        SizedBox(
          width: double.infinity,
          child: FilledButton(
            onPressed: _agreed ? _submit : null,
            style: FilledButton.styleFrom(
              backgroundColor: t.tealInk,
              padding: const EdgeInsets.symmetric(vertical: 15),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
            ),
            child: Text(widget.forDriver
                ? context.tr('series.offerCta', {'value': Fare.format(price)})
                : context.tr('series.joinCta')),
          ),
        ),
      ],
    );
  }
}

/// Ends a commitment, after showing both ways of ending and what each costs.
/// Returns true when it was ended (or set to end).
Future<bool> endSeries(BuildContext context, SeriesCommitment series) async {
  final done = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (_) => _EndSeriesSheet(series: series),
  );
  return done ?? false;
}

class _EndSeriesSheet extends StatefulWidget {
  const _EndSeriesSheet({required this.series});

  final SeriesCommitment series;

  @override
  State<_EndSeriesSheet> createState() => _EndSeriesSheetState();
}

class _EndSeriesSheetState extends State<_EndSeriesSheet> {
  final _api = SeriesApi();
  SeriesEndPreview? _preview;
  bool _loading = true;
  bool _busy = false;
  bool _now = false;
  CancelReason? _reason;

  bool get _driverCommitter => widget.series.side == SeriesSide.driverServes && widget.series.isMine;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final res = await _api.endPreview(widget.series.id);
    if (!mounted) return;
    setState(() {
      _loading = false;
      _preview = res.data;
    });
    if (!res.success) WanesAlerts.failure(context, res, title: context.tr('series.previewFailed'));
  }

  Future<void> _confirm() async {
    if (_busy) return;
    if (_now && _driverCommitter && _reason == null) {
      WanesAlerts.warning(context, context.tr('cancel.pickReason'));
      return;
    }
    setState(() => _busy = true);
    final res = await _api.end(widget.series.id, immediately: _now, reason: _reason);
    if (!mounted) return;
    setState(() => _busy = false);
    if (!res.success) {
      WanesAlerts.failure(context, res, title: context.tr('series.endFailed'));
      return;
    }
    WanesAlerts.info(context, context.tr(_now ? 'series.endedTitle' : 'series.endingTitle'));
    Navigator.pop(context, true);
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final p = _preview;
    final locale = context.l10n.localeName;

    return _SheetFrame(
      title: context.tr(widget.series.isMine ? 'series.endTitle' : 'series.releaseTitle'),
      subtitle: '${widget.series.originAddress} → ${widget.series.destinationAddress}',
      scrollable: true,
      children: [
        if (_loading)
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 24),
            child: Center(child: WanesSpinner()),
          )
        else if (p != null) ...[
          _choice(
            t,
            selected: !_now,
            title: context.tr('series.endWithNotice'),
            body: context.tr('series.endWithNoticeBody', {
              'date': DateFormat('EEE d MMM', locale).format(p.noticeEnd),
              'kept': p.daysKept,
            }),
            tag: context.tr('cancel.free'),
            tagColor: t.tealInk,
            onTap: () => setState(() => _now = false),
          ),
          const SizedBox(height: 10),
          _choice(
            t,
            selected: _now,
            title: context.tr('series.endNow'),
            body: context.trPlural('series.endNowBody', p.daysDroppedNow),
            tag: p.free
                ? context.tr('cancel.free')
                : p.pointsNow > 0
                    ? context.trPlural('series.endNowPoints', p.pointsNow)
                    : p.lateCancelsNow > 0
                        ? context.trPlural('series.endNowLate', p.lateCancelsNow)
                        : context.tr('cancel.free'),
            tagColor: (p.pointsNow > 0 || p.lateCancelsNow > 0) && !p.free ? t.amberInk : t.tealInk,
            onTap: () => setState(() => _now = true),
          ),
          if (_now && p.wouldSuspend && !p.free)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Text(context.tr('cancel.wouldSuspend'),
                  style: TextStyle(fontSize: 12.5, fontWeight: FontWeight.w800, color: t.alert)),
            ),
          if (_now && _driverCommitter) ...[
            const SizedBox(height: 14),
            Text(context.tr('cancel.reason'),
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 13.5, color: t.ink)),
            const SizedBox(height: 8),
            Wrap(spacing: 8, runSpacing: 8, children: [
              for (final r in CancelReason.values)
                ChoiceChip(
                  label: Text(context.tr(r.labelKey)),
                  selected: _reason == r,
                  onSelected: (_) => setState(() => _reason = r),
                ),
            ]),
          ],
          const SizedBox(height: 10),
          Text(context.tr('series.endRequeued'), style: TextStyle(fontSize: 12, color: t.ink2)),
        ],
        const SizedBox(height: 12),
        Row(children: [
          Expanded(
            child: OutlinedButton(
              onPressed: _busy ? null : () => Navigator.pop(context, false),
              style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(48)),
              child: Text(context.tr('series.keep')),
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: FilledButton(
              onPressed: _busy || p == null ? null : _confirm,
              style: FilledButton.styleFrom(
                backgroundColor: t.alert,
                foregroundColor: Colors.white,
                minimumSize: const Size.fromHeight(48),
              ),
              child: _busy
                  ? WanesSpinner.mono(Colors.white, size: 18)
                  : Text(context.tr(_now ? 'series.endNowCta' : 'series.endWithNoticeCta')),
            ),
          ),
        ]),
      ],
    );
  }

  Widget _choice(
    WanesTokens t, {
    required bool selected,
    required String title,
    required String body,
    required String tag,
    required Color tagColor,
    required VoidCallback onTap,
  }) =>
      GestureDetector(
        onTap: onTap,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 140),
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: selected ? t.tealTint : t.surface2,
            borderRadius: BorderRadius.circular(14),
            border: Border.all(color: selected ? t.teal : t.border, width: selected ? 1.5 : 1),
          ),
          child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Icon(selected ? Icons.radio_button_checked : Icons.radio_button_off,
                size: 20, color: selected ? t.tealInk : t.ink2),
            const SizedBox(width: 10),
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(title, style: TextStyle(fontWeight: FontWeight.w800, fontSize: 14, color: t.ink)),
                const SizedBox(height: 3),
                Text(body, style: TextStyle(fontSize: 12.5, height: 1.4, color: t.ink2)),
                const SizedBox(height: 6),
                Text(tag, style: TextStyle(fontWeight: FontWeight.w800, fontSize: 12, color: tagColor)),
              ]),
            ),
          ]),
        ),
      );
}

// ── Shared sheet pieces ────────────────────────────────────────────────────

/// The "this day, or every day?" fork: two options and nothing else.
class _ChoiceDialog extends StatelessWidget {
  const _ChoiceDialog({required this.title, required this.subtitle, required this.children});

  final String title;
  final String subtitle;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Dialog(
      backgroundColor: t.surface,
      insetPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 40),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(18, 18, 18, 16),
        child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Text(title,
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800, letterSpacing: -0.3, color: t.ink)),
          const SizedBox(height: 4),
          Text(subtitle, style: TextStyle(fontSize: 12.5, height: 1.4, color: t.ink2)),
          const SizedBox(height: 14),
          ...children,
        ]),
      ),
    );
  }
}

/// Sheet padding, with room for whatever the system has put over the bottom.
EdgeInsets _padding(BuildContext context) =>
    EdgeInsets.fromLTRB(20, 14, 20, 18 + MediaQuery.viewInsetsOf(context).bottom);

class _SheetFrame extends StatelessWidget {
  const _SheetFrame({
    required this.title,
    required this.children,
    this.subtitle,
    this.scrollable = false,
  });

  final String title;
  final String? subtitle;
  final List<Widget> children;
  final bool scrollable;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final content = Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Center(
          child: Container(
            width: 38,
            height: 4,
            decoration: BoxDecoration(color: t.border, borderRadius: BorderRadius.circular(2)),
          ),
        ),
        const SizedBox(height: 16),
        Text(title,
            style: TextStyle(fontSize: 19, fontWeight: FontWeight.w800, letterSpacing: -0.3, color: t.ink)),
        if (subtitle != null) ...[
          const SizedBox(height: 4),
          Text(subtitle!, style: TextStyle(fontSize: 13, height: 1.4, color: t.ink2)),
        ],
        const SizedBox(height: 16),
        ...children,
      ],
    );

    return ConstrainedBox(
      constraints: BoxConstraints(maxHeight: MediaQuery.of(context).size.height * .92),
      child: Container(
        decoration: BoxDecoration(
          color: t.surface,
          borderRadius: const BorderRadius.vertical(top: Radius.circular(24)),
        ),
        child: SafeArea(
          top: false,
          // The keyboard inset is *scroll padding*, never padding on the sheet
          // itself: taking it off the box shortens the box while the content
          // still paints its full height, and the last inch of a sheet — the
          // agreement and the button — ends up outside its own hit area,
          // visible and deaf to taps.
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Flexible(
                child: scrollable
                    ? SingleChildScrollView(padding: _padding(context), child: content)
                    : Padding(padding: _padding(context), child: content),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _OptionTile extends StatelessWidget {
  const _OptionTile({
    required this.icon,
    required this.title,
    required this.body,
    required this.onTap,
    this.accent = false,
  });

  final IconData icon;
  final String title;
  final String body;
  final VoidCallback onTap;
  final bool accent;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Material(
      color: accent ? t.tealTint : t.surface2,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: accent ? t.teal : t.border),
          ),
          child: Row(children: [
            Icon(icon, color: accent ? t.tealInk : t.ink2),
            const SizedBox(width: 12),
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(title, style: TextStyle(fontWeight: FontWeight.w800, fontSize: 14.5, color: t.ink)),
                const SizedBox(height: 3),
                Text(body, style: TextStyle(fontSize: 12.5, height: 1.4, color: t.ink2)),
              ]),
            ),
            Icon(Icons.chevron_right_rounded, color: t.ink2),
          ]),
        ),
      ),
    );
  }
}

/// The square −/+ used on the price rows.
class SeatStepperButton extends StatelessWidget {
  const SeatStepperButton({
    super.key,
    required this.glyph,
    required this.enabled,
    required this.onTap,
    this.accent = false,
  });

  final String glyph;
  final bool enabled;
  final VoidCallback onTap;
  final bool accent;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return GestureDetector(
      onTap: enabled ? onTap : null,
      child: Container(
        width: 34,
        height: 34,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: accent ? t.tealTint : t.surface,
          borderRadius: BorderRadius.circular(9),
          border: accent ? null : Border.all(color: t.border),
        ),
        child: Text(glyph,
            style: TextStyle(
                fontWeight: FontWeight.w800,
                fontSize: 19,
                color: enabled ? (accent ? t.tealInk : t.ink2) : t.ink2.withValues(alpha: .4))),
      ),
    );
  }
}
