namespace CrestApps.OrchardCore.Reports.ViewModels;

/// <summary>
/// The editor view model for the built-in report date-range filter.
/// </summary>
public class ReportDateRangeFilterViewModel
{
    /// <summary>
    /// Gets or sets the inclusive lower bound of the reporting period.
    /// </summary>
    public DateTime? From { get; set; }

    /// <summary>
    /// Gets or sets the inclusive upper bound of the reporting period.
    /// </summary>
    public DateTime? To { get; set; }
}
