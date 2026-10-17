namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The bounds the interactive Contact Center reports are generated within. Reports materialize every
/// interaction and activity in the requested window, so an unbounded range would let a single admin request
/// load an arbitrarily large result set into memory. Silently trimming rows would corrupt the aggregate
/// totals, so the range is capped and an over-wide request is rejected instead.
/// </summary>
public sealed class ContactCenterReportingOptions
{
    /// <summary>
    /// Gets or sets the widest reporting window a single report request may span. A request whose
    /// <c>toUtc - fromUtc</c> exceeds this value is rejected before any data is queried. The default of
    /// 400 days comfortably covers the built-in day, week, month, quarter, and year presets while still
    /// bounding an ad-hoc request.
    /// </summary>
    public TimeSpan MaximumReportRange { get; set; } = TimeSpan.FromDays(400);

    /// <summary>
    /// Gets or sets the widest period an agent workforce report reads from the agent state log when it is shown in
    /// the browser: the presence, time, utilization and payroll reports, the agent activity timeline and the
    /// reconciled payroll timecard. A longer period is refused with a message that names this limit, before any event
    /// is read. The period is measured in the tenant's time zone, so a month that crosses a daylight-saving change
    /// still counts as its calendar days. The default is 31 days.
    /// </summary>
    public TimeSpan MaximumWorkforceReportRange { get; set; } = TimeSpan.FromDays(31);

    /// <summary>
    /// Gets or sets the widest period an agent workforce report reads when it is exported. An export lists every row
    /// rather than the browser's first rows, so it is bounded separately; a longer period is refused the same way. The
    /// default is 31 days.
    /// </summary>
    public TimeSpan MaximumWorkforceExportRange { get; set; } = TimeSpan.FromDays(31);

    /// <summary>
    /// Gets or sets how far back the daily agent time summaries are kept up to date. The time-based workforce reports
    /// read a whole UTC day inside their period from its summary instead of from the day's state changes; a day older
    /// than this, or one that has no summary yet, is read from the log. Zero turns the summaries off, so every report
    /// reads the log. The default is 92 days.
    /// </summary>
    public TimeSpan AgentTimeSummaryBackfill { get; set; } = TimeSpan.FromDays(92);
}
