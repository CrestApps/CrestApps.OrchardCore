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

    /// <summary>
    /// Gets or sets the selected date-range preset key used to restore the picker option on reload.
    /// </summary>
    public string Range { get; set; }
}
