using System.Text.Json;
using Lifecycle.Api.Models;
using Lifecycle.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lifecycle.Api.Controllers;

[ApiController]
[Route("api/agent/tasks")]
public class AgentController : ControllerBase
{
    private readonly IAgentTaskQueue _queue;
    private readonly IAgentTaskStore _store;

    public AgentController(IAgentTaskQueue queue, IAgentTaskStore store)
    {
        _queue = queue;
        _store = store;
    }

    [HttpPost]
    public async Task<ActionResult<CreateTaskResponse>> CreateTask(
        [FromBody] AgentTaskRequest request,
        CancellationToken ct)
    {
        var taskId = Guid.NewGuid().ToString("N");
        var initial = new AgentTaskResult(
            taskId,
            AgentTaskStatus.Queued,
            new AgentTaskProgress("queued", "任务已排队"),
            null,
            null,
            null,
            DateTime.UtcNow,
            null);

        await _store.SaveAsync(initial, ct);
        await _queue.EnqueueAsync(new AgentTaskMessage(taskId, request), ct);

        return Accepted(new CreateTaskResponse(taskId));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AgentTaskResult>> GetTask(string id, CancellationToken ct)
    {
        var task = await _store.GetAsync(id, ct);
        if (task is null) return NotFound();
        return Ok(task);
    }

    [HttpGet("{id}/stream")]
    public async Task StreamTask(string id, CancellationToken ct)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        while (!ct.IsCancellationRequested)
        {
            var task = await _store.GetAsync(id, ct);
            if (task is null)
            {
                await WriteSseAsync("error", new { message = "task not found" }, ct);
                break;
            }

            await WriteSseAsync("progress", task, ct);

            if (task.Status is AgentTaskStatus.Completed or AgentTaskStatus.Failed)
                break;

            await Task.Delay(1000, ct);
        }
    }

    private async Task WriteSseAsync(string eventName, object data, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(data);
        await Response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}

[ApiController]
[Route("api/quota")]
public class QuotaController : ControllerBase
{
    private readonly IQuotaGuardService _quota;

    public QuotaController(IQuotaGuardService quota) => _quota = quota;

    [HttpGet("status")]
    public async Task<ActionResult<IReadOnlyList<QuotaStatus>>> Status(CancellationToken ct) =>
        Ok(await _quota.GetStatusAsync(ct));
}
