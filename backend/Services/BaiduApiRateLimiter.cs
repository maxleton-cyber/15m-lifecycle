using Lifecycle.Api.Models;
using Microsoft.Extensions.Options;

namespace Lifecycle.Api.Services;

public interface IBaiduApiRateLimiter
{
    /// <summary>通用百度 API 限流（地理编码、地点检索等）。</summary>
    Task AcquireAsync(CancellationToken ct = default);

    /// <summary>步行批量算路专用限流，峰值不超过 RouteMatrixMaxQps。</summary>
    Task AcquireRouteMatrixAsync(CancellationToken ct = default);
}

/// <summary>
/// 百度 Web 服务 API 限流：通用通道与批量算路通道分开控制。
/// </summary>
public sealed class BaiduApiRateLimiter : IBaiduApiRateLimiter
{
    private readonly SlidingWindowGate _general;
    private readonly SlidingWindowGate _routeMatrix;

    public BaiduApiRateLimiter(IOptions<BaiduMapSettings> settings)
    {
        var s = settings.Value;
        _general = new SlidingWindowGate(Math.Max(1, s.MaxQps));
        _routeMatrix = new SlidingWindowGate(Math.Max(1, s.RouteMatrixMaxQps));
    }

    public Task AcquireAsync(CancellationToken ct = default) =>
        _general.AcquireAsync(ct);

    public Task AcquireRouteMatrixAsync(CancellationToken ct = default) =>
        _routeMatrix.AcquireAsync(ct);

    private sealed class SlidingWindowGate
    {
        private readonly int _maxQps;
        private readonly Queue<DateTime> _timestamps = new();
        private readonly object _lock = new();

        public SlidingWindowGate(int maxQps) => _maxQps = maxQps;

        public async Task AcquireAsync(CancellationToken ct)
        {
            while (true)
            {
                TimeSpan wait;
                lock (_lock)
                {
                    var now = DateTime.UtcNow;
                    while (_timestamps.Count > 0 && (now - _timestamps.Peek()).TotalSeconds >= 1)
                        _timestamps.Dequeue();

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
    }
}
