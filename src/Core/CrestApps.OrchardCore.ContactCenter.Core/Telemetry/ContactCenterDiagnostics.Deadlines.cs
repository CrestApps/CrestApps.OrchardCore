using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.ContactCenter.Core.Telemetry;

/// <summary>
/// Deadlines: in-process deadline timers and the drain
/// a node runs when it shuts down.
/// </summary>
public static partial class ContactCenterDiagnostics
{
    /// <summary>
    /// The name of the span of the drain a node runs before it shuts down.
    /// </summary>
    internal const string ShutdownDrainSpanName = "crestapps.contactcenter.shutdown.drain";

    private static readonly Histogram<double> _deadlineFireLag = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.deadline.fire.lag",
        "The time from when a deadline was due to when its work started, by deadline kind.");

    private static readonly Counter<long> _deadlineConflictRetries = Meter.CreateCounter<long>(
        "crestapps.contactcenter.deadline.conflict_retry",
        "{retry}",
        "The number of deadlines run again because their work lost a write race to another transition, by deadline kind.");

    private static readonly Counter<long> _deadlinesFailed = Meter.CreateCounter<long>(
        "crestapps.contactcenter.deadline.failed",
        "{deadline}",
        "The number of deadlines whose work failed and was left to the background sweep, by deadline kind and reason.");

    private static readonly Histogram<double> _shutdownDrainDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.shutdown.drain.duration",
        "The time a node spent draining Contact Center work before it shut down, by outcome.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// Gets the bounded kind of a deadline from its key, such as <c>offer</c> from <c>offer:{reservation id}</c>.
    /// </summary>
    /// <param name="key">The deadline key. Only its prefix is read.</param>
    /// <returns>The kind, or <c>other</c>.</returns>
    internal static string GetDeadlineKind(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return CrestAppsOrchardCoreTelemetry.Unknown;
        }

        var separator = key.IndexOf(':');

        return GetReasonCode((separator > 0 ? key[..separator] : key).ToLowerInvariant().Replace('-', '_'));
    }

    /// <summary>
    /// Records how late a deadline's work started.
    /// </summary>
    /// <param name="key">The deadline key; only its kind is tagged.</param>
    /// <param name="lag">The time from when it was due to when its work started.</param>
    internal static void RecordDeadlineFired(string key, TimeSpan lag)
    {
        if (_deadlineFireLag.Enabled)
        {
            Record(_deadlineFireLag, Math.Max(0, lag.TotalSeconds), new TagList { { KindTag, GetDeadlineKind(key) } });
        }
    }

    /// <summary>
    /// Records that a deadline's work lost a write race and runs again.
    /// </summary>
    /// <param name="key">The deadline key; only its kind is tagged.</param>
    internal static void RecordDeadlineConflictRetry(string key)
    {
        if (_deadlineConflictRetries.Enabled)
        {
            Add(_deadlineConflictRetries, new TagList { { KindTag, GetDeadlineKind(key) } });
        }
    }

    /// <summary>
    /// Records that a deadline's work failed and was left to the background sweep.
    /// </summary>
    /// <param name="key">The deadline key; only its kind is tagged.</param>
    /// <param name="reason"><c>error</c>, or <c>conflicts_exhausted</c> when it kept losing write races.</param>
    internal static void RecordDeadlineFailed(string key, string reason)
    {
        if (_deadlinesFailed.Enabled)
        {
            Add(_deadlinesFailed, new TagList
            {
                { KindTag, GetDeadlineKind(key) },
                { CrestAppsOrchardCoreTelemetry.ReasonTag, reason },
            });
        }
    }

    /// <summary>
    /// Starts measuring the drain a node runs before it shuts down.
    /// </summary>
    /// <returns>The scope.</returns>
    internal static TelemetryScope StartShutdownDrain()
        => TelemetryScope.Start(ActivitySource, ShutdownDrainSpanName, _shutdownDrainDuration);
}
