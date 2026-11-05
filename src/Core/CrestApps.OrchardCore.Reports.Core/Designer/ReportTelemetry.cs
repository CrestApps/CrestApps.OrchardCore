using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// The OpenTelemetry spans and metrics of designed report runs. Add <see cref="SourceName"/> to the tracing sources and
/// <see cref="MeterName"/> to the meters an OpenTelemetry setup listens to. Nothing is recorded, and next to nothing is
/// spent, while no listener is attached.
/// </summary>
public static class ReportTelemetry
{
    /// <summary>
    /// The name of the activity source and of the meter.
    /// </summary>
    public const string SourceName = "CrestApps.OrchardCore.Reports";

    /// <summary>
    /// The name of the meter.
    /// </summary>
    public const string MeterName = SourceName;

    /// <summary>
    /// The tag that holds the identifier of the report.
    /// </summary>
    public const string ReportIdTag = "crestapps.report.id";

    /// <summary>
    /// The tag that holds the title of the report.
    /// </summary>
    public const string ReportTitleTag = "crestapps.report.title";

    /// <summary>
    /// The tag that holds what the run is for: Admin, Shared, Link, Preview, Widget, Workflow, or View.
    /// </summary>
    public const string RunSourceTag = "crestapps.report.source";

    /// <summary>
    /// The tag that holds the identifier of the query, empty for the main query.
    /// </summary>
    public const string QueryIdTag = "crestapps.report.query.id";

    /// <summary>
    /// The tag that holds the name of the data source.
    /// </summary>
    public const string DataSourceTag = "crestapps.report.data_source";

    /// <summary>
    /// The tag that holds the name of the data set.
    /// </summary>
    public const string DataSetTag = "crestapps.report.data_set";

    /// <summary>
    /// The tag that tells whether a run is filtered by values picked in its visuals.
    /// </summary>
    public const string SelectionTag = "crestapps.report.click_filtered";

    /// <summary>
    /// The tag that holds the outcome of a run: success, failure, or canceled.
    /// </summary>
    public const string OutcomeTag = "crestapps.report.outcome";

    /// <summary>
    /// Gets the activity source of report runs.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(SourceName);

    /// <summary>
    /// Gets the meter of report runs.
    /// </summary>
    public static Meter Meter { get; } = new(MeterName);

    /// <summary>
    /// Gets the histogram of the duration of report runs, in seconds.
    /// </summary>
    public static Histogram<double> RunDuration { get; } = Meter.CreateHistogram<double>("crestapps.report.run.duration", "s", "The duration of designed report runs.");

    /// <summary>
    /// Gets the histogram of the duration of the queries of report runs, in seconds.
    /// </summary>
    public static Histogram<double> QueryDuration { get; } = Meter.CreateHistogram<double>("crestapps.report.query.duration", "s", "The duration of the queries of designed report runs.");

    /// <summary>
    /// Gets the counter of the rows read from data sources.
    /// </summary>
    public static Counter<long> RowsRead { get; } = Meter.CreateCounter<long>("crestapps.report.rows.read", "{row}", "The rows designed reports read from their data sets.");

    /// <summary>
    /// Gets the counter of the rows queries return.
    /// </summary>
    public static Counter<long> RowsReturned { get; } = Meter.CreateCounter<long>("crestapps.report.rows.returned", "{row}", "The rows the queries of designed reports return.");

    /// <summary>
    /// Gets the counter of data set reads served from the rows another query of the same run read.
    /// </summary>
    public static Counter<long> SharedReads { get; } = Meter.CreateCounter<long>("crestapps.report.reads.shared", "{read}", "The data set reads served from the rows another query of the same report run read.");

    /// <summary>
    /// Gets the counter of report runs that failed.
    /// </summary>
    public static Counter<long> Failures { get; } = Meter.CreateCounter<long>("crestapps.report.run.failures", "{run}", "The designed report runs and queries that failed.");

    /// <summary>
    /// Gets the counter of the temporary files queries wrote rows to because they didn't fit in memory.
    /// </summary>
    public static Counter<long> Spills { get; } = Meter.CreateCounter<long>("crestapps.report.spills", "{file}", "The temporary files designed report queries wrote rows to because they didn't fit in memory.");

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
            activity.SetTag(RunSourceTag, context.Source);
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
        activity.SetStatus(canceled ? ActivityStatusCode.Unset : ActivityStatusCode.Error, exception.Message);
        activity.AddException(exception);
    }

    /// <summary>
    /// The tags of a metric about a run: its report, query, and source.
    /// </summary>
    /// <param name="context">The run context.</param>
    /// <returns>The tags.</returns>
    public static TagList Tags(ReportQueryExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new TagList
        {
            { ReportIdTag, context.ReportId },
            { RunSourceTag, context.Source },
            { QueryIdTag, context.QueryId },
        };
    }
}
