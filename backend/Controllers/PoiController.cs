using Lifecycle.Api.Models;
using Lifecycle.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lifecycle.Api.Controllers;

[ApiController]
[Route("api/poi")]
public class PoiController : ControllerBase
{
    private readonly IIsochroneService _isochrone;
    private readonly IPoiCoverageService _poiCoverage;

    public PoiController(IIsochroneService isochrone, IPoiCoverageService poiCoverage)
    {
        _isochrone = isochrone;
        _poiCoverage = poiCoverage;
    }

    [HttpGet("coverage")]
    public async Task<ActionResult<ReportData>> Coverage(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] string? polygonId = null,
        CancellationToken ct = default)
    {
        var center = new LatLng(lat, lng);
        var isochrone = await _isochrone.ComputeAsync(center, 15, ct);
        var report = await _poiCoverage.BuildReportAsync(center, isochrone, ct);
        return Ok(report);
    }
}
