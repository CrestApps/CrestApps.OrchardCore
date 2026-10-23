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
/// The editor of a report dashboard widget.
/// </summary>
public class ReportWidgetEditViewModel
{
    public string ReportName { get; set; }

    /// <summary>
    /// Gets or sets whether the widget type fixes the report, so the widget doesn't choose one.
    /// </summary>
    public bool ReportIsFixed { get; set; }

    public string FixedReportDisplayName { get; set; }

    public ReportWidgetPeriod Period { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public ReportWidgetContent Content { get; set; }

    public int MaxRows { get; set; }

    public int CacheMinutes { get; set; }

    public IList<SelectListItem> Reports { get; set; } = [];

    public IList<SelectListItem> Periods { get; set; } = [];

    public IList<SelectListItem> Contents { get; set; } = [];
}
