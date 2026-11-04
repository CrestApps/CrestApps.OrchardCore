namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Where a worker stands on the clock.
/// </summary>
public enum TimeClockState
{
    /// <summary>
    /// Not working.
    /// </summary>
    Out,

    /// <summary>
    /// Clocked in.
    /// </summary>
    Working,

    /// <summary>
    /// On an unpaid meal.
    /// </summary>
    OnMeal,

    /// <summary>
    /// On a paid break.
    /// </summary>
    OnBreak,
}
