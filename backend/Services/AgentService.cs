using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Lifecycle.Api.Models;
using Microsoft.Extensions.Options;

namespace Lifecycle.Api.Services;

public interface IAgentService
{
    Task<AgentTaskResult> ProcessTaskAsync(
        string taskId,
        AgentTaskRequest request,
        Action<AgentTaskProgress>? onProgress = null,
        CancellationToken ct = default);
}

public class AgentService : IAgentService
{
    private readonly IBaiduMapClient _map;
    private readonly IIsochroneService _isochrone;
    private readonly IPoiCoverageService _poiCoverage;
    private readonly IQuotaGuardService _quota;
    private readonly HttpClient _http;
    private readonly LlmSettings _llmSettings;
    private readonly AgentSettings _agentSettings;
    private readonly ILogger<AgentService> _logger;

    public AgentService(
        IBaiduMapClient map,
        IIsochroneService isochrone,
        IPoiCoverageService poiCoverage,
        IQuotaGuardService quota,
        HttpClient http,
        IOptions<LlmSettings> llmSettings,
        IOptions<AgentSettings> agentSettings,
        ILogger<AgentService> logger)
    {
        _map = map;
        _isochrone = isochrone;
        _poiCoverage = poiCoverage;
        _quota = quota;
        _http = http;
        _llmSettings = llmSettings.Value;
        _agentSettings = agentSettings.Value;
        _logger = logger;
    }

    public async Task<AgentTaskResult> ProcessTaskAsync(
        string taskId,
        AgentTaskRequest request,
        Action<AgentTaskProgress>? onProgress = null,
        CancellationToken ct = default)
    {
        try
        {
            onProgress?.Invoke(new AgentTaskProgress("parsing", "正在解析分析目标…"));
            var center = await ResolveCenterAsync(request, ct);

            onProgress?.Invoke(new AgentTaskProgress("isochrone", "正在基于真实路网计算15分钟步行等时圈…"));
            var isochrone = await _isochrone.ComputeAsync(center, _agentSettings.IsochroneMinutes, ct);

            onProgress?.Invoke(new AgentTaskProgress("poi", "正在检索圈内民生设施…"));
            var reportData = await _poiCoverage.BuildReportAsync(center, isochrone, ct);

            onProgress?.Invoke(new AgentTaskProgress("report", "正在生成AI体检报告…"));
            var aiReport = await GenerateReportAsync(reportData, ct);

            return new AgentTaskResult(
                taskId,
                AgentTaskStatus.Completed,
                new AgentTaskProgress("done", "分析完成"),
                reportData,
                aiReport,
                null,
                DateTime.UtcNow,
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent task {TaskId} failed", taskId);
            return new AgentTaskResult(
                taskId,
                AgentTaskStatus.Failed,
                null,
                null,
                null,
                ex.Message,
                DateTime.UtcNow,
                DateTime.UtcNow);
        }
    }

    private async Task<LatLng> ResolveCenterAsync(AgentTaskRequest request, CancellationToken ct)
    {
        if (request.Lat is not null && request.Lng is not null)
            return new LatLng(request.Lat.Value, request.Lng.Value);

        var query = request.Address
            ?? request.NaturalLanguageQuery
            ?? throw new ArgumentException("请提供坐标、地址或自然语言描述");

        var geocoded = await _map.GeocodeAsync(query, ct);
        return new LatLng(geocoded.Lat, geocoded.Lng);
    }

    private async Task<string> GenerateReportAsync(ReportData data, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_llmSettings.ApiKey) && _llmSettings.UseMockWhenNoKey)
            return GenerateTemplateReport(data);

        if (string.IsNullOrWhiteSpace(_llmSettings.ApiBaseUrl) ||
            string.IsNullOrWhiteSpace(_llmSettings.Model))
        {
            _logger.LogWarning(
                "LLM 未完整配置（需设置 ApiBaseUrl、ApiKey、Model），使用模板报告");
            return GenerateTemplateReport(data);
        }

        if (!await _quota.TryConsumeAsync("llm", 1, ct))
            return GenerateTemplateReport(data) + "\n\n（LLM配额受限，以上为模板报告）";

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                center = data.Center,
                overallScore = data.OverallScore,
                categories = data.Categories.Select(c => new
                {
                    c.Label,
                    c.Count,
                    c.Score,
                    c.IsBlindSpot,
                    c.NearestDistanceMeters
                }),
                blindSpots = data.BlindSpots.Where(b => b.IsBlindSpot)
            });

            var body = new
            {
                model = _llmSettings.Model,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = """
                            你是15分钟生活圈规划助手。仅基于提供的结构化数据生成体检报告。
                            禁止编造未在数据中出现的设施。输出分三段：总体摘要、分类点评、改善建议。
                            所有数字必须与输入数据一致。
                            """
                    },
                    new { role = "user", content = payload }
                },
                temperature = _llmSettings.Temperature
            };

            var endpoint = _llmSettings.ApiBaseUrl.TrimEnd('/') + "/chat/completions";
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Authorization", $"Bearer {_llmSettings.ApiKey}");

            var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var content = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            if (string.IsNullOrWhiteSpace(content) || !ValidateReport(content, data))
                return GenerateTemplateReport(data);

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM report generation failed, using template");
            return GenerateTemplateReport(data);
        }
    }

    private static bool ValidateReport(string report, ReportData data)
    {
        var scoreStr = data.OverallScore.ToString("F0");
        return report.Contains(scoreStr) || report.Contains(data.OverallScore.ToString("F1"));
    }

    private static string GenerateTemplateReport(ReportData data)
    {
        var blind = data.BlindSpots.Where(b => b.IsBlindSpot).ToList();
        var sb = new StringBuilder();

        sb.AppendLine("## 总体摘要");
        sb.AppendLine($"该区域15分钟步行生活圈综合评分 {data.OverallScore:F1}/100。");
        if (blind.Count > 0)
            sb.AppendLine($"发现 {blind.Count} 类设施存在覆盖盲区，建议优先补齐。");
        else
            sb.AppendLine("主要民生设施覆盖较为均衡。");

        sb.AppendLine();
        sb.AppendLine("## 分类点评");
        foreach (var cat in data.Categories)
        {
            var status = cat.IsBlindSpot ? "不足" : "达标";
            sb.AppendLine($"- **{cat.Label}**：圈内 {cat.Count} 处，评分 {cat.Score:F0}，{status}");
        }

        sb.AppendLine();
        sb.AppendLine("## 改善建议");
        if (blind.Count == 0)
        {
            sb.AppendLine("- 维持现有设施布局，关注老龄化社区的服务升级需求。");
        }
        else
        {
            foreach (var spot in blind)
                sb.AppendLine($"- {spot.Suggestion}");
        }

        return sb.ToString();
    }
}
