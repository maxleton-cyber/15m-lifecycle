using Lifecycle.Api.Models;

namespace Lifecycle.Api.Services;

public interface IPoiCoverageService
{
    Task<ReportData> BuildReportAsync(LatLng center, IsochroneResult isochrone, CancellationToken ct = default);
}

public class PoiCoverageService : IPoiCoverageService
{
    private readonly IBaiduMapClient _map;
    private readonly ILogger<PoiCoverageService> _logger;

    public PoiCoverageService(IBaiduMapClient map, ILogger<PoiCoverageService> logger)
    {
        _map = map;
        _logger = logger;
    }

    public async Task<ReportData> BuildReportAsync(LatLng center, IsochroneResult isochrone, CancellationToken ct = default)
    {
        // 1. 在等时圈范围内批量召回 POI（大类并集，非按类别逐条搜）
        var searchRadius = (int)Math.Clamp(Math.Ceiling(isochrone.RadiusMeters), 300, 2000);
        var rawPois = await _map.FetchPoisInIsochroneAsync(center, isochrone.Polygon, searchRadius, ct);

        // 2. 空间过滤：精确保留等时圈内的 POI
        var insidePois = rawPois
            .Where(p => GeoUtils.IsPointInPolygon(p.Lat, p.Lng, isochrone.Polygon))
            .ToList();

        var outsidePois = rawPois
            .Where(p => !GeoUtils.IsPointInPolygon(p.Lat, p.Lng, isochrone.Polygon))
            .ToList();

        _logger.LogInformation(
            "POI 清洗: 召回 {Raw} 条 → 圈内 {Inside} 条，圈外 {Outside} 条",
            rawPois.Count,
            insidePois.Count,
            outsidePois.Count);

        // 3. 规则分类：将 POI 映射到 8 类民生设施
        var classifiedInside = ClassifyPois(insidePois);
        var classifiedOutside = ClassifyPois(outsidePois);

        LogCategoryCounts("圈内", classifiedInside);

        var categories = new List<CategoryCoverage>();
        var blindSpots = new List<BlindSpotInfo>();

        foreach (var (category, label, requiredRadius) in PoiClassifier.Categories)
        {
            classifiedInside.TryGetValue(category, out var inside);
            inside ??= [];

            classifiedOutside.TryGetValue(category, out var outside);
            outside ??= [];

            var isBlind = inside.Count == 0;
            double? nearest = null;
            if (isBlind && outside.Count > 0)
                nearest = outside.Min(p => p.DistanceMeters);

            var avgDist = inside.Count > 0
                ? inside.Average(p => BaiduMapClient.HaversineMeters(center, new LatLng(p.Lat, p.Lng)))
                : requiredRadius;

            var score = ComputeScore(inside.Count, avgDist, requiredRadius);

            categories.Add(new CategoryCoverage(category, label, inside.Count, score, isBlind, nearest, inside));
            blindSpots.Add(new BlindSpotInfo(
                category,
                label,
                isBlind,
                nearest,
                isBlind
                    ? nearest is not null
                        ? $"等时圈内无{label}，最近同类设施约{nearest:F0}米（圈外）"
                        : $"等时圈内无{label}"
                    : $"{label}覆盖良好，圈内{inside.Count}处"));
        }

        var overall = categories.Count > 0 ? categories.Average(c => c.Score) : 0;

        return new ReportData(
            isochrone.PolygonId,
            center,
            isochrone,
            categories,
            blindSpots,
            Math.Round(overall, 1));
    }

    private static Dictionary<string, List<PoiItem>> ClassifyPois(IReadOnlyList<PoiItem> pois)
    {
        var buckets = PoiClassifier.Categories.ToDictionary(c => c.Category, _ => new List<PoiItem>());

        foreach (var poi in pois)
        {
            var categories = PoiClassifier.Classify(poi);
            foreach (var category in categories)
            {
                buckets[category].Add(poi with { Category = category });
            }
        }

        return buckets;
    }

    private void LogCategoryCounts(string scope, Dictionary<string, List<PoiItem>> buckets)
    {
        var summary = string.Join(", ", PoiClassifier.Categories.Select(c =>
            $"{c.Label}={buckets.GetValueOrDefault(c.Category)?.Count ?? 0}"));
        _logger.LogInformation("POI 分类({Scope}): {Summary}", scope, summary);
    }

    private static double ComputeScore(int count, double avgDistance, double requiredRadius)
    {
        if (count == 0) return 0;
        var countPart = 40 * Math.Log(1 + count);
        var distPart = 60 * (1 - Math.Min(1, avgDistance / requiredRadius));
        return Math.Min(100, countPart + distPart);
    }
}
