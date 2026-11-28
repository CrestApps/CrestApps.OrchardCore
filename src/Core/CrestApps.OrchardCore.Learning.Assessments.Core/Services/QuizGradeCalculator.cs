using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Combines the graded attempts (survey responses) of a learner into the quiz grade with the quiz's grading method.
/// </summary>
public static class QuizGradeCalculator
{
    /// <summary>
    /// Combines attempts. Only completed, fully graded attempts with a percent count, in the order they ended.
    /// </summary>
    /// <param name="responses">The learner's attempts.</param>
    /// <param name="method">The grading method.</param>
    /// <returns>The grade as a percent, or <see langword="null"/> when no attempt is graded.</returns>
    public static decimal? Calculate(IEnumerable<SurveyResponse> responses, GradingMethod method)
    {
        var graded = (responses ?? [])
            .Where(response => response.Status == SurveyResponseStatus.Completed && response.Grading == GradingStatus.Graded && response.Percent is not null)
            .OrderBy(response => response.CompletedUtc ?? response.StartedUtc)
            .Select(response => response.Percent.Value)
            .ToArray();

        return AttemptScores.Combine(graded, method);
    }
}
