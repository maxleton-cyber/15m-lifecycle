namespace Lifecycle.Api.Models;

public record LatLng(double Lat, double Lng);

public record GeocodeResult(double Lat, double Lng, string FormattedAddress, string CoordSystem = "bd09");

public record PlaceSuggestion(
    string Name,
    string Address,
    double Lat,
    double Lng,
    string? District,
    string? Tag,
    string CoordSystem = "bd09");

public record RegionNode(string Code, string Name, IReadOnlyList<RegionNode>? Children);

public record IsochroneResult(
    string PolygonId,
    LatLng Center,
    int Minutes,
    IReadOnlyList<LatLng> Polygon,
    double RadiusMeters);

public record PoiItem(
    string Id,
    string Name,
    string Category,
    double Lat,
    double Lng,
    string Address,
    double DistanceMeters,
    string? Tag = null);

public record CategoryCoverage(
    string Category,
    string Label,
    int Count,
    double Score,
    bool IsBlindSpot,
    double? NearestDistanceMeters,
    IReadOnlyList<PoiItem> Pois);

public record BlindSpotInfo(
    string Category,
    string Label,
    bool IsBlindSpot,
    double? NearestDistanceMeters,
    string Suggestion);

/// <summary>地图盲区多边形块（灰度 level = 缺失类别数）。</summary>
public record BlindAreaRegion(
    string Id,
    int Level,
    IReadOnlyList<string> MissingCategories,
    IReadOnlyList<LatLng> Polygon,
    double AreaM2,
    bool Degraded);

public record BlindAreaAnalysis(
    IReadOnlyList<BlindAreaRegion> BlindRegions,
    double BlindAreaRatio,
    double AverageMissingCount);

public record ReportData(
    string PolygonId,
    LatLng Center,
    IsochroneResult Isochrone,
    IReadOnlyList<CategoryCoverage> Categories,
    IReadOnlyList<BlindSpotInfo> BlindSpots,
    double OverallScore,
    BlindAreaAnalysis? BlindArea = null);

public record QuotaStatus(
    string ApiName,
    int DailyLimit,
    int Used,
    int Remaining);

public enum AgentTaskStatus
{
    Queued,
    Processing,
    Completed,
    Failed
}

public record AgentTaskRequest(
    double? Lat,
    double? Lng,
    string? Address,
    string? NaturalLanguageQuery);

public record AgentTaskProgress(string Stage, string Message);

public record AgentTaskResult(
    string TaskId,
    AgentTaskStatus Status,
    AgentTaskProgress? Progress,
    ReportData? ReportData,
    string? AiReport,
    string? Error,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public record CreateTaskResponse(string TaskId);

public record AiReportSections(
    string Summary,
    string CategoryReview,
    string Suggestions);
