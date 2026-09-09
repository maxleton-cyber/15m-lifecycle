namespace Lifecycle.Api.Services;

public interface IQuotaGuardService
{
    Task<bool> TryConsumeAsync(string apiName, int count = 1, CancellationToken ct = default);
    Task<IReadOnlyList<Models.QuotaStatus>> GetStatusAsync(CancellationToken ct = default);
    int GetDailyLimit(string apiName);
}

public class QuotaGuardService : IQuotaGuardService
{
    private static readonly Dictionary<string, int> DailyLimits = new()
    {
        ["geocode"] = 5000,
        ["walking_route"] = 5000,
        ["route_matrix"] = 5000,
        ["poi_search"] = 100,
        ["llm"] = 1000
    };

    private readonly IRedisCacheService _cache;
    private readonly Dictionary<string, int> _memoryUsage = new();
    private readonly object _lock = new();

    public QuotaGuardService(IRedisCacheService cache) => _cache = cache;

    public int GetDailyLimit(string apiName) =>
        DailyLimits.TryGetValue(apiName, out var limit) ? limit : 1000;

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

    public async Task<IReadOnlyList<Models.QuotaStatus>> GetStatusAsync(CancellationToken ct = default)
    {
        var result = new List<Models.QuotaStatus>();

        foreach (var (apiName, limit) in DailyLimits)
        {
            var key = $"quota:{apiName}:{DateTime.UtcNow:yyyyMMdd}";
            var used = await _cache.GetAsync<int>(key, ct);

            lock (_lock)
            {
                if (used == 0 && _memoryUsage.TryGetValue(key, out var memUsed))
                    used = memUsed;
            }

            result.Add(new Models.QuotaStatus(apiName, limit, used, Math.Max(0, limit - used)));
        }

        return result;
    }
}
