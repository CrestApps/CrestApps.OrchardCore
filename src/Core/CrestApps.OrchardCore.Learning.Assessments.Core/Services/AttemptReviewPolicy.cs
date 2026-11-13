using CrestApps.OrchardCore.Learning.Assessments.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Decides when a learner may see the answers and feedback of an attempt.
/// </summary>
public sealed class AttemptReviewPolicy
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttemptReviewPolicy"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    public AttemptReviewPolicy(IClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Says whether the learner may see the answers and feedback now.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public bool CanReview(AssessmentAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        if (attempt.Status != AttemptStatus.Graded)
        {
            return false;
        }

        return attempt.ReviewPolicy switch
        {
            ReviewPolicy.AfterGraded => true,
            ReviewPolicy.AfterClose => attempt.QuizClosesUtc is { } closes && _clock.UtcNow >= closes,
            _ => false,
        };
    }

    /// <summary>
    /// Says whether the learner may see the score now.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <returns><see langword="true"/> unless the policy hides it.</returns>
    public static bool CanSeeScore(AssessmentAttempt attempt)
        => attempt.ReviewPolicy != ReviewPolicy.Hidden && attempt.Status == AttemptStatus.Graded;
}
