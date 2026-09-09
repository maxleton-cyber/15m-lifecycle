namespace Lifecycle.Api.Models;

public class BaiduMapSettings
{
    public string Ak { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.map.baidu.com";
    public bool UseMockWhenNoAk { get; set; } = false;
    /// <summary>百度 Web 服务 API QPS 上限（个人开发者通常为 3）</summary>
    public int MaxQps { get; set; } = 3;
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

public class RedisSettings
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public bool UseMemoryFallback { get; set; } = true;
}
