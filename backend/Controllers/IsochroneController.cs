using Lifecycle.Api.Models;
using Lifecycle.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lifecycle.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IsochroneController : ControllerBase
{
    private readonly IIsochroneService _isochrone;

    public IsochroneController(IIsochroneService isochrone) => _isochrone = isochrone;

    [HttpGet]
    public async Task<ActionResult<IsochroneResult>> Get(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] int minutes = 15,
        CancellationToken ct = default)
    {
        var result = await _isochrone.ComputeAsync(new LatLng(lat, lng), minutes, ct);
        return Ok(result);
    }
}
