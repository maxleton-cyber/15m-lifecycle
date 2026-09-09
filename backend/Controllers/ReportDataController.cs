using Lifecycle.Api.Models;
using Lifecycle.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lifecycle.Api.Controllers;

[ApiController]
[Route("api/report-data")]
public class ReportDataController : ControllerBase
{
    private readonly IAgentTaskStore _store;

    public ReportDataController(IAgentTaskStore store) => _store = store;

    [HttpGet]
    public async Task<ActionResult<ReportData>> Get(
        [FromQuery] string? taskId,
        [FromQuery] string? polygonId,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(taskId))
        {
            var task = await _store.GetAsync(taskId, ct);
            if (task?.ReportData is not null)
                return Ok(task.ReportData);
            return NotFound();
        }

        return BadRequest("taskId is required");
    }
}
