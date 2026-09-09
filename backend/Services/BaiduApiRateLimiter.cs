using Lifecycle.Api.Models;
using Microsoft.Extensions.Options;

namespace Lifecycle.Api.Services;

public interface IBaiduApiRateLimiter
{
    /// <summary>等待直到允许发起下一次百度 API 请求（滑动窗口 QPS 限制）。</summary>
    Task AcquireAsync(CancellationToken ct = default);
}

/// <summary>
/// 百度 Web 服务 API 全局限流。个人开发者默认 3 QPS，所有 BaiduMapClient 请求经此排队。
/// </summary>
public sealed class BaiduApiRateLimiter : IBaiduApiRateLimiter
{
    private readonly int _maxQps;
    private readonly Queue<DateTime> _timestamps = new();
    private readonly object _lock = new();

    public BaiduApiRateLimiter(IOptions<BaiduMapSettings> settings)
    {
        _maxQps = Math.Max(1, settings.Value.MaxQps);
    }

    public async Task AcquireAsync(CancellationToken ct = default)
    {
        while (true)
        {
            TimeSpan wait;
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                PruneOlderThanOneSecond(now);

                if (_timestamps.Count < _maxQps)
                {
                    _timestamps.Enqueue(now);
                    return;
                }

                wait = _timestamps.Peek().AddSeconds(1) - now;
                if (wait < TimeSpan.FromMilliseconds(50))
                    wait = TimeSpan.FromMilliseconds(50);
            }

            await Task.Delay(wait, ct);
        }
    }

    private void PruneOlderThanOneSecond(DateTime now)
    {
        while (_timestamps.Count > 0 && (now - _timestamps.Peek()).TotalSeconds >= 1)
            _timestamps.Dequeue();
    }
}
