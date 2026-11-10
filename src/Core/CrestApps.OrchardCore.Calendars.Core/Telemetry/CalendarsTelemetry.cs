using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Calendars.Core.Telemetry;

/// <summary>
/// The meter and activity source of calendars, <c>CrestApps.OrchardCore.Calendars</c>: external calendar
/// synchronization and the observers of event changes. Calendar and event ids go on spans and logs only; calendar
/// names and subscription addresses go nowhere.
/// </summary>
public static class CalendarsTelemetry
{
    /// <summary>
    /// The name of the calendars meter and activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Calendars";

    /// <summary>
    /// The name of the span of one external calendar synchronization.
    /// </summary>
    public const string SyncSpanName = "crestapps.calendars.sync";

    /// <summary>
    /// The span tag that carries the id of the synchronized calendar. It is never a metric tag.
    /// </summary>
    public const string CalendarIdTag = "crestapps.calendars.calendar.id";

    /// <summary>
    /// The name of the deferred work that tells the event observers about the changes of a request.
    /// </summary>
    public const string EventChangesWork = "calendar_event_changes";

    /// <summary>
    /// The tag that records whether a synchronization read every external event (<see cref="ModeFull"/>) or only the
    /// changes since the last read (<see cref="ModeIncremental"/>).
    /// </summary>
    public const string ModeTag = "mode";

    /// <summary>
    /// The tag that records whether a subscription only reads (<c>one_way</c>) or also writes back (<c>two_way</c>).
    /// </summary>
    public const string SyncDirectionTag = "sync.direction";

    /// <summary>
    /// The tag that records the type name of an observer.
    /// </summary>
    public const string ObserverTag = "observer";

    /// <summary>
    /// The tag that records the kind of a change.
    /// </summary>
    public const string KindTag = "kind";

    /// <summary>
    /// Every external event was read.
    /// </summary>
    public const string ModeFull = "full";

    /// <summary>
    /// Only the external changes since the last read were read.
    /// </summary>
    public const string ModeIncremental = "incremental";

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/> of a synchronization the external calendar refused
    /// access to; it is paused until the calendar is reconnected.
    /// </summary>
    public const string OutcomeUnauthorized = "unauthorized";

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.ProviderTag"/> of a subscription whose provider is not registered.
    /// </summary>
    public const string ProviderUnavailable = "unavailable";

    /// <summary>
    /// The meter of calendars.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Calendars");

    /// <summary>
    /// The activity source of calendars.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Calendars");

    /// <summary>
    /// The duration of an external calendar synchronization, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.ProviderTag"/>, <see cref="SyncDirectionTag"/>, <see cref="ModeTag"/>
    /// and the <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>: <c>ok</c>, <c>error</c>, <c>cancelled</c> or
    /// <see cref="OutcomeUnauthorized"/>.
    /// </summary>
    public static Histogram<double> SyncDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.calendars.sync.duration",
        "The duration of an external calendar synchronization.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// The number of events a synchronization changed, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.ProviderTag"/> and the <see cref="CrestAppsOrchardCoreTelemetry.ResultTag"/>:
    /// <c>created</c>, <c>updated</c>, <c>removed</c>, <c>conflict</c> or <c>refused</c>.
    /// </summary>
    public static Counter<long> SyncEvents { get; } = Meter.CreateCounter<long>(
        "crestapps.calendars.sync.events",
        "{event}",
        "The number of events external calendar synchronizations created, updated, removed, found in conflict or refused.");

    /// <summary>
    /// The number of event changes an observer failed to handle, tagged with <see cref="ObserverTag"/> and
    /// <see cref="KindTag"/>. The failure is logged and swallowed, so the change itself is kept.
    /// </summary>
    public static Counter<long> ObserverFailures { get; } = Meter.CreateCounter<long>(
        "crestapps.calendars.observer.failures",
        "{failure}",
        "The number of calendar event changes an observer failed to handle.");

    /// <summary>
    /// Starts measuring an external calendar synchronization.
    /// </summary>
    /// <param name="providerName">The name of the subscription's provider, or <see langword="null"/> when it is not registered.</param>
    /// <param name="twoWay">Whether the subscription writes local changes back.</param>
    /// <returns>The scope of the synchronization.</returns>
    public static TelemetryScope StartSync(string providerName, bool twoWay)
    {
        return TelemetryScope.Start(
            ActivitySource,
            SyncSpanName,
            SyncDuration,
            new TagList
            {
                { CrestAppsOrchardCoreTelemetry.ProviderTag, providerName ?? ProviderUnavailable },
                { SyncDirectionTag, twoWay ? "two_way" : "one_way" },
            });
    }

    /// <summary>
    /// Records the events a synchronization changed.
    /// </summary>
    /// <param name="providerName">The name of the subscription's provider.</param>
    /// <param name="result">What the synchronization did.</param>
    public static void RecordSyncEvents(string providerName, ICalendarImportResult result)
    {
        if (result is null || !SyncEvents.Enabled)
        {
            return;
        }

        var provider = providerName ?? ProviderUnavailable;

        Add(provider, "created", result.Created);
        Add(provider, "updated", result.Updated);
        Add(provider, "removed", result.Removed);
        Add(provider, "conflict", result.Conflicts);
        Add(provider, "refused", result.Errors.Count);
    }

    /// <summary>
    /// Records that an observer failed to handle an event change.
    /// </summary>
    /// <param name="observer">The observer's type.</param>
    /// <param name="kind">The kind of the change.</param>
    public static void RecordObserverFailure(Type observer, string kind)
    {
        if (!ObserverFailures.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { ObserverTag, observer?.Name ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { KindTag, kind ?? CrestAppsOrchardCoreTelemetry.Unknown },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        ObserverFailures.Add(1, tags);
    }

    private static void Add(string provider, string result, int count)
    {
        if (count <= 0)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, provider },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        SyncEvents.Add(count, tags);
    }
}
