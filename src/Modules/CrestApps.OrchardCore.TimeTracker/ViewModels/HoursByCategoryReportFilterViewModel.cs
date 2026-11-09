namespace CrestApps.OrchardCore.TimeTracker.ViewModels;

/// <summary>
/// The filters of the hours by category report, besides its date range.
/// </summary>
public class HoursByCategoryReportFilterViewModel
{
    /// <summary>
    /// Gets or sets the user name search.
    /// </summary>
    public string Q { get; set; }

    /// <summary>
    /// Gets or sets whether each day has its own row.
    /// </summary>
    public bool ByDay { get; set; }
}
