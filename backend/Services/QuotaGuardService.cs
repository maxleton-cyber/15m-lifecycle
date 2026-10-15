using Lifecycle.Api.Models;
using Microsoft.Extensions.Options;

namespace Lifecycle.Api.Services;

public interface IQuotaGuardService
{
    Task<bool> TryConsumeAsync(string apiName, int count = 1, CancellationToken ct = default);
    Task<IReadOnlyList<QuotaStatus>> GetStatusAsync(CancellationToken ct = default);
    int GetDailyLimit(string apiName);
}

public class QuotaGuardService : IQuotaGuardService
{
    private readonly Dictionary<string, int> _dailyLimits;
    private readonly IRedisCacheService _cache;
    private readonly Dictionary<string, int> _memoryUsage = new();
    private readonly object _lock = new();

    public QuotaGuardService(IRedisCacheService cache, IOptions<BaiduMapSettings> baidu)
    {
        _cache = cache;
        var map = baidu.Value;
        _dailyLimits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["geocode"] = Math.Max(1, map.GeocodeDailyLimit),
            ["walking_route"] = Math.Max(1, map.RouteMatrixDailyLimit),
            ["route_matrix"] = Math.Max(1, map.RouteMatrixDailyLimit),
            ["poi_search"] = Math.Max(1, map.PoiSearchDailyLimit),
            ["llm"] = 1000,
        };
    }

    public int GetDailyLimit(string apiName) =>
        _dailyLimits.TryGetValue(apiName, out var limit) ? limit : 1000;

    public async Task<bool> TryConsumeAsync(string apiName, int count = 1, CancellationToken ct = default)
    {
        var key = $"quota:{apiName}:{DateTime.UtcNow:yyyyMMdd}";
        var limit = GetDailyLimit(apiName);

        var cached = await _cache.GetAsync<int>(key, ct);
        var used = cached;

        lock (_lock)
        {
            if (used == 0 && _memoryUsage.TryGetValue(key, out var memUsed))
                used = memUsed;
        }

        if (used + count > limit)
            return false;

        var newUsed = used + count;
        await _cache.SetAsync(key, newUsed, TimeSpan.FromDays(2), ct);

        lock (_lock)
            _memoryUsage[key] = newUsed;

        return true;
    }

    public async Task<IReadOnlyList<QuotaStatus>> GetStatusAsync(CancellationToken ct = default)
    {
        var result = new List<QuotaStatus>();

        foreach (var (apiName, limit) in _dailyLimits)
        {
            var key = $"quota:{apiName}:{DateTime.UtcNow:yyyyMMdd}";
            var used = await _cache.GetAsync<int>(key, ct);

            lock (_lock)
            {
                if (used == 0 && _memoryUsage.TryGetValue(key, out var memUsed))
                    used = memUsed;
            }

            result.Add(new QuotaStatus(apiName, limit, used, Math.Max(0, limit - used)));
        }

        return result;
    }
}
