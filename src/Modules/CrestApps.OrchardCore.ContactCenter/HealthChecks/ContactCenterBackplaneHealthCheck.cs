using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Redis;
using StackExchange.Redis;

namespace CrestApps.OrchardCore.ContactCenter.HealthChecks;

/// <summary>
/// Proves the SignalR Redis backplane by publishing a token on a dedicated, invocation-unique, tenant-qualified
/// channel and waiting to receive it back within a bounded time. Redis connectivity alone does not prove the
/// backplane works: a pub/sub round-trip is the only signal that a message published on one node would reach
/// subscribers on another. It is registered only when the <c>OrchardCore.Redis</c> feature is enabled.
/// </summary>
public sealed class ContactCenterBackplaneHealthCheck : IHealthCheck
{
    private static readonly TimeSpan _roundTripTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _cleanupTimeout = TimeSpan.FromSeconds(1);

    private readonly IRedisService _redisService;
    private readonly RedisOptions _redisOptions;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterBackplaneHealthCheck"/> class.
    /// </summary>
    /// <param name="redisOptions">The Redis options, read for the instance prefix used to qualify the channel.</param>
    /// <param name="shellSettings">The tenant shell settings, used to qualify the channel per tenant.</param>
    /// <param name="redisService">The Redis service to probe.</param>
    public ContactCenterBackplaneHealthCheck(
        IOptions<RedisOptions> redisOptions,
        ShellSettings shellSettings,
        IRedisService redisService)
    {
        _redisOptions = redisOptions.Value;
        _tenantName = shellSettings.Name;
        _redisService = redisService;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(_roundTripTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var deadline = linkedCts.Token;

        ISubscriber subscriber = null;
        var channel = RedisChannel.Literal(
            $"{_redisOptions.InstancePrefix}{_tenantName}:ContactCenter:HealthCheck:{Guid.NewGuid():N}");
        var subscribed = false;

        try
        {
            await _redisService.ConnectAsync().WaitAsync(deadline);

            if (_redisService.Connection is null || !_redisService.Connection.IsConnected)
            {
                return new HealthCheckResult(
                    context.Registration.FailureStatus,
                    "The Redis connection backing the SignalR backplane is not connected.");
            }

            subscriber = _redisService.Connection.GetSubscriber();
            var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Mark cleanup required before awaiting: WaitAsync abandons the await on timeout but does not cancel the
            // underlying subscription, so the handler on this invocation-unique channel must be torn down regardless.
            subscribed = true;
            await subscriber.SubscribeAsync(channel, (_, _) => received.TrySetResult(true)).WaitAsync(deadline);

            await subscriber.PublishAsync(channel, RedisValue.EmptyString).WaitAsync(deadline);
            await received.Task.WaitAsync(deadline);

            return HealthCheckResult.Healthy("The SignalR backplane publish/subscribe round-trip succeeded.");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "The SignalR backplane publish/subscribe round-trip did not complete within the probe timeout.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "The SignalR backplane publish/subscribe round-trip failed.",
                ex);
        }
        finally
        {
            if (subscribed && subscriber is not null)
            {
                try
                {
                    using var cleanupCts = new CancellationTokenSource(_cleanupTimeout);

                    await subscriber.UnsubscribeAsync(channel).WaitAsync(cleanupCts.Token);
                }
                catch (Exception)
                {
                    // Best-effort cleanup: the channel is invocation-unique, so an unsubscribe that never
                    // completes cannot disturb a concurrent probe, and the subscription is torn down with the
                    // connection at latest.
                }
            }
        }
    }
}
