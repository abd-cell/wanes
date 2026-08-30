using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace Wanes.Shareds.Extensions;

/// <summary>Builds SRID 4326 (WGS84) geometries for the spatial columns.</summary>
public static class GeoFactory
{
    public const int Srid = 4326;

    private static readonly GeometryFactory Factory =
        NtsGeometryServices.Instance.CreateGeometryFactory(Srid);

    /// <summary>Creates a Point. Note order: (longitude, latitude).</summary>
    public static Point Point(double lat, double lng) =>
        Factory.CreatePoint(new Coordinate(lng, lat));

    public static LineString Line(Point a, Point b) =>
        Factory.CreateLineString([a.Coordinate, b.Coordinate]);
}
