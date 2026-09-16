using NetTopologySuite.Geometries;
using NetTopologySuite.LinearReferencing;

namespace Wanes.Shareds.Extensions;

/// <summary>
/// Where a point falls along a route — the maths behind "this trip passes my
/// way".
///
/// Distance to the route is not enough on its own. Both of a rider's ends can
/// sit a hundred metres from the same line while the driver is travelling the
/// other way, so a corridor match has to know the *order*: the pickup must come
/// before the drop-off along the driver's direction of travel. That is what
/// projecting onto the line gives us, and it is why the corridor is expressed
/// against <c>Trip.Route</c> rather than against its two endpoints.
///
/// Everything here runs in memory over a candidate pool, so distances go through
/// <see cref="GeoDistance"/>; the fractions themselves come from
/// NetTopologySuite's linear referencing, which is unit-agnostic and therefore
/// safe on degrees.
/// </summary>
public static class RouteGeometry
{
    /// <summary>
    /// Where <paramref name="point"/> sits along <paramref name="route"/>, as a
    /// fraction from 0 (its start) to 1 (its end), plus how far off the route it
    /// is in kilometres.
    ///
    /// A route with no length — the same origin and destination, which the
    /// validation rules refuse but old rows may hold — answers fraction 0 and a
    /// straight distance to its start, so callers get an honest "off route"
    /// rather than a divide by zero.
    /// </summary>
    public static (double Fraction, double OffRouteKm) Locate(LineString? route, Point? point)
    {
        if (route == null || point == null || route.NumPoints == 0) return (0, 0);

        var indexed = new LengthIndexedLine(route);
        var index = indexed.Project(point.Coordinate);
        var length = route.Length;

        var nearest = indexed.ExtractPoint(index);
        var offRouteKm = GeoDistance.Km(point.Y, point.X, nearest.Y, nearest.X);

        return (length <= 0 ? 0 : Math.Clamp(index / length, 0, 1), offRouteKm);
    }

    /// <summary>
    /// The route's own length in kilometres, measured over its vertices rather
    /// than by <c>LineString.Length</c> — which, in memory, is degrees.
    /// </summary>
    public static double LengthKm(LineString? route)
    {
        if (route == null || route.NumPoints < 2) return 0;

        var total = 0.0;
        var coordinates = route.Coordinates;
        for (var i = 1; i < coordinates.Length; i++)
        {
            var a = coordinates[i - 1];
            var b = coordinates[i];
            total += GeoDistance.Km(a.Y, a.X, b.Y, b.X);
        }
        return total;
    }

    /// <summary>
    /// The point half way along the route.
    ///
    /// Used to bound a candidate pool against a whole journey rather than one of
    /// its ends: a circle centred here with a radius of half the route plus the
    /// match radius is the smallest one that certainly contains every trip that
    /// could lie along it. Centring on the origin instead would throw out exactly
    /// the corridor matches — they begin somewhere in the middle by definition.
    /// </summary>
    public static Point Midpoint(LineString? route)
    {
        if (route == null || route.NumPoints == 0) return GeoFactory.Point(0, 0);
        if (route.NumPoints == 1)
            return GeoFactory.Point(route.Coordinates[0].Y, route.Coordinates[0].X);

        var indexed = new LengthIndexedLine(route);
        var middle = indexed.ExtractPoint(route.Length / 2);
        return GeoFactory.Point(middle.Y, middle.X);
    }
}
