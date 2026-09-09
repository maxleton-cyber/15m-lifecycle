using System.Text.Json;
using System.Text.Json.Serialization;
using Lifecycle.Api.Models;

namespace Lifecycle.Api.Services;

public interface IRegionService
{
    IReadOnlyList<RegionNode> GetProvinces();
    IReadOnlyList<RegionNode> GetChildren(string parentCode);
}

internal sealed class PcaNode
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("children")]
    public List<PcaNode>? Children { get; set; }
}

public class RegionService : IRegionService
{
    private readonly List<PcaNode> _provinces;
    private readonly Dictionary<string, PcaNode> _index = new();

    public RegionService()
    {
        _provinces = LoadPcaData();
        foreach (var province in _provinces)
            IndexTree(province);
    }

    public IReadOnlyList<RegionNode> GetProvinces() =>
        _provinces.Select(p => new RegionNode(p.Code, p.Name, null)).ToList();

    public IReadOnlyList<RegionNode> GetChildren(string parentCode)
    {
        if (!_index.TryGetValue(parentCode, out var node))
            return Array.Empty<RegionNode>();

        var children = node.Children;
        if (children is null || children.Count == 0)
            return Array.Empty<RegionNode>();

        // 直辖市/特别行政区：跳过「市辖区」「县」中间层，直接返回区县
        if (children.Count == 1 && IsIntermediateLevel(children[0].Name))
        {
            return (children[0].Children ?? [])
                .Select(c => new RegionNode(c.Code, c.Name, null))
                .ToList();
        }

        return children.Select(c => new RegionNode(c.Code, c.Name, null)).ToList();
    }

    private static bool IsIntermediateLevel(string name) =>
        name is "市辖区" or "县" or "省直辖县级行政区划";

    private void IndexTree(PcaNode node)
    {
        _index[node.Code] = node;
        if (node.Children is null) return;
        foreach (var child in node.Children)
            IndexTree(child);
    }

    private static List<PcaNode> LoadPcaData()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "pca-code.json");
        if (!File.Exists(path))
            return [];

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<PcaNode>>(json) ?? [];
    }
}
