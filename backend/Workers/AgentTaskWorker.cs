using Lifecycle.Api.Models;
using Lifecycle.Api.Services;
using Microsoft.Extensions.Options;

namespace Lifecycle.Api.Workers;

public class AgentTaskWorker : BackgroundService
{
    private readonly IAgentTaskQueue _queue;
    private readonly IAgentService _agent;
    private readonly IAgentTaskStore _store;
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger<AgentTaskWorker> _logger;

    public AgentTaskWorker(
        IAgentTaskQueue queue,
        IAgentService agent,
        IAgentTaskStore store,
        IOptions<AgentSettings> settings,
        ILogger<AgentTaskWorker> logger)
    {
        _queue = queue;
        _agent = agent;
        _store = store;
        _semaphore = new SemaphoreSlim(settings.Value.MaxConcurrency);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.ReadAllAsync(stoppingToken))
        {
            await _semaphore.WaitAsync(stoppingToken);

            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessMessageAsync(message, stoppingToken);
                }
                finally
                {
                    _semaphore.Release();
                }
            }, stoppingToken);
        }
    }

    private async Task ProcessMessageAsync(AgentTaskMessage message, CancellationToken ct)
    {
        _logger.LogInformation("Processing agent task {TaskId}", message.TaskId);

        await _store.SaveAsync(new AgentTaskResult(
            message.TaskId,
            AgentTaskStatus.Processing,
            new AgentTaskProgress("queued", "任务已开始，准备执行…"),
            null,
            null,
            null,
            DateTime.UtcNow,
            null), ct);

        var result = await _agent.ProcessTaskAsync(
            message.TaskId,
            message.Request,
            progress =>
            {
                _ = _store.SaveAsync(new AgentTaskResult(
                    message.TaskId,
                    AgentTaskStatus.Processing,
                    progress,
                    null,
                    null,
                    null,
                    DateTime.UtcNow,
                    null), ct);
            },
            ct);

        await _store.SaveAsync(result, ct);
    }
}
