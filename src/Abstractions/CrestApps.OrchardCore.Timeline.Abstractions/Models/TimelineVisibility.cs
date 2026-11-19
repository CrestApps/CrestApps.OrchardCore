namespace CrestApps.OrchardCore.Timeline.Models;

/// <summary>
/// Who may read a timeline entry.
/// </summary>
public enum TimelineVisibility
{
    /// <summary>
    /// Everyone who may read the record, the customer included.
    /// </summary>
    Public,

    /// <summary>
    /// Only the people who work on the record.
    /// </summary>
    Internal,
}
