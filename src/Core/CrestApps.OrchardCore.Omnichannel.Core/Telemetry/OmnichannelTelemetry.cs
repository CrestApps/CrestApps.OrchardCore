using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Telemetry;

/// <summary>
/// The OpenTelemetry contract of the Omnichannel bulk work: the <c>CrestApps.OrchardCore.Omnichannel</c> meter and
/// activity source, and the instruments of activity loads. The names are a public integration surface.
/// </summary>
/// <remarks>
/// No instrument carries a batch, activity, contact or user id, or a phone number; ids go on spans and in logs only.
/// Nothing is recorded while no listener is attached.
/// </remarks>
public static class OmnichannelTelemetry
{
    /// <summary>
    /// The name of the meter and the activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Omnichannel";

    /// <summary>
    /// The span tag that records the id of the activity load. Never a metric tag.
    /// </summary>
    public const string BatchIdSpanTag = "crestapps.omnichannel.batch.id";

    /// <summary>
    /// A load started from the beginning.
    /// </summary>
    public const string TriggerStart = "start";

    /// <summary>
    /// A load carried on from where an earlier run stopped.
    /// </summary>
    public const string TriggerResume = "resume";

    /// <summary>
    /// The load went through every record it matched.
    /// </summary>
    public const string OutcomeCompleted = "completed";

    /// <summary>
    /// The load stopped before loading, with a reason the operator can act on.
    /// </summary>
    public const string OutcomeStopped = "stopped";

    /// <summary>
    /// The kind of load cannot be counted ahead of loading.
    /// </summary>
    public const string OutcomeUnsupported = "unsupported";

    /// <summary>
    /// A record was loaded as an activity.
    /// </summary>
    public const string RecordLoaded = "loaded";

    /// <summary>
    /// A record already had an open activity for the subject.
    /// </summary>
    public const string RecordDuplicate = "duplicate";

    /// <summary>
    /// A record, or a record sharing its number, had opted out of the channel.
    /// </summary>
    public const string RecordOptedOut = "opted_out";

    /// <summary>
    /// A record had no destination on the channel.
    /// </summary>
    public const string RecordNoDestination = "no_destination";

    /// <summary>
    /// A record had only numbers known not to be in service.
    /// </summary>
    public const string RecordNotInService = "not_in_service";

    /// <summary>
    /// A record was a converted lead, or a lead that is already a contact.
    /// </summary>
    public const string RecordLeadOrContact = "lead_or_contact";

    /// <summary>
    /// A record was over the limit of the load.
    /// </summary>
    public const string RecordOverLimit = "over_limit";

    /// <summary>
    /// The watchdog resumed a stalled load.
    /// </summary>
    public const string WatchdogResumed = "resumed";

    /// <summary>
    /// The watchdog gave up on a load that stopped too many times.
    /// </summary>
    public const string WatchdogGaveUp = "gave_up";

    /// <summary>
    /// The watchdog reset a load that could not be resumed, so it can be loaded again.
    /// </summary>
    public const string WatchdogReset = "reset";

    /// <summary>
    /// Gets the meter of the Omnichannel bulk work.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Omnichannel");

    /// <summary>
    /// Gets the activity source of the Omnichannel bulk work.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Omnichannel");

    /// <summary>
    /// Gets the duration of activity load runs, tagged with the <c>outcome</c> and the <c>trigger</c>.
    /// </summary>
    public static Histogram<double> BatchLoadDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.omnichannel.batch.load.duration",
        "The duration of one activity load run.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// Gets the duration of the pages of activity loads, from reading the page to committing it.
    /// </summary>
    public static Histogram<double> BatchLoadPageDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.omnichannel.batch.load.page.duration",
        "The duration of one committed page of an activity load.");

    /// <summary>
    /// Gets the number of records activity loads went through, counted once their page is committed, tagged with
    /// the <c>result</c>: <see cref="RecordLoaded"/>, <see cref="RecordDuplicate"/>, <see cref="RecordOptedOut"/>,
    /// <see cref="RecordNoDestination"/>, <see cref="RecordNotInService"/>, <see cref="RecordLeadOrContact"/> or
    /// <see cref="RecordOverLimit"/>. A preview counts nothing.
    /// </summary>
    public static Counter<long> BatchLoadRecords { get; } = Meter.CreateCounter<long>(
        "crestapps.omnichannel.batch.load.records",
        "{record}",
        "The number of records activity loads went through, by what became of them.");

    /// <summary>
    /// Gets the number of activity loads that stopped recording progress, as the watchdog last found them.
    /// </summary>
    public static Gauge<long> BatchLoadsStalled { get; } = Meter.CreateGauge<long>(
        "crestapps.omnichannel.batch.load.stalled",
        "{load}",
        "The number of activity loads that stopped recording progress, as the watchdog last found them.");

    /// <summary>
    /// Gets the duration of counting the records a load would load, tagged with the <c>outcome</c>.
    /// </summary>
    public static Histogram<double> BatchLoadPreviewDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.omnichannel.batch.load.preview.duration",
        "The duration of counting the records an activity load would load, in the request that asked.");

    /// <summary>
    /// Adds to a counter with one tag, and the tenant tag when it is enabled.
    /// </summary>
    /// <param name="counter">The counter.</param>
    /// <param name="value">The value to add. Zero and negative values are ignored.</param>
    /// <param name="key">The tag key.</param>
    /// <param name="tagValue">The bounded tag value.</param>
    public static void Add(Counter<long> counter, long value, string key, object tagValue)
    {
        ArgumentNullException.ThrowIfNull(counter);

        if (value <= 0 || !counter.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { key, tagValue },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        counter.Add(value, tags);
    }

    /// <summary>
    /// Adds to a counter with the given tags, and the tenant tag when it is enabled.
    /// </summary>
    /// <param name="counter">The counter.</param>
    /// <param name="value">The value to add. Zero and negative values are ignored.</param>
    /// <param name="tags">The bounded tags.</param>
    public static void Add(Counter<long> counter, long value, TagList tags)
    {
        ArgumentNullException.ThrowIfNull(counter);

        if (value <= 0 || !counter.Enabled)
        {
            return;
        }

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        counter.Add(value, tags);
    }

    /// <summary>
    /// Records a value on a histogram with the given tags, and the tenant tag when it is enabled.
    /// </summary>
    /// <param name="histogram">The histogram.</param>
    /// <param name="value">The value to record.</param>
    /// <param name="tags">The bounded tags.</param>
    public static void Record(Histogram<double> histogram, double value, TagList tags = default)
    {
        ArgumentNullException.ThrowIfNull(histogram);

        if (!histogram.Enabled)
        {
            return;
        }

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        histogram.Record(value, tags);
    }

    /// <summary>
    /// Records a value on a gauge, and the tenant tag when it is enabled.
    /// </summary>
    /// <param name="gauge">The gauge.</param>
    /// <param name="value">The value to record.</param>
    public static void Record(Gauge<long> gauge, long value)
    {
        ArgumentNullException.ThrowIfNull(gauge);

        if (!gauge.Enabled)
        {
            return;
        }

        var tags = new TagList();
        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        gauge.Record(value, tags);
    }

    /// <summary>
    /// Records the records an activity load went through since an earlier snapshot of its counts, on
    /// <see cref="BatchLoadRecords"/>. Call it after the counts were committed, so a page that is rolled back and
    /// loaded again by a resumed run is not counted twice.
    /// </summary>
    /// <param name="previous">The counts when they were last recorded.</param>
    /// <param name="current">The counts now committed.</param>
    public static void RecordLoadRecords(ActivityBatchLoadCounts previous, ActivityBatchLoadCounts current)
    {
        if (!BatchLoadRecords.Enabled)
        {
            return;
        }

        AddRecords(RecordLoaded, current.Loaded - previous.Loaded);
        AddRecords(RecordDuplicate, current.Duplicate - previous.Duplicate);
        AddRecords(RecordOptedOut, current.OptedOut - previous.OptedOut);
        AddRecords(RecordNoDestination, current.NoDestination - previous.NoDestination);
        AddRecords(RecordNotInService, current.NotInService - previous.NotInService);
        AddRecords(RecordLeadOrContact, current.LeadOrContact - previous.LeadOrContact);
        AddRecords(RecordOverLimit, current.OverLimit - previous.OverLimit);
    }

    private static void AddRecords(string result, long count)
    {
        Add(BatchLoadRecords, count, CrestAppsOrchardCoreTelemetry.ResultTag, result);
    }
}

/// <summary>
/// The counts of what an activity load did with the records it matched, as the load's batch holds them, so the change
/// between two commits can be recorded on <see cref="OmnichannelTelemetry.BatchLoadRecords"/>.
/// </summary>
/// <param name="Loaded">The records loaded as activities.</param>
/// <param name="Duplicate">The records that already had an open activity for the subject.</param>
/// <param name="OptedOut">The records that, or whose number, had opted out of the channel.</param>
/// <param name="NoDestination">The records with no destination on the channel.</param>
/// <param name="NotInService">The records with only numbers known not to be in service.</param>
/// <param name="LeadOrContact">The converted leads, and the leads that are already contacts.</param>
/// <param name="OverLimit">The records over the limit of the load.</param>
public readonly record struct ActivityBatchLoadCounts(
    long Loaded,
    long Duplicate,
    long OptedOut,
    long NoDestination,
    long NotInService,
    long LeadOrContact,
    long OverLimit)
{
    /// <summary>
    /// Reads the counts an activity batch holds.
    /// </summary>
    /// <param name="batch">The activity batch being loaded.</param>
    /// <returns>The counts.</returns>
    public static ActivityBatchLoadCounts Of(OmnichannelActivityBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        return new ActivityBatchLoadCounts(
            batch.TotalLoaded ?? 0,
            batch.TotalSkippedAsDuplicate,
            batch.TotalSkippedAsOptedOut + batch.TotalSkippedAsSharedNumberOptedOut,
            batch.TotalSkippedForNoDestination,
            batch.TotalSkippedAsNotInService,
            batch.TotalSkippedAsConverted + batch.TotalSkippedAsExistingContact,
            batch.TotalSkippedByLimit);
    }
}
