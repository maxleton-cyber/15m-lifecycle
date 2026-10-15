namespace Lifecycle.Api.Models;

public class BaiduMapSettings
{
    public string Ak { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.map.baidu.com";
    public bool UseMockWhenNoAk { get; set; } = false;
    /// <summary>通用百度 Web 服务 API QPS 上限（地理编码、地点检索等）</summary>
    public int MaxQps { get; set; } = 3;
    /// <summary>步行批量算路峰值并发/QPS 上限，超量时分批请求</summary>
    public int RouteMatrixMaxQps { get; set; } = 30;
    /// <summary>批量算路相邻批次间隔（毫秒）</summary>
    public int RouteMatrixBatchIntervalMs { get; set; } = 300;
    /// <summary>单次批量算路请求最多终点数（百度上限 50）</summary>
    public int RouteMatrixMaxDestinationsPerRequest { get; set; } = 50;
    /// <summary>地点检索（含输入提示）日配额</summary>
    public int PoiSearchDailyLimit { get; set; } = 5000;
    /// <summary>地理编码日配额</summary>
    public int GeocodeDailyLimit { get; set; } = 5000;
    /// <summary>步行批量算路日配额</summary>
    public int RouteMatrixDailyLimit { get; set; } = 5000;
}

public class LlmSettings
{
    /// <summary>OpenAI 兼容 API 根地址，如 https://api.openai.com/v1</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>模型名称，由用户自行配置，如 gpt-4o、deepseek-chat、qwen-plus 等</summary>
    public string Model { get; set; } = string.Empty;

    public bool UseMockWhenNoKey { get; set; } = true;

    public double Temperature { get; set; } = 0.3;
}

public class AgentSettings
{
    public int MaxConcurrency { get; set; } = 1;
    /// <summary>等时圈采样方向数（建议 24）</summary>
    public int IsochroneSampleDirections { get; set; } = 24;
    public int IsochroneMinutes { get; set; } = 15;
    /// <summary>百度步行 API 参考速度 m/s，用于等时圈二分查找距离上界估算</summary>
    public double MapApiWalkingSpeedMps { get; set; } = 1.2;
    /// <summary>每个方向二分查找迭代次数</summary>
    public int IsochroneBinarySearchIterations { get; set; } = 6;
}

/// <summary>盲区网格与可达判定配置（见 15min-life-circle-blind-area-spec.md）</summary>
public class BlindAreaSettings
{
    public double GridSizeMeters { get; set; } = 100;
    public double MinInsideRatio { get; set; } = 0.5;
    public double DefaultReachThresholdMeters { get; set; } = 1000;
    /// <summary>降级估算：直线距离 × 绕行系数 ≈ 步行距离</summary>
    public double DetourFactor { get; set; } = 1.3;
}

public class RedisSettings
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public bool UseMemoryFallback { get; set; } = true;
}
