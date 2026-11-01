namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What a punch records.
/// </summary>
public enum TimePunchKind
{
    /// <summary>
    /// The worker started work.
    /// </summary>
    In,

    /// <summary>
    /// The worker stopped work.
    /// </summary>
    Out,

    /// <summary>
    /// An unpaid meal started.
    /// </summary>
    MealStart,

    /// <summary>
    /// The meal ended.
    /// </summary>
    MealEnd,

    /// <summary>
    /// A paid break started.
    /// </summary>
    BreakStart,

    /// <summary>
    /// The break ended.
    /// </summary>
    BreakEnd,

    /// <summary>
    /// The worker moved to another activity, which is paid from then on.
    /// </summary>
    ActivityChange,

    /// <summary>
    /// Work on a job started.
    /// </summary>
    JobStart,

    /// <summary>
    /// Work on the job ended.
    /// </summary>
    JobEnd,
}
