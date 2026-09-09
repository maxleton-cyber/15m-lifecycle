using Lifecycle.Api.Models;

using Microsoft.Extensions.Options;



namespace Lifecycle.Api.Services;



public interface IIsochroneService

{

    Task<IsochroneResult> ComputeAsync(LatLng center, int minutes = 15, CancellationToken ct = default);

}



public class IsochroneService : IIsochroneService

{

    private readonly IBaiduMapClient _map;

    private readonly IRedisCacheService _cache;

    private readonly AgentSettings _settings;

    private readonly ILogger<IsochroneService> _logger;



    public IsochroneService(

        IBaiduMapClient map,

        IRedisCacheService cache,

        IOptions<AgentSettings> settings,

        ILogger<IsochroneService> logger)

    {

        _map = map;

        _cache = cache;

        _settings = settings.Value;

        _logger = logger;

    }



    public async Task<IsochroneResult> ComputeAsync(LatLng center, int minutes = 15, CancellationToken ct = default)

    {

        var cacheKey = $"isochrone:v5:{center.Lat:F4},{center.Lng:F4}:{minutes}";

        var cached = await _cache.GetAsync<IsochroneResult>(cacheKey, ct);

        if (cached is not null) return cached;



        var targetSeconds = minutes * 60.0;

        var directions = Math.Max(8, _settings.IsochroneSampleDirections);

        var angles = Enumerable.Range(0, directions)

            .Select(i => 2 * Math.PI * i / directions)

            .ToArray();



        // 二分上界：按百度 API 参考步行速度估算最大可达距离

        var maxDist = targetSeconds * _settings.MapApiWalkingSpeedMps * 1.15;

        var lows = new double[directions];

        var highs = new double[directions];



        for (var i = 0; i < directions; i++)

        {

            lows[i] = 80;

            highs[i] = Math.Clamp(maxDist, 400, 2000);

        }



        _logger.LogInformation(

            "计算等时圈: 中心({Lat},{Lng}), {Minutes}分钟步行(真实路网耗时), {Directions}方向",

            center.Lat, center.Lng, minutes, directions);



        for (var iter = 0; iter < _settings.IsochroneBinarySearchIterations; iter++)

        {

            var probePoints = new List<LatLng>(directions);

            var mids = new double[directions];



            for (var i = 0; i < directions; i++)

            {

                mids[i] = (lows[i] + highs[i]) / 2;

                probePoints.Add(BaiduMapClient.OffsetMeters(center, mids[i], angles[i]));

            }



            var durations = await _map.GetWalkingDurationsBatchAsync(center, probePoints, ct);

            if (durations.Count != directions)

                throw new InvalidOperationException(

                    $"步行批量算路结果数量异常: 期望 {directions}，实际 {durations.Count}");



            for (var i = 0; i < directions; i++)

            {

                if (durations[i] > targetSeconds)

                    highs[i] = mids[i];

                else

                    lows[i] = mids[i];

            }

        }



        var polygon = new List<LatLng>(directions);

        var maxRadius = 0.0;



        for (var i = 0; i < directions; i++)

        {

            var boundary = BaiduMapClient.OffsetMeters(center, lows[i], angles[i]);

            polygon.Add(boundary);

            var dist = BaiduMapClient.HaversineMeters(center, boundary);

            if (dist > maxRadius) maxRadius = dist;

        }



        polygon = SmoothPolygon(polygon);



        var polygonId = $"poly-{center.Lat:F4}-{center.Lng:F4}-{minutes}m-walk";

        var result = new IsochroneResult(polygonId, center, minutes, polygon, maxRadius);

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromDays(7), ct);

        return result;

    }



    /// <summary>对边界点做轻度平滑，减少多边形锯齿。</summary>

    private static List<LatLng> SmoothPolygon(List<LatLng> points)

    {

        if (points.Count < 3) return points;



        var smoothed = new List<LatLng>(points.Count);

        for (var i = 0; i < points.Count; i++)

        {

            var prev = points[(i - 1 + points.Count) % points.Count];

            var curr = points[i];

            var next = points[(i + 1) % points.Count];

            smoothed.Add(new LatLng(

                (prev.Lat + curr.Lat * 2 + next.Lat) / 4,

                (prev.Lng + curr.Lng * 2 + next.Lng) / 4));

        }



        return smoothed;

    }

}
