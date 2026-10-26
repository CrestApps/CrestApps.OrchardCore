using CrestApps.OrchardCore.Reports.Widgets.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Reports.Widgets.ViewModels;

/// <summary>
/// What a report dashboard widget shows.
/// </summary>
public class ReportWidgetViewModel
{
    public ReportWidgetOutcome Outcome { get; set; }

    public string ReportName { get; set; }

    public string ReportDisplayName { get; set; }

    public ReportWidgetResult Result { get; set; }

    /// <summary>
    /// Gets or sets the period the widget covers, as words.
    /// </summary>
    public string PeriodText { get; set; }

    /// <summary>
    /// Gets or sets when what the widget shows was made, as words.
    /// </summary>
    public string UpdatedText { get; set; }

    /// <summary>
    /// Gets or sets whether the person may open the full report.
    /// </summary>
    public bool CanOpenReport { get; set; }
}

/// <summary>
/// The choices every report dashboard widget's editor offers: the period, what to show, the rows per table and how long
/// to keep the result.
/// </summary>
public class ReportWidgetOptionsViewModel
{
    /// <summary>
    /// Gets or sets the period the widget covers.
    /// </summary>
    public ReportWidgetPeriod Period { get; set; }

    /// <summary>
    /// Gets or sets the first day of a custom period.
    /// </summary>
    public DateTime? From { get; set; }

    /// <summary>
    /// Gets or sets the last day of a custom period.
    /// </summary>
    public DateTime? To { get; set; }

    /// <summary>
    /// Gets or sets what of the report the widget shows.
    /// </summary>
    public ReportWidgetContent Content { get; set; }

    /// <summary>
    /// Gets or sets how many rows of each table the widget shows, or 0 for all of them.
    /// </summary>
    public int MaxRows { get; set; }

    /// <summary>
    /// Gets or sets how many minutes the widget keeps what it shows.
    /// </summary>
    public int CacheMinutes { get; set; }

    /// <summary>
    /// Gets or sets the periods to choose from.
    /// </summary>
    public IList<SelectListItem> Periods { get; set; } = [];

    /// <summary>
    /// Gets or sets what of the report can be shown.
    /// </summary>
    public IList<SelectListItem> Contents { get; set; } = [];
}

/// <summary>
/// The editor of a report dashboard widget.
/// </summary>
public class ReportWidgetEditViewModel : ReportWidgetOptionsViewModel
{
    public string ReportName { get; set; }

    /// <summary>
    /// Gets or sets whether the widget type fixes the report, so the widget doesn't choose one.
    /// </summary>
    public bool ReportIsFixed { get; set; }

    public string FixedReportDisplayName { get; set; }

    public IList<SelectListItem> Reports { get; set; } = [];
}
