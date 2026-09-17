import 'package:flutter/material.dart';

import '../core/acknowledgements.dart';
import '../core/l10n.dart';
import '../core/theme.dart';

/// Who the safety notes are written for. The two lists differ: a rider checks
/// the car, a driver checks the passengers.
enum SafetyAudience {
  rider('safety.rider', Acknowledgement.riderSafety, 5),
  driver('safety.driver', Acknowledgement.driverSafety, 5);

  const SafetyAudience(this.prefix, this.acknowledgement, this.count);

  final String prefix;
  final Acknowledgement acknowledgement;
  final int count;

  List<String> noteKeys() => [for (var i = 1; i <= count; i++) '$prefix.$i'];
}

/// Shows the safety notes. With [requireAck] the sheet can only be closed by
/// ticking the box and agreeing; the result says whether they did.
Future<bool> showSafetyNotes(
  BuildContext context,
  SafetyAudience audience, {
  bool requireAck = false,
}) async {
  final agreed = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (_) => _SafetySheet(audience: audience, requireAck: requireAck),
  );
  return agreed ?? false;
}

/// Makes sure the user has agreed to the notes once, asking if they have not.
/// Returns false when they backed out — the caller must not go on.
Future<bool> ensureSafetyAcknowledged(BuildContext context, SafetyAudience audience) async {
  if (await Acknowledgements.has(audience.acknowledgement)) return true;
  if (!context.mounted) return false;
  final agreed = await showSafetyNotes(context, audience, requireAck: true);
  if (agreed) await Acknowledgements.record(audience.acknowledgement);
  return agreed;
}

class _SafetySheet extends StatefulWidget {
  const _SafetySheet({required this.audience, required this.requireAck});

  final SafetyAudience audience;
  final bool requireAck;

  @override
  State<_SafetySheet> createState() => _SafetySheetState();
}

class _SafetySheetState extends State<_SafetySheet> {
  bool _ticked = false;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return ConstrainedBox(
      constraints: BoxConstraints(maxHeight: MediaQuery.of(context).size.height * .9),
      child: Container(
        decoration: BoxDecoration(
          color: t.surface,
          borderRadius: const BorderRadius.vertical(top: Radius.circular(24)),
        ),
        child: SafeArea(
          top: false,
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(20, 14, 20, 18),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const _Grabber(),
                const SizedBox(height: 16),
                Row(children: [
                  _IconTile(icon: Icons.shield_outlined, tint: t.tealTint, color: t.tealInk),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(context.tr('safety.title'),
                        style: TextStyle(
                            fontSize: 19, fontWeight: FontWeight.w800, letterSpacing: -0.3, color: t.ink)),
                  ),
                ]),
                const SizedBox(height: 8),
                Text(context.tr('safety.subtitle'),
                    style: TextStyle(fontSize: 13, height: 1.4, color: t.ink2)),
                const SizedBox(height: 14),
                for (final key in widget.audience.noteKeys())
                  Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Padding(
                        padding: const EdgeInsets.only(top: 2),
                        child: Icon(Icons.check_circle_outline_rounded, size: 18, color: t.tealInk),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Text(context.tr(key),
                            style: TextStyle(fontSize: 14, height: 1.4, color: t.ink)),
                      ),
                    ]),
                  ),
                const SizedBox(height: 4),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                  decoration: BoxDecoration(
                    color: t.amberTint,
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Row(children: [
                    Icon(Icons.emergency_outlined, size: 18, color: t.amberInk),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Text(context.tr('safety.emergency'),
                          style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: t.amberInk)),
                    ),
                  ]),
                ),
                const SizedBox(height: 16),
                if (widget.requireAck) ...[
                  AckCheckbox(
                    value: _ticked,
                    label: context.tr('safety.ack'),
                    onChanged: (v) => setState(() => _ticked = v),
                  ),
                  const SizedBox(height: 12),
                ],
                SizedBox(
                  width: double.infinity,
                  child: FilledButton(
                    onPressed: widget.requireAck && !_ticked
                        ? null
                        : () => Navigator.pop(context, widget.requireAck ? true : false),
                    style: FilledButton.styleFrom(
                      backgroundColor: t.tealInk,
                      padding: const EdgeInsets.symmetric(vertical: 15),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                    ),
                    child: Text(context.tr(widget.requireAck ? 'safety.agree' : 'common.dismiss')),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// A tappable checkbox row. The whole row toggles, because a 20 px box is a
/// poor target for a thumb.
class AckCheckbox extends StatelessWidget {
  const AckCheckbox({super.key, required this.value, required this.label, required this.onChanged});

  final bool value;
  final String label;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return InkWell(
      borderRadius: BorderRadius.circular(12),
      onTap: () => onChanged(!value),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 10),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: value ? t.tealInk : t.border),
          color: value ? t.tealTint : Colors.transparent,
        ),
        child: Row(children: [
          Icon(value ? Icons.check_box_rounded : Icons.check_box_outline_blank_rounded,
              size: 22, color: value ? t.tealInk : t.ink2),
          const SizedBox(width: 10),
          Expanded(
            child: Text(label,
                style: TextStyle(fontSize: 13.5, fontWeight: FontWeight.w600, height: 1.35, color: t.ink)),
          ),
        ]),
      ),
    );
  }
}

/// The "this is a shared ride" card. Riders see it wherever they commit to a
/// journey; with [value] and [onChanged] it also carries the agreement box.
class SharedRideNotice extends StatelessWidget {
  const SharedRideNotice({
    super.key,
    required this.body,
    this.value,
    this.onChanged,
  });

  final String body;
  final bool? value;
  final ValueChanged<bool>? onChanged;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: t.surface2,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: t.border),
      ),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
          _IconTile(icon: Icons.groups_2_outlined, tint: t.tealTint, color: t.tealInk),
          const SizedBox(width: 12),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(context.tr('shared.title'),
                  style: TextStyle(fontWeight: FontWeight.w800, fontSize: 14.5, color: t.ink)),
              const SizedBox(height: 3),
              Text(body, style: TextStyle(fontSize: 12.5, height: 1.45, color: t.ink2)),
            ]),
          ),
        ]),
        if (value != null && onChanged != null) ...[
          const SizedBox(height: 12),
          AckCheckbox(value: value!, label: context.tr('shared.ack'), onChanged: onChanged!),
        ],
      ]),
    );
  }
}

/// One line of safety advice with a way into the full notes.
class SafetyReminder extends StatelessWidget {
  const SafetyReminder({super.key, required this.audience});

  final SafetyAudience audience;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Material(
      color: Colors.transparent,
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () => showSafetyNotes(context, audience),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 8),
          child: Row(children: [
            Icon(Icons.shield_outlined, size: 16, color: t.tealInk),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                  context.tr(audience == SafetyAudience.rider
                      ? 'safety.reminderRider'
                      : 'safety.reminderDriver'),
                  style: TextStyle(fontSize: 12, height: 1.35, color: t.ink2)),
            ),
            const SizedBox(width: 8),
            Text(context.tr('safety.viewTips'),
                style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: t.tealInk)),
          ]),
        ),
      ),
    );
  }
}

/// Asks a rider to agree to sharing before they join somebody's request.
Future<bool> confirmSharedJoin(BuildContext context) async {
  final agreed = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (_) => const _SharedJoinSheet(),
  );
  return agreed ?? false;
}

class _SharedJoinSheet extends StatefulWidget {
  const _SharedJoinSheet();

  @override
  State<_SharedJoinSheet> createState() => _SharedJoinSheetState();
}

class _SharedJoinSheetState extends State<_SharedJoinSheet> {
  bool _ticked = false;

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
            const Center(child: _Grabber()),
            const SizedBox(height: 16),
            SharedRideNotice(
              body: context.tr('shared.riderJoin'),
              value: _ticked,
              onChanged: (v) => setState(() => _ticked = v),
            ),
            const SizedBox(height: 6),
            const SafetyReminder(audience: SafetyAudience.rider),
            const SizedBox(height: 10),
            FilledButton(
              onPressed: _ticked ? () => Navigator.pop(context, true) : null,
              style: FilledButton.styleFrom(
                backgroundColor: t.tealInk,
                padding: const EdgeInsets.symmetric(vertical: 15),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
              ),
              child: Text(context.tr('shared.join')),
            ),
          ]),
        ),
      ),
    );
  }
}

class _Grabber extends StatelessWidget {
  const _Grabber();

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Center(
      child: Container(
        width: 38,
        height: 4,
        decoration: BoxDecoration(color: t.border, borderRadius: BorderRadius.circular(2)),
      ),
    );
  }
}

class _IconTile extends StatelessWidget {
  const _IconTile({required this.icon, required this.tint, required this.color});

  final IconData icon;
  final Color tint;
  final Color color;

  @override
  Widget build(BuildContext context) => Container(
        width: 38,
        height: 38,
        alignment: Alignment.center,
        decoration: BoxDecoration(color: tint, borderRadius: BorderRadius.circular(12)),
        child: Icon(icon, size: 20, color: color),
      );
}
