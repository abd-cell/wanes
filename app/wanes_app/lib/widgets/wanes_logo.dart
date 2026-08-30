import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import '../core/theme.dart';
import 'wanes_motion.dart';

/// The Wanes brand mark (from the design doc): a teal rounded-square badge with
/// a dark-ink route curve from an origin dot to an amber destination pin.
/// Optionally paired with the lowercase "wanes" wordmark.
class WanesLogo extends StatelessWidget {
  const WanesLogo({
    super.key,
    this.size = 46,
    this.showWordmark = false,
    this.horizontal = false,
    this.plain = false,
    this.draw = 1.0,
    this.pinScale = 1.0,
  });

  /// Badge edge length in logical pixels.
  final double size;
  final bool showWordmark;

  /// `@keyframes sdraw` — how much of the route curve is drawn (0 → 1).
  final double draw;

  /// `@keyframes spop` — the destination pin's scale (0 → 1.18 → 1).
  final double pinScale;

  /// Draw the bare mark with no teal badge behind it — how the splash shows it
  /// on the white tile (ink route, teal origin dot, amber destination).
  final bool plain;

  /// When true, lays the wordmark beside the badge (nav/app-bar style);
  /// otherwise stacks it below (splash/login style).
  final bool horizontal;

  @override
  Widget build(BuildContext context) {
    final badge = plain
        ? SizedBox(
            width: size,
            height: size,
            child: CustomPaint(
                painter: _MarkPainter(plain: true, draw: draw, pinScale: pinScale)),
          )
        : Container(
            width: size,
            height: size,
            decoration: BoxDecoration(
              color: WanesColors.route,
              borderRadius: BorderRadius.circular(size * 0.3),
              boxShadow: [
                BoxShadow(
                  color: WanesColors.route.withValues(alpha: .55),
                  blurRadius: size * 0.48,
                  offset: Offset(0, size * 0.22),
                ),
              ],
            ),
            child: CustomPaint(
                painter: _MarkPainter(draw: draw, pinScale: pinScale)),
          );

    if (!showWordmark) return badge;

    final wordmark = Text(
      'wanes',
      style: GoogleFonts.plusJakartaSans(
        fontSize: size * 0.86,
        fontWeight: FontWeight.w800,
        letterSpacing: -size * 0.03,
        color: Theme.of(context).colorScheme.onSurface,
      ),
    );

    return horizontal
        ? Row(mainAxisSize: MainAxisSize.min, children: [
            badge,
            SizedBox(width: size * 0.3),
            wordmark,
          ])
        : Column(mainAxisSize: MainAxisSize.min, children: [
            badge,
            SizedBox(height: size * 0.28),
            wordmark,
          ]);
  }
}

/// Draws the route mark from the design's 48×48 viewBox, scaled to the badge.
class _MarkPainter extends CustomPainter {
  _MarkPainter({this.plain = false, this.draw = 1.0, this.pinScale = 1.0});

  /// On the splash tile the route is drawn in ink and the origin dot in teal;
  /// inside the teal badge both are the deep brand ink.
  final bool plain;

  /// `@keyframes sdraw` — the CSS animates `stroke-dashoffset` from the full
  /// `stroke-dasharray:120` down to 0; here we extract that fraction of the
  /// path instead, which is the same reveal without faking a dash pattern.
  final double draw;

  /// `@keyframes spop` — the destination pin's scale.
  final double pinScale;

  @override
  void paint(Canvas canvas, Size size) {
    final s = size.width / 48.0; // scale from the 48-unit design grid
    Offset p(double x, double y) => Offset(x * s, y * s);
    final stroke = plain ? WanesColors.ink : WanesColors.inkDeep;

    // route: M11 37 Q 20 20 37 13
    final route = Path()
      ..moveTo(11 * s, 37 * s)
      ..quadraticBezierTo(20 * s, 20 * s, 37 * s, 13 * s);
    final reveal = draw.clamp(0.0, 1.0);
    if (reveal > 0) {
      final metric = route.computeMetrics().first;
      canvas.drawPath(
        reveal >= 1
            ? route
            : metric.extractPath(0, metric.length * reveal),
        Paint()
          ..style = PaintingStyle.stroke
          ..color = stroke
          ..strokeWidth = 4.5 * s
          ..strokeCap = StrokeCap.round,
      );
    }

    // origin dot
    canvas.drawCircle(
      p(11, 37),
      (plain ? 5.5 : 5.2) * s,
      Paint()..color = plain ? WanesColors.route : WanesColors.inkDeep,
    );

    // destination pin (amber with dark-ink ring) — `transform-origin:37px 13px`
    final pin = pinScale.clamp(0.0, 2.0);
    if (pin <= 0) return;
    final r = (plain ? 6.5 : 6) * s * pin;
    canvas.drawCircle(p(37, 13), r, Paint()..color = WanesColors.ping);
    canvas.drawCircle(
      p(37, 13),
      r,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 3 * s * pin
        ..color = stroke,
    );
  }

  @override
  bool shouldRepaint(_MarkPainter old) =>
      old.plain != plain || old.draw != draw || old.pinScale != pinScale;
}

/// The mark playing its entrance: the route curve draws itself in
/// (`sdraw 1.3s ease .35s both`) and the destination pin pops on top
/// (`spop .5s ease 1.2s both`) — prototype screens 00 SPLASH and 01 LOGIN.
class AnimatedWanesLogo extends StatefulWidget {
  const AnimatedWanesLogo({
    super.key,
    this.size = 46,
    this.showWordmark = false,
    this.horizontal = false,
    this.plain = false,
    this.drawDelay = const Duration(milliseconds: 350),
    this.pinDelay = const Duration(milliseconds: 1200),
  });

  final double size;
  final bool showWordmark;
  final bool horizontal;
  final bool plain;

  /// Splash uses `.35s`, login `.3s`.
  final Duration drawDelay;

  /// Splash uses `1.2s`, login `1.15s`.
  final Duration pinDelay;

  @override
  State<AnimatedWanesLogo> createState() => _AnimatedWanesLogoState();
}

class _AnimatedWanesLogoState extends State<AnimatedWanesLogo>
    with SingleTickerProviderStateMixin {
  /// One timeline for both tracks. Separate delayed timers drift apart when
  /// the event loop is busy, which can land the pin before the curve reaches
  /// it; a shared controller keeps the `both` fill-mode offsets exact.
  static const _total = 1700.0;

  late final AnimationController _c = AnimationController(
      vsync: this, duration: const Duration(milliseconds: 1700))
    ..forward();

  late final Animation<double> _draw = CurvedAnimation(
    parent: _c,
    curve: Interval(widget.drawDelay.inMilliseconds / _total,
        (widget.drawDelay.inMilliseconds + WanesMotion.draw.inMilliseconds) / _total,
        curve: Curves.ease),
  );

  late final Animation<double> _pin = CurvedAnimation(
    parent: _c,
    curve: Interval(widget.pinDelay.inMilliseconds / _total,
        (widget.pinDelay.inMilliseconds + 500) / _total,
        curve: Curves.ease),
  );

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _c,
      builder: (_, __) => WanesLogo(
        size: widget.size,
        showWordmark: widget.showWordmark,
        horizontal: widget.horizontal,
        plain: widget.plain,
        draw: _draw.value,
        // The interval already carries the easing, so spop's 0 → 1.18 → 1
        // shape is applied to the eased track linearly.
        pinScale: spopScale(_pin.value, Curves.linear),
      ),
    );
  }
}
