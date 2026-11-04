namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// One working day of a rotation: in which week, on which day, which template.
/// </summary>
public sealed class ShiftPatternDay
{
    /// <summary>
    /// Gets or sets the week of the rotation, from 0.
    /// </summary>
    public int Week { get; set; }

    /// <summary>
    /// Gets or sets the day of the week.
    /// </summary>
    public DayOfWeek Day { get; set; }

    /// <summary>
    /// Gets or sets the shift template worked that day.
    /// </summary>
    public string TemplateId { get; set; }
}
