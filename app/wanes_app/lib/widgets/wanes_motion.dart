import 'package:flutter/material.dart';

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

  /// `dashflow` — the dashed proposed route creeping along.
  static const dashFlow = Duration(milliseconds: 1200);

  /// `wanering` — a full rotation, used for the countdown ring's sweep.
  static const ring = Duration(milliseconds: 8000);

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
