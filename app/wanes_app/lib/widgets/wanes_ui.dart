import 'dart:math' as math;

import 'package:flutter/material.dart';
import '../core/l10n.dart';
import '../core/push_service.dart';
import '../core/theme.dart';
import 'wanes_motion.dart';

/// ─────────────────────────────────────────────────────────────────────────
/// Wanes shared UI kit — the reusable building blocks that give every screen
/// the prototype's exact look (surfaces, mono micro-labels, route dots, the
/// teal CTA, stat tiles, list rows and the bottom nav). All colours resolve
/// through [WanesTokens] so light/dark stay 1:1 with the design.
/// ─────────────────────────────────────────────────────────────────────────

/// Uppercase JetBrains-Mono micro-label (the tiny FROM / SEATS / TODAY tags).
class MonoLabel extends StatelessWidget {
  const MonoLabel(this.text, {super.key, this.color, this.size = 10, this.spacing = 0.9});
  final String text;
  final Color? color;
  final double size;
  final double spacing;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Text(
      text.toUpperCase(),
      style: WanesTheme.mono(size: size, weight: FontWeight.w600, color: color ?? t.ink2, spacing: spacing),
    );
  }
}

/// The prototype surface card: 18px radius, hairline border, soft lifted shadow.
class WanesCard extends StatefulWidget {
  const WanesCard({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(16),
    this.radius = 18,
    this.color,
    this.border = true,
    this.shadow = true,
    this.onTap,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final double radius;
  final Color? color;
  final bool border;
  final bool shadow;
  final VoidCallback? onTap;

  @override
  State<WanesCard> createState() => _WanesCardState();
}

class _WanesCardState extends State<WanesCard> {
  /// `transition:transform .16s ease,box-shadow .16s ease` with
  /// `style-hover="transform:translateY(-3px)"` on the design's cards. Touch
  /// has no hover, so a tappable card lifts while it is held.
  bool _down = false;

  void _set(bool v) {
    if (_down != v && mounted) setState(() => _down = v);
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final radius = widget.radius;
    final tappable = widget.onTap != null;
    final held = _down && tappable;
    final content = AnimatedContainer(
      duration: WanesMotion.press,
      curve: Curves.ease,
      padding: widget.padding,
      decoration: BoxDecoration(
        color: widget.color ?? t.surface,
        borderRadius: BorderRadius.circular(radius),
        border: widget.border ? Border.all(color: t.border) : null,
        boxShadow: widget.shadow
            ? [
                BoxShadow(
                    color: t.shadow,
                    blurRadius: held ? 32 : 24,
                    offset: Offset(0, held ? 14 : 8),
                    spreadRadius: -12)
              ]
            : null,
      ),
      child: widget.child,
    );
    if (!tappable) return content;
    return AnimatedSlide(
      offset: Offset(0, held ? -0.02 : 0), // ≈3px on a typical card
      duration: WanesMotion.press,
      curve: Curves.ease,
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          borderRadius: BorderRadius.circular(radius),
          onTap: widget.onTap,
          onTapDown: (_) => _set(true),
          onTapUp: (_) => _set(false),
          onTapCancel: () => _set(false),
          child: content,
        ),
      ),
    );
  }
}

/// Section title row with an optional trailing "See all" link.
class SectionHeader extends StatelessWidget {
  const SectionHeader(this.title, {super.key, this.actionLabel, this.onAction});
  final String title;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(title, style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15, color: t.ink)),
        if (actionLabel != null)
          GestureDetector(
            onTap: onAction,
            child: Text(actionLabel!, style: WanesTheme.mono(size: 12, color: t.tealInk, weight: FontWeight.w600)),
          ),
      ],
    );
  }
}

/// The teal call-to-action — full-width, glowing, with an optional trailing
/// arrow (Find rides / Book seat / Confirm booking …).
class PrimaryButton extends StatefulWidget {
  const PrimaryButton({
    super.key,
    required this.label,
    this.onPressed,
    this.busy = false,
    this.arrow = true,
    this.icon,
    this.color,
    this.foreground,
  });

  final String label;
  final VoidCallback? onPressed;
  final bool busy;
  final bool arrow;
  final IconData? icon;
  final Color? color;
  final Color? foreground;

  @override
  State<PrimaryButton> createState() => _PrimaryButtonState();
}

class _PrimaryButtonState extends State<PrimaryButton> {
  /// `transition:transform .16s ease` + `style-hover="transform:translateY(-2px)"`.
  /// Touch has no hover, so the lift plays while the button is held.
  bool _down = false;

  void _set(bool v) {
    if (_down != v && mounted) setState(() => _down = v);
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final label = widget.label;
    final icon = widget.icon;
    final arrow = widget.arrow;
    final busy = widget.busy;
    final bg = widget.color ?? t.teal;
    final fg = widget.foreground ?? t.onTeal;
    final enabled = widget.onPressed != null && !busy;
    final held = _down && enabled;
    return Opacity(
      opacity: enabled ? 1 : 0.55,
      child: AnimatedSlide(
        offset: Offset(0, held ? -0.038 : 0), // ≈2px on the 52px button
        duration: WanesMotion.press,
        curve: Curves.ease,
        child: AnimatedContainer(
        duration: WanesMotion.press,
        curve: Curves.ease,
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(14),
          boxShadow: enabled
              ? [
                  BoxShadow(
                      color: bg.withValues(alpha: held ? .65 : .55),
                      blurRadius: held ? 32 : 26,
                      offset: Offset(0, held ? 16 : 12),
                      spreadRadius: -10)
                ]
              : null,
        ),
        child: Material(
          color: bg,
          borderRadius: BorderRadius.circular(14),
          child: InkWell(
            borderRadius: BorderRadius.circular(14),
            onTap: enabled ? widget.onPressed : null,
            onTapDown: enabled ? (_) => _set(true) : null,
            onTapUp: enabled ? (_) => _set(false) : null,
            onTapCancel: enabled ? () => _set(false) : null,
            child: Container(
              height: 52,
              alignment: Alignment.center,
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: busy
                  ? WanesSpinner.mono(fg, size: 22)
                  : Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        if (icon != null) ...[Icon(icon, size: 19, color: fg), const SizedBox(width: 8)],
                        Text(label, style: TextStyle(color: fg, fontWeight: FontWeight.w800, fontSize: 16)),
                        if (arrow) ...[const SizedBox(width: 8), Icon(Icons.arrow_forward, size: 18, color: fg)],
                      ],
                    ),
            ),
          ),
        ),
        ),
      ),
    );
  }
}

/// Origin/destination marker: teal ringed dot (from) or amber rotated square (to).
class RouteDot extends StatelessWidget {
  const RouteDot({super.key, this.destination = false});
  final bool destination;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    if (destination) {
      return Transform.rotate(
        angle: 0.785398, // 45°
        child: Container(width: 12, height: 12,
            decoration: BoxDecoration(color: t.amber, borderRadius: BorderRadius.circular(3))),
      );
    }
    return Container(
      width: 12, height: 12,
      decoration: BoxDecoration(
        color: t.teal,
        shape: BoxShape.circle,
        boxShadow: [BoxShadow(color: t.tealTint, spreadRadius: 3)],
      ),
    );
  }
}

/// Initials avatar — mono initials on a tinted rounded square/circle.
class AvatarBadge extends StatelessWidget {
  const AvatarBadge(this.label, {super.key, this.size = 42, this.radius, this.tint, this.fg});
  final String label;
  final double size;
  final double? radius;
  final Color? tint;
  final Color? fg;

  /// "Layla Haddad" → "LH". First + last initial, or the single initial of a
  /// one-word name; "?" when there is no name at all.
  static String initialsOf(String name) {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
    return (parts.first[0] + parts.last[0]).toUpperCase();
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Container(
      width: size, height: size,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: tint ?? t.surface2,
        borderRadius: BorderRadius.circular(radius ?? size / 2),
        border: tint == null ? Border.all(color: t.border) : null,
      ),
      child: Text(
        label.isEmpty ? '?' : label,
        style: WanesTheme.mono(size: size * 0.34, weight: FontWeight.w700, color: fg ?? t.tealInk),
      ),
    );
  }
}

/// Small tinted status pill with a dot — LIVE · SEARCHING, VERIFIED, etc.
class StatusPill extends StatelessWidget {
  const StatusPill({super.key, required this.label, required this.color, this.tint, this.dot = true});
  final String label;
  final Color color;
  final Color? tint;
  final bool dot;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 11, vertical: 6),
      decoration: BoxDecoration(
        color: tint ?? color.withValues(alpha: .13),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(mainAxisSize: MainAxisSize.min, children: [
        if (dot) ...[
          Container(width: 7, height: 7, decoration: BoxDecoration(color: color, shape: BoxShape.circle)),
          const SizedBox(width: 7),
        ],
        Text(label, style: WanesTheme.mono(size: 10.5, weight: FontWeight.w700, color: color, spacing: 1.4)),
      ]),
    );
  }
}

/// A big stat: value over a mono label (earnings / trips / rating tiles).
class StatCell extends StatelessWidget {
  const StatCell({super.key, required this.value, required this.label, this.valueColor});
  final String value;
  final String label;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(value, style: TextStyle(fontWeight: FontWeight.w800, fontSize: 22, color: valueColor ?? t.ink, letterSpacing: -0.5)),
        const SizedBox(height: 2),
        MonoLabel(label, spacing: 0.6),
      ],
    );
  }
}

/// Icon-box list row: rounded tinted box, title + subtitle, trailing chevron.
class WanesListRow extends StatelessWidget {
  const WanesListRow({
    super.key,
    required this.icon,
    required this.title,
    this.subtitle,
    this.trailing,
    this.iconColor,
    this.iconTint,
    this.onTap,
    this.divider = true,
  });

  final IconData icon;
  final String title;
  final String? subtitle;
  final Widget? trailing;
  final Color? iconColor;
  final Color? iconTint;
  final VoidCallback? onTap;
  final bool divider;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final row = Container(
      padding: const EdgeInsets.symmetric(vertical: 12),
      decoration: divider ? BoxDecoration(border: Border(bottom: BorderSide(color: t.border))) : null,
      child: Row(children: [
        Container(
          width: 38, height: 38,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: iconTint ?? t.surface2,
            borderRadius: BorderRadius.circular(10),
            border: iconTint == null ? Border.all(color: t.border) : null,
          ),
          child: Icon(icon, size: 18, color: iconColor ?? t.ink2),
        ),
        const SizedBox(width: 13),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(title, style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
            if (subtitle != null) ...[
              const SizedBox(height: 1),
              Text(subtitle!, style: TextStyle(fontSize: 12, color: t.ink2), maxLines: 1, overflow: TextOverflow.ellipsis),
            ],
          ]),
        ),
        trailing ?? Icon(Icons.chevron_right, size: 18, color: t.ink2),
      ]),
    );
    if (onTap == null) return row;
    return InkWell(onTap: onTap, child: row);
  }
}

/// Static star rating display (filled amber stars).
class RatingStars extends StatelessWidget {
  const RatingStars(this.rating, {super.key, this.size = 14});
  final double rating;
  final double size;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Row(mainAxisSize: MainAxisSize.min, children: [
      Icon(Icons.star_rounded, size: size + 2, color: t.amber),
      const SizedBox(width: 3),
      Text(rating.toStringAsFixed(1), style: WanesTheme.mono(size: size - 1, weight: FontWeight.w700, color: t.ink2)),
    ]);
  }
}

/// The bottom navigation — pill highlight on the active item, matching the
/// prototype's floating tab bar. Three items either way: the driver shell's
/// [defaultItems] and the rider's [riderItems].
class WanesBottomNav extends StatelessWidget {
  const WanesBottomNav({super.key, required this.index, required this.onSelect, this.items});

  final int index;
  final ValueChanged<int> onSelect;

  /// Defaults to [defaultItems], translated for the current language.
  final List<WanesNavItem>? items;

  /// Home · Trips · Profile — the driver shell's three tabs.
  static List<WanesNavItem> defaultItems(BuildContext context) => [
        WanesNavItem(Icons.home_outlined, context.tr('nav.home')),
        WanesNavItem(Icons.subject_rounded, context.tr('nav.trips')),
        WanesNavItem(Icons.person_outline_rounded, context.tr('nav.profile')),
      ];

  /// Home · Bookings · Profile — the rider shell. The rider's bookings carry
  /// the journey with them, so there is no separate trips tab.
  static List<WanesNavItem> riderItems(BuildContext context) => [
        WanesNavItem(Icons.home_outlined, context.tr('nav.home')),
        WanesNavItem(Icons.confirmation_number_outlined, context.tr('nav.bookings')),
        WanesNavItem(Icons.person_outline_rounded, context.tr('nav.profile')),
      ];

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final items = this.items ?? defaultItems(context);
    return Container(
      decoration: BoxDecoration(
        color: t.surface,
        border: Border(top: BorderSide(color: t.border)),
      ),
      child: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceAround,
            children: List.generate(items.length, (i) {
              final active = i == index;
              return Expanded(
                child: InkWell(
                  borderRadius: BorderRadius.circular(12),
                  onTap: () => onSelect(i),
                  child: Padding(
                    padding: const EdgeInsets.symmetric(vertical: 6),
                    child: Column(mainAxisSize: MainAxisSize.min, children: [
                      Icon(items[i].icon, size: 22, color: active ? t.tealInk : t.ink2),
                      const SizedBox(height: 3),
                      Text(items[i].label,
                          style: TextStyle(
                              fontSize: 10,
                              fontWeight: active ? FontWeight.w700 : FontWeight.w600,
                              color: active ? t.tealInk : t.ink2)),
                    ]),
                  ),
                ),
              );
            }),
          ),
        ),
      ),
    );
  }
}

class WanesNavItem {
  const WanesNavItem(this.icon, this.label);
  final IconData icon;
  final String label;
}

/// A framed, tinted breakdown row (Fare / Service fee / Total) used on the
/// confirm-booking and rate screens.
class AmountRow extends StatelessWidget {
  const AmountRow(this.label, this.amount, {super.key, this.emphasize = false});
  final String label;
  final String amount;
  final bool emphasize;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final style = emphasize
        ? TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: t.ink)
        : TextStyle(fontSize: 14, color: t.ink2);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 5),
      child: Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
        Text(label, style: style),
        Text(amount, style: emphasize
            ? TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: t.ink)
            : TextStyle(fontSize: 14, fontWeight: FontWeight.w700, color: t.ink)),
      ]),
    );
  }
}

/// A stepper (– value +) matching the SEATS control.
class SeatStepper extends StatelessWidget {
  const SeatStepper({super.key, required this.value, required this.onChanged, this.min = 1, this.max = 6});
  final int value;
  final ValueChanged<int> onChanged;
  final int min;
  final int max;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    Widget btn(String glyph, bool enabled, VoidCallback onTap, {bool accent = false}) => GestureDetector(
          onTap: enabled ? onTap : null,
          child: Container(
            width: 26, height: 26,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: accent ? t.tealTint : t.surface,
              borderRadius: BorderRadius.circular(7),
              border: accent ? null : Border.all(color: t.border),
            ),
            child: Text(glyph,
                style: TextStyle(
                    fontWeight: FontWeight.w800, fontSize: 16,
                    color: enabled ? (accent ? t.tealInk : t.ink2) : t.ink2.withValues(alpha: .4))),
          ),
        );
    return Row(mainAxisSize: MainAxisSize.min, children: [
      btn('–', value > min, () => onChanged(value - 1)),
      Padding(
        padding: const EdgeInsets.symmetric(horizontal: 14),
        child: Text('$value', style: WanesTheme.mono(size: 15, weight: FontWeight.w700, color: t.ink)),
      ),
      btn('+', value < max, () => onChanged(value + 1), accent: true),
    ]);
  }
}

/// ─────────────────────────────────────────────────────────────────────────
/// Prototype-exact primitives used by the driver flow (screens 08 · 09 · 10 · 12)
/// ─────────────────────────────────────────────────────────────────────────

/// The design's `@keyframes wanelive` dot — fades to 40% and shrinks to 70%
/// at the half-way point. Used on "You're online" and the hail badges.
class PulseDot extends StatefulWidget {
  const PulseDot({
    super.key,
    required this.color,
    this.size = 8,
    this.duration = WanesMotion.live,
  });

  final Color color;
  final double size;
  final Duration duration;

  @override
  State<PulseDot> createState() => _PulseDotState();
}

class _PulseDotState extends State<PulseDot> with SingleTickerProviderStateMixin {
  // `wanelive` runs 1 -> .4 -> 1 within its stated duration, so a reversing
  // controller covers only half the keyframe per pass.
  late final AnimationController _c =
      AnimationController(vsync: this, duration: widget.duration ~/ 2)..repeat(reverse: true);

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: widget.size,
      height: widget.size,
      child: AnimatedBuilder(
        animation: _c,
        builder: (_, __) {
          final v = Curves.easeInOut.transform(_c.value);
          return Opacity(
            opacity: 1 - 0.6 * v,
            child: Transform.scale(
              scale: 1 - 0.3 * v,
              child: DecoratedBox(
                decoration: BoxDecoration(color: widget.color, shape: BoxShape.circle),
              ),
            ),
          );
        },
      ),
    );
  }
}

/// The prototype's pill toggle — a plain track with a sliding knob, coloured
/// per call site (dark track + teal knob on the online banner, hairline track
/// + surface knob on the settings rows).
class WanesPillSwitch extends StatelessWidget {
  const WanesPillSwitch({
    super.key,
    required this.value,
    this.onChanged,
    this.width = 52,
    this.height = 30,
    this.onTrack,
    this.onKnob,
    this.offTrack,
    this.offKnob,
    this.busy = false,
  });

  final bool value;
  final ValueChanged<bool>? onChanged;
  final double width;
  final double height;
  final Color? onTrack;
  final Color? onKnob;
  final Color? offTrack;
  final Color? offKnob;
  final bool busy;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final knobSize = height - 6;
    final track = value ? (onTrack ?? t.onTeal) : (offTrack ?? t.border);
    final knob = value ? (onKnob ?? t.teal) : (offKnob ?? t.surface);
    return GestureDetector(
      onTap: onChanged == null || busy ? null : () => onChanged!(!value),
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 180),
        curve: Curves.easeOut,
        width: width,
        height: height,
        decoration: BoxDecoration(color: track, borderRadius: BorderRadius.circular(height / 2)),
        child: Stack(children: [
          AnimatedPositioned(
            duration: const Duration(milliseconds: 180),
            curve: Curves.easeOut,
            top: 3,
            left: value ? width - knobSize - 3 : 3,
            child: Container(
              width: knobSize,
              height: knobSize,
              alignment: Alignment.center,
              decoration: BoxDecoration(color: knob, shape: BoxShape.circle),
              child: busy
                  ? WanesSpinner.mono(track, size: knobSize - 10)
                  : null,
            ),
          ),
        ]),
      ),
    );
  }
}

/// Countdown ring — the amber arc around the time-remaining readout on the
/// incoming-request sheet. [progress] is the fraction still left (1 → 0).
class CountdownRing extends StatefulWidget {
  const CountdownRing({
    super.key,
    required this.progress,
    required this.label,
    this.size = 60,
    this.thickness = 6,
    this.color,
  });

  final double progress;
  final String label;
  final double size;
  final double thickness;
  final Color? color;

  @override
  State<CountdownRing> createState() => _CountdownRingState();
}

class _CountdownRingState extends State<CountdownRing>
    with SingleTickerProviderStateMixin {
  /// `@keyframes wanering` — a slow rotation carrying a brighter head around
  /// the arc, so a request that is still counting down reads as live rather
  /// than as a frozen dial.
  late final AnimationController _spin =
      AnimationController(vsync: this, duration: WanesMotion.ring)..repeat();

  @override
  void dispose() {
    _spin.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final size = widget.size;
    final thickness = widget.thickness;
    final label = widget.label;
    final arc = widget.color ?? t.amber;
    return SizedBox(
      width: size,
      height: size,
      child: CustomPaint(
        painter: _RingPainter(
            widget.progress.clamp(0.0, 1.0), arc, t.border, thickness, _spin),
        child: Center(
          child: Container(
            width: size - thickness * 2,
            height: size - thickness * 2,
            alignment: Alignment.center,
            decoration: BoxDecoration(color: t.surface, shape: BoxShape.circle),
            child: FittedBox(
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 4),
                child: Text(label,
                    style: WanesTheme.mono(size: 17, weight: FontWeight.w800, color: arc, spacing: 0)),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _RingPainter extends CustomPainter {
  _RingPainter(this.progress, this.arc, this.track, this.thickness, this.spin)
      : super(repaint: spin);
  final double progress;
  final Color arc;
  final Color track;
  final double thickness;

  /// 0 → 1 through one full turn (`@keyframes wanering`).
  final Animation<double> spin;

  @override
  void paint(Canvas canvas, Size size) {
    final center = (Offset.zero & size).center;
    final radius = size.shortestSide / 2 - thickness / 2;
    final rect = Rect.fromCircle(center: center, radius: radius);
    final p = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = thickness
      ..color = track;
    canvas.drawCircle(center, radius, p);
    if (progress <= 0) return;
    final sweep = 2 * math.pi * progress;
    canvas.drawArc(rect, -math.pi / 2, sweep, false, p..color = arc);

    // The rotating highlight: a short bright segment travelling the arc. It is
    // trimmed to what is left of the countdown rather than clipped, so it never
    // paints over the spent part of the ring.
    const head = math.pi / 6;
    final start = spin.value * 2 * math.pi; // offset along the remaining arc
    if (start < sweep) {
      canvas.drawArc(
        rect,
        -math.pi / 2 + start,
        math.min(head, sweep - start),
        false,
        Paint()
          ..style = PaintingStyle.stroke
          ..strokeCap = StrokeCap.round
          ..strokeWidth = thickness
          ..color = Colors.white.withValues(alpha: .35),
      );
    }
  }

  @override
  bool shouldRepaint(_RingPainter old) =>
      old.progress != progress || old.arc != arc || old.track != track;
}

/// A 38px circular button on `surface-2` with a hairline border — the back
/// chevron and the theme switch in the prototype.
class CircleIconButton extends StatelessWidget {
  const CircleIconButton({super.key, required this.icon, this.onTap, this.color, this.size = 38});
  final IconData icon;
  final VoidCallback? onTap;
  final Color? color;
  final double size;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Material(
      color: t.surface2,
      shape: CircleBorder(side: BorderSide(color: t.border)),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: SizedBox(
          width: size,
          height: size,
          child: Icon(icon, size: size * 0.53, color: color ?? t.ink),
        ),
      ),
    );
  }
}

/// Bell button with a live unread badge — the home header's notifications
/// entry. The count comes from [PushService.unreadCount], so a push landing
/// while the tab is on screen bumps the badge without a rebuild from above.
class NotificationBellButton extends StatelessWidget {
  const NotificationBellButton({super.key, this.onTap, this.size = 42});
  final VoidCallback? onTap;
  final double size;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return ValueListenableBuilder<int>(
      valueListenable: PushService.instance.unreadCount,
      builder: (context, count, _) => Semantics(
        button: true,
        label: context.tr('notif.title'),
        child: Stack(
          clipBehavior: Clip.none,
          children: [
            CircleIconButton(
              icon: Icons.notifications_none_rounded,
              onTap: onTap,
              size: size,
            ),
            if (count > 0)
              Positioned(
                top: -3,
                right: -3,
                child: Container(
                  constraints: BoxConstraints(minWidth: size * 0.43),
                  height: size * 0.43,
                  alignment: Alignment.center,
                  padding: const EdgeInsets.symmetric(horizontal: 5),
                  decoration: BoxDecoration(
                    color: t.info,
                    borderRadius: BorderRadius.circular(999),
                    border: Border.all(color: t.surface, width: 2),
                  ),
                  child: Text(
                    count > 99 ? '99+' : '$count',
                    style: WanesTheme.mono(
                        size: size * 0.22,
                        weight: FontWeight.w700,
                        color: Colors.white,
                        spacing: 0),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

/// Screen header: circular back button + title (prototype 09).
class ScreenHeader extends StatelessWidget {
  const ScreenHeader({super.key, required this.title, this.onBack, this.trailing});
  final String title;
  final VoidCallback? onBack;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Row(children: [
      CircleIconButton(
        icon: Icons.chevron_left_rounded,
        onTap: onBack ?? () => Navigator.maybePop(context),
      ),
      const SizedBox(width: 14),
      Expanded(
        child: Text(title,
            style: TextStyle(
                fontSize: 19, fontWeight: FontWeight.w800, letterSpacing: -0.4, color: t.ink)),
      ),
      if (trailing != null) trailing!,
    ]);
  }
}

/// A card whose children are stacked with full-bleed hairline dividers between
/// them — the Documents / Vehicles / Ratings group on the driver profile.
class GroupedCard extends StatelessWidget {
  const GroupedCard({super.key, required this.children, this.radius = 16});
  final List<Widget> children;
  final double radius;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final rows = <Widget>[];
    for (var i = 0; i < children.length; i++) {
      rows.add(children[i]);
      if (i != children.length - 1) rows.add(Divider(height: 1, thickness: 1, color: t.border));
    }
    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: BorderRadius.circular(radius),
        border: Border.all(color: t.border),
        boxShadow: [
          BoxShadow(color: t.shadow, blurRadius: 14, offset: const Offset(0, 4), spreadRadius: -10),
        ],
      ),
      child: Column(mainAxisSize: MainAxisSize.min, children: rows),
    );
  }
}

/// A row inside a [GroupedCard]: rounded icon box, title (+ optional subtitle),
/// and either a trailing widget or the default chevron.
class GroupedRow extends StatelessWidget {
  const GroupedRow({
    super.key,
    required this.icon,
    required this.title,
    this.subtitle,
    this.trailing,
    this.iconColor,
    this.onTap,
  });

  final IconData icon;
  final String title;
  final String? subtitle;
  final Widget? trailing;
  final Color? iconColor;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        child: Row(children: [
          Container(
            width: 34,
            height: 34,
            alignment: Alignment.center,
            decoration: BoxDecoration(color: t.surface2, borderRadius: BorderRadius.circular(10)),
            child: Icon(icon, size: 18, color: iconColor ?? t.ink2),
          ),
          const SizedBox(width: 13),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(title, style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
              if (subtitle != null) ...[
                const SizedBox(height: 1),
                Text(subtitle!,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: WanesTheme.mono(
                        size: 11, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
              ],
            ]),
          ),
          const SizedBox(width: 10),
          trailing ?? Icon(Icons.chevron_right_rounded, size: 18, color: t.ink2),
        ]),
      ),
    );
  }
}

/// A bordered tile with a centred mono value over a small caption — the
/// Trips / Rating / Completed trio on the driver profile.
class MetricTile extends StatelessWidget {
  const MetricTile({super.key, required this.value, required this.label, this.valueColor});
  final String value;
  final String label;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 13),
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: t.border),
        boxShadow: [
          BoxShadow(color: t.shadow, blurRadius: 14, offset: const Offset(0, 4), spreadRadius: -10),
        ],
      ),
      child: Column(mainAxisSize: MainAxisSize.min, children: [
        FittedBox(
          child: Text(value,
              style: WanesTheme.mono(
                  size: 19, weight: FontWeight.w800, color: valueColor ?? t.ink, spacing: 0)),
        ),
        const SizedBox(height: 2),
        Text(label, style: TextStyle(fontSize: 11, color: t.ink2)),
      ]),
    );
  }
}

/// The small stacked stat boxes beside the earnings hero (6 trips · 3.2h online).
class MiniStat extends StatelessWidget {
  const MiniStat({super.key, required this.value, required this.label});
  final String value;
  final String label;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 13, vertical: 10),
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: t.border),
      ),
      child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(value, style: WanesTheme.mono(size: 18, weight: FontWeight.w800, color: t.ink, spacing: 0)),
            Text(label, style: TextStyle(fontSize: 11, color: t.ink2)),
          ]),
    );
  }
}

/// Tinted mono chip (VERIFIED, "Suggested 5.000 د.أ", "All valid").
class TintChip extends StatelessWidget {
  const TintChip(
    this.label, {
    super.key,
    this.color,
    this.tint,
    this.size = 11,
    this.radius = 8,
    this.padding,
  });

  final String label;
  final Color? color;
  final Color? tint;
  final double size;
  final double radius;
  final EdgeInsetsGeometry? padding;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Container(
      padding: padding ?? const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: tint ?? t.tealTint,
        borderRadius: BorderRadius.circular(radius),
      ),
      child: Text(label,
          style: WanesTheme.mono(
              size: size, weight: FontWeight.w700, color: color ?? t.tealInk, spacing: 0.6)),
    );
  }
}

/// "★ 4.7 · 1 seat" inline — amber star glyph followed by mono text, exactly
/// how the design writes it inside request and profile rows.
class InlineRating extends StatelessWidget {
  const InlineRating(this.text, {super.key, this.size = 11});
  final String text;
  final double size;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Row(mainAxisSize: MainAxisSize.min, children: [
      Text('★', style: TextStyle(fontSize: size + 1, color: t.amberInk, height: 1.2)),
      const SizedBox(width: 4),
      Flexible(
        child: Text(text,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: WanesTheme.mono(size: size, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
      ),
    ]);
  }
}

/// The amber "N INCOMING REQUEST" / "NEW RIDE REQUEST" strap — a pulsing dot
/// followed by a tracked mono caption.
class LiveCaption extends StatelessWidget {
  const LiveCaption(this.text, {super.key, this.dotColor, this.textColor, this.dotSize = 8});
  final String text;
  final Color? dotColor;
  final Color? textColor;
  final double dotSize;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Row(mainAxisSize: MainAxisSize.min, children: [
      PulseDot(color: dotColor ?? t.amber, size: dotSize),
      const SizedBox(width: 8),
      Flexible(
        child: Text(text.toUpperCase(),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: WanesTheme.mono(
                size: 10.5, weight: FontWeight.w600, color: textColor ?? t.amberInk, spacing: 1.05)),
      ),
    ]);
  }
}

/// ─────────────────────────────────────────────────────────────────────────
/// Rider-flow primitives (screens 03 · 04 · 06 · 07)
/// ─────────────────────────────────────────────────────────────────────────

/// The two-up pill selector on the results sheet ("Carpool · 3" / "Hail a ride"):
/// a `surface-2` track with the active tab lifted onto `surface`.
class SegmentedToggle extends StatelessWidget {
  const SegmentedToggle({
    super.key,
    required this.labels,
    required this.index,
    required this.onSelect,
  });

  final List<String> labels;
  final int index;
  final ValueChanged<int> onSelect;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Container(
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: t.surface2,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: t.border),
      ),
      child: Row(
        children: List.generate(labels.length, (i) {
          final active = i == index;
          return Expanded(
            child: GestureDetector(
              behavior: HitTestBehavior.opaque,
              onTap: () => onSelect(i),
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 160),
                padding: const EdgeInsets.symmetric(vertical: 9),
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: active ? t.surface : Colors.transparent,
                  borderRadius: BorderRadius.circular(9),
                  boxShadow: active
                      ? [BoxShadow(color: t.shadow, blurRadius: 6, offset: const Offset(0, 2), spreadRadius: -3)]
                      : null,
                ),
                child: Text(
                  labels[i],
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: active ? FontWeight.w700 : FontWeight.w600,
                    color: active ? t.ink : t.ink2,
                  ),
                ),
              ),
            ),
          );
        }),
      ),
    );
  }
}

/// A small mono chip on `surface-2` — the departure time / seats-left / detour
/// tags under a result card.
class MetaChip extends StatelessWidget {
  const MetaChip(this.label, {super.key, this.color, this.tint});
  final String label;
  final Color? color;
  final Color? tint;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
      decoration: BoxDecoration(
        color: tint ?? t.surface2,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(label,
          maxLines: 1,
          style: WanesTheme.mono(size: 11, weight: FontWeight.w500, color: color ?? t.ink, spacing: 0)),
    );
  }
}

/// Overlapping initials avatars — "3 drivers notified" on the hail screen.
class AvatarStack extends StatelessWidget {
  const AvatarStack({
    super.key,
    required this.initials,
    this.size = 30,
    this.overlap = 10,
    this.tint,
    this.fg,
    this.ringColor,
  });

  final List<String> initials;
  final double size;
  final double overlap;
  final Color? tint;
  final Color? fg;
  final Color? ringColor;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    if (initials.isEmpty) return const SizedBox.shrink();
    final step = size - overlap;
    return SizedBox(
      width: size + step * (initials.length - 1),
      height: size,
      child: Stack(
        children: [
          for (var i = 0; i < initials.length; i++)
            Positioned(
              left: i * step,
              child: Container(
                width: size,
                height: size,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: t.surface,
                  shape: BoxShape.circle,
                  border: Border.all(color: ringColor ?? tint ?? t.amberTint, width: 2),
                ),
                child: Text(initials[i],
                    style: WanesTheme.mono(
                        size: size * 0.37,
                        weight: FontWeight.w700,
                        color: fg ?? t.amberInk,
                        spacing: 0)),
              ),
            ),
        ],
      ),
    );
  }
}

/// One stage on the active-trip rail — a title with an optional mono detail
/// line under it, exactly as screen 06 draws it.
class TripStage {
  const TripStage(this.title, [this.detail]);

  final String title;

  /// The small monospaced line beneath — a time, an address, an estimate. Null
  /// leaves the stage as a bare title.
  final String? detail;
}

/// The vertical trip-progress rail from screen 06: a node per stage down the
/// left, its title and detail to the right, and a connector that runs teal
/// behind the trip and hairline ahead of it.
///
/// The node the trip is *on* keeps a white core beating inside it
/// (`@keyframes wanelive`) — the one thing on the screen that says the rail is
/// live rather than a receipt. [muted] takes that away: a cancelled trip has
/// no live stage, so nothing on it should pulse.
class TripTimeline extends StatelessWidget {
  const TripTimeline({
    super.key,
    required this.stages,
    required this.current,
    this.muted = false,
  });

  final List<TripStage> stages;

  /// Index of the stage the trip has reached. Everything before it is done.
  final int current;

  /// The trip ended early — draw the rail as history, with no beating node.
  final bool muted;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: List.generate(stages.length, (i) {
        final stage = stages[i];
        final last = i == stages.length - 1;
        final done = i < current;
        final here = i == current && !muted;
        final ahead = i > current;

        return IntrinsicHeight(
          child: Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            SizedBox(
              width: 18,
              child: Column(children: [
                _node(t, done: done, here: here, ahead: ahead, muted: muted),
                if (!last)
                  Expanded(
                    child: Center(
                      child: Container(
                        width: 2,
                        constraints: const BoxConstraints(minHeight: 34),
                        color: done && !muted ? t.teal : t.border,
                      ),
                    ),
                  ),
              ]),
            ),
            const SizedBox(width: 15),
            Expanded(
              child: Padding(
                padding: EdgeInsets.only(bottom: last ? 0 : 14),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(
                    stage.title,
                    style: TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: 14,
                      height: 1.25,
                      color: here ? t.tealInk : (ahead || muted ? t.ink2 : t.ink),
                    ),
                  ),
                  if (stage.detail != null) ...[
                    const SizedBox(height: 2),
                    Text(
                      stage.detail!,
                      style: WanesTheme.mono(size: 11, weight: FontWeight.w500, color: t.ink2),
                    ),
                  ],
                ]),
              ),
            ),
          ]),
        );
      }),
    );
  }

  /// The 18px node. Reached stages are solid teal inside a 4px tint ring; the
  /// stages still to come are hollow with a hairline border.
  Widget _node(WanesTokens t,
      {required bool done, required bool here, required bool ahead, required bool muted}) {
    final reached = (done || here) && !muted;
    return Container(
      width: 18,
      height: 18,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: reached ? t.teal : t.surface,
        shape: BoxShape.circle,
        border: reached ? null : Border.all(color: t.border, width: 2),
        boxShadow: reached ? [BoxShadow(color: t.tealTint, spreadRadius: 4)] : null,
      ),
      child: here ? const PulseDot(color: Colors.white, size: 7) : null,
    );
  }
}

/// A tappable star row — the big amber rating control on screen 07.
class StarPicker extends StatelessWidget {
  const StarPicker({super.key, required this.value, required this.onChanged, this.size = 34});
  final int value;
  final ValueChanged<int> onChanged;
  final double size;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: List.generate(5, (i) {
        final filled = i < value;
        return GestureDetector(
          onTap: () => onChanged(i + 1),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 4),
            child: Icon(
              filled ? Icons.star_rounded : Icons.star_outline_rounded,
              size: size,
              color: filled ? t.amber : t.border,
            ),
          ),
        );
      }),
    );
  }
}

/// A circular icon action (call / message) as drawn on the active-trip sheet.
class RoundAction extends StatelessWidget {
  const RoundAction({
    super.key,
    required this.icon,
    this.onTap,
    this.filled = false,
    this.size = 44,
  });

  final IconData icon;
  final VoidCallback? onTap;

  /// Solid teal (the call button) rather than the outlined `surface-2` one.
  final bool filled;
  final double size;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Material(
      color: filled ? t.teal : t.surface2,
      shape: CircleBorder(
        side: filled ? BorderSide.none : BorderSide(color: t.border),
      ),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: SizedBox(
          width: size,
          height: size,
          child: Icon(icon, size: size * 0.45, color: filled ? t.onTeal : t.tealInk),
        ),
      ),
    );
  }
}

/// The rounded sheet that sits over a map (screens 04 · 06 · 10) — grabber on
/// top, `surface`, 24px top corners and an upward shadow.
class MapSheet extends StatelessWidget {
  const MapSheet({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.fromLTRB(22, 12, 22, 18),
  });

  final Widget child;
  final EdgeInsetsGeometry padding;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: const BorderRadius.vertical(top: Radius.circular(24)),
        boxShadow: [
          BoxShadow(color: t.shadow, blurRadius: 40, offset: const Offset(0, -14), spreadRadius: -14),
        ],
      ),
      child: SafeArea(
        top: false,
        child: Padding(
          padding: padding,
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Container(
              width: 38,
              height: 4,
              decoration: BoxDecoration(color: t.border, borderRadius: BorderRadius.circular(2)),
            ),
            const SizedBox(height: 12),
            child,
          ]),
        ),
      ),
    );
  }
}

/// The pinned bottom action bar (`--bg` with a hairline top border) used by the
/// confirm-booking and rate screens.
class BottomActionBar extends StatelessWidget {
  const BottomActionBar({super.key, required this.child});
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Container(
      padding: const EdgeInsets.fromLTRB(20, 14, 20, 14),
      decoration: BoxDecoration(
        color: t.bg,
        border: Border(top: BorderSide(color: t.border)),
      ),
      child: SafeArea(top: false, child: child),
    );
  }
}
