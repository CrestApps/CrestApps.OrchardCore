using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Workforce.Core.Telemetry;

/// <summary>
/// The meter and activity source of the workforce, <c>CrestApps.OrchardCore.Workforce</c>: the notifications sent to
/// workers, the adherence alerts raised and the payroll exports. Worker, timesheet and batch ids go on spans and logs
/// only; worker names go nowhere.
/// </summary>
public static class WorkforceTelemetry
{
    /// <summary>
    /// The name of the workforce meter and activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Workforce";

    /// <summary>
    /// The name of the span of a payroll export.
    /// </summary>
    public const string PayrollExportSpanName = "crestapps.workforce.payroll.export";

    /// <summary>
    /// The span tag that carries the id of the exported pay period calendar. It is never a metric tag.
    /// </summary>
    public const string PayPeriodCalendarIdTag = "crestapps.workforce.pay_period_calendar.id";

    /// <summary>
    /// The span tag that carries the id of the payroll export batch. It is never a metric tag.
    /// </summary>
    public const string PayrollBatchIdTag = "crestapps.workforce.payroll_batch.id";

    /// <summary>
    /// The tag that records the kind of a notification.
    /// </summary>
    public const string KindTag = "kind";

    /// <summary>
    /// The tag that records the scheduled activity an agent was out of adherence with.
    /// </summary>
    public const string ActivityTag = "activity";

    /// <summary>
    /// The tag that records the presence status an agent had when the adherence alert was raised.
    /// </summary>
    public const string StatusTag = "status";

    /// <summary>
    /// A notification reached the channel.
    /// </summary>
    public const string NotificationSent = "sent";

    /// <summary>
    /// A notification was not sent through a channel, because the worker turned the channel or the kind off, or it is
    /// their quiet hours.
    /// </summary>
    public const string NotificationSkipped = "skipped";

    /// <summary>
    /// A channel failed to send a notification.
    /// </summary>
    public const string NotificationFailed = "failed";

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/> of a payroll export that was not run, with its
    /// <see cref="CrestAppsOrchardCoreTelemetry.ReasonTag"/>.
    /// </summary>
    public const string OutcomeRejected = "rejected";

    /// <summary>
    /// The payroll export provider was not found.
    /// </summary>
    public const string ReasonNoProvider = "no_provider";

    /// <summary>
    /// The pay period calendar was not found.
    /// </summary>
    public const string ReasonNoCalendar = "no_calendar";

    /// <summary>
    /// Someone else is exporting the same pay period.
    /// </summary>
    public const string ReasonBusy = "busy";

    /// <summary>
    /// No timesheet of the pay period is locked yet.
    /// </summary>
    public const string ReasonNoneLocked = "none_locked";

    /// <summary>
    /// Every locked timesheet of the pay period was exported already.
    /// </summary>
    public const string ReasonAllExported = "all_exported";

    /// <summary>
    /// The meter of the workforce.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Workforce");

    /// <summary>
    /// The activity source of the workforce.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Workforce");

    /// <summary>
    /// The number of notifications offered to the channels, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.ChannelTag"/>,
    /// <see cref="KindTag"/> and the <see cref="CrestAppsOrchardCoreTelemetry.ResultTag"/>: <see cref="NotificationSent"/>,
    /// <see cref="NotificationSkipped"/> or <see cref="NotificationFailed"/>.
    /// </summary>
    public static Counter<long> Notifications { get; } = Meter.CreateCounter<long>(
        "crestapps.workforce.notifications",
        "{notification}",
        "The number of workforce notifications offered to each channel, by what became of them.");

    /// <summary>
    /// The number of adherence alerts raised for agents out of adherence too long, tagged with <see cref="ActivityTag"/>
    /// and <see cref="StatusTag"/>.
    /// </summary>
    public static Counter<long> AdherenceAlerts { get; } = Meter.CreateCounter<long>(
        "crestapps.workforce.adherence.alerts",
        "{alert}",
        "The number of adherence alerts raised for agents out of adherence longer than their rule allows.");

    /// <summary>
    /// The duration of a payroll export, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.ProviderTag"/>, the
    /// <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/> (<c>ok</c>, <see cref="OutcomeRejected"/>, <c>error</c>
    /// or <c>cancelled</c>) and, when it was rejected, the <see cref="CrestAppsOrchardCoreTelemetry.ReasonTag"/>.
    /// </summary>
    public static Histogram<double> PayrollExportDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.workforce.payroll.export.duration",
        "The duration of a payroll export.");

    /// <summary>
    /// The number of rows a payroll export wrote, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.ProviderTag"/>.
    /// </summary>
    public static Histogram<long> PayrollExportRows { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        Meter,
        "crestapps.workforce.payroll.export.rows",
        "{row}",
        "The number of rows a payroll export wrote.");

    /// <summary>
    /// Records what became of a notification on one channel.
    /// </summary>
    /// <param name="channel">The channel's name, such as <c>inbox</c>.</param>
    /// <param name="kind">The kind of the notification.</param>
    /// <param name="result">The result: <see cref="NotificationSent"/>, <see cref="NotificationSkipped"/> or <see cref="NotificationFailed"/>.</param>
    public static void RecordNotification(string channel, string kind, string result)
    {
        if (!Notifications.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, channel ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { KindTag, kind ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        Notifications.Add(1, tags);
    }

    /// <summary>
    /// Records an adherence alert.
    /// </summary>
    /// <param name="activity">The scheduled activity the agent was out of adherence with.</param>
    /// <param name="status">The agent's presence status.</param>
    public static void RecordAdherenceAlert(string activity, string status)
    {
        if (!AdherenceAlerts.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { ActivityTag, activity ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { StatusTag, status ?? CrestAppsOrchardCoreTelemetry.Unknown },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        AdherenceAlerts.Add(1, tags);
    }

    /// <summary>
    /// Starts measuring a payroll export.
    /// </summary>
    /// <returns>The scope of the export; add the provider with <see cref="TelemetryScope.AddMetricTag(string, object)"/> once it is known.</returns>
    public static TelemetryScope StartPayrollExport()
    {
        return TelemetryScope.Start(ActivitySource, PayrollExportSpanName, PayrollExportDuration);
    }

    /// <summary>
    /// Marks a payroll export as not run.
    /// </summary>
    /// <param name="scope">The scope of the export.</param>
    /// <param name="reason">Why it was not run, such as <see cref="ReasonBusy"/>.</param>
    public static void RejectPayrollExport(TelemetryScope scope, string reason)
    {
        ArgumentNullException.ThrowIfNull(scope);

        scope.SetOutcome(OutcomeRejected);
        scope.AddMetricTag(CrestAppsOrchardCoreTelemetry.ReasonTag, reason);
    }

    /// <summary>
    /// Records the rows a payroll export wrote.
    /// </summary>
    /// <param name="provider">The provider's name.</param>
    /// <param name="rows">The number of rows.</param>
    public static void RecordPayrollExportRows(string provider, long rows)
    {
        if (!PayrollExportRows.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, provider ?? CrestAppsOrchardCoreTelemetry.Unknown },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        PayrollExportRows.Record(rows, tags);
    }
}
