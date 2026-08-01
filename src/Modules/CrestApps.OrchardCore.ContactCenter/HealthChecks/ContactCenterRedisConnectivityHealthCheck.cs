using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrchardCore.Redis;

namespace CrestApps.OrchardCore.ContactCenter.HealthChecks;

/// <summary>
/// Pings the Redis connection shared by the distributed lock and the SignalR backplane. It reports healthy with
/// nothing probed when Redis is not enabled, because a deployment that declares no Redis dependency has none to
/// be unhealthy about; the topology validator, not this probe, decides whether Redis is required.
/// </summary>
public sealed class ContactCenterRedisConnectivityHealthCheck : IHealthCheck
{
    private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(2);

    private readonly IRedisService _redisService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterRedisConnectivityHealthCheck"/> class.
    /// </summary>
    /// <param name="redisService">The optional Redis service; <see langword="null"/> when Redis is not enabled.</param>
    public ContactCenterRedisConnectivityHealthCheck(IRedisService redisService = null)
    {
        _redisService = redisService;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_redisService is null)
        {
            return HealthCheckResult.Healthy("Redis is not enabled; there is no Redis dependency to probe.");
        }

        using var timeoutCts = new CancellationTokenSource(_probeTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var deadline = linkedCts.Token;

        try
        {
            await _redisService.ConnectAsync().WaitAsync(deadline);

            if (_redisService.Database is null)
            {
                return new HealthCheckResult(
                    context.Registration.FailureStatus,
                    "The Redis connection is enabled but no database could be resolved.");
            }

            var latency = await _redisService.Database.PingAsync().WaitAsync(deadline);

            return HealthCheckResult.Healthy($"Redis is reachable ({latency.TotalMilliseconds:F0} ms).");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "The Redis connectivity probe did not complete within the probe timeout.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "The Redis connection shared by the distributed lock and the SignalR backplane is unreachable.",
                ex);
        }
    }
}
