using Lifecycle.Api.Models;
using Microsoft.Extensions.Options;

namespace Lifecycle.Api.Services;

public interface IBlindAreaService
{
    BlindAreaAnalysis Analyze(
        IsochroneResult isochrone,
        IReadOnlyDictionary<string, List<PoiItem>> facilitiesByCategory);
}

/// <summary>
/// 等时圈内网格盲区分析：覆盖判定用「直线距离 × 绕行系数」估算步行距离（降级口径），
/// 避免对每个格子发起海量算路；结果标记 Degraded。
/// </summary>
public class BlindAreaService : IBlindAreaService
{
    private readonly BlindAreaSettings _settings;

    public BlindAreaService(IOptions<BlindAreaSettings> settings)
    {
        _settings = settings.Value;
    }

    public BlindAreaAnalysis Analyze(
        IsochroneResult isochrone,
        IReadOnlyDictionary<string, List<PoiItem>> facilitiesByCategory)
    {
        var polygon = isochrone.Polygon;
        if (polygon.Count < 3)
        {
            return new BlindAreaAnalysis([], 0, 0);
        }

        var gridSize = Math.Clamp(_settings.GridSizeMeters, 50, 300);
        var minRatio = Math.Clamp(_settings.MinInsideRatio, 0.1, 1.0);
        var categoryMeta = PoiClassifier.Categories.ToDictionary(
            c => c.Category,
            c => (c.Label, Reach: c.RequiredRadiusMeters > 0
                ? c.RequiredRadiusMeters
                : _settings.DefaultReachThresholdMeters));

        var (minLat, maxLat, minLng, maxLng) = BBox(polygon);
        var latStep = gridSize / 111_320.0;
        var midLat = (minLat + maxLat) / 2;
        var lngStep = gridSize / (111_320.0 * Math.Cos(midLat * Math.PI / 180.0));

        var blindRegions = new List<BlindAreaRegion>();
        var validArea = 0.0;
        var blindArea = 0.0;
        var missingAreaSum = 0.0;
        var cellArea = gridSize * gridSize;
        var blindId = 0;

        for (var lat = minLat; lat < maxLat; lat += latStep)
        {
            for (var lng = minLng; lng < maxLng; lng += lngStep)
            {
                var corners = new[]
                {
                    new LatLng(lat, lng),
                    new LatLng(lat, lng + lngStep),
                    new LatLng(lat + latStep, lng + lngStep),
                    new LatLng(lat + latStep, lng),
                };

                var insideCorners = corners.Count(c => GeoUtils.IsPointInPolygon(c.Lat, c.Lng, polygon));
                var ratio = insideCorners / 4.0;
                var center = new LatLng(lat + latStep / 2, lng + lngStep / 2);
                var centerInside = GeoUtils.IsPointInPolygon(center.Lat, center.Lng, polygon);

                if (!centerInside && ratio < minRatio)
                    continue;

                if (!centerInside)
                {
                    // 中心在圈外时用角点均值近似重心
                    var inPts = corners.Where(c => GeoUtils.IsPointInPolygon(c.Lat, c.Lng, polygon)).ToList();
                    if (inPts.Count == 0) continue;
                    center = new LatLng(inPts.Average(p => p.Lat), inPts.Average(p => p.Lng));
                }

                var cellPoly = corners.ToList();
                var cellValidArea = cellArea * Math.Max(ratio, minRatio);
                validArea += cellValidArea;

                var missing = new List<string>();
                foreach (var (category, meta) in categoryMeta)
                {
                    facilitiesByCategory.TryGetValue(category, out var list);
                    list ??= [];
                    if (list.Count == 0)
                    {
                        missing.Add(meta.Label);
                        continue;
                    }

                    var minStraight = list.Min(p =>
                        BaiduMapClient.HaversineMeters(center, new LatLng(p.Lat, p.Lng)));
                    // 网格覆盖用直线距离 ≤ R（与类别盲区口径一致，避免 1km 内设施被 1.3 绕行系数误判）
                    if (minStraight > meta.Reach)
                        missing.Add(meta.Label);
                }

                var level = missing.Count;
                missingAreaSum += cellValidArea * level;

                if (level <= 0)
                    continue;

                blindArea += cellValidArea;
                blindRegions.Add(new BlindAreaRegion(
                    $"blind_{++blindId}",
                    level,
                    missing,
                    cellPoly,
                    cellValidArea,
                    Degraded: false));
            }
        }

        return new BlindAreaAnalysis(
            blindRegions,
            validArea > 0 ? Math.Round(blindArea / validArea, 3) : 0,
            validArea > 0 ? Math.Round(missingAreaSum / validArea, 2) : 0);
    }

    private static (double MinLat, double MaxLat, double MinLng, double MaxLng) BBox(
        IReadOnlyList<LatLng> polygon)
    {
        var minLat = polygon.Min(p => p.Lat);
        var maxLat = polygon.Max(p => p.Lat);
        var minLng = polygon.Min(p => p.Lng);
        var maxLng = polygon.Max(p => p.Lng);
        return (minLat, maxLat, minLng, maxLng);
    }
}
