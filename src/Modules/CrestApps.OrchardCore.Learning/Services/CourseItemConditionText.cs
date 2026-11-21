using CrestApps.OrchardCore.Learning.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Says, in the learner's words, what completes a course item: "Mark it as done", "Watch 80% of the video", "Pass with
/// 70%". The player and the course page show it beside each item, as Moodle's activity completion conditions.
/// </summary>
public sealed class CourseItemConditionText
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseItemConditionText"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CourseItemConditionText(IStringLocalizer<CourseItemConditionText> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets what completes an outline entry, or <see langword="null"/> for text shown in place and for a completion mode
    /// with no known text.
    /// </summary>
    /// <param name="entry">The outline entry.</param>
    /// <returns>The condition.</returns>
    public LocalizedString Get(CourseOutlineEntry entry)
    {
        if (entry is null || entry.Inline || entry.Part is null)
        {
            return null;
        }

        var part = entry.Part;

        return part.CompletionMode switch
        {
            CompletionModes.Viewed => S["View it"],
            CompletionModes.MarkedDone => S["Mark it as done"],
            CompletionModes.MinimumTime => S.Plural(part.MinimumMinutes ?? 0, "Spend 1 minute on it", "Spend {0} minutes on it"),
            CompletionModes.Watched => S["Watch {0}% of the video", entry.Item.Get<VideoLessonPart>(nameof(VideoLessonPart))?.RequiredPercent ?? 90],
            CompletionModes.Acknowledged => S["Confirm the statement"],
            CompletionModes.Submitted => S["Submit it"],
            CompletionModes.Graded => S["Receive a grade"],
            CompletionModes.Passed => part.PassMark is { } passMark ? S["Pass with {0}%", passMark.ToString("0.##")] : S["Pass it"],
            CompletionModes.MinimumScore => part.PassMark is { } minimum ? S["Receive a score of {0}% or more", minimum.ToString("0.##")] : S["Receive a score"],
            CompletionModes.Attended => S["Attend it"],
            CompletionModes.PackageReported => S["Complete the activity"],
            _ => null,
        };
    }
}
