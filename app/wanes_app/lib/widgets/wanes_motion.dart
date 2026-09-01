import 'dart:math' as math;
import 'dart:ui' show PathMetric, VertexMode, Vertices;

import 'package:flutter/material.dart';

import '../core/theme.dart';

/// Motion primitives transcribed from the prototype's `@keyframes`
/// (`~/Downloads/Wanes prototype.html` -> RiderFlow / DriverFlow).
///
/// The CSS timing keywords map onto Flutter curves exactly:
/// `ease` -> [Curves.ease], `ease-in-out` -> [Curves.easeInOut],
/// `ease-out` -> [Curves.easeOut]. Durations below are the CSS values, so a
/// keyframe written `1.2s` is a *full* cycle — never drive one of these with
/// `repeat(reverse: true)` at the same duration, which would halve the speed.
class WanesMotion {
  const WanesMotion._();

  /// `spop` — scale(0) -> 1.18 -> 1 with a fade. Splash tile / destination pin.
  static const pop = Duration(milliseconds: 600);
  static const popCurve = Cubic(.3, 1.3, .5, 1);

  /// `sfade` — opacity 0 -> 1 while rising 14px. Wordmark, tagline, list rows.
  static const fade = Duration(milliseconds: 700);
  static const fadeRise = 14.0;

  /// `sdraw` — stroke-dashoffset 120 -> 0. The logo's route curve drawing in.
  static const draw = Duration(milliseconds: 1300);

  /// `sload` — three dots pulsing, staggered .18s apart.
  static const load = Duration(milliseconds: 1200);
  static const loadStagger = Duration(milliseconds: 180);

  /// `wanelive` — the live/online dot: opacity 1->.4, scale 1->.7 at 50%.
  static const live = Duration(milliseconds: 1200);

  /// `waneping` — expanding hail rings: scale .35->1.9, opacity .6->0.
  static const ping = Duration(milliseconds: 2400);

  /// `carbob` — the car marker rocking on the active-trip map.
  static const bob = Duration(milliseconds: 2400);

  /// The active-trip car sliding along the route when the trip moves on a
  /// stage. The prototype's map is a still, so this borrows the design's own
  /// slow, eased timing rather than a keyframe.
  static const marker = Duration(milliseconds: 1100);

  /// `dashflow` — the dashed proposed route creeping along.
  static const dashFlow = Duration(milliseconds: 1200);

  /// `wanering` — a full rotation, used for the countdown ring's sweep.
  static const ring = Duration(milliseconds: 8000);

  /// One lap of the splash's orbit loader ([WanesOrbitLoader]).
  static const orbit = Duration(milliseconds: 2600);

  /// `sbar` — scaleX 0 -> 1 for a progress/meter fill.
  static const bar = Duration(milliseconds: 700);

  /// The prototype's press feedback (`transition:transform .16s ease`).
  static const press = Duration(milliseconds: 160);
  static const pressFast = Duration(milliseconds: 140);
}

/// The scale track of `@keyframes spop`: 0 -> (62%) 1.18 -> 1. Callers that
/// already drive an eased timeline pass [Curves.linear] so the easing is not
/// applied twice.
double spopScale(double t, [Curve curve = WanesMotion.popCurve]) {
  final e = curve.transform(t.clamp(0.0, 1.0));
  return e <= 0.62 ? 1.18 * (e / 0.62) : 1.18 - 0.18 * ((e - 0.62) / 0.38);
}

/// `@keyframes sload` — the splash loader. Every dot runs off one controller,
/// offset by the prototype's .18s stagger.
class SLoadDots extends StatefulWidget {
  const SLoadDots({
    super.key,
    required this.color,
    this.count = 3,
    this.size = 9,
    this.gap = 8,
  });

  final Color color;
  final int count;
  final double size;
  final double gap;

  @override
  State<SLoadDots> createState() => _SLoadDotsState();
}

class _SLoadDotsState extends State<SLoadDots>
    with SingleTickerProviderStateMixin {
  late final AnimationController _c =
      AnimationController(vsync: this, duration: WanesMotion.load)..repeat();

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final stagger =
        WanesMotion.loadStagger.inMilliseconds / WanesMotion.load.inMilliseconds;
    return AnimatedBuilder(
      animation: _c,
      builder: (_, __) => Row(
        mainAxisSize: MainAxisSize.min,
        mainAxisAlignment: MainAxisAlignment.center,
        children: List.generate(widget.count, (i) {
          final p = (_c.value - i * stagger) % 1.0;
          // 0%,80%,100% -> scale .5 / opacity .35; 40% -> scale 1 / opacity 1.
          final double lift;
          if (p < 0.4) {
            lift = Curves.easeInOut.transform(p / 0.4);
          } else if (p < 0.8) {
            lift = Curves.easeInOut.transform(1 - (p - 0.4) / 0.4);
          } else {
            lift = 0;
          }
          return Padding(
            padding: EdgeInsets.symmetric(horizontal: widget.gap / 2),
            child: Opacity(
              opacity: 0.35 + 0.65 * lift,
              child: Transform.scale(
                scale: 0.5 + 0.5 * lift,
                child: SizedBox(
                  width: widget.size,
                  height: widget.size,
                  child: DecoratedBox(
                    decoration: BoxDecoration(
                        color: widget.color, shape: BoxShape.circle),
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

/// `animation:wanelive 1s steps(1) infinite` — the caret in the active OTP box.
/// `steps(1)` means no fade: it is either fully on or fully off.
class BlinkingCaret extends StatefulWidget {
  const BlinkingCaret({
    super.key,
    required this.color,
    this.width = 2,
    this.height = 22,
  });

  final Color color;
  final double width;
  final double height;

  @override
  State<BlinkingCaret> createState() => _BlinkingCaretState();
}

class _BlinkingCaretState extends State<BlinkingCaret>
    with SingleTickerProviderStateMixin {
  late final AnimationController _c =
      AnimationController(vsync: this, duration: const Duration(seconds: 1))
        ..repeat();

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _c,
      builder: (_, __) => Opacity(
        opacity: _c.value < 0.5 ? 1 : 0, // steps(1): hard blink, no fade
        child: SizedBox(
          width: widget.width,
          height: widget.height,
          child: DecoratedBox(
            decoration: BoxDecoration(
              color: widget.color,
              borderRadius: BorderRadius.circular(widget.width / 2),
            ),
          ),
        ),
      ),
    );
  }
}

/// A tinted tap target that lifts while held, keeping the ink ripple. The
/// prototype's smaller controls use `transition:transform .14s ease` with a
/// 1px hover lift (the "Book seat" chip on results); [lift] is in pixels.
class LiftInk extends StatefulWidget {
  const LiftInk({
    super.key,
    required this.child,
    required this.color,
    this.onTap,
    this.radius = 10,
    this.lift = 1,
    this.height = 30,
    this.duration = WanesMotion.pressFast,
  });

  final Widget child;
  final Color color;
  final VoidCallback? onTap;
  final double radius;
  final double lift;

  /// Approximate target height — [AnimatedSlide] works in fractions of the
  /// child's size, so the pixel lift is converted through it.
  final double height;
  final Duration duration;

  @override
  State<LiftInk> createState() => _LiftInkState();
}

class _LiftInkState extends State<LiftInk> {
  bool _down = false;

  void _set(bool v) {
    if (_down != v && mounted) setState(() => _down = v);
  }

  @override
  Widget build(BuildContext context) {
    final enabled = widget.onTap != null;
    final held = _down && enabled;
    final radius = BorderRadius.circular(widget.radius);
    return AnimatedSlide(
      offset: Offset(0, held ? -widget.lift / widget.height : 0),
      duration: widget.duration,
      curve: Curves.ease,
      child: Material(
        color: widget.color,
        borderRadius: radius,
        child: InkWell(
          borderRadius: radius,
          onTap: widget.onTap,
          onTapDown: enabled ? (_) => _set(true) : null,
          onTapUp: enabled ? (_) => _set(false) : null,
          onTapCancel: enabled ? () => _set(false) : null,
          child: widget.child,
        ),
      ),
    );
  }
}

/// `@keyframes sbar` — a meter that grows from scaleX(0) to its value.
class GrowBar extends StatelessWidget {
  const GrowBar({
    super.key,
    required this.value,
    required this.color,
    this.track,
    this.height = 6,
    this.radius = 999,
    this.duration = WanesMotion.bar,
  });

  final double value;
  final Color color;
  final Color? track;
  final double height;
  final double radius;
  final Duration duration;

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(radius),
      child: SizedBox(
        height: height,
        child: Stack(children: [
          if (track != null) Positioned.fill(child: ColoredBox(color: track!)),
          // Both layers must be positioned: a bare ColoredBox under the
          // Stack's loose constraints collapses to zero and the fill vanishes.
          Positioned.fill(
            child: TweenAnimationBuilder<double>(
              tween: Tween(begin: 0, end: value.clamp(0.0, 1.0)),
              duration: duration,
              curve: Curves.ease,
              builder: (_, v, __) => FractionallySizedBox(
                alignment: AlignmentDirectional.centerStart,
                widthFactor: v,
                child: ColoredBox(color: color),
              ),
            ),
          ),
        ]),
      ),
    );
  }
}

/// One point of a comet's spine: where it is, and which way is "sideways".
typedef CometSample = ({Offset position, Offset normal});

/// Paints a comet — a trail that tapers and fades out behind its head — as a
/// triangle strip sampled along whatever curve [sample] describes, from the
/// end of the tail (`f == 0`) to the head (`f == 1`).
///
/// The strip is the point of it. Stroking a trail as a chain of short segments
/// is the obvious approach and the wrong one: round caps overlap at every
/// joint, the alpha doubles there, and a smooth trail reads as a string of
/// beads. Stroking it once through a `SweepGradient` trades that for a
/// different fault — a sweep measures its angle with `atan2`, so the ramp only
/// tracks the curve while the two agree, which anything but a circle centred
/// on the shader does not, and it clips hard where the trail crosses the
/// shader's own seam. Per-vertex colour has neither problem: the taper and the
/// fade ride on the geometry, in one draw call.
void paintComet(
  Canvas canvas, {
  required CometSample? Function(double f) sample,
  required Color color,
  required double alpha,
  required double tailHalfWidth,
  required double headHalfWidth,
  int samples = 44,
  double falloff = 2.2,
}) {
  if (alpha <= 0) return;
  final points = <Offset>[];
  final colors = <Color>[];
  for (var i = 0; i <= samples; i++) {
    final f = i / samples; // 0 at the tail's end, 1 at the head
    final at = sample(f);
    if (at == null) continue;
    final halfWidth = tailHalfWidth + (headHalfWidth - tailHalfWidth) * f;
    final fade = color.withValues(
        alpha: (alpha * math.pow(f, falloff).toDouble()).clamp(0.0, 1.0));
    points.add(at.position + at.normal * halfWidth);
    colors.add(fade);
    points.add(at.position - at.normal * halfWidth);
    colors.add(fade);
  }
  if (points.length < 6) return;
  canvas.drawVertices(
    Vertices(VertexMode.triangleStrip, points, colors: colors),
    BlendMode.dst, // take the vertex colours, ignore the paint's
    Paint(),
  );
}

/// A loading ring that runs *around* a square-ish child — the splash's mark on
/// its white tile. Not lifted from a prototype keyframe: it extends the brand
/// mark's own idea (a route travelling from an origin dot to a destination pin)
/// out into the wait, so the screen reads as a journey in progress rather than
/// a spinner parked next to a logo.
///
/// Three layers, all off one ticker:
///   * a **halo** breathing behind the tile, twice per lap;
///   * a faint **track** — a rounded square concentric with the tile, its
///     corner radius offset by [gap] so the two curves stay parallel;
///   * two **comets** running that track half a lap apart, the leading one in
///     the brand amber with a glowing head, the trailing one a dimmer white so
///     the ring never looks lopsided.
///
/// [reveal] (0 → 1) is driven by the caller's own intro timeline so the ring
/// can arrive after the tile has popped, fading and settling out from the mark
/// rather than snapping on.
class WanesOrbitLoader extends StatefulWidget {
  const WanesOrbitLoader({
    super.key,
    required this.child,
    required this.size,
    required this.radius,
    required this.accent,
    this.gap = 16,
    this.track = Colors.white,
    this.reveal = 1.0,
  });

  /// The child's edge length; the ring is laid out `gap` outside it.
  final double size;

  /// The child's corner radius. The track's own radius adds [gap] to it.
  final double radius;

  /// Distance from the child's edge to the track.
  final double gap;

  /// The leading comet and the halo — the brand amber on the splash.
  final Color accent;

  /// The track and the trailing comet.
  final Color track;

  /// 0 → 1 entrance, owned by the caller's timeline.
  final double reveal;

  final Widget child;

  @override
  State<WanesOrbitLoader> createState() => _WanesOrbitLoaderState();
}

class _WanesOrbitLoaderState extends State<WanesOrbitLoader>
    with SingleTickerProviderStateMixin {
  late final AnimationController _c =
      AnimationController(vsync: this, duration: WanesMotion.orbit)..repeat();

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final box = widget.size + widget.gap * 2;
    return SizedBox(
      width: box,
      height: box,
      child: Stack(
        alignment: Alignment.center,
        children: [
          // Painted first so the halo sits behind the tile's own shadow.
          Positioned.fill(
            child: IgnorePointer(
              child: AnimatedBuilder(
                animation: _c,
                builder: (_, __) => Transform.scale(
                  // The ring settles inward as it fades in.
                  scale: 0.88 + 0.12 * widget.reveal,
                  child: CustomPaint(
                    painter: _OrbitPainter(
                      lap: _c.value,
                      reveal: widget.reveal.clamp(0.0, 1.0),
                      radius: widget.radius + widget.gap,
                      accent: widget.accent,
                      track: widget.track,
                    ),
                  ),
                ),
              ),
            ),
          ),
          widget.child,
        ],
      ),
    );
  }
}

class _OrbitPainter extends CustomPainter {
  const _OrbitPainter({
    required this.lap,
    required this.reveal,
    required this.radius,
    required this.accent,
    required this.track,
  });

  /// Position of the leading comet's head along the track, 0 → 1.
  final double lap;
  final double reveal;
  final double radius;
  final Color accent;
  final Color track;

  /// Share of the track the tail covers behind its head.
  static const _tail = 0.26;

  /// Steps the tail is sampled at along the track.
  static const _samples = 44;


  @override
  void paint(Canvas canvas, Size size) {
    if (reveal <= 0) return;
    final rect = (Offset.zero & size).deflate(2.5);
    final rrect = RRect.fromRectAndRadius(rect, Radius.circular(radius));

    // Halo — breathing twice a lap, behind everything.
    final breath = 0.5 - 0.5 * math.cos((lap * 2 % 1.0) * 2 * math.pi);
    canvas.drawRRect(
      rrect.inflate(4 + 7 * breath),
      Paint()
        ..color = accent.withValues(alpha: (0.05 + 0.07 * breath) * reveal)
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 20),
    );

    final path = Path()..addRRect(rrect);
    final metric = path.computeMetrics().first;
    final len = metric.length;

    // Track.
    canvas.drawPath(
      path,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.4
        ..color = track.withValues(alpha: 0.16 * reveal),
    );

    // The trailing comet is drawn first so the amber head wins any overlap.
    _comet(canvas, metric, len,
        head: (lap + 0.5) % 1.0, color: track, alpha: 0.40 * reveal);
    _comet(canvas, metric, len, head: lap, color: accent, alpha: reveal, glow: true);
  }

  /// One comet: a strip walked along the track from the tail to [head], with
  /// a glowing dot at the head itself.
  void _comet(
    Canvas canvas,
    PathMetric metric,
    double len, {
    required double head,
    required Color color,
    required double alpha,
    bool glow = false,
  }) {
    final headAt = head * len;
    final tail = _tail * len;

    paintComet(
      canvas,
      samples: _samples,
      color: color,
      alpha: alpha,
      tailHalfWidth: 0.5,
      headHalfWidth: 1.8,
      sample: (f) {
        final tangent =
            metric.getTangentForOffset(_wrap(headAt - tail * (1 - f), len));
        if (tangent == null) return null;
        // The tangent is unit length, so its perpendicular gives the strip its
        // width without any normalising of our own.
        return (
          position: tangent.position,
          normal: Offset(-tangent.vector.dy, tangent.vector.dx),
        );
      },
    );

    final tangent = metric.getTangentForOffset(_wrap(headAt, len));
    if (tangent == null) return;
    if (glow) {
      canvas.drawCircle(
        tangent.position,
        9,
        Paint()
          ..color = color.withValues(alpha: 0.35 * alpha)
          ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 9),
      );
    }
    canvas.drawCircle(tangent.position, glow ? 3.4 : 2.4,
        Paint()..color = color.withValues(alpha: alpha));
    canvas.drawCircle(tangent.position, glow ? 1.5 : 1.0,
        Paint()..color = Colors.white.withValues(alpha: 0.9 * alpha));
  }

  double _wrap(double v, double len) {
    final m = v % len;
    return m < 0 ? m + len : m;
  }

  @override
  bool shouldRepaint(_OrbitPainter old) =>
      old.lap != lap ||
      old.reveal != reveal ||
      old.radius != radius ||
      old.accent != accent ||
      old.track != track;
}

/// The app's loading spinner — everywhere a screen, a list or a button is
/// waiting on the network. Replaces Material's `CircularProgressIndicator`,
/// which is a stock arc in a stock rhythm and belongs to no brand.
///
/// It is the splash's orbit ([WanesOrbitLoader]) shrunk to a circle: a faint
/// track with a comet running it, an amber head leading a teal trail — the
/// mark's own route-to-a-pin, going round. The trail also *breathes*, stretching
/// through the top of each turn and gathering back up at the bottom, so a long
/// wait keeps moving rather than sitting on one repeating loop.
class WanesSpinner extends StatefulWidget {
  const WanesSpinner({
    super.key,
    this.size = 30,
    this.color,
    this.accent,
    this.showTrack = true,
  });

  /// Edge length of the square the spinner paints into.
  final double size;

  /// The trail. Defaults to the brand teal.
  final Color? color;

  /// The head. Defaults to the brand amber; pass the same value as [color] on
  /// a coloured button, where a second hue would only look like a mistake.
  final Color? accent;

  /// The faint ring the comet runs on. Off for the smallest instances, where
  /// it muddies more than it frames.
  final bool showTrack;

  /// A single-colour spinner, for inside a filled button or beside a label
  /// that already carries a colour of its own — anywhere a second hue would
  /// read as a mistake rather than as the brand.
  factory WanesSpinner.mono(Color color, {double size = 20}) =>
      WanesSpinner(size: size, color: color, accent: color, showTrack: false);

  @override
  State<WanesSpinner> createState() => _WanesSpinnerState();
}

class _WanesSpinnerState extends State<WanesSpinner>
    with SingleTickerProviderStateMixin {
  /// Two turns per cycle, so the breath lands once per turn.
  static const _cycle = Duration(milliseconds: 2200);

  late final AnimationController _c =
      AnimationController(vsync: this, duration: _cycle)..repeat();

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return SizedBox(
      width: widget.size,
      height: widget.size,
      child: RepaintBoundary(
        child: AnimatedBuilder(
          animation: _c,
          builder: (_, __) => CustomPaint(
            painter: _SpinnerPainter(
              phase: _c.value,
              color: widget.color ?? t.teal,
              accent: widget.accent ?? t.amber,
              showTrack: widget.showTrack,
            ),
          ),
        ),
      ),
    );
  }
}

class _SpinnerPainter extends CustomPainter {
  const _SpinnerPainter({
    required this.phase,
    required this.color,
    required this.accent,
    required this.showTrack,
  });

  /// 0 → 1 over two full turns.
  final double phase;
  final Color color;
  final Color accent;
  final bool showTrack;

  @override
  void paint(Canvas canvas, Size size) {
    final center = size.center(Offset.zero);
    // Everything scales off the box so a 16pt spinner in a button and a 40pt
    // one on an empty screen read as the same object.
    final unit = size.shortestSide / 30;
    final stroke = 3.0 * unit;
    final radius = (size.shortestSide - stroke * 2.2) / 2;
    if (radius <= 0) return;

    if (showTrack) {
      canvas.drawCircle(
        center,
        radius,
        Paint()
          ..style = PaintingStyle.stroke
          ..strokeWidth = stroke * 0.4
          ..color = color.withValues(alpha: 0.13),
      );
    }

    // Head angle: two turns a cycle, starting at twelve o'clock.
    final head = phase * 4 * math.pi - math.pi / 2;
    // Breath: the tail is let out over the first half of each turn and reeled
    // back in over the second, which is what stops the loop reading as a loop.
    final breath = 0.5 - 0.5 * math.cos(phase * 4 * math.pi);
    final sweep = (0.16 + 0.52 * breath) * 2 * math.pi;

    Offset onCircle(double angle) =>
        center + Offset(math.cos(angle), math.sin(angle)) * radius;

    paintComet(
      canvas,
      samples: 40,
      color: color,
      alpha: 1,
      tailHalfWidth: stroke * 0.18,
      headHalfWidth: stroke * 0.62,
      falloff: 1.5,
      sample: (f) {
        final angle = head - sweep * (1 - f);
        // On a circle the outward radial *is* the sideways direction.
        return (
          position: onCircle(angle),
          normal: Offset(math.cos(angle), math.sin(angle)),
        );
      },
    );

    final tip = onCircle(head);
    canvas.drawCircle(
      tip,
      stroke * 1.2,
      Paint()
        ..color = accent.withValues(alpha: 0.18)
        ..maskFilter = MaskFilter.blur(BlurStyle.normal, stroke * 0.9),
    );
    canvas.drawCircle(tip, stroke * 0.6, Paint()..color = accent);
  }

  @override
  bool shouldRepaint(_SpinnerPainter old) =>
      old.phase != phase ||
      old.color != color ||
      old.accent != accent ||
      old.showTrack != showTrack;
}
