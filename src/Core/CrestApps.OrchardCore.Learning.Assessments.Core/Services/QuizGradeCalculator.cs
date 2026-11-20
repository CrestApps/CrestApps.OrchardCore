using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Models;

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

        return AttemptScores.Combine([.. graded.Select(attempt => attempt.Percent.Value)], method);
    }
}
