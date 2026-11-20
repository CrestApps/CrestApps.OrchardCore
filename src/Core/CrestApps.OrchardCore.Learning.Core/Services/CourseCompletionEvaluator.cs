using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Decides whether an enrollment meets its course's completion criteria: every required item completed, and every extra
/// criterion of the course met. A criterion whose name no registered criterion answers is not met.
/// </summary>
public sealed class CourseCompletionEvaluator
{
    private readonly IEnumerable<ICourseCompletionCriterion> _criteria;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseCompletionEvaluator"/> class.
    /// </summary>
    /// <param name="criteria">The registered completion criteria.</param>
    public CourseCompletionEvaluator(IEnumerable<ICourseCompletionCriterion> criteria)
    {
        _criteria = criteria;
    }

    /// <summary>
    /// Decides whether an enrollment is complete.
    /// </summary>
    /// <param name="outline">The outline of the course.</param>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="progress">The progress of the enrollment.</param>
    /// <param name="grade">The course grade.</param>
    /// <returns><see langword="true"/> when every criterion is met.</returns>
    public async Task<bool> IsCompleteAsync(CourseOutline outline, Enrollment enrollment, EnrollmentProgress progress, decimal? grade)
    {
        ArgumentNullException.ThrowIfNull(outline);

        // A course without completion criteria never completes; the learner's progress is still kept.
        if (outline.Course?.Get<CoursePart>(nameof(CoursePart))?.NeverCompletes == true)
        {
            return false;
        }

        var required = outline.RequiredEntries.ToArray();

        if (required.Length == 0 && (outline.Course?.Get<CoursePart>(nameof(CoursePart))?.CompletionCriteria?.Count ?? 0) == 0)
        {
            return false;
        }

        foreach (var entry in required)
        {
            if (progress?.Find(entry.Item.ContentItemId)?.State != CourseItemState.Completed)
            {
                return false;
            }
        }

        foreach (var setting in outline.Course?.Get<CoursePart>(nameof(CoursePart))?.CompletionCriteria ?? [])
        {
            if (setting is null || string.IsNullOrEmpty(setting.Name))
            {
                continue;
            }

            var criterion = _criteria.FirstOrDefault(candidate => string.Equals(candidate.Name, setting.Name, StringComparison.OrdinalIgnoreCase));

            if (criterion is null || !await criterion.IsMetAsync(new CompletionCriterionContext
            {
                Settings = setting,
                Enrollment = enrollment,
                Progress = progress,
                Course = outline.Course,
                Outline = outline,
                Grade = grade,
            }))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Computes the share of required items completed.
    /// </summary>
    /// <param name="outline">The outline of the course.</param>
    /// <param name="progress">The progress of the enrollment.</param>
    /// <returns>A percentage from 0 to 100.</returns>
    public static int ComputePercent(CourseOutline outline, EnrollmentProgress progress)
    {
        ArgumentNullException.ThrowIfNull(outline);

        var required = outline.RequiredEntries.ToArray();

        if (required.Length == 0)
        {
            return 0;
        }

        var completed = required.Count(entry => progress?.Find(entry.Item.ContentItemId)?.State == CourseItemState.Completed);

        return (int)Math.Floor(completed * 100m / required.Length);
    }
}
