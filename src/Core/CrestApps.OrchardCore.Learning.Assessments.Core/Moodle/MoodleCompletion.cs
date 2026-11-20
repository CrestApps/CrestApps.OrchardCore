using System.Globalization;
using CrestApps.OrchardCore.Learning.Core.Moodle;
using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Moodle;

/// <summary>
/// Maps Moodle's activity completion onto how a course item completes, and whether it counts toward the course.
/// </summary>
public static class MoodleCompletion
{
    /// <summary>
    /// Gets whether an activity counts toward the course's completion: the activities the course completion criteria
    /// name when there are any, else every activity with completion tracking.
    /// </summary>
    /// <param name="backup">The backup.</param>
    /// <param name="activity">The activity.</param>
    /// <returns><see langword="true"/> when the item is required.</returns>
    public static bool IsRequired(MoodleBackup backup, MoodleActivity activity)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(activity);

        var named = backup.Course?.CompletionCriteria
            .Where(criterion => criterion.Type == MoodleCompletionCriterion.Activity && !string.IsNullOrEmpty(criterion.ModuleId))
            .Select(criterion => criterion.ModuleId)
            .ToHashSet(StringComparer.Ordinal) ?? [];

        return named.Count > 0 ? named.Contains(activity.ModuleId) : activity.Completion != 0;
    }

    /// <summary>
    /// Maps the completion of a SCORM activity: a required score becomes Minimum score at that score (as a percentage of
    /// the activity's most points: Moodle's "viewed, graded and a score of at least N"), a passing grade Passed at the
    /// grade to pass, manual completion Marked done, and anything else (viewed, a status of completed or passed) the
    /// status the package reports.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <returns>The completion mode and, for Passed, the pass mark.</returns>
    public static (string Mode, decimal? PassMark) ForScorm(MoodleActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (activity.Completion == 1)
        {
            return (CompletionModes.MarkedDone, null);
        }

        if (activity.Completion == 2)
        {
            var maxGrade = Number(activity.Get("maxgrade")) ?? 0;

            if (maxGrade <= 0)
            {
                maxGrade = 100;
            }

            if (Number(activity.Get("completionscorerequired")) is { } score and > 0)
            {
                return (CompletionModes.MinimumScore, Percent(score, maxGrade));
            }

            if (activity.CompletionPassGrade && activity.GradePass is > 0)
            {
                return (CompletionModes.Passed, Percent(activity.GradePass.Value, activity.GradeMax is > 0 ? activity.GradeMax.Value : maxGrade));
            }
        }

        return (CompletionModes.PackageReported, null);
    }

    /// <summary>
    /// Maps the completion of a quiz: a passing grade becomes Passed (the pass mark is the quiz's), a grade Graded, and
    /// anything else Submitted.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <returns>The completion mode.</returns>
    public static string ForQuiz(MoodleActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (activity.Completion == 2 && activity.CompletionPassGrade && activity.GradePass is > 0)
        {
            return CompletionModes.Passed;
        }

        return activity.Completion == 2 && activity.CompletionUseGrade ? CompletionModes.Graded : CompletionModes.Submitted;
    }

    /// <summary>
    /// Gets a quiz's pass mark, as a percentage of its most points, or <see langword="null"/> when it has no grade to pass.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <returns>The pass mark.</returns>
    public static decimal? QuizPassMark(MoodleActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var max = activity.GradeMax is > 0 ? activity.GradeMax.Value : Number(activity.Get("grade")) ?? 0;

        return activity.GradePass is > 0 && max > 0 ? Percent(activity.GradePass.Value, max) : null;
    }

    /// <summary>
    /// Maps the completion of a file, a link or a page: manual completion becomes Marked done, anything else Viewed.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <returns>The completion mode.</returns>
    public static string ForViewed(MoodleActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        return activity.Completion == 1 ? CompletionModes.MarkedDone : CompletionModes.Viewed;
    }

    /// <summary>
    /// Maps the role of a Moodle completion by role onto a course staff role: a teacher is an instructor, a non-editing
    /// teacher an assistant, and any other role keeps its short name.
    /// </summary>
    /// <param name="roleShortName">The role's short name.</param>
    /// <returns>The course staff role, or <see langword="null"/>.</returns>
    public static string StaffRole(string roleShortName)
        => roleShortName?.Trim() switch
        {
            null or "" => null,
            "editingteacher" => LearningConstants.CourseStaffRoles.Instructor,
            "teacher" => LearningConstants.CourseStaffRoles.Assistant,
            var role => role,
        };

    private static decimal Percent(decimal value, decimal max)
        => Math.Clamp(Math.Round(value / max * 100, 2), 0, 100);

    private static decimal? Number(string value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : null;
}
