namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// What a labor rule parameter holds, which sets how it is edited.
/// </summary>
public enum LaborRuleParameterKind
{
    /// <summary>
    /// A number of hours.
    /// </summary>
    Hours,

    /// <summary>
    /// A number of minutes.
    /// </summary>
    Minutes,

    /// <summary>
    /// A number of days.
    /// </summary>
    Days,

    /// <summary>
    /// A whole number.
    /// </summary>
    Count,

    /// <summary>
    /// A percentage.
    /// </summary>
    Percent,

    /// <summary>
    /// A number of years, such as an age.
    /// </summary>
    Years,

    /// <summary>
    /// A time of day, such as 22:00.
    /// </summary>
    Time,
}
