import 'dart:math' as math;

import 'package:flutter/material.dart';
import '../core/theme.dart';
import 'wanes_motion.dart';

/// One of the prototype's hand-drawn route curves: an SVG `viewBox` plus the
/// path inside it, rendered like `preserveAspectRatio="xMidYMid slice"`.
class MapRouteSpec {
  const MapRouteSpec({required this.viewBox, required this.build, this.dashed = false});

  final Size viewBox;
  final Path Function() build;

  /// Dashed (`stroke-dasharray:2 12`) for a *proposed* route, solid for one
  /// that is actually happening — the design's distinction.
  final bool dashed;
}

/// The exact curves the prototype ships, one per screen that draws a map.
class MapRoutes {
  const MapRoutes._();

  /// Driver 10 — incoming request, amber and dashed.
  static final hail = MapRouteSpec(
    viewBox: const Size(320, 320),
    dashed: true,
    build: () => Path()
      ..moveTo(70, 250)
      ..cubicTo(150, 230, 130, 120, 250, 90),
  );

  /// Rider 03 — carpool results header strip.
  static final results = MapRouteSpec(
    viewBox: const Size(320, 236),
    build: () => Path()
      ..moveTo(52, 186)
      ..cubicTo(130, 176, 128, 96, 250, 66),
  );

  /// Rider 06 — active trip.
  static final activeTrip = MapRouteSpec(
    viewBox: const Size(320, 400),
    build: () => Path()
      ..moveTo(60, 330)
      ..cubicTo(150, 300, 120, 160, 240, 120),
  );
}

/// The prototype's stylised map: a 28px grid over `--map-bg`, two diagonal
/// road bands, and optionally a route curve. Markers are supplied as
/// [children] so each screen can place its own.
///
/// It is decoration, not cartography — the same drawing the design ships.
class MapBackdrop extends StatefulWidget {
  const MapBackdrop({
    super.key,
    this.route,
    this.routeColor,
    this.children = const [],
    this.gridSize = 28,
    this.startMarker,
    this.endMarker,
  });

  final MapRouteSpec? route;
  final Color? routeColor;
  final List<Widget> children;

  /// The grid pitch. The design uses 28px full-bleed and 10px in the small
  /// thumbnail on the confirm-booking screen.
  final double gridSize;

  /// Markers pinned to the route's own endpoints. The curve is scaled to the
  /// box like SVG `slice`, so a fixed percentage drifts away from it on wider
  /// screens — placing these from the same transform keeps them on the line.
  final Widget? startMarker;
  final Widget? endMarker;

  @override
  State<MapBackdrop> createState() => _MapBackdropState();
}

class _MapBackdropState extends State<MapBackdrop>
    with SingleTickerProviderStateMixin {
  /// `@keyframes dashflow` — a dashed *proposed* route creeps forward. Only
  /// runs when there is one to animate; a solid route stays still.
  late final AnimationController _dash =
      AnimationController(vsync: this, duration: WanesMotion.dashFlow);

  @override
  void initState() {
    super.initState();
    if (widget.route?.dashed ?? false) _dash.repeat();
  }

  @override
  void didUpdateWidget(MapBackdrop old) {
    super.didUpdateWidget(old);
    final flow = widget.route?.dashed ?? false;
    if (flow && !_dash.isAnimating) {
      _dash.repeat();
    } else if (!flow && _dash.isAnimating) {
      _dash.stop();
    }
  }

  @override
  void dispose() {
    _dash.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final spec = widget.route;
    final startMarker = widget.startMarker;
    final endMarker = widget.endMarker;
    final gridSize = widget.gridSize;
    final routeColor = widget.routeColor;
    return Stack(
      fit: StackFit.expand,
      children: [
        RepaintBoundary(
          child: AnimatedBuilder(
            animation: _dash,
            builder: (_, __) => CustomPaint(
              painter: _MapPainter(
                bg: t.mapBg,
                line: t.mapLine,
                road: t.mapRoad,
                route: spec,
                routeColor: routeColor ?? t.teal,
                grid: gridSize,
                dashPhase: _dash.value,
              ),
            ),
          ),
        ),
        if (spec != null && (startMarker != null || endMarker != null))
          Positioned.fill(
            child: LayoutBuilder(
              builder: (_, box) {
                final ends = _routeEnds(spec, box.biggest);
                return Stack(clipBehavior: Clip.none, children: [
                  if (startMarker != null) _at(ends.$1, startMarker),
                  if (endMarker != null) _at(ends.$2, endMarker),
                ]);
              },
            ),
          ),
        ...widget.children,
      ],
    );
  }

  /// Centres [child] on [p] without needing to know its size.
  Widget _at(Offset p, Widget child) => Positioned(
        left: p.dx,
        top: p.dy,
        child: FractionalTranslation(translation: const Offset(-0.5, -0.5), child: child),
      );

  /// First and last point of the route, in box coordinates.
  static (Offset, Offset) _routeEnds(MapRouteSpec spec, Size size) {
    final scale = math.max(size.width / spec.viewBox.width, size.height / spec.viewBox.height);
    final dx = (size.width - spec.viewBox.width * scale) / 2;
    final dy = (size.height - spec.viewBox.height * scale) / 2;
    Offset map(Offset p) => Offset(dx + p.dx * scale, dy + p.dy * scale);

    final metric = spec.build().computeMetrics().first;
    final a = metric.getTangentForOffset(0)!.position;
    final b = metric.getTangentForOffset(metric.length)!.position;
    return (map(a), map(b));
  }
}

/// The teal "you are here" dot — a filled circle ringed in the page colour.
class MapDot extends StatelessWidget {
  const MapDot({super.key, this.size = 16, this.color, this.ring = 3});
  final double size;
  final Color? color;
  final double ring;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: color ?? t.teal,
        shape: BoxShape.circle,
        border: Border.all(color: t.bg, width: ring),
      ),
    );
  }
}

/// The teardrop destination pin (a circle with one square corner, rotated 45°).
class MapPin extends StatelessWidget {
  const MapPin({super.key, this.size = 22, this.color});
  final double size;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Transform.translate(
      offset: Offset(0, -size / 2),
      child: Transform.rotate(
        angle: -math.pi / 4,
        child: Container(
          width: size,
          height: size,
          decoration: BoxDecoration(
            color: color ?? t.amber,
            borderRadius: BorderRadius.only(
              topLeft: Radius.circular(size / 2),
              topRight: Radius.circular(size / 2),
              bottomRight: Radius.circular(size / 2),
            ),
            boxShadow: const [
              BoxShadow(color: Color(0x4D000000), blurRadius: 8, offset: Offset(0, 3)),
            ],
          ),
        ),
      ),
    );
  }
}

/// Expanding rings under a marker — the design's `@keyframes waneping`, three
/// circles staggered a third of a cycle apart. Used while hailing.
class PingRings extends StatefulWidget {
  const PingRings({
    super.key,
    this.size = 60,
    this.color,
    this.duration = WanesMotion.ping,
    this.child,
  });

  final double size;
  final Color? color;
  final Duration duration;
  final Widget? child;

  @override
  State<PingRings> createState() => _PingRingsState();
}

class _PingRingsState extends State<PingRings> with SingleTickerProviderStateMixin {
  late final AnimationController _c =
      AnimationController(vsync: this, duration: widget.duration)..repeat();

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final color = widget.color ?? t.amber;
    return SizedBox(
      width: widget.size,
      height: widget.size,
      child: AnimatedBuilder(
        animation: _c,
        builder: (_, __) => Stack(
          alignment: Alignment.center,
          children: [
            for (var i = 0; i < 3; i++) _ring((_c.value + i / 3) % 1.0, color),
            if (widget.child != null) widget.child!,
          ],
        ),
      ),
    );
  }

  /// scale .35 → 1.9, opacity .6 → 0. The CSS runs `ease-out` across the whole
  /// keyframe, so both tracks are eased off the same curve.
  Widget _ring(double p, Color color) {
    final e = Curves.easeOut.transform(p.clamp(0.0, 1.0));
    return Opacity(
        opacity: (1 - e) * 0.6,
        child: Transform.scale(
          scale: 0.35 + e * 1.55,
          child: Container(
            width: widget.size,
            height: widget.size,
            decoration: BoxDecoration(color: color, shape: BoxShape.circle),
          ),
        ),
      );
  }
}

class _MapPainter extends CustomPainter {
  _MapPainter({
    required this.bg,
    required this.line,
    required this.road,
    required this.route,
    required this.routeColor,
    required this.grid,
    this.dashPhase = 0,
  });

  final Color bg;
  final Color line;
  final Color road;
  final MapRouteSpec? route;
  final Color routeColor;
  final double grid;

  /// 0 → 1 through one dash+gap period (`@keyframes dashflow`).
  final double dashPhase;

  @override
  void paint(Canvas canvas, Size size) {
    canvas.drawRect(Offset.zero & size, Paint()..color = bg);
    _paintGrid(canvas, size);
    // linear-gradient(118deg, … 44% → 48%) and (28deg, … 62% → 65%)
    _paintRoad(canvas, size, 118, 0.44, 0.48);
    _paintRoad(canvas, size, 28, 0.62, 0.65);
    final r = route;
    if (r != null) _paintRoute(canvas, size, r);
  }

  void _paintGrid(Canvas canvas, Size size) {
    final p = Paint()
      ..color = line
      ..strokeWidth = 1;
    for (var y = grid - 0.5; y < size.height; y += grid) {
      canvas.drawLine(Offset(0, y), Offset(size.width, y), p);
    }
    for (var x = grid - 0.5; x < size.width; x += grid) {
      canvas.drawLine(Offset(x, 0), Offset(x, size.height), p);
    }
  }

  /// One CSS `linear-gradient(<deg>, transparent a, road a b, transparent b)`
  /// band, drawn as a thick line perpendicular to the gradient axis.
  void _paintRoad(Canvas canvas, Size size, double degrees, double from, double to) {
    final rad = degrees * math.pi / 180;
    // CSS 0deg points up, angles run clockwise; y grows downward on canvas.
    final dir = Offset(math.sin(rad), -math.cos(rad));
    final axis = (size.width * dir.dx).abs() + (size.height * dir.dy).abs();
    final center = Offset(size.width / 2, size.height / 2);
    final start = center - dir * (axis / 2);
    final mid = start + dir * (axis * (from + to) / 2);
    final perp = Offset(-dir.dy, dir.dx);
    final reach = size.width + size.height;
    canvas.drawLine(
      mid - perp * reach,
      mid + perp * reach,
      Paint()
        ..color = road
        ..strokeWidth = axis * (to - from),
    );
  }

  /// Scale the route like SVG `preserveAspectRatio="xMidYMid slice"` and draw
  /// it in viewBox units so stroke width and dash rhythm scale with it.
  void _paintRoute(Canvas canvas, Size size, MapRouteSpec spec) {
    final scale = math.max(size.width / spec.viewBox.width, size.height / spec.viewBox.height);
    final dx = (size.width - spec.viewBox.width * scale) / 2;
    final dy = (size.height - spec.viewBox.height * scale) / 2;

    final p = Paint()
      ..color = routeColor
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round
      ..strokeWidth = 5;

    canvas.save();
    canvas.translate(dx, dy);
    canvas.scale(scale);
    final path = spec.build();
    if (!spec.dashed) {
      canvas.drawPath(path, p);
    } else {
      // `stroke-dasharray:2 12`. Walking the start back by one full period
      // and sliding it forward by the phase keeps the loop seamless.
      const dash = 2.0, gap = 12.0;
      const period = dash + gap;
      for (final metric in path.computeMetrics()) {
        var d = -period + dashPhase * period;
        while (d < metric.length) {
          final a = math.max(0.0, d);
          final b = math.min(d + dash, metric.length);
          if (b > a) canvas.drawPath(metric.extractPath(a, b), p);
          d += period;
        }
      }
    }
    canvas.restore();
  }

  @override
  bool shouldRepaint(_MapPainter old) =>
      old.bg != bg ||
      old.line != line ||
      old.road != road ||
      old.route != route ||
      old.routeColor != routeColor ||
      old.grid != grid ||
      old.dashPhase != dashPhase;
}
