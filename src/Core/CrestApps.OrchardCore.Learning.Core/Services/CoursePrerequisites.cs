using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Checks a course's prerequisites for a person with the registered <see cref="ICoursePrerequisiteEvaluator"/>s, the
/// same way an enrollment does, so a cart can refuse a course before the person pays for it.
/// </summary>
public static class CoursePrerequisites
{
    /// <summary>
    /// Gets whether a person meets every prerequisite of a course. A prerequisite of a kind no evaluator knows is not met.
    /// </summary>
    /// <param name="evaluators">The registered evaluators.</param>
    /// <param name="userId">The person.</param>
    /// <param name="part">The course's part; <see langword="null"/> has no prerequisites.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when every prerequisite is met.</returns>
    public static async Task<bool> MeetsAsync(IEnumerable<ICoursePrerequisiteEvaluator> evaluators, string userId, CoursePart part, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evaluators);

        foreach (var prerequisite in part?.Prerequisites ?? [])
        {
            if (prerequisite is null || string.IsNullOrEmpty(prerequisite.Kind) || string.IsNullOrEmpty(prerequisite.Id))
            {
                continue;
            }

            var evaluator = evaluators.FirstOrDefault(candidate => string.Equals(candidate.Kind, prerequisite.Kind, StringComparison.OrdinalIgnoreCase));

            if (evaluator is null || !await evaluator.IsMetAsync(userId, prerequisite, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }
}
