import 'package:flutter/material.dart';

import '../../core/l10n.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/wanes_alerts.dart';
import '../../widgets/wanes_motion.dart';

/// Cancelling a trip, with the cost read out first.
///
/// The sheet asks the server what this cancellation would be recorded as —
/// free, counted, or late — and how close it brings the driver to a pause,
/// before the driver commits. Once riders depend on the trip a reason is
/// required, and the reasons that may not be the driver's fault (a breakdown,
/// a safety concern) are flagged for an admin to review.
///
/// Returns true when the trip was cancelled.
Future<bool> showCancelTripSheet(BuildContext context, Trip trip) async {
  final done = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (_) => _CancelTripSheet(trip: trip),
  );
  return done ?? false;
}

class _CancelTripSheet extends StatefulWidget {
  const _CancelTripSheet({required this.trip});

  final Trip trip;

  @override
  State<_CancelTripSheet> createState() => _CancelTripSheetState();
}

class _CancelTripSheetState extends State<_CancelTripSheet> {
  final _trips = TripService();
  final _note = TextEditingController();

  CancelPreview? _preview;
  CancelReason? _reason;
  bool _loading = true;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _note.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    final res = await _trips.cancelPreview(widget.trip.id);
    if (!mounted) return;
    setState(() {
      _loading = false;
      _preview = res.data;
    });
    if (!res.success) {
      WanesAlerts.failure(context, res, title: context.tr('cancel.previewFailed'));
    }
  }

  Future<void> _confirm() async {
    final preview = _preview;
    if (preview == null || _busy) return;
    if (preview.reasonRequired && _reason == null) {
      WanesAlerts.warning(context, context.tr('cancel.pickReason'));
      return;
    }
    setState(() => _busy = true);
    final res = await _trips.cancel(widget.trip.id, reason: _reason, note: _note.text);
    if (!mounted) return;
    setState(() => _busy = false);
    if (!res.success) {
      WanesAlerts.failure(context, res, title: context.tr('cancel.failed'));
      return;
    }
    WanesAlerts.info(context, context.tr('cancel.done'),
        message: context.tr(preview.ridersRequeued ? 'cancel.doneRequeued' : 'cancel.doneBody'));
    Navigator.pop(context, true);
  }

  String _consequence(CancelPreview p) {
    return switch (p.kind) {
      ReliabilityKind.freeCancel => context.tr('cancel.free'),
      ReliabilityKind.lateCancel => context.trPlural('cancel.late', p.points),
      _ => context.trPlural('cancel.counted', p.points),
    };
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final p = _preview;
    final costly = p != null && p.points > 0;

    return Container(
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: const BorderRadius.vertical(top: Radius.circular(24)),
      ),
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      child: SafeArea(
        top: false,
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 18),
          child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Text(context.tr('cancel.title'),
                style: TextStyle(fontSize: 19, fontWeight: FontWeight.w800, color: t.ink)),
            const SizedBox(height: 12),
            if (_loading)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 24),
                child: Center(child: WanesSpinner()),
              )
            else if (p != null) ...[
              Container(
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: costly ? t.amberTint : t.tealTint,
                  borderRadius: BorderRadius.circular(14),
                ),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Row(children: [
                    Icon(costly ? Icons.warning_amber_rounded : Icons.check_circle_outline,
                        color: costly ? t.amberInk : t.tealInk),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(_consequence(p),
                          style: TextStyle(
                              fontWeight: FontWeight.w800, color: costly ? t.amberInk : t.tealInk)),
                    ),
                  ]),
                  const SizedBox(height: 6),
                  if (p.ridersAffected > 0)
                    Text(context.trPlural('cancel.ridersAffected', p.ridersAffected),
                        style: TextStyle(fontSize: 12.5, color: t.ink)),
                  if (costly)
                    Text(
                        context.tr('cancel.standing', {
                          'after': p.pointsAfter,
                          'limit': p.suspendPoints,
                          'days': p.windowDays,
                        }),
                        style: TextStyle(fontSize: 12.5, color: t.ink)),
                  if (p.wouldSuspend)
                    Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: Text(context.tr('cancel.wouldSuspend'),
                          style: TextStyle(fontSize: 12.5, fontWeight: FontWeight.w800, color: t.alert)),
                    ),
                  if (p.ridersRequeued)
                    Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: Text(context.tr('cancel.requeued'),
                          style: TextStyle(fontSize: 12.5, color: t.ink2)),
                    ),
                ]),
              ),
              if (p.reasonRequired) ...[
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
                if (_reason == CancelReason.vehicleProblem ||
                    _reason == CancelReason.safetyConcern ||
                    _reason == CancelReason.emergency)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text(context.tr('cancel.reviewNote'),
                        style: TextStyle(fontSize: 12, color: t.ink2)),
                  ),
                const SizedBox(height: 10),
                TextField(
                  controller: _note,
                  maxLength: 500,
                  maxLines: 2,
                  decoration: InputDecoration(hintText: context.tr('cancel.noteHint')),
                ),
              ],
            ],
            const SizedBox(height: 10),
            Row(children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: _busy ? null : () => Navigator.pop(context, false),
                  style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(48)),
                  child: Text(context.tr('cancel.keep')),
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
                      : Text(context.tr('cancel.confirm')),
                ),
              ),
            ]),
          ]),
        ),
      ),
    );
  }
}
