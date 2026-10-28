using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Reports.Widgets.Models;

/// <summary>
/// Makes an admin dashboard widget show a report built in the report builder, or a reusable view as a table, for a
/// period and a number of rows the widget chooses, and keep what it shows for a while instead of running the report on
/// every visit to the dashboard. The widget shows either a report or a view, never both.
/// </summary>
public sealed class ReportDesignWidgetPart : ContentPart
{
    /// <summary>
    /// The period a new widget covers.
    /// </summary>
    public const ReportWidgetPeriod DefaultPeriod = ReportWidgetPeriod.Last30Days;

    /// <summary>
    /// What a new widget shows of its report: everything, since the person who built the report chose its visuals.
    /// </summary>
    public const ReportWidgetContent DefaultContent = ReportWidgetContent.Everything;

    /// <summary>
    /// How many rows of each table a new widget shows.
    /// </summary>
    public const int DefaultMaxRows = 10;

    /// <summary>
    /// Gets or sets the identifier of the published report to show.
    /// </summary>
    public string ReportId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the reusable view to show as a table, when the widget shows no report.
    /// </summary>
    public string ViewId { get; set; }

    /// <summary>
    /// Gets or sets the period the widget covers. It is set on the first date range or relative date filter the report
    /// lets its viewers change.
    /// </summary>
    public ReportWidgetPeriod Period { get; set; } = DefaultPeriod;

    /// <summary>
    /// Gets or sets the first day of a custom period, in the site's time zone.
    /// </summary>
    public DateTime? From { get; set; }

    /// <summary>
    /// Gets or sets the last day of a custom period, in the site's time zone.
    /// </summary>
    public DateTime? To { get; set; }

    /// <summary>
    /// Gets or sets what of the report the widget shows. A view is always shown as a table.
    /// </summary>
    public ReportWidgetContent Show { get; set; } = DefaultContent;

    /// <summary>
    /// Gets or sets how many rows of each table the widget shows, or 0 for all of them.
    /// </summary>
    public int MaxRows { get; set; } = DefaultMaxRows;

    /// <summary>
    /// Gets or sets how many minutes the widget keeps what it shows before it runs the report again; 0 runs it on every
    /// visit.
    /// </summary>
    public int CacheMinutes { get; set; } = ReportWidgetConstants.DefaultCacheMinutes;
}
