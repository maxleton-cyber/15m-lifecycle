using System.Net.Http.Json;
using System.Text.Json;
using Lifecycle.Api.Models;
using Microsoft.Extensions.Options;

namespace Lifecycle.Api.Services;

public interface IBaiduMapClient
{
    Task<GeocodeResult> GeocodeAsync(string address, CancellationToken ct = default);
    Task<IReadOnlyList<PlaceSuggestion>> SuggestPlacesAsync(string query, string region, CancellationToken ct = default);
    Task<double> GetWalkingDurationSecondsAsync(LatLng from, LatLng to, CancellationToken ct = default);
    Task<IReadOnlyList<double>> GetWalkingDurationsBatchAsync(
        LatLng from, IReadOnlyList<LatLng> destinations, CancellationToken ct = default);
    Task<IReadOnlyList<PoiItem>> SearchPoiAsync(LatLng center, string query, int radiusMeters, CancellationToken ct = default);
    /// <summary>在等时圈范围内批量召回 POI（大类并集检索 + 去重，不含分类）。</summary>
    Task<IReadOnlyList<PoiItem>> FetchPoisInIsochroneAsync(
        LatLng center,
        IReadOnlyList<LatLng> polygon,
        double radiusMeters,
        CancellationToken ct = default);
}

public class BaiduMapClient : IBaiduMapClient
{
    private readonly HttpClient _http;
    private readonly BaiduMapSettings _settings;
    private readonly IQuotaGuardService _quota;
    private readonly IRedisCacheService _cache;
    private readonly IBaiduApiRateLimiter _rateLimiter;
    private readonly ILogger<BaiduMapClient> _logger;

    public BaiduMapClient(
        HttpClient http,
        IOptions<BaiduMapSettings> settings,
        IQuotaGuardService quota,
        IRedisCacheService cache,
        IBaiduApiRateLimiter rateLimiter,
        ILogger<BaiduMapClient> logger)
    {
        _http = http;
        _settings = settings.Value;
        _quota = quota;
        _cache = cache;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    private bool UseMock => string.IsNullOrWhiteSpace(_settings.Ak) && _settings.UseMockWhenNoAk;

    private void EnsureAkConfigured()
    {
        if (string.IsNullOrWhiteSpace(_settings.Ak))
            throw new InvalidOperationException(
                "未配置 BAIDU_MAP_AK，无法使用百度地图服务。请在环境变量中配置服务端 AK。");
    }

    private async Task<JsonElement> GetBaiduJsonAsync(string url, CancellationToken ct)
    {
        await _rateLimiter.AcquireAsync(ct);
        return await _http.GetFromJsonAsync<JsonElement>(url, ct);
    }

    public async Task<GeocodeResult> GeocodeAsync(string address, CancellationToken ct = default)
    {
        var cacheKey = $"geocode:v2:{address}";
        var cached = await _cache.GetAsync<GeocodeResult>(cacheKey, ct);
        if (cached is not null) return cached;

        if (UseMock)
        {
            var mock = await MockGeocodeAsync(address, ct);
            await _cache.SetAsync(cacheKey, mock, TimeSpan.FromDays(7), ct);
            return mock;
        }

        EnsureAkConfigured();

        if (!await _quota.TryConsumeAsync("geocode", 1, ct))
            throw new InvalidOperationException("地理编码配额已耗尽");

        var url = $"{_settings.BaseUrl}/geocoding/v3/?address={Uri.EscapeDataString(address)}&output=json&ak={_settings.Ak}";
        var response = await GetBaiduJsonAsync(url, ct);
        var status = response.GetProperty("status").GetInt32();
        if (status != 0)
            throw new InvalidOperationException($"地理编码失败: status={status}");

        var loc = response.GetProperty("result").GetProperty("location");
        var result = new GeocodeResult(
            loc.GetProperty("lat").GetDouble(),
            loc.GetProperty("lng").GetDouble(),
            response.GetProperty("result").GetProperty("formatted_address").GetString() ?? address);

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromDays(7), ct);
        return result;
    }

    public async Task<IReadOnlyList<PlaceSuggestion>> SuggestPlacesAsync(
        string query, string region, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<PlaceSuggestion>();

        var cacheKey = $"suggest:v3:{region}:{query}";
        var cached = await _cache.GetAsync<List<PlaceSuggestion>>(cacheKey, ct);
        if (cached is not null) return cached;

        if (UseMock)
        {
            var mock = await MockSuggestAsync(query, region, ct);
            await _cache.SetAsync(cacheKey, mock, TimeSpan.FromDays(1), ct);
            return mock;
        }

        EnsureAkConfigured();

        if (!await _quota.TryConsumeAsync("poi_search", 1, ct))
            throw new InvalidOperationException("地点检索配额已耗尽");

        var url = $"{_settings.BaseUrl}/place/v2/suggestion?query={Uri.EscapeDataString(query)}&region={Uri.EscapeDataString(string.IsNullOrWhiteSpace(region) ? "全国" : region)}&city_limit=false&output=json&ak={_settings.Ak}";
        var response = await GetBaiduJsonAsync(url, ct);
        var status = response.GetProperty("status").GetInt32();
        if (status != 0)
        {
            var message = response.TryGetProperty("message", out var msgEl)
                ? msgEl.GetString()
                : null;
            _logger.LogWarning("地点建议 API 失败: status={Status}, message={Message}", status, message);
            throw new InvalidOperationException(
                message is { Length: > 0 }
                    ? $"地点建议失败: {message} (status={status})"
                    : $"地点建议失败: status={status}，请检查 BAIDU_MAP_AK 权限与白名单");
        }

        var suggestions = new List<PlaceSuggestion>();
        if (response.TryGetProperty("result", out var results))
        {
            foreach (var item in results.EnumerateArray())
            {
                if (!item.TryGetProperty("location", out var loc)) continue;
                suggestions.Add(new PlaceSuggestion(
                    item.GetProperty("name").GetString() ?? query,
                    item.GetProperty("address").GetString() ?? "",
                    loc.GetProperty("lat").GetDouble(),
                    loc.GetProperty("lng").GetDouble(),
                    item.TryGetProperty("district", out var d) ? d.GetString() : null,
                    item.TryGetProperty("tag", out var t) ? t.GetString() : null));
            }
        }

        await _cache.SetAsync(cacheKey, suggestions, TimeSpan.FromDays(1), ct);
        return suggestions;
    }

    public async Task<double> GetWalkingDurationSecondsAsync(LatLng from, LatLng to, CancellationToken ct = default)
    {
        var durations = await GetWalkingDurationsBatchAsync(from, [to], ct);
        return durations[0];
    }

    public async Task<IReadOnlyList<double>> GetWalkingDurationsBatchAsync(
        LatLng from, IReadOnlyList<LatLng> destinations, CancellationToken ct = default)
    {
        if (destinations.Count == 0)
            return Array.Empty<double>();

        EnsureAkConfigured();

        var results = new double[destinations.Count];
        var pending = new List<(int Index, LatLng Point)>();

        for (var i = 0; i < destinations.Count; i++)
        {
            var to = destinations[i];
            var cacheKey = $"walk:{from.Lat:F5},{from.Lng:F5}:{to.Lat:F5},{to.Lng:F5}";
            var cached = await _cache.GetAsync<double>(cacheKey, ct);
            if (cached > 0)
                results[i] = cached;
            else
                pending.Add((i, to));
        }

        if (pending.Count == 0)
            return results;

        const int maxBatchSize = 50;
        for (var offset = 0; offset < pending.Count; offset += maxBatchSize)
        {
            var chunk = pending.Skip(offset).Take(maxBatchSize).ToList();
            if (!await _quota.TryConsumeAsync("route_matrix", chunk.Count, ct))
                throw new InvalidOperationException("步行批量算路配额已耗尽");

            var chunkDurations = await FetchWalkingDurationsBatchAsync(from, chunk.Select(p => p.Point).ToList(), ct);
            for (var j = 0; j < chunk.Count; j++)
            {
                var (index, point) = chunk[j];
                results[index] = chunkDurations[j];
                var cacheKey = $"walk:{from.Lat:F5},{from.Lng:F5}:{point.Lat:F5},{point.Lng:F5}";
                await _cache.SetAsync(cacheKey, chunkDurations[j], TimeSpan.FromDays(7), ct);
            }
        }

        return results;
    }

    private async Task<IReadOnlyList<double>> FetchWalkingDurationsBatchAsync(
        LatLng from, IReadOnlyList<LatLng> destinations, CancellationToken ct)
    {
        var destinationParam = string.Join("|", destinations.Select(d => $"{d.Lat:F6},{d.Lng:F6}"));
        var url =
            $"{_settings.BaseUrl}/routematrix/v2/walking?output=json" +
            $"&origins={from.Lat:F6},{from.Lng:F6}" +
            $"&destinations={destinationParam}" +
            $"&coord_type=bd09ll&ak={_settings.Ak}";

        var response = await GetMapApiWithRetryAsync(url, "步行批量算路", ct);
        var routeResults = response.GetProperty("result");

        if (routeResults.GetArrayLength() != destinations.Count)
        {
            throw new InvalidOperationException(
                $"步行批量算路结果数量异常: 期望 {destinations.Count}，实际 {routeResults.GetArrayLength()}");
        }

        var durations = new List<double>(destinations.Count);
        foreach (var item in routeResults.EnumerateArray())
        {
            durations.Add(item.GetProperty("duration").GetProperty("value").GetDouble());
        }

        return durations;
    }

    private async Task<JsonElement> GetMapApiWithRetryAsync(
        string url, string apiName, CancellationToken ct, int maxRetries = 5)
    {
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            var response = await GetBaiduJsonAsync(url, ct);
            var status = response.GetProperty("status").GetInt32();
            if (status == 0)
                return response;

            var message = response.TryGetProperty("message", out var msgEl)
                ? msgEl.GetString()
                : null;

            if ((status == 401 || status == 402) && attempt < maxRetries)
            {
                var delayMs = Math.Max(1000, 400 * (attempt + 1));
                _logger.LogWarning(
                    "{ApiName} 并发超限(status={Status})，{Delay}ms 后重试 ({Attempt}/{Max})",
                    apiName, status, delayMs, attempt + 1, maxRetries);
                await Task.Delay(delayMs, ct);
                continue;
            }

            _logger.LogWarning("{ApiName} 失败: status={Status}, message={Message}", apiName, status, message);
            throw new InvalidOperationException(
                status is 401 or 402
                    ? $"{apiName}并发超限(status={status})，请稍后重试或在百度控制台提升配额"
                    : message is { Length: > 0 }
                        ? $"{apiName}失败: {message} (status={status})"
                        : $"{apiName}失败: status={status}，请检查 BAIDU_MAP_AK 是否开通步行批量算路权限");
        }

        throw new InvalidOperationException($"{apiName}失败: 超过最大重试次数");
    }

    private static readonly string[] IsochroneRecallBatches =
    [
        "医院$诊所$社区卫生$卫生院$门诊$综合医院$医务室$药店$药房",
        "幼儿园$小学$中学$高中$大学$学院$学校",
        "菜市场$农贸市场$商场$百货$商铺$市场$购物$店铺",
        "便利店$超市$大卖场$仓储",
        "养老$敬老院$老年$护理",
        "地铁站$轨道交通$轻轨",
        // 公交站单独一批，避免被地铁口/周边商户挤占分页
        "公交车站$公交站$巴士站$公共汽车站$BRT",
        "公园$绿地$广场$花园",
    ];

    public async Task<IReadOnlyList<PoiItem>> FetchPoisInIsochroneAsync(
        LatLng center,
        IReadOnlyList<LatLng> polygon,
        double radiusMeters,
        CancellationToken ct = default)
    {
        var bounds = GeoUtils.FormatPolygonBounds(polygon);
        var cacheKey = $"poi:isochrone:v5:{center.Lat:F4},{center.Lng:F4}:{radiusMeters:F0}:{bounds.GetHashCode()}";
        var cached = await _cache.GetAsync<List<PoiItem>>(cacheKey, ct);
        if (cached is not null) return cached;

        EnsureAkConfigured();

        var merged = new Dictionary<string, PoiItem>(StringComparer.Ordinal);
        const int pageSize = 20;
        const int maxPages = 8;
        var useBounds = true;

        foreach (var queryBatch in IsochroneRecallBatches)
        {
            for (var page = 0; page < maxPages; page++)
            {
                if (!await _quota.TryConsumeAsync("poi_search", 1, ct))
                    break;

                var (pagePois, boundsSupported) = await FetchIsochronePoiPageAsync(
                    center, bounds, (int)Math.Ceiling(radiusMeters), queryBatch, page, pageSize, useBounds, ct);

                if (!boundsSupported)
                    useBounds = false;

                if (pagePois.Count == 0)
                    break;

                foreach (var poi in pagePois)
                    merged[poi.Id] = poi;

                if (pagePois.Count < pageSize)
                    break;
            }
        }

        var result = merged.Values.ToList();
        await _cache.SetAsync(cacheKey, result, TimeSpan.FromDays(7), ct);
        _logger.LogInformation(
            "等时圈 POI 召回: {Count} 条（{Mode}，{Batches} 批）",
            result.Count,
            useBounds ? "多边形" : "圆形",
            IsochroneRecallBatches.Length);

        return result;
    }

    private async Task<(List<PoiItem> Pois, bool BoundsSupported)> FetchIsochronePoiPageAsync(
        LatLng center,
        string bounds,
        int radiusMeters,
        string queryBatch,
        int pageNum,
        int pageSize,
        bool useBounds,
        CancellationToken ct)
    {
        string url = useBounds
            ? $"{_settings.BaseUrl}/place/v2/search?query={Uri.EscapeDataString(queryBatch)}" +
              $"&bounds={bounds}&scope=2&page_size={pageSize}&page_num={pageNum}" +
              $"&coord_type=bd09ll&ret_coordtype=bd09ll&output=json&ak={_settings.Ak}"
            : $"{_settings.BaseUrl}/place/v2/search?query={Uri.EscapeDataString(queryBatch)}" +
              $"&location={center.Lat},{center.Lng}&radius={radiusMeters}" +
              $"&scope=2&page_size={pageSize}&page_num={pageNum}" +
              $"&coord_type=bd09ll&ret_coordtype=bd09ll&output=json&ak={_settings.Ak}";

        try
        {
            var response = await GetBaiduJsonAsync(url, ct);
            var status = response.GetProperty("status").GetInt32();
            if (status == 9 && useBounds)
            {
                _logger.LogWarning("多边形 POI 检索无权限，回退圆形检索");
                return await FetchIsochronePoiPageAsync(
                    center, bounds, radiusMeters, queryBatch, pageNum, pageSize, useBounds: false, ct);
            }

            if (status != 0)
            {
                _logger.LogWarning("POI 召回失败: query={Query} status={Status}", queryBatch, status);
                return ([], useBounds);
            }

            if (!response.TryGetProperty("results", out var results))
                return ([], useBounds);

            var pois = new List<PoiItem>();
            foreach (var item in results.EnumerateArray())
                pois.Add(ParsePoiItem(item, center));

            return (pois, useBounds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "POI 召回异常: query={Query}", queryBatch);
            return ([], useBounds);
        }
    }

    private static PoiItem ParsePoiItem(JsonElement item, LatLng center)
    {
        var loc = item.GetProperty("location");
        var lat = loc.GetProperty("lat").GetDouble();
        var lng = loc.GetProperty("lng").GetDouble();
        var tag = ExtractPoiTags(item);
        var distance = item.TryGetProperty("detail_info", out var detail) &&
                       detail.TryGetProperty("distance", out var dist)
            ? dist.GetDouble()
            : HaversineMeters(center, new LatLng(lat, lng));

        return new PoiItem(
            item.GetProperty("uid").GetString() ?? Guid.NewGuid().ToString(),
            item.GetProperty("name").GetString() ?? "未知",
            string.Empty,
            lat,
            lng,
            item.GetProperty("address").GetString() ?? "",
            distance,
            tag);
    }

    private static string? ExtractPoiTags(JsonElement item)
    {
        if (!item.TryGetProperty("detail_info", out var detail))
            return null;

        var parts = new List<string>();
        if (detail.TryGetProperty("tag", out var tagEl) && tagEl.ValueKind == JsonValueKind.String)
        {
            var tag = tagEl.GetString();
            if (!string.IsNullOrWhiteSpace(tag)) parts.Add(tag);
        }

        if (detail.TryGetProperty("classified_poi_tag", out var classifiedEl) &&
            classifiedEl.ValueKind == JsonValueKind.String)
        {
            var classified = classifiedEl.GetString();
            if (!string.IsNullOrWhiteSpace(classified)) parts.Add(classified);
        }

        return parts.Count == 0 ? null : string.Join(";", parts);
    }

    public async Task<IReadOnlyList<PoiItem>> SearchPoiAsync(LatLng center, string query, int radiusMeters, CancellationToken ct = default)
    {
        var cacheKey = $"poi:v2:{center.Lat:F4},{center.Lng:F4}:{query}:{radiusMeters}";
        var cached = await _cache.GetAsync<List<PoiItem>>(cacheKey, ct);
        if (cached is not null) return cached;

        EnsureAkConfigured();

        var pois = new List<PoiItem>();
        const int pageSize = 20;
        const int maxPages = 3;

        for (var page = 0; page < maxPages; page++)
        {
            if (!await _quota.TryConsumeAsync("poi_search", 1, ct))
                break;

            var (pagePois, _) = await FetchIsochronePoiPageAsync(
                center, string.Empty, radiusMeters, query, page, pageSize, useBounds: false, ct);
            if (pagePois.Count == 0)
                break;

            pois.AddRange(pagePois);
            if (pagePois.Count < pageSize)
                break;
        }

        await _cache.SetAsync(cacheKey, pois, TimeSpan.FromDays(7), ct);
        return pois;
    }

    private async Task<GeocodeResult> MockGeocodeAsync(string address, CancellationToken ct)
    {
        var nominatim = await TryNominatimSearchAsync(address, 1, ct);
        if (nominatim.Count == 0)
            nominatim = await TryNominatimSearchAsync($"{address} 中国", 1, ct);
        if (nominatim.Count > 0)
        {
            var r = nominatim[0];
            return new GeocodeResult(r.Lat, r.Lng, r.DisplayName, "wgs84");
        }
        return GetMockCenterFallback(address);
    }

    private async Task<List<PlaceSuggestion>> MockSuggestAsync(string query, string region, CancellationToken ct)
    {
        var searchText = string.IsNullOrWhiteSpace(region) ? query : $"{region}{query}";
        var nominatim = await TryNominatimSearchAsync(searchText, 8, ct);
        if (nominatim.Count == 0)
            nominatim = await TryNominatimSearchAsync(query, 8, ct);
        if (nominatim.Count > 0)
        {
            return nominatim.Select(r => new PlaceSuggestion(
                r.Name,
                r.DisplayName,
                r.Lat,
                r.Lng,
                region,
                r.Type,
                "wgs84")).ToList();
        }
        return GenerateMockSuggestionsFallback(query, region);
    }

    private async Task<List<NominatimResult>> TryNominatimSearchAsync(
        string query, int limit, CancellationToken ct)
    {
        try
        {
            var url =
                $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&limit={limit}&countrycodes=cn&addressdetails=1";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "15m-lifecycle-app/1.0 (demo geocode)");
            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (json.ValueKind != JsonValueKind.Array) return [];

            var results = new List<NominatimResult>();
            foreach (var item in json.EnumerateArray())
            {
                if (!item.TryGetProperty("lat", out var latEl) ||
                    !item.TryGetProperty("lon", out var lonEl))
                    continue;

                var lat = double.Parse(latEl.GetString() ?? "0");
                var lng = double.Parse(lonEl.GetString() ?? "0");
                var name = item.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                    ? nameEl.GetString() ?? query
                    : query;
                var display = item.TryGetProperty("display_name", out var dispEl)
                    ? dispEl.GetString() ?? name
                    : name;
                var type = item.TryGetProperty("type", out var typeEl)
                    ? typeEl.GetString()
                    : null;

                results.Add(new NominatimResult(name, display, lat, lng, type));
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nominatim geocode failed for {Query}", query);
            return [];
        }
    }

    private sealed record NominatimResult(
        string Name, string DisplayName, double Lat, double Lng, string? Type);

    private static List<PlaceSuggestion> GenerateMockSuggestionsFallback(string query, string region)
    {
        var baseLat = region.Contains("上海") ? 31.2304 : 40.0725;
        var baseLng = region.Contains("上海") ? 121.4737 : 116.3365;

        var communities = new[]
        {
            ("龙跃苑一区", "龙跃苑一区", 40.0752, 116.3348),
            ("龙跃苑二区", "龙跃苑二区", 40.0738, 116.3381),
            ("龙跃苑三区", "龙跃苑三区", 40.0715, 116.3412),
            ("龙腾苑", "龙腾苑", 40.0698, 116.3355),
            ("和谐家园", "和谐家园", 40.0745, 116.3298),
            ("流星花园", "流星花园", 40.0771, 116.3425),
            ("东亚上北", "东亚上北鑫座", 40.0689, 116.3378),
            ("国仕汇", "国仕汇", 40.0763, 116.3312),
        };

        var filtered = communities
            .Where(c => string.IsNullOrEmpty(query) || c.Item1.Contains(query, StringComparison.OrdinalIgnoreCase) || c.Item2.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(c => new PlaceSuggestion(
                c.Item1,
                $"{region}{c.Item2}",
                c.Item3,
                c.Item4,
                region,
                "住宅区"))
            .ToList();

        if (filtered.Count == 0)
        {
            filtered.Add(new PlaceSuggestion(
                query,
                $"{region}{query}",
                baseLat,
                baseLng,
                region,
                "住宅区"));
        }

        return filtered;
    }

    private static GeocodeResult GetMockCenterFallback(string address)
    {
        // Default: Beijing Huilongguan area for demo
        return address.Contains("上海", StringComparison.OrdinalIgnoreCase)
            ? new GeocodeResult(31.2304, 121.4737, "上海市人民广场")
            : new GeocodeResult(40.0725, 116.3365, address.Length > 0 ? address : "北京市昌平区回龙观东大街");
    }

    private static List<PoiItem> GenerateMockPois(LatLng center, string query, int radiusMeters)
    {
        var random = new Random(HashCode.Combine(center.Lat, center.Lng, query));
        var maxDist = Math.Min(radiusMeters, 1200);
        var count = random.Next(query switch
        {
            "药店" => 4,
            "小学" => 3,
            "菜市场" => 3,
            "社区医院" => 3,
            _ => 5
        }, query switch
        {
            "药店" => 8,
            "小学" => 6,
            "菜市场" => 6,
            "社区医院" => 6,
            _ => 10
        });

        var pois = new List<PoiItem>();
        for (var i = 0; i < count; i++)
        {
            var angle = random.NextDouble() * 2 * Math.PI;
            var dist = random.Next(150, Math.Max(200, maxDist));
            var offset = OffsetMeters(center, dist, angle);
            pois.Add(new PoiItem(
                $"mock-{query}-{i}",
                $"{query}{i + 1}号",
                query,
                offset.Lat,
                offset.Lng,
                $"距中心约{dist}米",
                dist));
        }

        return pois;
    }

    public static double HaversineMeters(LatLng a, LatLng b)
    {
        const double R = 6371000;
        var dLat = (b.Lat - a.Lat) * Math.PI / 180;
        var dLng = (b.Lng - a.Lng) * Math.PI / 180;
        var lat1 = a.Lat * Math.PI / 180;
        var lat2 = b.Lat * Math.PI / 180;
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Sqrt(h));
    }

    public static LatLng OffsetMeters(LatLng origin, double meters, double bearingRadians)
    {
        const double R = 6371000;
        var lat1 = origin.Lat * Math.PI / 180;
        var lng1 = origin.Lng * Math.PI / 180;
        var lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(meters / R) +
                             Math.Cos(lat1) * Math.Sin(meters / R) * Math.Cos(bearingRadians));
        var lng2 = lng1 + Math.Atan2(
            Math.Sin(bearingRadians) * Math.Sin(meters / R) * Math.Cos(lat1),
            Math.Cos(meters / R) - Math.Sin(lat1) * Math.Sin(lat2));
        return new LatLng(lat2 * 180 / Math.PI, lng2 * 180 / Math.PI);
    }
}
