namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// How many people may be off on one date, in place of the allotment's weekday number.
/// </summary>
public sealed class TimeOffAllotmentDate
{
    /// <summary>
    /// Gets or sets the date.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets how many people may be off that date.
    /// </summary>
    public int Slots { get; set; }
}
