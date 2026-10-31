namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A part of a shift template spent on one activity, such as the meal break, placed from the start of the shift.
/// </summary>
public sealed class ShiftTemplateSegment
{
    /// <summary>
    /// Gets or sets the activity.
    /// </summary>
    public string ActivityCodeId { get; set; }

    /// <summary>
    /// Gets or sets how many minutes after the shift starts the segment starts.
    /// </summary>
    public int OffsetMinutes { get; set; }

    /// <summary>
    /// Gets or sets how long the segment lasts.
    /// </summary>
    public int LengthMinutes { get; set; }
}
