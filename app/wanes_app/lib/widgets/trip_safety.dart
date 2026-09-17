import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:url_launcher/url_launcher.dart';

import '../core/app_config.dart';
import '../core/device_location.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../services/services.dart';
import 'wanes_alerts.dart';
import 'wanes_motion.dart';

/// The rider's four digits, big enough to read out at a kerb.
class BoardingCodeCard extends StatelessWidget {
  const BoardingCodeCard({super.key, required this.code});

  final String code;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      decoration: BoxDecoration(
        color: t.tealTint,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: t.teal),
      ),
      child: Row(children: [
        Icon(Icons.pin_outlined, color: t.tealInk),
        const SizedBox(width: 12),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(context.tr('boarding.yourCode'),
                style: TextStyle(fontWeight: FontWeight.w800, fontSize: 14, color: t.ink)),
            const SizedBox(height: 2),
            Text(context.tr('boarding.yourCodeBody'),
                style: TextStyle(fontSize: 12, height: 1.35, color: t.ink2)),
          ]),
        ),
        const SizedBox(width: 10),
        Text(code,
            textDirection: TextDirection.ltr,
            style: WanesTheme.mono(size: 26, weight: FontWeight.w800, color: t.tealInk, spacing: 4)),
      ]),
    );
  }
}

/// Asks the driver for the code the rider reads out. Null when they backed out.
Future<String?> showBoardingCodeEntry(BuildContext context, String riderName) =>
    showDialog<String>(context: context, builder: (_) => _BoardingCodeDialog(riderName: riderName));

/// Owns its controller, so the field outlives the pop animation — disposing it
/// when the future completes tore it down while the dialog was still fading.
class _BoardingCodeDialog extends StatefulWidget {
  const _BoardingCodeDialog({required this.riderName});

  final String riderName;

  @override
  State<_BoardingCodeDialog> createState() => _BoardingCodeDialogState();
}

class _BoardingCodeDialogState extends State<_BoardingCodeDialog> {
  final _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _submit() {
    final v = _controller.text.trim();
    if (v.length == BoardingCodeLength.digits) Navigator.pop(context, v);
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: Text(context.tr('boarding.enterTitle', {'name': widget.riderName})),
        content: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Text(context.tr('boarding.enterBody')),
          const SizedBox(height: 12),
          TextField(
            controller: _controller,
            autofocus: true,
            keyboardType: TextInputType.number,
            textAlign: TextAlign.center,
            maxLength: BoardingCodeLength.digits,
            inputFormatters: [FilteringTextInputFormatter.digitsOnly],
            style: const TextStyle(fontSize: 26, letterSpacing: 8, fontWeight: FontWeight.w800),
            decoration: const InputDecoration(counterText: '', hintText: '• • • •'),
            onSubmitted: (_) => _submit(),
          ),
        ]),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: Text(context.tr('common.cancel'))),
          FilledButton(onPressed: _submit, child: Text(context.tr('boarding.confirm'))),
        ],
      );
}

/// Mirrors the server's `BoardingCodes.Length`.
abstract final class BoardingCodeLength {
  static const digits = 4;
}

/// The red emergency button. Opens a sheet: call the emergency number, alert
/// the Wanes safety team (with location), and — for a rider — share the trip.
class SosButton extends StatelessWidget {
  const SosButton({super.key, this.tripId, this.bookingId, this.compact = false});

  final int? tripId;
  final int? bookingId;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Tooltip(
      message: context.tr('sos.title'),
      child: Material(
        color: t.alert,
        shape: const StadiumBorder(),
        child: InkWell(
          customBorder: const StadiumBorder(),
          onTap: () => showSosSheet(context, tripId: tripId, bookingId: bookingId),
          child: Padding(
            padding: EdgeInsets.symmetric(horizontal: compact ? 10 : 14, vertical: 8),
            child: Row(mainAxisSize: MainAxisSize.min, children: [
              const Icon(Icons.sos_rounded, color: Colors.white, size: 18),
              if (!compact) ...[
                const SizedBox(width: 6),
                Text(context.tr('sos.button'),
                    style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 13)),
              ],
            ]),
          ),
        ),
      ),
    );
  }
}

Future<void> showSosSheet(BuildContext context, {int? tripId, int? bookingId}) =>
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => _SosSheet(tripId: tripId, bookingId: bookingId),
    );

class _SosSheet extends StatefulWidget {
  const _SosSheet({this.tripId, this.bookingId});

  final int? tripId;
  final int? bookingId;

  @override
  State<_SosSheet> createState() => _SosSheetState();
}

class _SosSheetState extends State<_SosSheet> {
  final _safety = SafetyApi();
  bool _alerting = false;
  bool _alerted = false;
  bool _contactNotified = false;

  String get _number => AppConfigController.value.emergencyNumber;

  Future<void> _call() async {
    var launched = false;
    try {
      launched = await launchUrl(Uri(scheme: 'tel', path: _number));
    } catch (_) {
      launched = false;
    }
    if (!launched && mounted) {
      WanesAlerts.info(context, context.tr('trip.callFailed'), message: _number);
    }
  }

  Future<void> _alert() async {
    if (_alerting || _alerted) return;
    setState(() => _alerting = true);
    // Location is best-effort: an SOS without coordinates still reaches the team.
    final fix = await DeviceLocation.instance.current().timeout(
          const Duration(seconds: 8),
          onTimeout: () => const LocationFix.failed(LocationFailure.timeout),
        );
    final res = await _safety.raise(
      kind: 1,
      tripId: widget.tripId,
      bookingId: widget.bookingId,
      lat: fix.lat,
      lng: fix.lng,
    );
    if (!mounted) return;
    setState(() {
      _alerting = false;
      _alerted = res.success;
      _contactNotified = res.data?.emergencyContactNotified ?? false;
    });
    if (!res.success) {
      WanesAlerts.failure(context, res, title: context.tr('sos.alertFailed'), onRetry: _alert);
    }
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
          padding: const EdgeInsets.fromLTRB(20, 14, 20, 18),
          child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Center(
              child: Container(
                width: 38,
                height: 4,
                decoration: BoxDecoration(color: t.border, borderRadius: BorderRadius.circular(2)),
              ),
            ),
            const SizedBox(height: 16),
            Row(children: [
              Icon(Icons.sos_rounded, color: t.alert, size: 28),
              const SizedBox(width: 10),
              Expanded(
                child: Text(context.tr('sos.title'),
                    style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800, color: t.ink)),
              ),
            ]),
            const SizedBox(height: 6),
            Text(context.tr('sos.body'), style: TextStyle(fontSize: 13, height: 1.4, color: t.ink2)),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: _call,
              icon: const Icon(Icons.call_rounded),
              label: Text(context.tr('sos.call', {'number': _number})),
              style: FilledButton.styleFrom(
                backgroundColor: t.alert,
                foregroundColor: Colors.white,
                padding: const EdgeInsets.symmetric(vertical: 15),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
              ),
            ),
            const SizedBox(height: 10),
            OutlinedButton.icon(
              onPressed: _alerting || _alerted ? null : _alert,
              icon: _alerting
                  ? WanesSpinner.mono(t.ink, size: 18)
                  : Icon(_alerted ? Icons.check_circle_rounded : Icons.shield_outlined),
              label: Text(context.tr(_alerted ? 'sos.alerted' : 'sos.alertTeam')),
              style: OutlinedButton.styleFrom(
                foregroundColor: _alerted ? t.tealInk : t.ink,
                side: BorderSide(color: t.border),
                padding: const EdgeInsets.symmetric(vertical: 14),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
              ),
            ),
            if (_alerted) ...[
              const SizedBox(height: 8),
              Text(
                context.tr(_contactNotified ? 'sos.contactNotified' : 'sos.noContact'),
                style: TextStyle(fontSize: 12, color: t.ink2),
              ),
            ],
            if (widget.bookingId != null) ...[
              const SizedBox(height: 10),
              ShareTripButton(bookingId: widget.bookingId!),
            ],
          ]),
        ),
      ),
    );
  }
}

/// Shares a read-only link to the rider's trip with someone they trust.
class ShareTripButton extends StatefulWidget {
  const ShareTripButton({super.key, required this.bookingId});

  final int bookingId;

  @override
  State<ShareTripButton> createState() => _ShareTripButtonState();
}

/// Creates (or reuses) the rider's trip link, copies a message with it, and
/// offers it to WhatsApp — how most people here would send it.
Future<void> shareTrip(BuildContext context, int bookingId) async {
  final res = await SafetyApi().share(bookingId);
  if (!context.mounted) return;
  final link = res.data;
  if (!res.success || link == null) {
    WanesAlerts.failure(context, res, title: context.tr('share.failed'));
    return;
  }

  final text = link.url != null
      ? context.tr('share.message', {'url': link.url})
      : context.tr('share.messageCode', {'code': link.token});
  await Clipboard.setData(ClipboardData(text: text));
  if (!context.mounted) return;

  var opened = false;
  try {
    opened = await launchUrl(
      Uri.parse('https://wa.me/?text=${Uri.encodeComponent(text)}'),
      mode: LaunchMode.externalApplication,
    );
  } catch (_) {
    opened = false;
  }
  // The link is on the clipboard either way.
  if (context.mounted && !opened) {
    WanesAlerts.success(context, context.tr('share.copied'));
  }
}

class _ShareTripButtonState extends State<ShareTripButton> {
  bool _busy = false;

  Future<void> _share() async {
    if (_busy) return;
    setState(() => _busy = true);
    await shareTrip(context, widget.bookingId);
    if (mounted) setState(() => _busy = false);
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return OutlinedButton.icon(
      onPressed: _busy ? null : _share,
      icon: const Icon(Icons.ios_share_rounded),
      label: Text(context.tr('share.button')),
      style: OutlinedButton.styleFrom(
        foregroundColor: t.tealInk,
        side: BorderSide(color: t.border),
        padding: const EdgeInsets.symmetric(vertical: 14),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      ),
    );
  }
}
