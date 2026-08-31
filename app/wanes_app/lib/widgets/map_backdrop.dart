import 'dart:math' as math;
import 'dart:ui' show PathMetric;

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

/// How one stretch of a route reads: already travelled, still only proposed,
/// or merely there for context.
enum MapLegStyle { solid, dashed, faint }

/// A marker somewhere along the route, and how the route reads on either side
/// of it — the live-trip car and its two legs.
///
/// [MapBackdrop] glides the marker whenever [at] changes, so a screen only has
/// to hand over the new value when the trip moves on a stage.
class MapProgress {
  const MapProgress({
    required this.at,
    this.behind = MapLegStyle.solid,
    this.ahead = MapLegStyle.dashed,
    this.marker,
  });

  /// 0 -> 1 along the curve, measured from its start.
  final double at;

  /// The stretch from the route's start up to [at] — solid once it has been
  /// covered.
  final MapLegStyle behind;

  /// The stretch from [at] to the route's end — dashed while it is still ahead.
  final MapLegStyle ahead;

  /// Drawn centred on [at]. Null leaves the split route without a marker.
  final Widget? marker;
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
    this.progress,
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

  /// A marker part-way along the route — where the trip has got to. Set it and
  /// the route is drawn as two legs either side of the marker instead of one
  /// uniform curve.
  final MapProgress? progress;

  @override
  State<MapBackdrop> createState() => _MapBackdropState();
}

class _MapBackdropState extends State<MapBackdrop> with TickerProviderStateMixin {
  /// `@keyframes dashflow` — a dashed *proposed* route creeps forward. Only
  /// runs when there is one to animate; a solid route stays still.
  late final AnimationController _dash =
      AnimationController(vsync: this, duration: WanesMotion.dashFlow);

  /// Slides the progress marker from where it was to where it now is, so a
  /// stage change reads as the car moving rather than teleporting.
  late final AnimationController _glide =
      AnimationController(vsync: this, duration: WanesMotion.marker, value: 1);

  late double _from = widget.progress?.at ?? 0;
  late double _to = _from;

  /// Whether anything on the map is dashed, and so needs the flow running.
  bool get _flowing {
    final p = widget.progress;
    if (p != null) {
      return p.behind == MapLegStyle.dashed || p.ahead == MapLegStyle.dashed;
    }
    return widget.route?.dashed ?? false;
  }

  /// The marker's position this frame, part-way through the glide.
  double get _at => _from + (_to - _from) * Curves.easeInOut.transform(_glide.value);

  @override
  void initState() {
    super.initState();
    if (_flowing) _dash.repeat();
  }

  @override
  void didUpdateWidget(MapBackdrop old) {
    super.didUpdateWidget(old);
    if (_flowing && !_dash.isAnimating) {
      _dash.repeat();
    } else if (!_flowing && _dash.isAnimating) {
      _dash.stop();
    }

    final to = widget.progress?.at;
    if (to == null) {
      _from = _to = 0;
    } else if (to != _to) {
      // Glide on from wherever the last one had reached, not from its target —
      // two stage changes in quick succession must not jump the marker back.
      _from = old.progress == null ? to : _at;
      _to = to;
      _glide.forward(from: 0);
    }
  }

  @override
  void dispose() {
    _dash.dispose();
    _glide.dispose();
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
    final progress = widget.progress;
    final marker = progress?.marker;
    return Stack(
      fit: StackFit.expand,
      children: [
        RepaintBoundary(
          child: AnimatedBuilder(
            animation: Listenable.merge([_dash, _glide]),
            builder: (_, __) => CustomPaint(
              painter: _MapPainter(
                bg: t.mapBg,
                line: t.mapLine,
                road: t.mapRoad,
                route: spec,
                routeColor: routeColor ?? t.teal,
                grid: gridSize,
                dashPhase: _dash.value,
                progressAt: progress == null ? null : _at,
                behind: progress?.behind ?? MapLegStyle.solid,
                ahead: progress?.ahead ?? MapLegStyle.dashed,
              ),
            ),
          ),
        ),
        if (spec != null && (startMarker != null || endMarker != null || marker != null))
          Positioned.fill(
            child: LayoutBuilder(
              builder: (_, box) {
                final size = box.biggest;
                return Stack(clipBehavior: Clip.none, children: [
                  if (startMarker != null) _pin(_pointOn(spec, size, 0), startMarker),
                  if (endMarker != null) _pin(_pointOn(spec, size, 1), endMarker),
                  if (marker != null)
                    AnimatedBuilder(
                      animation: _glide,
                      builder: (_, child) => _pin(_pointOn(spec, size, _at), child!),
                      child: marker,
                    ),
                ]);
              },
            ),
          ),
        ...widget.children,
      ],
    );
  }

  /// Centres [child] on [p] without needing to know its size.
  Widget _pin(Offset p, Widget child) => Positioned(
        left: p.dx,
        top: p.dy,
        child: FractionalTranslation(translation: const Offset(-0.5, -0.5), child: child),
      );

  /// The point [fraction] of the way along the route, in box coordinates —
  /// scaled by the same `slice` transform the painter uses, so a marker stays
  /// on the line at any screen width.
  static Offset _pointOn(MapRouteSpec spec, Size size, double fraction) {
    final scale = math.max(size.width / spec.viewBox.width, size.height / spec.viewBox.height);
    final dx = (size.width - spec.viewBox.width * scale) / 2;
    final dy = (size.height - spec.viewBox.height * scale) / 2;

    final metric = spec.build().computeMetrics().first;
    final p = metric.getTangentForOffset(metric.length * fraction.clamp(0.0, 1.0))!.position;
    return Offset(dx + p.dx * scale, dy + p.dy * scale);
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
    this.progressAt,
    this.behind = MapLegStyle.solid,
    this.ahead = MapLegStyle.dashed,
  });

  final Color bg;
  final Color line;
  final Color road;
  final MapRouteSpec? route;
  final Color routeColor;
  final double grid;

  /// 0 → 1 through one dash+gap period (`@keyframes dashflow`).
  final double dashPhase;

  /// Where the route splits into its two legs, or null for one uniform curve.
  final double? progressAt;
  final MapLegStyle behind;
  final MapLegStyle ahead;

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

    canvas.save();
    canvas.translate(dx, dy);
    canvas.scale(scale);
    final at = progressAt;
    for (final metric in spec.build().computeMetrics()) {
      if (at == null) {
        _paintLeg(canvas, metric, 0, metric.length,
            spec.dashed ? MapLegStyle.dashed : MapLegStyle.solid);
      } else {
        final split = metric.length * at.clamp(0.0, 1.0);
        _paintLeg(canvas, metric, 0, split, behind);
        _paintLeg(canvas, metric, split, metric.length, ahead);
      }
    }
    canvas.restore();
  }

  /// One stretch of the route, [from]..[to] along [metric] in viewBox units.
  void _paintLeg(Canvas canvas, PathMetric metric, double from, double to, MapLegStyle style) {
    if (to <= from) return;
    final p = Paint()
      ..color = style == MapLegStyle.faint ? routeColor.withValues(alpha: 0.3) : routeColor
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round
      ..strokeWidth = style == MapLegStyle.faint ? 4 : 5;

    if (style != MapLegStyle.dashed) {
      canvas.drawPath(metric.extractPath(from, to), p);
      return;
    }
    // `stroke-dasharray:2 12`. Walking the start back by one full period
    // and sliding it forward by the phase keeps the loop seamless.
    const dash = 2.0, gap = 12.0;
    const period = dash + gap;
    var d = from - period + dashPhase * period;
    while (d < to) {
      final a = math.max(from, d);
      final b = math.min(d + dash, to);
      if (b > a) canvas.drawPath(metric.extractPath(a, b), p);
      d += period;
    }
  }

  @override
  bool shouldRepaint(_MapPainter old) =>
      old.bg != bg ||
      old.line != line ||
      old.road != road ||
      old.route != route ||
      old.routeColor != routeColor ||
      old.grid != grid ||
      old.dashPhase != dashPhase ||
      old.progressAt != progressAt ||
      old.behind != behind ||
      old.ahead != ahead;
}
