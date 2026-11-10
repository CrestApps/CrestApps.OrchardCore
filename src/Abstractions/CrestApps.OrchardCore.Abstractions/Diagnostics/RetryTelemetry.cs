using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CrestApps.OrchardCore.Diagnostics;

/// <summary>
/// Counts the retries of the solution's own retry loops, such as webhook deliveries, data connector requests and
/// data source syncs, on <c>crestapps.retry.attempts</c>. The code that retries also logs a warning with the attempt
/// number, so a retry storm shows on a chart and in the logs.
/// </summary>
public static class RetryTelemetry
{
    /// <summary>
    /// The highest <c>attempt</c> tag value; later attempts are counted under it, so the tag stays bounded.
    /// </summary>
    public const int MaxAttemptTag = 5;

    /// <summary>
    /// The number of retries, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.AreaTag"/>, the
    /// <see cref="CrestAppsOrchardCoreTelemetry.ReasonTag"/> and the <c>attempt</c>.
    /// </summary>
    public static Counter<long> Attempts { get; } = CrestAppsOrchardCoreTelemetry.PlatformMeter.CreateCounter<long>(
        "crestapps.retry.attempts",
        "{retry}",
        "The number of times the solution's own retry loops tried an operation again.");

    /// <summary>
    /// Records that an operation is tried again.
    /// </summary>
    /// <param name="area">The area, such as <c>webhooks</c> or <c>dataconnectors</c>.</param>
    /// <param name="reason">Why it is retried, from a bounded set, such as <c>timeout</c>, <c>network</c>,
    /// <c>status_5xx</c> or <c>status_429</c>.</param>
    /// <param name="attempt">The number of the attempt about to be made, starting at 2 for the first retry.</param>
    public static void RecordRetry(string area, string reason, int attempt)
    {
        if (!Attempts.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.AreaTag, area ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { CrestAppsOrchardCoreTelemetry.ReasonTag, reason ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { "attempt", Math.Clamp(attempt, 1, MaxAttemptTag) },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        Attempts.Add(1, tags);
    }
}
