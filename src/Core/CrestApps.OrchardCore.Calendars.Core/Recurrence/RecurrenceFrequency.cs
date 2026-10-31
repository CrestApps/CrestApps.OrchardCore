namespace CrestApps.OrchardCore.Calendars.Core.Recurrence;

/// <summary>
/// How often a recurrence rule repeats (RFC 5545 <c>FREQ</c>).
/// </summary>
public enum RecurrenceFrequency
{
    /// <summary>
    /// Every day.
    /// </summary>
    Daily,

    /// <summary>
    /// Every week.
    /// </summary>
    Weekly,

    /// <summary>
    /// Every month.
    /// </summary>
    Monthly,

    /// <summary>
    /// Every year.
    /// </summary>
    Yearly,
}
