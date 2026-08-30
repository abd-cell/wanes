import 'dart:async';
import 'package:flutter/material.dart';
import '../core/l10n.dart';
import '../core/sse_client.dart';
import '../core/theme.dart';
import '../widgets/map_backdrop.dart';
import '../widgets/wanes_motion.dart';
import '../widgets/wanes_ui.dart';

/// Hail / no match — prototype screen 04. The map pings out from the rider's
/// pin while nearby drivers are notified; a sheet reports how many were
/// reached and how long the request has left.
class SearchingScreen extends StatefulWidget {
  const SearchingScreen({super.key, this.rideRequestId, this.driversNotified = 0});

  final int? rideRequestId;

  /// How many drivers the search actually reached (from the search response).
  final int driversNotified;

  @override
  State<SearchingScreen> createState() => _SearchingScreenState();
}

class _SearchingScreenState extends State<SearchingScreen> {
  final _sse = SseClient();
  StreamSubscription<Map<String, dynamic>>? _sseSub;
  Timer? _timer;

  /// A hail lives for 10 minutes server-side; this is the time left on it.
  static const _ttl = Duration(minutes: 10);
  late final DateTime _expiresAt = DateTime.now().add(_ttl);
  Duration _left = _ttl;

  bool _accepted = false;

  @override
  void initState() {
    super.initState();
    _timer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (!mounted) return;
      setState(() => _left = _expiresAt.difference(DateTime.now()));
    });
    _listenForAccept();
  }

  Future<void> _listenForAccept() async {
    _sseSub = _sse.events.listen((event) {
      if (!mounted) return;
      if (event['type'] == 'DriverAccepted') {
        setState(() => _accepted = true);
        _timer?.cancel();
      }
    });
    await _sse.connect();
  }

  @override
  void dispose() {
    _timer?.cancel();
    _sseSub?.cancel();
    _sse.dispose();
    super.dispose();
  }

  String get _clock {
    if (_left.isNegative) return '0:00';
    return '${_left.inMinutes}:${(_left.inSeconds % 60).toString().padLeft(2, '0')}';
  }

  bool get _expired => !_accepted && _left.isNegative;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final accent = _accepted ? t.teal : t.amber;

    return Scaffold(
      backgroundColor: t.bg,
      body: Column(children: [
        Expanded(
          child: MapBackdrop(
            children: [
              // The rider's own pin, pinging outward while we search.
              Align(
                alignment: const Alignment(0, -0.12), // left 50% · top 44%
                child: PingRings(
                  color: accent,
                  child: Container(
                    width: 20,
                    height: 20,
                    decoration: BoxDecoration(
                      color: accent,
                      shape: BoxShape.circle,
                      border: Border.all(color: t.bg, width: 3),
                    ),
                  ),
                ),
              ),
              // Nearby drivers, at the design's fixed positions.
              const Align(alignment: Alignment(-0.48, -0.40), child: MapDot(size: 12, ring: 2)),
              const Align(alignment: Alignment(0.48, 0.12), child: MapDot(size: 12, ring: 2)),
              const Align(alignment: Alignment(0.28, -0.56), child: MapDot(size: 12, ring: 2)),
            ],
          ),
        ),
        MapSheet(
          padding: const EdgeInsets.fromLTRB(22, 14, 22, 20),
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Align(
              alignment: AlignmentDirectional.centerStart,
              child: _accepted
                  ? LiveCaption(context.tr('hail.driverFound'),
                      dotColor: t.teal, textColor: t.tealInk, dotSize: 9)
                  : LiveCaption(
                      context.tr(_expired ? 'hail.expiredTag' : 'hail.liveSearching'),
                      dotSize: 9),
            ),
            const SizedBox(height: 10),
            Text(
              context.tr(_accepted
                  ? 'hail.acceptedTitle'
                  : _expired
                      ? 'hail.expiredTitle'
                      : 'hail.searchingTitle'),
              style: TextStyle(
                  fontSize: 21, fontWeight: FontWeight.w800, letterSpacing: -0.42, color: t.ink),
            ),
            const SizedBox(height: 6),
            Text(
              context.tr(_accepted
                  ? 'hail.acceptedBody'
                  : _expired
                      ? 'hail.expiredBody'
                      : 'hail.searchingBody'),
              style: TextStyle(color: t.ink2, fontSize: 14, height: 1.5),
            ),
            if (!_accepted && !_expired) ...[
              const SizedBox(height: 16),
              _notifiedRow(t),
            ],
            const SizedBox(height: 14),
            if (_accepted)
              PrimaryButton(
                  label: context.tr('hail.trackTrip'),
                  arrow: false,
                  onPressed: () => Navigator.pop(context))
            else
              SizedBox(
                width: double.infinity,
                height: 50,
                child: OutlinedButton(
                  onPressed: () => Navigator.pop(context),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: t.ink,
                    side: BorderSide(color: t.border),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                  ),
                  child: Text(context.tr(_expired ? 'common.back' : 'hail.cancelRequest'),
                      style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
                ),
              ),
          ]),
        ),
      ]),
    );
  }

  /// Amber-tint strip: who we reached, and the time left on the request.
  Widget _notifiedRow(WanesTokens t) {
    final n = widget.driversNotified;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      decoration: BoxDecoration(color: t.amberTint, borderRadius: BorderRadius.circular(14)),
      child: Column(children: [
        Row(children: [
        if (n > 0) ...[
          // One badge per driver reached, capped at three so the row stays put.
          AvatarStack(initials: List.filled(n > 3 ? 3 : n, 'D')),
          const SizedBox(width: 11),
        ],
        Expanded(
          child: Text(
            n == 0
                ? context.tr('hail.lookingForDrivers')
                : context.trPlural('hail.driversNotified', n),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(fontWeight: FontWeight.w700, fontSize: 13.5, color: t.ink),
          ),
        ),
        Text(_clock,
            style: WanesTheme.mono(size: 15, weight: FontWeight.w700, color: t.amberInk, spacing: 0)),
        ]),
        const SizedBox(height: 10),
        // `@keyframes sbar` — the strip grows in on first paint, then tracks
        // the hail's remaining TTL down as the clock beside it counts.
        GrowBar(
          value: _left.isNegative
              ? 0
              : _left.inMilliseconds / _ttl.inMilliseconds,
          color: t.amber,
          track: t.amberInk.withValues(alpha: .16),
          height: 5,
        ),
      ]),
    );
  }
}
