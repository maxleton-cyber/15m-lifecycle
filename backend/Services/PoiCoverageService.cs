using Lifecycle.Api.Models;
using Microsoft.Extensions.Options;

namespace Lifecycle.Api.Services;

public interface IPoiCoverageService
{
    Task<ReportData> BuildReportAsync(LatLng center, IsochroneResult isochrone, CancellationToken ct = default);
}

public class PoiCoverageService : IPoiCoverageService
{
    private readonly IBaiduMapClient _map;
    private readonly IBlindAreaService _blindArea;
    private readonly BlindAreaSettings _blindSettings;
    private readonly ILogger<PoiCoverageService> _logger;

    public PoiCoverageService(
        IBaiduMapClient map,
        IBlindAreaService blindArea,
        IOptions<BlindAreaSettings> blindSettings,
        ILogger<PoiCoverageService> logger)
    {
        _map = map;
        _blindArea = blindArea;
        _blindSettings = blindSettings.Value;
        _logger = logger;
    }

    public async Task<ReportData> BuildReportAsync(LatLng center, IsochroneResult isochrone, CancellationToken ct = default)
    {
        // 缓冲：圈外包一圈，至少覆盖各类 R 的最大值，供圈缘格子判定圈外设施
        var maxR = PoiClassifier.Categories.Max(c => c.RequiredRadiusMeters);
        if (maxR <= 0) maxR = _blindSettings.DefaultReachThresholdMeters;
        var searchRadius = (int)Math.Clamp(
            Math.Ceiling(isochrone.RadiusMeters + maxR),
            500,
            3000);

        var rawPois = await _map.FetchPoisInIsochroneAsync(center, isochrone.Polygon, searchRadius, ct);

        // 保留「等时圈 + 缓冲」内设施：缓冲用圆心半径近似
        var buffered = rawPois
            .Where(p => BaiduMapClient.HaversineMeters(center, new LatLng(p.Lat, p.Lng)) <= searchRadius)
            .ToList();

        var insidePois = buffered
            .Where(p => GeoUtils.IsPointInPolygon(p.Lat, p.Lng, isochrone.Polygon))
            .ToList();

        var nearPois = buffered
            .Where(p => !GeoUtils.IsPointInPolygon(p.Lat, p.Lng, isochrone.Polygon))
            .ToList();

        _logger.LogInformation(
            "POI 清洗: 召回 {Raw} 条 → 缓冲内 {Buffered} 条（圈内 {Inside}，圈外邻近 {Near}）",
            rawPois.Count,
            buffered.Count,
            insidePois.Count,
            nearPois.Count);

        var classifiedInside = ClassifyPois(insidePois);
        var classifiedNear = ClassifyPois(nearPois);
        // 盲区判定可用圈外邻近设施（步行可达可能在圈外）
        var classifiedForBlind = MergeBuckets(classifiedInside, classifiedNear);

        LogCategoryCounts("圈内", classifiedInside);

        var categories = new List<CategoryCoverage>();
        var blindSpots = new List<BlindSpotInfo>();

        foreach (var (category, label, requiredRadius) in PoiClassifier.Categories)
        {
            classifiedInside.TryGetValue(category, out var inside);
            inside ??= [];
            classifiedNear.TryGetValue(category, out var near);
            near ??= [];

            var reach = requiredRadius > 0 ? requiredRadius : _blindSettings.DefaultReachThresholdMeters;
            double? nearest = null;
            var allForCat = inside.Concat(near).ToList();
            if (allForCat.Count > 0)
            {
                nearest = allForCat.Min(p =>
                    BaiduMapClient.HaversineMeters(center, new LatLng(p.Lat, p.Lng)));
            }

            // 类别盲区：中心 R 内（直线距离）无该类设施则盲
            var isBlind = nearest is null || nearest > reach;

            var avgDist = inside.Count > 0
                ? inside.Average(p => BaiduMapClient.HaversineMeters(center, new LatLng(p.Lat, p.Lng)))
                : nearest ?? reach;

            var score = ComputeScore(Math.Max(inside.Count, isBlind ? 0 : 1), avgDist, reach);

            categories.Add(new CategoryCoverage(category, label, inside.Count, score, isBlind, nearest, inside));
            blindSpots.Add(new BlindSpotInfo(
                category,
                label,
                isBlind,
                nearest,
                isBlind
                    ? nearest is not null
                        ? $"按 {reach:F0}m 可达阈值，{label}覆盖不足；最近同类约 {nearest:F0} 米"
                        : $"按 {reach:F0}m 可达阈值，未找到可用{label}"
                    : inside.Count > 0
                        ? $"{label}覆盖良好，圈内 {inside.Count} 处"
                        : $"{label}在 {reach:F0}m 内可达（最近约 {nearest:F0} 米）"));
        }

        var blindArea = _blindArea.Analyze(isochrone, classifiedForBlind);
        _logger.LogInformation(
            "盲区网格: 盲区块 {Blind} 个，盲区面积占比 {Ratio:P1}，平均缺失 {Avg}",
            blindArea.BlindRegions.Count,
            blindArea.BlindAreaRatio,
            blindArea.AverageMissingCount);

        var overall = categories.Count > 0 ? categories.Average(c => c.Score) : 0;

        return new ReportData(
            isochrone.PolygonId,
            center,
            isochrone,
            categories,
            blindSpots,
            Math.Round(overall, 1),
            blindArea);
    }

    private static Dictionary<string, List<PoiItem>> ClassifyPois(IReadOnlyList<PoiItem> pois)
    {
        var buckets = PoiClassifier.Categories.ToDictionary(c => c.Category, _ => new List<PoiItem>());

        foreach (var poi in pois)
        {
            var categories = PoiClassifier.Classify(poi);
            foreach (var category in categories)
            {
                if (!buckets.ContainsKey(category)) continue;
                buckets[category].Add(poi with { Category = category });
            }
        }

        return buckets;
    }

    private static Dictionary<string, List<PoiItem>> MergeBuckets(
        Dictionary<string, List<PoiItem>> a,
        Dictionary<string, List<PoiItem>> b)
    {
        var merged = PoiClassifier.Categories.ToDictionary(c => c.Category, _ => new List<PoiItem>());
        foreach (var key in merged.Keys.ToList())
        {
            if (a.TryGetValue(key, out var la)) merged[key].AddRange(la);
            if (b.TryGetValue(key, out var lb)) merged[key].AddRange(lb);
        }

        return merged;
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
