import 'package:flutter/material.dart';
import '../core/l10n.dart';
import '../core/session.dart';
import '../core/theme.dart';
import '../widgets/wanes_logo.dart';
import '../widgets/wanes_motion.dart';
import 'complete_profile_screen.dart';
import 'rider_shell.dart';
import 'login_screen.dart';

/// Splash — prototype screen 00. A full-bleed teal field with a highlight in
/// the top-left, the mark on a white tile, the wordmark and three loading
/// dots. Routes on to Home (if a session exists) or Login.
///
/// The whole intro runs off **one** controller rather than a set of delayed
/// timers. On a cold start the event loop is congested enough that independent
/// `Future.delayed` callbacks drift apart by a second or more, which showed up
/// as the wordmark arriving after the route curve had already been cut off by
/// the hand-off. Sharing a timeline keeps the pieces in step with each other
/// and lets the navigation wait on the animation instead of on the clock.
class SplashScreen extends StatefulWidget {
  const SplashScreen({super.key});

  @override
  State<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<SplashScreen>
    with SingleTickerProviderStateMixin {
  /// The prototype's timeline ends at 1.7s (the pin pops at 1.2s over 0.5s).
  static const _timeline = Duration(milliseconds: 1700);

  /// A beat to read the finished mark before the screen hands off.
  static const _hold = Duration(milliseconds: 450);

  late final AnimationController _intro =
      AnimationController(vsync: this, duration: _timeline);

  /// Maps a CSS `delay`/`duration` pair onto the shared timeline.
  Animation<double> _track(int delayMs, int durationMs, Curve curve) {
    const total = 1700.0;
    return CurvedAnimation(
      parent: _intro,
      curve: Interval(delayMs / total, (delayMs + durationMs) / total,
          curve: curve),
    );
  }

  // The five tracks, each keyed to its rule in the prototype.
  late final _tile = _track(0, 600, WanesMotion.popCurve); // spop .6s
  late final _draw = _track(350, 1300, Curves.ease); // sdraw 1.3s .35s
  late final _word = _track(500, 700, Curves.ease); // sfade .7s .5s
  late final _tag = _track(700, 700, Curves.ease); // sfade .7s .7s
  late final _pin = _track(1200, 500, Curves.ease); // spop .5s 1.2s

  @override
  void initState() {
    super.initState();
    _intro.forward().whenComplete(() {
      if (!mounted) return;
      Future<void>.delayed(_hold, _goNext);
    });
  }

  @override
  void dispose() {
    _intro.dispose();
    super.dispose();
  }

  void _goNext() {
    if (!mounted) return;
    // A session that never got past the name step (app killed mid-onboarding)
    // lands back on the gate rather than in the app.
    final profile = Session.instance.profile;
    final Widget next = !Session.instance.isLoggedIn
        ? const LoginScreen()
        : (profile?.isComplete ?? false)
            ? const RiderShell()
            : CompleteProfileScreen(profile: profile);
    Navigator.of(context).pushReplacement(
      PageRouteBuilder(
        transitionDuration: const Duration(milliseconds: 400),
        pageBuilder: (_, __, ___) => next,
        transitionsBuilder: (_, animation, __, child) =>
            FadeTransition(opacity: animation, child: child),
      ),
    );
  }

  /// `@keyframes sfade` — rise 14px while fading in.
  Widget _fade(Animation<double> a, Widget child) => AnimatedBuilder(
        animation: a,
        child: child,
        builder: (_, c) => Opacity(
          opacity: a.value,
          child: Transform.translate(
            offset: Offset(0, WanesMotion.fadeRise * (1 - a.value)),
            child: c,
          ),
        ),
      );

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    const onTeal = Color(0xFF04241F);
    return Scaffold(
      backgroundColor: t.teal,
      body: DecoratedBox(
        decoration: BoxDecoration(
          gradient: RadialGradient(
            center: const Alignment(-0.44, -0.64), // circle at 28% 18%
            radius: 0.9,
            colors: [Colors.white.withValues(alpha: .20), t.teal],
            stops: const [0, 0.55],
          ),
        ),
        child: SafeArea(
          child: Column(children: [
            Expanded(
              child: Center(
                child: Column(mainAxisSize: MainAxisSize.min, children: [
                  // `animation:spop .6s cubic-bezier(.3,1.3,.5,1) both`
                  AnimatedBuilder(
                    animation: _intro,
                    builder: (_, __) => Opacity(
                      opacity: _tile.value == 0 ? 0 : 1,
                      child: Transform.scale(
                        // `_tile` already carries the curve, so the raw track
                        // is fed through spop's 0 → 1.18 → 1 shape linearly.
                        scale: spopScale(_tile.value, Curves.linear),
                        child: Container(
                          width: 98,
                          height: 98,
                          alignment: Alignment.center,
                          decoration: BoxDecoration(
                            color: Colors.white,
                            borderRadius: BorderRadius.circular(28),
                            boxShadow: const [
                              BoxShadow(
                                  color: Color(0x80042420),
                                  blurRadius: 44,
                                  offset: Offset(0, 22),
                                  spreadRadius: -16),
                            ],
                          ),
                          // The route curve draws itself in from .35s and the
                          // amber pin pops on at 1.2s, over the tile's own pop.
                          child: WanesLogo(
                            size: 56,
                            plain: true,
                            draw: _draw.value,
                            pinScale: spopScale(_pin.value, Curves.linear),
                          ),
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: 22),
                  // `animation:sfade .7s ease .5s both`
                  _fade(
                    _word,
                    const Text('wanes',
                        style: TextStyle(
                            fontSize: 40,
                            fontWeight: FontWeight.w800,
                            letterSpacing: -1.2,
                            color: onTeal)),
                  ),
                  const SizedBox(height: 6),
                  // `animation:sfade .7s ease .7s both`
                  _fade(
                    _tag,
                    Text(context.tr('brand.tagline'),
                        style: WanesTheme.mono(
                            size: 12.5,
                            weight: FontWeight.w500,
                            color: onTeal.withValues(alpha: .72),
                            spacing: 0.25)),
                  ),
                ]),
              ),
            ),
            // `animation:sload 1.2s ease-in-out infinite` ×3, .18s apart.
            const Padding(
              padding: EdgeInsets.only(bottom: 52),
              child: SLoadDots(color: onTeal),
            ),
          ]),
        ),
      ),
    );
  }
}
