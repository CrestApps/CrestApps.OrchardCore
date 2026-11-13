using CrestApps.OrchardCore.Learning.Assessments.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Turns the graded attempts of a learner on a quiz into the quiz score, by the grading method.
/// </summary>
public static class QuizGradeCalculator
{
    /// <summary>
    /// Computes the quiz score in percent.
    /// </summary>
    /// <param name="attempts">The attempts of the learner on the quiz.</param>
    /// <param name="method">The grading method.</param>
    /// <returns>The score, or <see langword="null"/> when no attempt is graded.</returns>
    public static decimal? Calculate(IEnumerable<AssessmentAttempt> attempts, GradingMethod method)
    {
        var graded = attempts
            .Where(attempt => attempt.Status == AttemptStatus.Graded && attempt.Percent is not null)
            .OrderBy(attempt => attempt.SubmittedUtc ?? attempt.StartedUtc)
            .ToList();

        if (graded.Count == 0)
        {
            return null;
        }

        return method switch
        {
            GradingMethod.Latest => graded[^1].Percent,
            GradingMethod.First => graded[0].Percent,
            GradingMethod.Average => Math.Round(graded.Average(attempt => attempt.Percent.Value), 2, MidpointRounding.AwayFromZero),
            _ => graded.Max(attempt => attempt.Percent),
        };
    }
}
