using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// The OpenTelemetry spans and metrics of reports: designed report runs, coded reports, exports, views, subscriptions,
/// the designer and the widgets. Add <see cref="SourceName"/> to the tracing sources and <see cref="MeterName"/> to the
/// meters an OpenTelemetry setup listens to. Nothing is recorded, and next to nothing is spent, while no listener is
/// attached.
/// </summary>
/// <remarks>
/// Metrics carry bounded tags only: the run source, the data source, the outcome and the values each instrument
/// documents. The report, query, view and subscription identifiers, the data set and the report title go on spans and
/// log lines only (see <see cref="CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys"/>).
/// </remarks>
public static partial class ReportTelemetry
{
    /// <summary>
    /// The name of the activity source and of the meter.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Reports";

    /// <summary>
    /// The name of the meter.
    /// </summary>
    public const string MeterName = SourceName;

    /// <summary>
    /// The span tag that holds the identifier of the report. Never a metric tag.
    /// </summary>
    public const string ReportIdTag = "crestapps.report.id";

    /// <summary>
    /// The span tag that holds the title of the report. Never a metric tag.
    /// </summary>
    public const string ReportTitleTag = "crestapps.report.title";

    /// <summary>
    /// The tag that holds what the run is for, one of the values of <see cref="ReportRunSources"/>.
    /// </summary>
    public const string RunSourceTag = "crestapps.report.source";

    /// <summary>
    /// The span tag that holds the identifier of the query, empty for the main query. Never a metric tag.
    /// </summary>
    public const string QueryIdTag = "crestapps.report.query.id";

    /// <summary>
    /// The tag that holds the name of the data source, such as <c>Contents</c>.
    /// </summary>
    public const string DataSourceTag = "crestapps.report.data_source";

    /// <summary>
    /// The span tag that holds the name of the data set. Never a metric tag.
    /// </summary>
    public const string DataSetTag = "crestapps.report.data_set";

    /// <summary>
    /// The span tag that holds the identifier of a view. Never a metric tag.
    /// </summary>
    public const string ViewIdTag = "crestapps.report.view.id";

    /// <summary>
    /// The span tag that holds the identifier of a subscription. Never a metric tag.
    /// </summary>
    public const string SubscriptionIdTag = "crestapps.report.subscription.id";

    /// <summary>
    /// The tag that tells whether a run is filtered by values picked in its visuals.
    /// </summary>
    public const string SelectionTag = "crestapps.report.click_filtered";

    /// <summary>
    /// The legacy tag that holds the outcome of a run: <c>success</c>, <c>failure</c>, or <c>canceled</c>. It is kept for
    /// one release next to <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/> (<c>ok</c>, <c>error</c>,
    /// <c>cancelled</c>), which replaces it.
    /// </summary>
    public const string OutcomeTag = "crestapps.report.outcome";

    /// <summary>
    /// The tag of <see cref="Failures"/> that tells a failed run (<see cref="LevelRun"/>) from a failed query
    /// (<see cref="LevelQuery"/>).
    /// </summary>
    public const string LevelTag = "crestapps.report.level";

    /// <summary>
    /// The <see cref="LevelTag"/> value of a run that failed.
    /// </summary>
    public const string LevelRun = "run";

    /// <summary>
    /// The <see cref="LevelTag"/> value of a query that failed.
    /// </summary>
    public const string LevelQuery = "query";

    /// <summary>
    /// The tag of <see cref="LimitHits"/> that names the limit, one of the <c>Limit</c> constants.
    /// </summary>
    public const string LimitTag = "crestapps.report.limit";

    /// <summary>
    /// The <see cref="LimitTag"/> value of <see cref="ReportQueryLimits.MaxRowsPerDataSet"/>.
    /// </summary>
    public const string LimitRowsPerDataSet = "rows_per_dataset";

    /// <summary>
    /// The <see cref="LimitTag"/> value of <see cref="ReportQueryLimits.MaxJoinedRows"/>.
    /// </summary>
    public const string LimitJoinedRows = "joined_rows";

    /// <summary>
    /// The <see cref="LimitTag"/> value of <see cref="ReportQueryLimits.MaxResultRows"/>.
    /// </summary>
    public const string LimitResultRows = "result_rows";

    /// <summary>
    /// The <see cref="LimitTag"/> value of <see cref="ReportQueryLimits.MaxJoinKeys"/>.
    /// </summary>
    public const string LimitJoinKeys = "join_keys";

    /// <summary>
    /// The <see cref="LimitTag"/> value of <see cref="ReportQueryLimits.MaxSharedReadRows"/>.
    /// </summary>
    public const string LimitSharedReadRows = "shared_read_rows";

    /// <summary>
    /// The span duration above which a designed report run is logged as slow.
    /// </summary>
    public static readonly TimeSpan SlowRunThreshold = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the activity source of reports.
    /// </summary>
    public static ActivitySource ActivitySource => ReportTelemetrySource.ActivitySource;

    /// <summary>
    /// Gets the meter of reports.
    /// </summary>
    public static Meter Meter => ReportTelemetrySource.Meter;

    /// <summary>
    /// Gets the histogram of the duration of designed report runs, in seconds, tagged with the source and the outcome.
    /// </summary>
    public static Histogram<double> RunDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.report.run.duration",
        "The duration of designed report runs.");

    /// <summary>
    /// Gets the histogram of the duration of the queries of report runs, in seconds, tagged with the source and the
    /// outcome.
    /// </summary>
    public static Histogram<double> QueryDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.report.query.duration",
        "The duration of the queries of designed report runs.");

    /// <summary>
    /// Gets the counter of the rows read from data sources, tagged with the source and the data source.
    /// </summary>
    public static Counter<long> RowsRead { get; } = Meter.CreateCounter<long>("crestapps.report.rows.read", "{row}", "The rows designed reports read from their data sets.");

    /// <summary>
    /// Gets the counter of the rows queries return, tagged with the source.
    /// </summary>
    public static Counter<long> RowsReturned { get; } = Meter.CreateCounter<long>("crestapps.report.rows.returned", "{row}", "The rows the queries of designed reports return.");

    /// <summary>
    /// Gets the counter of data set reads served from the rows another query of the same run read, tagged with the
    /// source and the data source.
    /// </summary>
    public static Counter<long> SharedReads { get; } = Meter.CreateCounter<long>("crestapps.report.reads.shared", "{read}", "The data set reads served from the rows another query of the same report run read.");

    /// <summary>
    /// Gets the counter of the reads of a data set more than one query of a run reads that could not be served from the
    /// rows kept, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.ReasonTag"/>: <c>disabled</c>,
    /// <c>join_key</c>, <c>conditions</c> or <c>too_many_rows</c>.
    /// </summary>
    public static Counter<long> SharedReadMisses { get; } = Meter.CreateCounter<long>("crestapps.report.reads.shared.miss", "{read}", "The reads of a data set several queries of a run read that were not served from the rows kept.");

    /// <summary>
    /// Gets the counter of report runs and queries that failed, tagged with the source and the <see cref="LevelTag"/>:
    /// each failed run counts once at <see cref="LevelRun"/>, each failed query once at <see cref="LevelQuery"/>.
    /// </summary>
    public static Counter<long> Failures { get; } = Meter.CreateCounter<long>("crestapps.report.run.failures", "{run}", "The designed report runs and queries that failed.");

    /// <summary>
    /// Gets the counter of the temporary files queries wrote rows to because they didn't fit in memory.
    /// </summary>
    public static Counter<long> Spills { get; } = Meter.CreateCounter<long>("crestapps.report.spills", "{file}", "The temporary files designed report queries wrote rows to because they didn't fit in memory.");

    /// <summary>
    /// Gets the counter of the times a run reached one of its size limits, tagged with the <see cref="LimitTag"/> and the
    /// source.
    /// </summary>
    public static Counter<long> LimitHits { get; } = Meter.CreateCounter<long>("crestapps.report.limit.hits", "{hit}", "The times designed report runs reached one of their size limits.");

    /// <summary>
    /// Gets the counter of the grouped queries that tried to let their data source group them, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/> (<c>used</c> or <c>fallback</c>), the
    /// <see cref="CrestAppsOrchardCoreTelemetry.ReasonTag"/> of a fallback and the data source.
    /// </summary>
    public static Counter<long> AggregatePushdown { get; } = Meter.CreateCounter<long>("crestapps.report.aggregate.pushdown", "{query}", "The grouped report queries that let their data source group them, or read every row.");

    /// <summary>
    /// Gets the counter of the <c>GROUP BY</c> statements built over a YesSql index, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.ResultTag"/>: <c>built</c>, <c>unbuildable</c> or <c>too_many_groups</c>.
    /// </summary>
    public static Counter<long> IndexAggregates { get; } = Meter.CreateCounter<long>("crestapps.report.aggregate.index", "{statement}", "The grouping statements report data sources ran over an index table, by result.");

    /// <summary>
    /// Gets the counter of the formula errors a query swallowed, recorded once per query with the number of errors.
    /// </summary>
    public static Counter<long> CalculationErrors { get; } = Meter.CreateCounter<long>("crestapps.report.calc.errors", "{error}", "The calculated field values that failed to compute and were left empty.");

    /// <summary>
    /// Gets the histogram of the queries a designed report run executed, tagged with the source.
    /// </summary>
    public static Histogram<long> RunQueries { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        Meter,
        "crestapps.report.run.queries",
        "{query}",
        "The queries one designed report run executed.");

    /// <summary>
    /// Gets the counter of the queries of a run that did not run, tagged with the source and the
    /// <see cref="CrestAppsOrchardCoreTelemetry.ReasonTag"/>: <c>invalid</c> or <c>failed</c>.
    /// </summary>
    public static Counter<long> SkippedQueries { get; } = Meter.CreateCounter<long>("crestapps.report.run.queries.skipped", "{query}", "The queries of designed report runs that did not run.");

    /// <summary>
    /// Gets the counter of the runs refused before they started, tagged with the source and the
    /// <see cref="CrestAppsOrchardCoreTelemetry.ReasonTag"/>: <c>owner_missing</c>.
    /// </summary>
    public static Counter<long> RejectedRuns { get; } = Meter.CreateCounter<long>("crestapps.report.run.rejected", "{run}", "The designed report runs refused before they started.");

    /// <summary>
    /// Gets the histogram of the time runs waited for the gate of <see cref="ReportQueryLimits.MaxConcurrentRuns"/>.
    /// </summary>
    public static Histogram<double> GateWait { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.report.gate.wait",
        "The time report runs waited for the concurrent run limit.");

    /// <summary>
    /// Gets the number of runs that hold the gate.
    /// </summary>
    public static UpDownCounter<long> GateActive { get; } = Meter.CreateUpDownCounter<long>("crestapps.report.gate.active", "{run}", "The report runs that hold the concurrent run limit.");

    /// <summary>
    /// Gets the number of runs that wait for the gate.
    /// </summary>
    public static UpDownCounter<long> GateWaiting { get; } = Meter.CreateUpDownCounter<long>("crestapps.report.gate.waiting", "{run}", "The report runs that wait for the concurrent run limit.");

    /// <summary>
    /// Gets the histogram of the duration of query plans, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>:
    /// <c>ok</c>, <c>invalid</c> or <c>error</c>.
    /// </summary>
    public static Histogram<double> PlanDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.report.plan.duration",
        "The duration of report query plans, including the data set schemas they read.");

    /// <summary>
    /// Gets the histogram of the duration of the data set schema reads of query plans, tagged with the data source and
    /// the <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>: <c>ok</c>, <c>not_found</c> or <c>error</c>.
    /// </summary>
    public static Histogram<double> SchemaDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.report.schema.duration",
        "The duration of the data set schema reads of report query plans.");

    /// <summary>
    /// Gets the histogram of the duration of the streamed reads of a query, such as a live view another report reads,
    /// tagged with the source and the outcome.
    /// </summary>
    public static Histogram<double> StreamDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.report.stream.duration",
        "The duration of report queries whose rows are streamed to another report.");

    /// <summary>
    /// Gets the histogram of the duration of the index pages report data sources read, tagged with the
    /// <see cref="IndexTag"/> and the <see cref="PhaseTag"/>.
    /// </summary>
    public static Histogram<double> IndexPageDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.report.index.page.duration",
        "The duration of the index pages report data sources read.");

    /// <summary>
    /// Gets the counter of the index pages report data sources read, tagged with the <see cref="IndexTag"/> and the
    /// <see cref="PhaseTag"/>.
    /// </summary>
    public static Counter<long> IndexPages { get; } = Meter.CreateCounter<long>("crestapps.report.index.pages", "{page}", "The index pages report data sources read.");

    /// <summary>
    /// The tag that holds the type name of the index a data source reads, such as <c>OmnichannelActivityIndex</c>.
    /// </summary>
    public const string IndexTag = "crestapps.report.index";

    /// <summary>
    /// The tag of the index page instruments: <see cref="PhaseIndex"/> or <see cref="PhaseRecords"/>.
    /// </summary>
    public const string PhaseTag = "crestapps.report.phase";

    /// <summary>
    /// The <see cref="PhaseTag"/> value of the read of the index rows of a page.
    /// </summary>
    public const string PhaseIndex = "index";

    /// <summary>
    /// The <see cref="PhaseTag"/> value of the read of the records of a page.
    /// </summary>
    public const string PhaseRecords = "records";

    /// <summary>
    /// Starts a span, or returns <see langword="null"/> when no listener records it.
    /// </summary>
    /// <param name="name">The name of the span.</param>
    /// <param name="context">The run context whose report, query, and source tag the span, or <see langword="null"/>.</param>
    /// <returns>The span, or <see langword="null"/>.</returns>
    public static Activity Start(string name, ReportQueryExecutionContext context = null)
    {
        var activity = ActivitySource.StartActivity(name, ActivityKind.Internal);

        if (activity is not null && context is not null)
        {
            activity.SetTag(ReportIdTag, context.ReportId);
            activity.SetTag(ReportTitleTag, context.ReportTitle);
            activity.SetTag(RunSourceTag, ReportRunSources.Normalize(context.Source));
            activity.SetTag(QueryIdTag, context.QueryId);
        }

        return activity;
    }

    /// <summary>
    /// Marks a span failed with an exception, unless it is <see langword="null"/>.
    /// </summary>
    /// <param name="activity">The span.</param>
    /// <param name="exception">The exception.</param>
    public static void Fail(Activity activity, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (activity is null)
        {
            return;
        }

        var canceled = exception is OperationCanceledException;

        activity.SetTag(OutcomeTag, canceled ? "canceled" : "failure");
        CrestAppsOrchardCoreTelemetry.RecordException(activity, exception);
    }

    /// <summary>
    /// The tags of a metric about a run: its source, normalized to the values of <see cref="ReportRunSources"/>. The
    /// report and query identifiers are span tags only.
    /// </summary>
    /// <param name="context">The run context.</param>
    /// <returns>The tags.</returns>
    public static TagList Tags(ReportQueryExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Tags(context.Source);
    }

    /// <summary>
    /// The tags of a metric about a run of a source: the source, normalized to the values of
    /// <see cref="ReportRunSources"/>, and the tenant when <see cref="CrestAppsOrchardCoreTelemetry.IncludeTenantName"/>
    /// is on.
    /// </summary>
    /// <param name="source">What the run is for.</param>
    /// <returns>The tags.</returns>
    public static TagList Tags(string source)
    {
        var tags = new TagList
        {
            { RunSourceTag, ReportRunSources.Normalize(source) },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);

        return tags;
    }

    /// <summary>
    /// Records that a run reached one of its size limits.
    /// </summary>
    /// <param name="context">The run context.</param>
    /// <param name="limit">The limit, one of the <c>Limit</c> constants.</param>
    public static void RecordLimitHit(ReportQueryExecutionContext context, string limit)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!LimitHits.Enabled)
        {
            return;
        }

        var tags = Tags(context);

        tags.Add(LimitTag, limit);
        LimitHits.Add(1, tags);
    }

    /// <summary>
    /// Records that a grouped query let its data source group it, or why it read every row instead.
    /// </summary>
    /// <param name="dataSource">The data source of the query's first data set, or <see langword="null"/>.</param>
    /// <param name="reason">Why the source did not group the query, or <see langword="null"/> when it did.</param>
    public static void RecordAggregatePushdown(string dataSource, string reason)
    {
        if (!AggregatePushdown.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, reason is null ? "used" : "fallback" },
            { DataSourceTag, string.IsNullOrEmpty(dataSource) ? CrestAppsOrchardCoreTelemetry.Unknown : dataSource },
        };

        if (reason is not null)
        {
            tags.Add(CrestAppsOrchardCoreTelemetry.ReasonTag, reason);
        }

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        AggregatePushdown.Add(1, tags);
    }

    /// <summary>
    /// Records what became of a grouping statement over a YesSql index.
    /// </summary>
    /// <param name="index">The index type.</param>
    /// <param name="result"><c>built</c>, <c>unbuildable</c> or <c>too_many_groups</c>.</param>
    public static void RecordIndexAggregate(Type index, string result)
    {
        if (!IndexAggregates.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
            { IndexTag, index?.Name ?? CrestAppsOrchardCoreTelemetry.Unknown },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        IndexAggregates.Add(1, tags);
    }

    /// <summary>
    /// Records a read of a data set several queries of a run read that was not served from the rows kept.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    /// <param name="reason">Why the read was not served: <c>disabled</c>, <c>join_key</c>, <c>conditions</c> or
    /// <c>too_many_rows</c>.</param>
    public static void RecordSharedReadMiss(string dataSource, string reason)
    {
        if (!SharedReadMisses.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ReasonTag, reason },
            { DataSourceTag, string.IsNullOrEmpty(dataSource) ? CrestAppsOrchardCoreTelemetry.Unknown : dataSource },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        SharedReadMisses.Add(1, tags);
    }

    /// <summary>
    /// Gets the outcome tag value of an operation that ended with an exception, in the shared vocabulary.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><c>cancelled</c>, <c>timeout</c> or <c>error</c>.</returns>
    public static string GetOutcome(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return CrestAppsOrchardCoreTelemetry.GetOutcome(exception);
    }

    /// <summary>
    /// Adds a positive value to a counter while it is enabled.
    /// </summary>
    /// <param name="counter">The counter.</param>
    /// <param name="tags">The bounded tags.</param>
    /// <param name="value">The value to add.</param>
    public static void Add(Counter<long> counter, TagList tags, long value = 1)
    {
        ArgumentNullException.ThrowIfNull(counter);

        if (counter.Enabled && value > 0)
        {
            counter.Add(value, tags);
        }
    }
}

// The meter and the activity source live in their own type, so every instrument of the partial ReportTelemetry class,
// whatever file declares it, finds them created: the order of static initializers across partial files is not defined.
internal static class ReportTelemetrySource
{
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Reports");

    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Reports");
}
