namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// What a learner sees of an attempt at each moment of the review, as Moodle's review options: during the attempt (for
/// questions checked with immediate feedback), right after the submit, later while the quiz is open, and after it
/// closes.
/// </summary>
public sealed class QuizReviewOptions
{
    /// <summary>
    /// How long after the submit the moment right after it lasts, as in Moodle.
    /// </summary>
    public static readonly TimeSpan ImmediatelyAfterWindow = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets what a learner sees of a question checked during the attempt.
    /// </summary>
    public ReviewShows During { get; set; }

    /// <summary>
    /// Gets or sets what a learner sees within two minutes of the submit.
    /// </summary>
    public ReviewShows ImmediatelyAfter { get; set; }

    /// <summary>
    /// Gets or sets what a learner sees later, while the quiz is still open.
    /// </summary>
    public ReviewShows LaterWhileOpen { get; set; }

    /// <summary>
    /// Gets or sets what a learner sees after the quiz closes.
    /// </summary>
    public ReviewShows AfterClose { get; set; }

    /// <summary>
    /// Gets what a learner sees at a moment.
    /// </summary>
    /// <param name="moment">The moment.</param>
    /// <returns>What is shown.</returns>
    public ReviewShows For(ReviewMoment moment)
        => moment switch
        {
            ReviewMoment.During => During,
            ReviewMoment.ImmediatelyAfter => ImmediatelyAfter,
            ReviewMoment.LaterWhileOpen => LaterWhileOpen,
            ReviewMoment.AfterClose => AfterClose,
            _ => ReviewShows.None,
        };

    /// <summary>
    /// Gets the review options a review policy stands for, so a quiz without its own options reviews as it always did.
    /// </summary>
    /// <param name="policy">The review policy.</param>
    /// <returns>The options.</returns>
    public static QuizReviewOptions FromPolicy(ReviewPolicy policy)
        => policy switch
        {
            ReviewPolicy.AfterGraded => new QuizReviewOptions { During = ReviewShows.All, ImmediatelyAfter = ReviewShows.All, LaterWhileOpen = ReviewShows.All, AfterClose = ReviewShows.All },
            ReviewPolicy.AfterClose => new QuizReviewOptions { During = ReviewShows.Score, ImmediatelyAfter = ReviewShows.Score, LaterWhileOpen = ReviewShows.Score, AfterClose = ReviewShows.All },
            ReviewPolicy.ScoreOnly => new QuizReviewOptions { During = ReviewShows.Score, ImmediatelyAfter = ReviewShows.Score, LaterWhileOpen = ReviewShows.Score, AfterClose = ReviewShows.Score },
            _ => new QuizReviewOptions(),
        };
}
