namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What kind of activity a segment of a shift is.
/// </summary>
public enum ActivityKind
{
    /// <summary>
    /// Regular work.
    /// </summary>
    Work,

    /// <summary>
    /// A short break.
    /// </summary>
    Break,

    /// <summary>
    /// A meal break.
    /// </summary>
    Meal,

    /// <summary>
    /// A meeting.
    /// </summary>
    Meeting,

    /// <summary>
    /// Training.
    /// </summary>
    Training,

    /// <summary>
    /// Coaching.
    /// </summary>
    Coaching,

    /// <summary>
    /// Work away from the main task, such as back-office work.
    /// </summary>
    OfflineWork,

    /// <summary>
    /// Travel between jobs.
    /// </summary>
    Travel,

    /// <summary>
    /// Available to be called.
    /// </summary>
    OnCall,

    /// <summary>
    /// Time off.
    /// </summary>
    TimeOff,

    /// <summary>
    /// Anything else.
    /// </summary>
    Other,
}
