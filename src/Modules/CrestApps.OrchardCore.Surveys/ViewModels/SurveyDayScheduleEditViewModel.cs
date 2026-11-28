using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The rule of one weekday in the access editor.
/// </summary>
public class SurveyDayScheduleEditViewModel
{
    /// <summary>
    /// Gets or sets the day.
    /// </summary>
    public DayOfWeek Day { get; set; }

    /// <summary>
    /// Gets or sets the rule.
    /// </summary>
    public DayScheduleMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the start of the range.
    /// </summary>
    public TimeOnly? From { get; set; }

    /// <summary>
    /// Gets or sets the end of the range.
    /// </summary>
    public TimeOnly? To { get; set; }
}
