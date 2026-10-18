using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrestApps.OrchardCore.ContactCenter.HealthChecks;

/// <summary>
/// Raises the dialer's heartbeat warning as a health check, so a stalled dialer alerts: due work is waiting in a paced
/// campaign queue but no pacing cycle of that queue has completed for longer than the stall threshold.
/// </summary>
/// <remarks>
/// A dialer that runs but finds no agent, or nothing due, is healthy: its cycles complete and say so. Only a queue whose
/// cycles stopped completing, or whose cycles all skip themselves because they cannot take its pacing lock, fails the
/// check. The verdict is the same on every node, because it is read from the shared
/// record of pacing cycles, so the check is an alerting dependency check and never gates readiness.
/// </remarks>
public sealed class ContactCenterDialerPacingHealthCheck : IHealthCheck
{
    private readonly IDialerPacingHealthService _healthService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterDialerPacingHealthCheck"/> class.
    /// </summary>
    /// <param name="healthService">The service that reads what the dialer is doing for each paced campaign queue.</param>
    public ContactCenterDialerPacingHealthCheck(IDialerPacingHealthService healthService)
    {
        _healthService = healthService;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        DialerPacingHealthSnapshot snapshot;

        try
        {
            snapshot = await _healthService.EvaluateAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Unable to read the dialer's pacing cycles.", ex);
        }

        return Describe(snapshot, context.Registration.FailureStatus);
    }

    /// <summary>
    /// Turns the dialer's state into a health check result.
    /// </summary>
    /// <param name="snapshot">The dialer's state.</param>
    /// <param name="failureStatus">The status a stalled dialer reports.</param>
    /// <returns>The result.</returns>
    internal static HealthCheckResult Describe(DialerPacingHealthSnapshot snapshot, HealthStatus failureStatus)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var data = new Dictionary<string, object>
        {
            ["paced_queues"] = snapshot.Queues.Count,
            ["stalled_queues"] = Count(snapshot, DialerPacingHealthState.Stalled),
            ["lock_not_acquired_queues"] = Count(snapshot, DialerPacingHealthState.LockNotAcquired),
            ["idle_queues"] = Count(snapshot, DialerPacingHealthState.Idle),
            ["held_back_queues"] = Count(snapshot, DialerPacingHealthState.HeldBack),
            ["dialing_queues"] = Count(snapshot, DialerPacingHealthState.Dialing),
            ["nothing_to_call_queues"] = Count(snapshot, DialerPacingHealthState.NothingToCall),
            ["stalled_after_minutes"] = snapshot.StalledAfter.TotalMinutes,
        };

        var lockNotAcquired = snapshot.Queues
            .Where(queue => queue.State == DialerPacingHealthState.LockNotAcquired)
            .ToArray();

        if (lockNotAcquired.Length > 0)
        {
            data["lock_not_acquired_queue_ids"] = string.Join(",", lockNotAcquired.Select(queue => queue.QueueId));

            var sinceUtc = lockNotAcquired.Min(queue => queue.LockNotAcquiredSinceUtc) ?? snapshot.EvaluatedUtc;
            var description = string.Format(
                CultureInfo.InvariantCulture,
                "Due work is waiting but the pacing lock of {0} campaign queue(s) has not been acquired since {1:o}, so every pacing cycle skips itself.",
                lockNotAcquired.Length,
                sinceUtc);

            return new HealthCheckResult(failureStatus, description, data: data);
        }

        var stalled = snapshot.Queues
            .Where(queue => queue.State == DialerPacingHealthState.Stalled)
            .ToArray();

        if (stalled.Length > 0)
        {
            data["stalled_queue_ids"] = string.Join(",", stalled.Select(queue => queue.QueueId));

            var description = string.Format(
                CultureInfo.InvariantCulture,
                "Due work is waiting but no pacing cycle has completed for {0} minutes in {1} campaign queue(s).",
                (int)stalled.Max(queue => queue.TimeWithoutCompletedCycle).TotalMinutes,
                stalled.Length);

            return new HealthCheckResult(failureStatus, description, data: data);
        }

        if (snapshot.Queues.Count == 0)
        {
            return HealthCheckResult.Healthy("No paced campaign has work waiting.", data);
        }

        return HealthCheckResult.Healthy(
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} paced campaign queue(s) with waiting work: {1} dialing, {2} idle with no agent available, {3} held back, {4} with nothing due.",
                snapshot.Queues.Count,
                data["dialing_queues"],
                data["idle_queues"],
                data["held_back_queues"],
                data["nothing_to_call_queues"]),
            data);
    }

    private static int Count(DialerPacingHealthSnapshot snapshot, DialerPacingHealthState state)
        => snapshot.Queues.Count(queue => queue.State == state);
}
