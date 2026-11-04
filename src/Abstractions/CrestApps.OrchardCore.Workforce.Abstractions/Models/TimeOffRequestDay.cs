namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// One day of a time-off request and what it counts.
/// </summary>
public sealed class TimeOffRequestDay
{
    /// <summary>
    /// Gets or sets the day.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets what the day counts, in the type's unit.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the minutes off that day.
    /// </summary>
    public int Minutes { get; set; }
}
