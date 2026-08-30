import 'package:flutter/material.dart';
import '../core/fare.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';

/// Rate the trip — prototype screen 07. Completion header, the "How was …?"
/// card with its star row, the trip receipt, then a pinned submit bar.
///
/// The design's tip chips are not built: there is no payment backend, so a tip
/// control would collect an amount nothing could ever charge. The receipt shows
/// what the driver set as the fare and is settled with them directly.
class RateScreen extends StatefulWidget {
  const RateScreen({
    super.key,
    required this.bookingId,
    required this.driverName,
    this.trip,
    this.seats = 1,
  });

  final int bookingId;
  final String driverName;
  final Trip? trip;
  final int seats;

  @override
  State<RateScreen> createState() => _RateScreenState();
}

class _RateScreenState extends State<RateScreen> {
  final _ratings = RatingService();
  final _comment = TextEditingController();
  int _stars = 5;
  bool _busy = false;

  String get _firstName {
    final n = widget.driverName.trim();
    return n.isEmpty ? context.tr('rate.yourDriver') : n.split(' ').first;
  }

  double get _fare => (widget.trip?.pricePerSeat ?? 0) * widget.seats;

  static String initialsOf(String name) {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
    return (parts.first[0] + parts.last[0]).toUpperCase();
  }

  @override
  void dispose() {
    _comment.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    setState(() => _busy = true);
    final res = await _ratings.rate(widget.bookingId, _stars,
        comment: _comment.text.trim().isEmpty ? null : _comment.text.trim());
    if (!mounted) return;
    setState(() => _busy = false);
    if (!res.success) {
      WanesAlerts.failure(context, res,
          title: context.tr('rate.sendFailed'), onRetry: _submit);
      return;
    }
    WanesAlerts.success(context, context.tr('rate.thanks'),
        message: context.tr('rate.thanksBody'));
    Navigator.of(context).popUntil((r) => r.isFirst);
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final trip = widget.trip;

    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        bottom: false,
        child: Column(children: [
          Expanded(
            child: ListView(
              padding: const EdgeInsets.fromLTRB(22, 14, 22, 24),
              children: [
                Column(children: [
                  Container(
                    width: 56,
                    height: 56,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: t.teal,
                      shape: BoxShape.circle,
                      boxShadow: [
                        BoxShadow(
                            color: t.teal, blurRadius: 24, offset: const Offset(0, 10), spreadRadius: -8),
                      ],
                    ),
                    child: Icon(Icons.check_rounded, size: 30, color: t.onTeal),
                  ),
                  const SizedBox(height: 14),
                  Text(context.tr('rate.tripCompleted'),
                      style: TextStyle(
                          fontSize: 22, fontWeight: FontWeight.w800, letterSpacing: -0.44, color: t.ink)),
                  if (trip != null) ...[
                    const SizedBox(height: 4),
                    Text(
                      context.tr('common.routeSummary', {
                        'from': _short(trip.originAddress),
                        'to': _short(trip.destinationAddress),
                      }),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: WanesTheme.mono(
                          size: 12, weight: FontWeight.w500, color: t.ink2, spacing: 0),
                    ),
                  ],
                ]),
                const SizedBox(height: 20),
                _ratingCard(t),
                const SizedBox(height: 16),
                TextField(
                  controller: _comment,
                  maxLines: 3,
                  decoration: InputDecoration(
                    hintText: context.tr('rate.noteHint'),
                    alignLabelWithHint: true,
                  ),
                ),
                if (_fare > 0) ...[
                  const SizedBox(height: 16),
                  _receiptCard(t),
                ],
              ],
            ),
          ),
          BottomActionBar(
            child: PrimaryButton(
              label: context.tr('rate.submit'),
              arrow: false,
              busy: _busy,
              onPressed: _busy ? null : _submit,
            ),
          ),
        ]),
      ),
    );
  }

  Widget _ratingCard(WanesTokens t) {
    return WanesCard(
      radius: 16,
      child: Column(children: [
        Row(mainAxisAlignment: MainAxisAlignment.center, children: [
          AvatarBadge(initialsOf(widget.driverName), size: 40, tint: t.tealTint, fg: t.tealInk),
          const SizedBox(width: 10),
          Flexible(
            child: Text(context.tr('rate.howWas', {'name': _firstName}),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15, color: t.ink)),
          ),
        ]),
        const SizedBox(height: 14),
        StarPicker(value: _stars, onChanged: (v) => setState(() => _stars = v)),
      ]),
    );
  }

  Widget _receiptCard(WanesTokens t) {
    return WanesCard(
      radius: 16,
      child: Column(children: [
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 6),
          child: Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
            Text(context.trPlural('booking.fareForSeats', widget.seats),
                style: TextStyle(fontSize: 14, color: t.ink2)),
            Text(Fare.format(_fare),
                style: WanesTheme.mono(size: 14, weight: FontWeight.w600, color: t.ink, spacing: 0)),
          ]),
        ),
        const SizedBox(height: 6),
        Divider(height: 1, thickness: 1, color: t.border),
        const SizedBox(height: 10),
        Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
          Text(context.tr('common.total'),
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15, color: t.ink)),
          Text(Fare.format(_fare),
              style: WanesTheme.mono(size: 17, weight: FontWeight.w800, color: t.tealInk, spacing: 0)),
        ]),
        const SizedBox(height: 8),
        Row(children: [
          Icon(Icons.info_outline_rounded, size: 14, color: t.ink2),
          const SizedBox(width: 6),
          Expanded(
            child: Text(context.tr('rate.settleUp'),
                style: TextStyle(fontSize: 11.5, color: t.ink2)),
          ),
        ]),
      ]),
    );
  }

  static String _short(String address) => address.split(',').first.trim();
}
