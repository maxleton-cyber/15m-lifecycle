namespace Lifecycle.Api.Configuration;

/// <summary>
/// 加载项目根目录 .env，与 docker-compose 环境变量命名保持一致。
/// 仅在对应 ASP.NET 配置项尚未设置时写入，避免覆盖已有环境变量。
/// </summary>
public static class EnvFileLoader
{
    private static readonly (string EnvKey, string ConfigKey)[] Mappings =
    [
        ("BAIDU_MAP_AK", "BaiduMap__Ak"),
        ("BAIDU_MAP_MAX_QPS", "BaiduMap__MaxQps"),
        ("BAIDU_ROUTE_MATRIX_MAX_QPS", "BaiduMap__RouteMatrixMaxQps"),
        ("BAIDU_ROUTE_MATRIX_BATCH_INTERVAL_MS", "BaiduMap__RouteMatrixBatchIntervalMs"),
        ("BAIDU_POI_SEARCH_DAILY_LIMIT", "BaiduMap__PoiSearchDailyLimit"),
        ("BAIDU_GEOCODE_DAILY_LIMIT", "BaiduMap__GeocodeDailyLimit"),
        ("BAIDU_ROUTE_MATRIX_DAILY_LIMIT", "BaiduMap__RouteMatrixDailyLimit"),
        ("LLM_API_BASE_URL", "Llm__ApiBaseUrl"),
        ("LLM_API_KEY", "Llm__ApiKey"),
        ("LLM_MODEL", "Llm__Model"),
        ("REDIS_CONNECTION_STRING", "Redis__ConnectionString"),
        ("AGENT_MAX_CONCURRENCY", "Agent__MaxConcurrency"),
    ];

    public static void LoadFromProjectRoot(string contentRootPath)
    {
        var dir = contentRootPath;
        for (var depth = 0; depth < 5; depth++)
        {
            var path = Path.Combine(dir, ".env");
            if (File.Exists(path))
            {
                ApplyFile(path);
                return;
            }

            var parent = Path.GetFullPath(Path.Combine(dir, ".."));
            if (parent == dir) break;
            dir = parent;
        }
    }

    private static void ApplyFile(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
                value = value[1..^1];

            if (string.IsNullOrEmpty(value)) continue;

            foreach (var (envKey, configKey) in Mappings)
            {
                if (!string.Equals(key, envKey, StringComparison.OrdinalIgnoreCase)) continue;
                SetIfEmpty(configKey, value);
                break;
            }
        }
    }

    private static void SetIfEmpty(string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key))) return;
        Environment.SetEnvironmentVariable(key, value);
    }
}
