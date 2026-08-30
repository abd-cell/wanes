import 'package:flutter/material.dart';
import '../core/theme.dart';

/// Reusable radar / sonar animation used in place of a real map.
///
/// A centered "you" dot with a breathing glow, concentric rings pulsing
/// outward, and optional peer dots (nearby drivers). Self-contained animation.
class RadarView extends StatefulWidget {
  const RadarView({
    super.key,
    this.centerColor = WanesColors.ping,
    this.ringColor = WanesColors.ping,
    this.dotColor = WanesColors.route,
    this.showDots = true,
    this.dots = _defaultDots,
  });

  final Color centerColor;
  final Color ringColor;
  final Color dotColor;
  final bool showDots;
  final List<Offset> dots; // normalized -1..1 around center

  static const _defaultDots = <Offset>[
    Offset(-0.5, -0.6), Offset(0.55, -0.35),
    Offset(-0.45, 0.5), Offset(0.4, 0.55),
  ];

  @override
  State<RadarView> createState() => _RadarViewState();
}

class _RadarViewState extends State<RadarView> with SingleTickerProviderStateMixin {
  late final AnimationController _c;

  @override
  void initState() {
    super.initState();
    _c = AnimationController(vsync: this, duration: const Duration(milliseconds: 2600))..repeat();
  }

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _c,
      builder: (_, __) => CustomPaint(
        size: Size.infinite,
        painter: _RadarPainter(
          t: _c.value,
          centerColor: widget.centerColor,
          ringColor: widget.ringColor,
          dotColor: widget.dotColor,
          dots: widget.showDots ? widget.dots : const [],
        ),
      ),
    );
  }
}

class _RadarPainter extends CustomPainter {
  _RadarPainter({
    required this.t,
    required this.centerColor,
    required this.ringColor,
    required this.dotColor,
    required this.dots,
  });

  final double t;
  final Color centerColor;
  final Color ringColor;
  final Color dotColor;
  final List<Offset> dots;

  @override
  void paint(Canvas canvas, Size size) {
    final center = Offset(size.width / 2, size.height / 2);
    final maxR = size.shortestSide * 0.5;

    for (var i = 0; i < 3; i++) {
      final phase = (t + i / 3) % 1.0;
      final r = maxR * (0.2 + phase * 0.9);
      final opacity = (1 - phase) * 0.5;
      canvas.drawCircle(center, r, Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2
        ..color = ringColor.withValues(alpha: opacity));
    }

    for (final d in dots) {
      final p = center + Offset(d.dx * maxR, d.dy * maxR);
      canvas.drawCircle(p, 6, Paint()..color = dotColor.withValues(alpha: .9));
      canvas.drawCircle(p, 10, Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1
        ..color = dotColor.withValues(alpha: .4));
    }

    final glow = 5 + 6 * (0.5 + 0.5 * (t * 2 % 1 - 0.5).abs());
    canvas.drawCircle(center, 9 + glow, Paint()..color = centerColor.withValues(alpha: .15));
    canvas.drawCircle(center, 9, Paint()..color = centerColor);
  }

  @override
  bool shouldRepaint(_RadarPainter old) => old.t != t;
}
