namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// The rule of one day.
/// </summary>
public enum DayScheduleMode
{
    /// <summary>
    /// Responses may start all day.
    /// </summary>
    AllowAll,

    /// <summary>
    /// Responses may not start.
    /// </summary>
    PreventAll,

    /// <summary>
    /// Responses may start between two times.
    /// </summary>
    Between,
}
