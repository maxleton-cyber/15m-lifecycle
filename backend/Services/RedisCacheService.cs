namespace Lifecycle.Api.Services;

public interface IRedisCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}

public class RedisCacheService : IRedisCacheService
{
    private readonly StackExchange.Redis.IConnectionMultiplexer? _redis;
    private readonly Dictionary<string, (string Json, DateTime Expires)> _memory = new();
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        StackExchange.Redis.IConnectionMultiplexer? redis,
        ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        if (_redis is not null)
        {
            try
            {
                var db = _redis.GetDatabase();
                var value = await db.StringGetAsync(key);
                if (value.HasValue)
                    return System.Text.Json.JsonSerializer.Deserialize<T>(value!);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis read failed for key {Key}, falling back to memory", key);
            }
        }

        if (_memory.TryGetValue(key, out var entry) && entry.Expires > DateTime.UtcNow)
            return System.Text.Json.JsonSerializer.Deserialize<T>(entry.Json);

        return default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        var ttl = expiry ?? TimeSpan.FromHours(24);

        if (_redis is not null)
        {
            try
            {
                var db = _redis.GetDatabase();
                await db.StringSetAsync(key, json, ttl);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis write failed for key {Key}, falling back to memory", key);
            }
        }

        _memory[key] = (json, DateTime.UtcNow.Add(ttl));
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        if (_redis is not null)
        {
            try
            {
                var db = _redis.GetDatabase();
                return await db.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis exists check failed for key {Key}", key);
            }
        }

        return _memory.TryGetValue(key, out var entry) && entry.Expires > DateTime.UtcNow;
    }
}
