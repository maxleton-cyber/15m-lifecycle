using Lifecycle.Api.Models;
using Lifecycle.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lifecycle.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GeocodeController : ControllerBase
{
    private readonly IBaiduMapClient _map;
    private readonly IRegionService _regions;

    public GeocodeController(IBaiduMapClient map, IRegionService regions)
    {
        _map = map;
        _regions = regions;
    }

    [HttpGet]
    public async Task<ActionResult<GeocodeResult>> Geocode([FromQuery] string address, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(address))
            return BadRequest("address is required");

        var result = await _map.GeocodeAsync(address, ct);
        return Ok(result);
    }

    [HttpGet("suggest")]
    public async Task<ActionResult<IReadOnlyList<PlaceSuggestion>>> Suggest(
        [FromQuery] string query,
        [FromQuery] string? region,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Ok(Array.Empty<PlaceSuggestion>());

        try
        {
            var result = await _map.SuggestPlacesAsync(query, region ?? "", ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("regions")]
    public ActionResult<IReadOnlyList<RegionNode>> Regions([FromQuery] string? parent)
    {
        if (string.IsNullOrWhiteSpace(parent))
            return Ok(_regions.GetProvinces());

        return Ok(_regions.GetChildren(parent));
    }
}
