namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What a published schedule change did.
/// </summary>
public enum ScheduleChangeKind
{
    /// <summary>
    /// A shift was added.
    /// </summary>
    Added,

    /// <summary>
    /// A shift was removed.
    /// </summary>
    Removed,

    /// <summary>
    /// A shift's times changed.
    /// </summary>
    Moved,

    /// <summary>
    /// A shift moved to another worker.
    /// </summary>
    Reassigned,

    /// <summary>
    /// Only the breaks or activities inside a shift changed.
    /// </summary>
    SegmentsChanged,

    /// <summary>
    /// Two workers exchanged shifts.
    /// </summary>
    Swapped,

    /// <summary>
    /// An open shift was given to a worker.
    /// </summary>
    OpenShiftFilled,
}
