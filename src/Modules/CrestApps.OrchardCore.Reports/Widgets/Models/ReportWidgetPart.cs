using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Reports.Widgets.Models;

/// <summary>
/// Makes an admin dashboard widget show a report, for a period and a number of rows the widget chooses, and keep what it
/// shows for a while instead of running the report on every visit to the dashboard. A value the widget leaves unset is
/// the default of its widget type.
/// </summary>
public sealed class ReportWidgetPart : ContentPart
{
    /// <summary>
    /// Gets or sets the technical name of the report to show, when the widget type doesn't fix one.
    /// </summary>
    public string ReportName { get; set; }

    /// <summary>
    /// Gets or sets the period the widget covers.
    /// </summary>
    public ReportWidgetPeriod? Period { get; set; }

    /// <summary>
    /// Gets or sets the first day of a custom period, in the site's time zone.
    /// </summary>
    public DateTime? From { get; set; }

    /// <summary>
    /// Gets or sets the last day of a custom period, in the site's time zone.
    /// </summary>
    public DateTime? To { get; set; }

    /// <summary>
    /// Gets or sets what of the report the widget shows.
    /// </summary>
    public ReportWidgetContent? Show { get; set; }

    /// <summary>
    /// Gets or sets how many rows of each table the widget shows.
    /// </summary>
    public int? MaxRows { get; set; }

    /// <summary>
    /// Gets or sets how many minutes the widget keeps what it shows before it runs the report again; 0 runs it on every
    /// visit.
    /// </summary>
    public int CacheMinutes { get; set; } = ReportWidgetConstants.DefaultCacheMinutes;
}
