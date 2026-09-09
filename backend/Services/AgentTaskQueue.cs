using System.Threading.Channels;
using Lifecycle.Api.Models;

namespace Lifecycle.Api.Services;

public record AgentTaskMessage(string TaskId, AgentTaskRequest Request);

public interface IAgentTaskQueue
{
    ValueTask EnqueueAsync(AgentTaskMessage message, CancellationToken ct = default);
    IAsyncEnumerable<AgentTaskMessage> ReadAllAsync(CancellationToken ct = default);
}

public class AgentTaskQueue : IAgentTaskQueue
{
    private readonly Channel<AgentTaskMessage> _channel =
        Channel.CreateBounded<AgentTaskMessage>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    public ValueTask EnqueueAsync(AgentTaskMessage message, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(message, ct);

    public IAsyncEnumerable<AgentTaskMessage> ReadAllAsync(CancellationToken ct = default) =>
        _channel.Reader.ReadAllAsync(ct);
}

public interface IAgentTaskStore
{
    Task SaveAsync(AgentTaskResult task, CancellationToken ct = default);
    Task<AgentTaskResult?> GetAsync(string taskId, CancellationToken ct = default);
}

public class AgentTaskStore : IAgentTaskStore
{
    private readonly IRedisCacheService _cache;

    public AgentTaskStore(IRedisCacheService cache) => _cache = cache;

    public async Task SaveAsync(AgentTaskResult task, CancellationToken ct = default) =>
        await _cache.SetAsync($"agent-task:{task.TaskId}", task, TimeSpan.FromDays(1), ct);

    public async Task<AgentTaskResult?> GetAsync(string taskId, CancellationToken ct = default) =>
        await _cache.GetAsync<AgentTaskResult>($"agent-task:{taskId}", ct);
}
