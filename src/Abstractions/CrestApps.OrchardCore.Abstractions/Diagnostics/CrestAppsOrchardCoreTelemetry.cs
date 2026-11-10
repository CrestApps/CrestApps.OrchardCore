using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.Core;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Diagnostics;

/// <summary>
/// The OpenTelemetry conventions of the CrestApps Orchard Core modules: how meters and activity sources are named,
/// the tag names and values they share, and the tags a metric may never carry. It builds on
/// <see cref="CrestAppsTelemetry"/>, the contract of the CrestApps.Core framework, and reuses its outcome values and
/// bucket boundaries, so the modules and the framework read the same on one dashboard.
/// </summary>
/// <remarks>
/// Every area emits through a meter and an activity source named <c>CrestApps.OrchardCore.&lt;Area&gt;</c>, created
/// with <see cref="CreateMeter(string)"/> and <see cref="CreateActivitySource(string)"/>, and names its instruments
/// <c>crestapps.&lt;area&gt;.&lt;thing&gt;</c> with UCUM units. Nothing is recorded until a listener, such as the
/// OpenTelemetry SDK, subscribes to <see cref="SourceNameWildcard"/>.
/// </remarks>
public static class CrestAppsOrchardCoreTelemetry
{
    /// <summary>
    /// The prefix of every meter and activity source of the CrestApps Orchard Core modules.
    /// </summary>
    public const string SourceNamePrefix = "CrestApps.OrchardCore";

    /// <summary>
    /// The wildcard that matches every meter and activity source of the CrestApps Orchard Core modules, for the
    /// <c>OrchardCore:OpenTelemetry:Tracing:Sources</c> and <c>OrchardCore:OpenTelemetry:Metrics:Meters</c> settings.
    /// </summary>
    public const string SourceNameWildcard = SourceNamePrefix + ".*";

    /// <summary>
    /// The name of the meter and activity source of the shared platform instruments: background tasks, deferred
    /// work, distributed locks, retries and the background work pacer.
    /// </summary>
    public const string PlatformSourceName = SourceNamePrefix + ".Platform";

    /// <summary>
    /// The tag that records how an operation ended. Its values are <see cref="OutcomeOk"/>, <see cref="OutcomeError"/>,
    /// <see cref="OutcomeSkipped"/>, <see cref="OutcomeCancelled"/> and <see cref="OutcomeTimeout"/>, plus bounded
    /// values an area documents.
    /// </summary>
    public const string OutcomeTag = CrestAppsTelemetry.OutcomeTag;

    /// <summary>
    /// The tag that records the full name of the exception type that failed an operation.
    /// </summary>
    public const string ErrorTypeTag = CrestAppsTelemetry.ErrorTypeTag;

    /// <summary>
    /// The tag that records the provider that served an operation, such as <c>Telnyx</c> or <c>Stripe</c>.
    /// </summary>
    public const string ProviderTag = CrestAppsTelemetry.ProviderTag;

    /// <summary>
    /// The tag that records which kind of operation was performed.
    /// </summary>
    public const string OperationTag = CrestAppsTelemetry.OperationTag;

    /// <summary>
    /// The tag that records why an operation ended as it did. Its values are a bounded set each area documents.
    /// </summary>
    public const string ReasonTag = "reason";

    /// <summary>
    /// The tag that records what became of one item an operation processed, such as <c>imported</c> or <c>skipped</c>.
    /// </summary>
    public const string ResultTag = "result";

    /// <summary>
    /// The tag that records the direction of a call or a message: <c>inbound</c> or <c>outbound</c>.
    /// </summary>
    public const string DirectionTag = "direction";

    /// <summary>
    /// The tag that records the channel of an interaction, such as <c>voice</c>, <c>sms</c> or <c>chat</c>.
    /// </summary>
    public const string ChannelTag = "channel";

    /// <summary>
    /// The tag that records a file or export format, such as <c>csv</c> or <c>xlsx</c>.
    /// </summary>
    public const string FormatTag = "format";

    /// <summary>
    /// The tag that records the class of an HTTP status code: <c>1xx</c> to <c>5xx</c>, or <c>none</c> when no
    /// response was received. See <see cref="ExternalCallTelemetry.GetStatusClass(int?)"/>.
    /// </summary>
    public const string StatusClassTag = "status_class";

    /// <summary>
    /// The tag that records what started an operation, such as <c>schedule</c>, <c>manual</c> or <c>workflow</c>.
    /// </summary>
    public const string TriggerTag = "trigger";

    /// <summary>
    /// The tag that records the background task, the deferred task or the service that did the work, by its
    /// type name.
    /// </summary>
    public const string TaskTag = "task";

    /// <summary>
    /// The tag that records the area of the solution an instrument of the platform measures, such as
    /// <c>contactcenter</c> or <c>commerce</c>.
    /// </summary>
    public const string AreaTag = "area";

    /// <summary>
    /// The tag that records the name of the tenant. Metrics only carry it when <see cref="IncludeTenantName"/> is on;
    /// spans and logs carry the tenant through <c>OrchardCore.OpenTelemetry</c> without it.
    /// </summary>
    public const string TenantTag = "tenant";

    /// <summary>
    /// The operation completed.
    /// </summary>
    public const string OutcomeOk = CrestAppsTelemetry.OutcomeOk;

    /// <summary>
    /// The operation failed.
    /// </summary>
    public const string OutcomeError = CrestAppsTelemetry.OutcomeError;

    /// <summary>
    /// The operation was cancelled before it completed.
    /// </summary>
    public const string OutcomeCancelled = CrestAppsTelemetry.OutcomeCancelled;

    /// <summary>
    /// The operation decided there was nothing to do, or that another worker was already doing it.
    /// </summary>
    public const string OutcomeSkipped = "skipped";

    /// <summary>
    /// The operation ran out of time.
    /// </summary>
    public const string OutcomeTimeout = "timeout";

    /// <summary>
    /// The operation was refused, for example by a rate limit or a permission check.
    /// </summary>
    public const string OutcomeBlocked = CrestAppsTelemetry.OutcomeBlocked;

    /// <summary>
    /// The value of a bounded tag when the actual value is not one of the values the area documents.
    /// </summary>
    public const string Other = "other";

    /// <summary>
    /// The value of a tag when the actual value is not known.
    /// </summary>
    public const string Unknown = "unknown";

    /// <summary>
    /// The tag keys a metric must never carry, because their values are unbounded or personal: ids, phone numbers,
    /// addresses, user names, URLs and hosts, lock keys, file names, SQL text and free-text names. Spans and logs may
    /// carry ids, but never phone numbers, transcripts or SIP URIs.
    /// </summary>
    public static IReadOnlySet<string> BannedMetricTagKeys { get; } = new string[]
    {
        "id",
        "item_id",
        "call.id",
        "call_id",
        "interaction.id",
        "interaction_id",
        "activity.id",
        "activity_id",
        "reservation.id",
        "contact.id",
        "contact_id",
        "entry.id",
        "entry_id",
        "batch.id",
        "batch_id",
        "session.id",
        "session_id",
        "profile.id",
        "profile_id",
        "user",
        "user.id",
        "user_id",
        "user.name",
        "username",
        "phone",
        "phone_number",
        "address",
        "email",
        "url",
        "uri",
        "url.full",
        "host",
        "server.address",
        "lock.key",
        "key",
        "file",
        "file.name",
        "file_name",
        "sql",
        "db.query.text",
        "query.text",
        "transcript",
        "crestapps.report.id",
        "crestapps.report.query.id",
        "crestapps.report.data_set",
        "crestapps.report.title",
        "crestapps.report.view.id",
        "crestapps.report.subscription.id",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The histogram buckets, in seconds, of operations that take from a few milliseconds to a couple of minutes.
    /// </summary>
    public static IReadOnlyList<double> DurationBucketBoundaries => CrestAppsTelemetry.DurationBucketBoundaries;

    /// <summary>
    /// The histogram buckets, in seconds, of long-running work such as imports, exports and voice sessions.
    /// </summary>
    public static IReadOnlyList<double> LongDurationBucketBoundaries => CrestAppsTelemetry.LongDurationBucketBoundaries;

    /// <summary>
    /// The histogram buckets of counts, such as the rows of a batch or the items of a run.
    /// </summary>
    public static IReadOnlyList<long> CountBucketBoundaries { get; } = [1, 5, 10, 25, 50, 100, 250, 500, 1_000, 2_500, 5_000, 10_000, 50_000, 100_000];

    /// <summary>
    /// The histogram buckets, in bytes, of file and payload sizes.
    /// </summary>
    public static IReadOnlyList<long> SizeBucketBoundaries { get; } = [1_024, 10_240, 102_400, 1_048_576, 10_485_760, 52_428_800, 104_857_600, 524_288_000, 1_073_741_824];

    /// <summary>
    /// Gets or sets a value indicating whether the instruments recorded through the shared helpers also carry the
    /// <see cref="TenantTag"/>. It is off by default, because it multiplies the number of series a collector stores
    /// by the number of tenants. The host sets it once at startup, from the
    /// <c>OrchardCore:CrestApps:OpenTelemetry:Metrics:IncludeTenantName</c> configuration value.
    /// </summary>
    public static bool IncludeTenantName { get; set; }

    /// <summary>
    /// The meter of the shared platform instruments.
    /// </summary>
    public static Meter PlatformMeter { get; } = CreateMeter("Platform");

    /// <summary>
    /// The activity source of the shared platform spans.
    /// </summary>
    public static ActivitySource PlatformActivitySource { get; } = CreateActivitySource("Platform");

    /// <summary>
    /// Gets the name of the meter and activity source of an area.
    /// </summary>
    /// <param name="area">The area, in PascalCase, such as <c>ContentTransfer</c>.</param>
    /// <returns>The name, such as <c>CrestApps.OrchardCore.ContentTransfer</c>.</returns>
    public static string GetSourceName(string area)
    {
        ArgumentException.ThrowIfNullOrEmpty(area);

        return SourceNamePrefix + "." + area;
    }

    /// <summary>
    /// Creates the meter of an area. Call it once per area and keep the meter in a static field.
    /// </summary>
    /// <param name="area">The area, in PascalCase, such as <c>ContentTransfer</c>.</param>
    /// <returns>The meter, named <c>CrestApps.OrchardCore.&lt;Area&gt;</c>.</returns>
    public static Meter CreateMeter(string area)
    {
        return new Meter(GetSourceName(area), GetVersion());
    }

    /// <summary>
    /// Creates the activity source of an area. Call it once per area and keep the source in a static field.
    /// </summary>
    /// <param name="area">The area, in PascalCase, such as <c>ContentTransfer</c>.</param>
    /// <returns>The activity source, named <c>CrestApps.OrchardCore.&lt;Area&gt;</c>.</returns>
    public static ActivitySource CreateActivitySource(string area)
    {
        return new ActivitySource(GetSourceName(area), GetVersion());
    }

    /// <summary>
    /// Creates a histogram that records a duration in seconds, with bucket boundaries advised for seconds so a
    /// collector does not fall back to buckets sized for milliseconds.
    /// </summary>
    /// <param name="meter">The meter of the area.</param>
    /// <param name="name">The instrument name, such as <c>crestapps.contenttransfer.import.duration</c>.</param>
    /// <param name="description">What the instrument measures.</param>
    /// <param name="bucketBoundaries">The bucket boundaries in seconds, or <see langword="null"/> for <see cref="DurationBucketBoundaries"/>.</param>
    /// <returns>The histogram.</returns>
    public static Histogram<double> CreateDurationHistogram(
        Meter meter,
        string name,
        string description,
        IReadOnlyList<double> bucketBoundaries = null)
    {
        ArgumentNullException.ThrowIfNull(meter);
        ArgumentException.ThrowIfNullOrEmpty(name);

        return meter.CreateHistogram<double>(
            name,
            "s",
            description,
            tags: null,
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = bucketBoundaries ?? DurationBucketBoundaries,
            });
    }

    /// <summary>
    /// Creates a histogram of counts or sizes, with bucket boundaries advised for them.
    /// </summary>
    /// <param name="meter">The meter of the area.</param>
    /// <param name="name">The instrument name.</param>
    /// <param name="unit">The UCUM unit, such as <c>{row}</c> or <c>By</c>.</param>
    /// <param name="description">What the instrument measures.</param>
    /// <param name="bucketBoundaries">The bucket boundaries, or <see langword="null"/> for <see cref="CountBucketBoundaries"/>.</param>
    /// <returns>The histogram.</returns>
    public static Histogram<long> CreateCountHistogram(
        Meter meter,
        string name,
        string unit,
        string description,
        IReadOnlyList<long> bucketBoundaries = null)
    {
        ArgumentNullException.ThrowIfNull(meter);
        ArgumentException.ThrowIfNullOrEmpty(name);

        return meter.CreateHistogram<long>(
            name,
            unit,
            description,
            tags: null,
            advice: new InstrumentAdvice<long>
            {
                HistogramBucketBoundaries = bucketBoundaries ?? CountBucketBoundaries,
            });
    }

    /// <summary>
    /// Adds the <see cref="TenantTag"/> to the tags of a metric when <see cref="IncludeTenantName"/> is on and the
    /// code runs in a tenant's scope.
    /// </summary>
    /// <param name="tags">The tags of the measurement.</param>
    public static void AddTenantTag(ref TagList tags)
    {
        if (!IncludeTenantName)
        {
            return;
        }

        var tenant = ShellScope.Context?.Settings?.Name;

        if (!string.IsNullOrEmpty(tenant))
        {
            tags.Add(TenantTag, tenant);
        }
    }

    /// <summary>
    /// Gets the <see cref="OutcomeTag"/> value of an operation that ended with an exception.
    /// </summary>
    /// <param name="exception">The exception the operation ended with.</param>
    /// <returns><see cref="OutcomeTimeout"/> for a timeout, <see cref="OutcomeCancelled"/> for a cancellation,
    /// otherwise <see cref="OutcomeError"/>.</returns>
    public static string GetOutcome(Exception exception)
    {
        return exception switch
        {
            TimeoutException => OutcomeTimeout,
            OperationCanceledException { InnerException: TimeoutException } => OutcomeTimeout,
            _ => CrestAppsTelemetry.GetOutcome(exception),
        };
    }

    /// <summary>
    /// Marks a span as failed by an exception. A cancellation leaves the status unset, as it is not an error.
    /// </summary>
    /// <remarks>
    /// The span gets the exception type and stack trace, but not the message: a provider or database error can quote
    /// a phone number, an address or a SIP URI, which a span must never carry. The message stays in the logs.
    /// </remarks>
    /// <param name="activity">The span, or <see langword="null"/> when nothing listens.</param>
    /// <param name="exception">The exception the operation ended with.</param>
    public static void RecordException(Activity activity, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (activity is null)
        {
            return;
        }

        var outcome = GetOutcome(exception);
        var errorType = exception.GetType().FullName;

        activity.SetTag(OutcomeTag, outcome);
        activity.SetTag(ErrorTypeTag, errorType);

        if (outcome != OutcomeCancelled)
        {
            activity.SetStatus(ActivityStatusCode.Error, errorType);
            activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                { "exception.type", errorType },
                { "exception.stacktrace", exception.StackTrace },
            }));
        }
    }

    private static string GetVersion()
    {
        return typeof(CrestAppsOrchardCoreTelemetry).Assembly.GetName().Version?.ToString();
    }
}
