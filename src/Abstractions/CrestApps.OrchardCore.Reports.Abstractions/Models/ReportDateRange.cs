namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Represents the resolved reporting period contributed by the built-in date-range filter. The date
/// range is stored in the report filter like any other filter; a report that does not contribute the
/// date-range filter leaves these values unset.
/// </summary>
public sealed class ReportDateRange
{
    /// <summary>
    /// Gets or sets the inclusive lower UTC bound of the reporting period.
    /// </summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the inclusive upper UTC bound of the reporting period.
    /// </summary>
    public DateTime? ToUtc { get; set; }

    /// <summary>
    /// Gets or sets the selected date-range preset key (for example <c>today</c>, <c>last30</c>, or
    /// <c>custom</c>) used to restore the picker's selected option when the report is reloaded.
    /// </summary>
    public string Key { get; set; }
}
