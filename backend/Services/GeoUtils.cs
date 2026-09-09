using Lifecycle.Api.Models;

namespace Lifecycle.Api.Services;

public static class GeoUtils
{
    /// <summary>射线法判断点是否在多边形内。</summary>
    public static bool IsPointInPolygon(double lat, double lng, IReadOnlyList<LatLng> polygon)
    {
        var inside = false;
        var j = polygon.Count - 1;

        for (var i = 0; i < polygon.Count; i++)
        {
            var xi = polygon[i].Lng;
            var yi = polygon[i].Lat;
            var xj = polygon[j].Lng;
            var yj = polygon[j].Lat;

            if ((yi > lat) != (yj > lat) &&
                lng < (xj - xi) * (lat - yi) / (yj - yi + double.Epsilon) + xi)
                inside = !inside;

            j = i;
        }

        return inside;
    }

    /// <summary>将多边形格式化为百度 bounds 参数（首尾闭合，最多 100 对坐标）。</summary>
    public static string FormatPolygonBounds(IReadOnlyList<LatLng> polygon, int maxVertices = 99)
    {
        if (polygon.Count == 0)
            throw new ArgumentException("polygon is empty");

        var points = SimplifyPolygon(polygon, maxVertices);
        var parts = points.Select(p => $"{p.Lat:F6},{p.Lng:F6}").ToList();
        var first = points[0];
        var last = points[^1];
        if (Math.Abs(first.Lat - last.Lat) > 1e-6 || Math.Abs(first.Lng - last.Lng) > 1e-6)
            parts.Add($"{first.Lat:F6},{first.Lng:F6}");

        return string.Join(",", parts);
    }

    private static List<LatLng> SimplifyPolygon(IReadOnlyList<LatLng> polygon, int maxVertices)
    {
        if (polygon.Count <= maxVertices)
            return polygon.ToList();

        var step = (double)polygon.Count / maxVertices;
        var simplified = new List<LatLng>(maxVertices);
        for (var i = 0; i < maxVertices; i++)
            simplified.Add(polygon[(int)Math.Floor(i * step)]);

        return simplified;
    }
}
