import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';
import '../core/theme.dart';
import 'map_backdrop.dart' show MapProgress, MapLegStyle;

/// A real, pannable map for the rider's live trip: OpenStreetMap tiles under
/// the trip's actual origin and destination, with the driver drawn between
/// them.
///
/// This is the one screen that gets real cartography — everywhere else still
/// uses the prototype's [MapBackdrop] illustration, which is decoration and
/// cannot be panned or zoomed. The two share [MapProgress] deliberately: the
/// stage logic that decides how far along the car is, and how each leg reads,
/// is the same whether it is drawn on a grid or on real streets.
///
/// Tiles come from openstreetmap.org, whose usage policy requires the
/// attribution rendered in the corner. Keep it.
class LiveTripMap extends StatefulWidget {
  const LiveTripMap({
    super.key,
    required this.origin,
    required this.destination,
    required this.progress,
    this.driverAt,
    this.routeColor,
    this.dimmed = false,
    this.startMarker,
    this.endMarker,
    this.overlays = const [],
    this.tileProvider,
  });

  final LatLng origin;
  final LatLng destination;

  /// How far along the trip is and how each leg should read. [MapProgress.at]
  /// positions the car only when [driverAt] is absent.
  final MapProgress progress;

  /// The driver's actual reported position. When present it wins over the
  /// stage-derived guess — a real fix beats an interpolation.
  final LatLng? driverAt;

  final Color? routeColor;

  /// Greys the whole route out, for a cancelled trip.
  final bool dimmed;

  final Widget? startMarker;
  final Widget? endMarker;

  /// Drawn above the map, positioned by the caller (the ETA card, refresh).
  final List<Widget> overlays;

  /// Injectable so a test never reaches the network for tiles.
  final TileProvider? tileProvider;

  @override
  State<LiveTripMap> createState() => _LiveTripMapState();
}

class _LiveTripMapState extends State<LiveTripMap> with SingleTickerProviderStateMixin {
  final _controller = MapController();

  /// Slides the car from where it was drawn to where it now belongs, so a new
  /// fix — or another tick of the estimate — reads as the car driving rather
  /// than jumping. Without this the marker teleports and the movement is lost.
  late final AnimationController _glide = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 900),
  );
  late LatLng _from = _target;
  late LatLng _to = _target;

  @override
  void initState() {
    super.initState();
    _glide.addListener(() {
      if (mounted) setState(() {});
    });
  }

  @override
  void didUpdateWidget(LiveTripMap old) {
    super.didUpdateWidget(old);
    final next = _target;
    if (next != _to) {
      // Start the new slide from wherever the car is on screen right now, not
      // from the last target — interrupting mid-glide must not snap it back.
      _from = _drawnAt;
      _to = next;
      _glide.forward(from: 0);
    }
  }

  @override
  void dispose() {
    _glide.dispose();
    super.dispose();
  }

  /// Where the car is painted this frame — [_from] easing toward [_to].
  LatLng get _drawnAt {
    if (!_glide.isAnimating && _glide.value == 0) return _from;
    final f = Curves.easeInOut.transform(_glide.value);
    return LatLng(
      _from.latitude + (_to.latitude - _from.latitude) * f,
      _from.longitude + (_to.longitude - _from.longitude) * f,
    );
  }

  /// Where the car belongs: the real fix if we have one, otherwise a point
  /// interpolated along the straight route by how far the stage says we are.
  LatLng get _target {
    final real = widget.driverAt;
    if (real != null) return real;
    final f = widget.progress.at.clamp(0.0, 1.0);
    return LatLng(
      widget.origin.latitude + (widget.destination.latitude - widget.origin.latitude) * f,
      widget.origin.longitude + (widget.destination.longitude - widget.origin.longitude) * f,
    );
  }

  /// The two endpoints, nudged apart if they coincide — a zero-area bounds
  /// cannot be fitted, and a trip with no coordinates yet would hit exactly
  /// that.
  LatLngBounds get _bounds {
    var (a, b) = (widget.origin, widget.destination);
    if ((a.latitude - b.latitude).abs() < 0.002 &&
        (a.longitude - b.longitude).abs() < 0.002) {
      a = LatLng(a.latitude - 0.004, a.longitude - 0.004);
      b = LatLng(b.latitude + 0.004, b.longitude + 0.004);
    }
    return LatLngBounds(a, b);
  }

  Polyline _leg(List<LatLng> points, MapLegStyle style, Color color) => Polyline(
        points: points,
        strokeWidth: style == MapLegStyle.faint ? 3 : 4.5,
        color: switch (style) {
          MapLegStyle.faint => color.withValues(alpha: 0.28),
          _ => color,
        },
        pattern: style == MapLegStyle.dashed
            ? const StrokePattern.dotted(spacingFactor: 1.6)
            : const StrokePattern.solid(),
      );

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final color = widget.dimmed ? t.ink2 : (widget.routeColor ?? t.teal);
    final car = _drawnAt;

    return Stack(children: [
      Positioned.fill(
        child: FlutterMap(
          mapController: _controller,
          options: MapOptions(
            // Fitted once to the whole route and then left alone: the car
            // moving across a stationary map is the thing to watch, and
            // chasing it would fight the rider's own panning.
            initialCameraFit: CameraFit.bounds(
              bounds: _bounds,
              padding: const EdgeInsets.fromLTRB(56, 96, 56, 56),
            ),
            // Rotation is off: a north-up map is easier to read at a glance,
            // and the markers are not rotation-aware.
            interactionOptions: const InteractionOptions(
              flags: InteractiveFlag.all & ~InteractiveFlag.rotate,
            ),
          ),
          children: [
            TileLayer(
              urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
              userAgentPackageName: 'com.wanes.app',
              tileProvider: widget.tileProvider ?? NetworkTileProvider(),
              // A missing tile should leave a gap, never take the screen down.
              errorImage: null,
            ),
            PolylineLayer(polylines: [
              _leg([widget.origin, car], widget.progress.behind, color),
              _leg([car, widget.destination], widget.progress.ahead, color),
            ]),
            MarkerLayer(markers: [
              if (widget.startMarker != null)
                Marker(
                    point: widget.origin,
                    width: 26,
                    height: 26,
                    child: widget.startMarker!),
              if (widget.endMarker != null)
                Marker(
                    point: widget.destination,
                    width: 34,
                    height: 34,
                    alignment: Alignment.topCenter,
                    child: widget.endMarker!),
              if (widget.progress.marker != null)
                Marker(
                  point: car,
                  // Roomy enough for the ping rings around the car badge.
                  width: 96,
                  height: 96,
                  child: Center(child: widget.progress.marker!),
                ),
            ]),
            // Required by the OpenStreetMap tile usage policy.
            const RichAttributionWidget(
              alignment: AttributionAlignment.bottomLeft,
              attributions: [TextSourceAttribution('OpenStreetMap contributors')],
            ),
          ],
        ),
      ),
      ...widget.overlays,
    ]);
  }
}
