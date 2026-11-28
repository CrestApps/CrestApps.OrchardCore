namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Gives the first day of the week weekly limits count from: the site's.
/// </summary>
public interface ISurveyWeekStartProvider
{
    /// <summary>
    /// Gets the first day of the week.
    /// </summary>
    /// <returns>The day.</returns>
    Task<DayOfWeek> GetFirstDayOfWeekAsync();
}
