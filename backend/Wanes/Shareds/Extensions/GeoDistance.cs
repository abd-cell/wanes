using NetTopologySuite.Geometries;

namespace Wanes.Shareds.Extensions;

/// <summary>
/// Great-circle distance between two SRID 4326 points, in application code.
///
/// Not the same tool as <c>Point.Distance(other)</c>. That one translates to
/// SQL Server's <c>geography::STDistance</c> and answers in metres — but only
/// inside a query. Called on a point that is already in memory it runs
/// NetTopologySuite's planar maths over longitude and latitude and answers in
/// *degrees*, which is a number that looks plausible and means nothing. Anything
/// computing a distance outside a query has to come through here.
/// </summary>
public static class GeoDistance
{
    private const double EarthRadiusKm = 6371.0088;

    /// <summary>Kilometres between two points, or 0 when either is missing.</summary>
    public static double Km(Point? a, Point? b)
    {
        if (a == null || b == null) return 0;
        return Km(a.Y, a.X, b.Y, b.X);
    }

    /// <summary>Kilometres between two lat/lng pairs (haversine).</summary>
    public static double Km(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = Radians(lat2 - lat1);
        var dLng = Radians(lng2 - lng1);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(Radians(lat1)) * Math.Cos(Radians(lat2))
                * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;
}
