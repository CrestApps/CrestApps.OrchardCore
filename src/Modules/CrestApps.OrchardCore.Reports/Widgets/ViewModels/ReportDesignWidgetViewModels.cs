using CrestApps.OrchardCore.Reports.Widgets.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Reports.Widgets.ViewModels;

/// <summary>
/// What a dashboard widget of a report built in the report builder shows.
/// </summary>
public class ReportDesignWidgetViewModel
{
    /// <summary>
    /// Gets or sets what the widget shows, or why it shows nothing.
    /// </summary>
    public ReportDesignWidgetResult Result { get; set; }

    /// <summary>
    /// Gets or sets the period the widget covers, as words.
    /// </summary>
    public string PeriodText { get; set; }

    /// <summary>
    /// Gets or sets when what the widget shows was made, as words.
    /// </summary>
    public string UpdatedText { get; set; }

    /// <summary>
    /// Gets or sets whether the person may open the report or the view the widget shows.
    /// </summary>
    public bool CanOpen { get; set; }
}

/// <summary>
/// The editor of a dashboard widget of a report built in the report builder.
/// </summary>
public class ReportDesignWidgetEditViewModel : ReportWidgetOptionsViewModel
{
    /// <summary>
    /// Gets or sets the report or view the widget shows, as <c>report:{id}</c> or <c>view:{id}</c>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the reports and views the editor may choose from.
    /// </summary>
    public IList<SelectListItem> Sources { get; set; } = [];
}
