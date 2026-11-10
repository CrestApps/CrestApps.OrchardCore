using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Reports.Designer;

public static partial class ReportTelemetry
{
    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.TriggerTag"/> value of work the background tasks started.
    /// </summary>
    public const string TriggerSchedule = "schedule";

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.TriggerTag"/> value of work a person started, such as
    /// <b>Refresh now</b> or <b>Send now</b>.
    /// </summary>
    public const string TriggerManual = "manual";

    /// <summary>
    /// The tag of <see cref="ViewReads"/>: <c>snapshot</c>, <c>live</c>, <c>stale_snapshot</c> or <c>first_run</c>.
    /// </summary>
    public const string ViewReadModeTag = "crestapps.report.view.mode";

    /// <summary>
    /// The tag of <see cref="WidgetRequests"/> that tells a designed report widget (<c>design</c>) from a coded one
    /// (<c>coded</c>).
    /// </summary>
    public const string KindTag = "crestapps.report.kind";

    /// <summary>
    /// The tag of <see cref="WidgetRequests"/> that tells whether the widget was served from its cache: <c>hit</c>,
    /// <c>miss</c> or <c>off</c>.
    /// </summary>
    public const string CacheTag = "crestapps.report.cache";

    /// <summary>
    /// The tag of <see cref="WorkflowTasks"/> that names the task: <c>run</c>, <c>export</c>, <c>email</c> or
    /// <c>share_link</c>.
    /// </summary>
    public const string WorkflowTaskTag = "crestapps.report.workflow_task";

    /// <summary>
    /// Gets the histogram of the duration of view refreshes that ran the view, tagged with the outcome and the
    /// <see cref="CrestAppsOrchardCoreTelemetry.TriggerTag"/>.
    /// </summary>
    public static Histogram<double> ViewRefreshDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        ReportTelemetrySource.Meter,
        "crestapps.report.view.refresh.duration",
        "The duration of the refreshes of scheduled report views.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// Gets the counter of view refresh attempts, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.TriggerTag"/>
    /// and the outcome: <c>refreshed</c>, <c>failed</c>, <c>busy</c>, <c>not_due</c>, <c>not_scheduled</c> or
    /// <c>not_found</c>.
    /// </summary>
    public static Counter<long> ViewRefreshes { get; } = ReportTelemetrySource.Meter.CreateCounter<long>(
        "crestapps.report.view.refreshes",
        "{refresh}",
        "The refresh attempts of scheduled report views, by how they ended.");

    /// <summary>
    /// Gets the histogram of the rows a view refresh stored.
    /// </summary>
    public static Histogram<long> SnapshotRows { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        ReportTelemetrySource.Meter,
        "crestapps.report.view.snapshot.rows",
        "{row}",
        "The rows the refreshes of scheduled report views stored.");

    /// <summary>
    /// Gets the age, in seconds, of the oldest stored rows of a scheduled view, recorded by each sweep of the view
    /// refresh background task. Alert when it grows well past the longest refresh interval.
    /// </summary>
    public static Gauge<double> SnapshotAge { get; } = ReportTelemetrySource.Meter.CreateGauge<double>(
        "crestapps.report.view.snapshot.age",
        "s",
        "The age of the oldest stored rows of a scheduled report view, at the last sweep.");

    /// <summary>
    /// Gets the counter of the reads of views by other reports, tagged with the <see cref="ViewReadModeTag"/>.
    /// </summary>
    public static Counter<long> ViewReads { get; } = ReportTelemetrySource.Meter.CreateCounter<long>(
        "crestapps.report.view.reads",
        "{read}",
        "The reads of report views by other reports, by whether they read the stored rows or ran the view.");

    /// <summary>
    /// Gets the counter of the view reads refused, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.ReasonTag"/>:
    /// <c>loop</c> or <c>depth</c>.
    /// </summary>
    public static Counter<long> ViewRejections { get; } = ReportTelemetrySource.Meter.CreateCounter<long>(
        "crestapps.report.view.rejected",
        "{read}",
        "The report view reads refused because a view reads itself or views are nested too deeply.");

    /// <summary>
    /// Gets the counter of report subscription runs, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.TriggerTag"/>,
    /// the outcome (<c>ok</c>, <c>skipped</c> or <c>error</c>) and the <see cref="CrestAppsOrchardCoreTelemetry.ReasonTag"/>
    /// of a run that sent nothing: <c>no_email</c>, <c>no_recipients</c>, <c>report_unavailable</c>,
    /// <c>export_denied</c>, <c>send_failed</c> or <c>exception</c>.
    /// </summary>
    public static Counter<long> SubscriptionSends { get; } = ReportTelemetrySource.Meter.CreateCounter<long>(
        "crestapps.report.subscription.sends",
        "{email}",
        "The report subscription runs, by whether they emailed the report.");

    /// <summary>
    /// Gets the histogram of the duration of report subscription runs, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.TriggerTag"/> and the outcome.
    /// </summary>
    public static Histogram<double> SubscriptionDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        ReportTelemetrySource.Meter,
        "crestapps.report.subscription.duration",
        "The duration of report subscription runs, from running the report to sending the email.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// Gets the counter of the designer saves refused with a 409 response, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.ReasonTag"/>: <c>conflict</c> or <c>busy</c>.
    /// </summary>
    public static Counter<long> DesignConflicts { get; } = ReportTelemetrySource.Meter.CreateCounter<long>(
        "crestapps.report.design.conflicts",
        "{request}",
        "The report designer saves refused because someone else saved first or the report was busy.");

    /// <summary>
    /// Gets the counter of report publishes, tagged with the outcome: <c>saved</c>, <c>invalid</c>, <c>conflict</c>,
    /// <c>busy</c> or <c>not_found</c>.
    /// </summary>
    public static Counter<long> DesignPublishes { get; } = ReportTelemetrySource.Meter.CreateCounter<long>(
        "crestapps.report.design.publishes",
        "{publish}",
        "The report designer publishes, by how they ended.");

    /// <summary>
    /// Gets the counter of the problems the designer found in the designs it checked.
    /// </summary>
    public static Counter<long> DesignValidationErrors { get; } = ReportTelemetrySource.Meter.CreateCounter<long>(
        "crestapps.report.design.validation_errors",
        "{error}",
        "The problems the report designer found in the queries it checked.");

    /// <summary>
    /// Gets the counter of the requests of shared report links, which anonymous visitors can reach, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.ResultTag"/>: <c>opened</c>, <c>exported</c>, <c>not_found</c> or
    /// <c>export_denied</c>.
    /// </summary>
    public static Counter<long> SharedLinkRequests { get; } = ReportTelemetrySource.Meter.CreateCounter<long>(
        "crestapps.report.shared_link.requests",
        "{request}",
        "The requests of shared report links, by result.");

    /// <summary>
    /// Gets the counter of the report workflow tasks that ran, tagged with the <see cref="WorkflowTaskTag"/> and the
    /// outcome.
    /// </summary>
    public static Counter<long> WorkflowTasks { get; } = ReportTelemetrySource.Meter.CreateCounter<long>(
        "crestapps.report.workflow.tasks",
        "{task}",
        "The report workflow tasks that ran, by task and outcome.");

    /// <summary>
    /// Gets the counter of report widget renders, tagged with the <see cref="KindTag"/>, the <see cref="CacheTag"/> and
    /// the outcome.
    /// </summary>
    public static Counter<long> WidgetRequests { get; } = ReportTelemetrySource.Meter.CreateCounter<long>(
        "crestapps.report.widget.requests",
        "{request}",
        "The report widget renders, by kind, cache use and outcome.");

    /// <summary>
    /// Gets the counter of the presence hub calls refused, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.OperationTag"/>:
    /// <c>join</c> or <c>presence</c>.
    /// </summary>
    public static Counter<long> HubDenied { get; } = ReportTelemetrySource.Meter.CreateCounter<long>(
        "crestapps.report.hub.denied",
        "{call}",
        "The report designer presence hub calls refused because the caller may not edit the report.");

    /// <summary>
    /// Records a view refresh attempt.
    /// </summary>
    /// <param name="outcome"><c>refreshed</c>, <c>failed</c>, <c>busy</c>, <c>not_due</c>, <c>not_scheduled</c> or <c>not_found</c>.</param>
    /// <param name="trigger"><see cref="TriggerSchedule"/> or <see cref="TriggerManual"/>.</param>
    public static void RecordViewRefresh(string outcome, string trigger)
    {
        if (!ViewRefreshes.Enabled)
        {
            return;
        }

        ViewRefreshes.Add(1, Build(
            (CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome),
            (CrestAppsOrchardCoreTelemetry.TriggerTag, trigger)));
    }

    /// <summary>
    /// Records a read of a view by another report.
    /// </summary>
    /// <param name="mode"><c>snapshot</c>, <c>live</c>, <c>stale_snapshot</c> or <c>first_run</c>.</param>
    public static void RecordViewRead(string mode)
    {
        Activity.Current?.SetTag(ViewReadModeTag, mode);

        if (ViewReads.Enabled)
        {
            ViewReads.Add(1, Build((ViewReadModeTag, mode)));
        }
    }

    /// <summary>
    /// Records a refused view read.
    /// </summary>
    /// <param name="reason"><c>loop</c> or <c>depth</c>.</param>
    public static void RecordViewRejected(string reason)
    {
        if (ViewRejections.Enabled)
        {
            ViewRejections.Add(1, Build((CrestAppsOrchardCoreTelemetry.ReasonTag, reason)));
        }
    }

    /// <summary>
    /// Records the age of the oldest stored rows of a scheduled view.
    /// </summary>
    /// <param name="age">The age.</param>
    public static void RecordSnapshotAge(TimeSpan age)
    {
        if (SnapshotAge.Enabled)
        {
            SnapshotAge.Record(Math.Max(0, age.TotalSeconds), Build());
        }
    }

    /// <summary>
    /// Records a report subscription run.
    /// </summary>
    /// <param name="outcome"><c>ok</c>, <c>skipped</c> or <c>error</c>.</param>
    /// <param name="reason">Why it sent nothing, or <see langword="null"/>.</param>
    /// <param name="trigger"><see cref="TriggerSchedule"/> or <see cref="TriggerManual"/>.</param>
    public static void RecordSubscriptionSend(string outcome, string reason, string trigger)
    {
        if (!SubscriptionSends.Enabled)
        {
            return;
        }

        var tags = Build(
            (CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome),
            (CrestAppsOrchardCoreTelemetry.TriggerTag, trigger));

        if (!string.IsNullOrEmpty(reason))
        {
            tags.Add(CrestAppsOrchardCoreTelemetry.ReasonTag, reason);
        }

        SubscriptionSends.Add(1, tags);
    }

    /// <summary>
    /// Adds one to a counter with bounded tags and the tenant tag, while the counter is enabled.
    /// </summary>
    /// <param name="counter">The counter.</param>
    /// <param name="tags">The bounded tags.</param>
    public static void Count(Counter<long> counter, params (string Key, object Value)[] tags)
    {
        ArgumentNullException.ThrowIfNull(counter);

        if (counter.Enabled)
        {
            counter.Add(1, Build(tags));
        }
    }

    private static TagList Build(params (string Key, object Value)[] tags)
    {
        var list = new TagList();

        foreach (var (key, value) in tags)
        {
            list.Add(key, value ?? CrestAppsOrchardCoreTelemetry.Unknown);
        }

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref list);

        return list;
    }
}
