using CrestApps.OrchardCore.Learning.Assessments.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Decides what a learner sees of an attempt: the review options of the quiz per moment (during the attempt, right after
/// the submit, later while the quiz is open, after it closes), or, for an attempt without them, its review policy.
/// After the submit the review opens once the attempt is graded.
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
    /// Gets the moment of the review an attempt is at now.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <returns>The moment.</returns>
    public ReviewMoment MomentOf(AssessmentAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        var now = _clock.UtcNow;

        if (attempt.IsOpen)
        {
            return ReviewMoment.During;
        }

        if (attempt.QuizClosesUtc is { } closes && now >= closes)
        {
            return ReviewMoment.AfterClose;
        }

        return attempt.SubmittedUtc is { } submitted && now < submitted + QuizReviewOptions.ImmediatelyAfterWindow
            ? ReviewMoment.ImmediatelyAfter
            : ReviewMoment.LaterWhileOpen;
    }

    /// <summary>
    /// Gets what the learner sees of a submitted attempt now. Nothing shows before the attempt is graded.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <returns>What is shown.</returns>
    public ReviewShows Shows(AssessmentAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        if (attempt.Status != AttemptStatus.Graded)
        {
            return ReviewShows.None;
        }

        return Options(attempt).For(MomentOf(attempt));
    }

    /// <summary>
    /// Gets what the learner sees of one question now: during the attempt, what the review options allow for a question
    /// checked with immediate feedback; afterwards, what they allow for the attempt.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <param name="item">The question.</param>
    /// <returns>What is shown.</returns>
    public ReviewShows ShowsFor(AssessmentAttempt attempt, AttemptItem item)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(item);

        if (attempt.IsOpen)
        {
            return item.CheckedUtc is null ? ReviewShows.None : Options(attempt).During;
        }

        return Shows(attempt);
    }

    /// <summary>
    /// Says whether the learner may see the answers or feedback of an attempt now.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <returns><see langword="true"/> when the review shows more than the score.</returns>
    public bool CanReview(AssessmentAttempt attempt)
        => (Shows(attempt) & (ReviewShows.RightAnswers | ReviewShows.Feedback | ReviewShows.GeneralFeedback)) != ReviewShows.None;

    /// <summary>
    /// Says whether the learner may see the score of an attempt now.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public bool CanSeeScore(AssessmentAttempt attempt)
        => Shows(attempt).HasFlag(ReviewShows.Score);

    private static QuizReviewOptions Options(AssessmentAttempt attempt)
        => attempt.ReviewOptions ?? QuizReviewOptions.FromPolicy(attempt.ReviewPolicy);
}
