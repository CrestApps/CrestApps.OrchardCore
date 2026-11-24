using CrestApps.OrchardCore.Learning.Certifications.Models;

namespace CrestApps.OrchardCore.Learning.Certifications.Services;

/// <summary>
/// What a review of external learning did.
/// </summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="Submission">The external learning as it stands after the review, or <see langword="null"/> when it does not exist.</param>
public sealed record ExternalLearningReviewResult(ExternalLearningReviewOutcome Outcome, ExternalLearningSubmission Submission)
{
    /// <summary>
    /// Gets whether the review was recorded.
    /// </summary>
    public bool Succeeded => Outcome == ExternalLearningReviewOutcome.Reviewed;
}

/// <summary>
/// The outcomes of a review of external learning.
/// </summary>
public enum ExternalLearningReviewOutcome
{
    /// <summary>
    /// The review was recorded.
    /// </summary>
    Reviewed,

    /// <summary>
    /// The external learning does not exist.
    /// </summary>
    NotFound,

    /// <summary>
    /// It was already reviewed.
    /// </summary>
    AlreadyReviewed,

    /// <summary>
    /// The reviewer did the learning: nobody reviews their own.
    /// </summary>
    OwnLearning,

    /// <summary>
    /// Credits were already posted for it, so it cannot be rejected.
    /// </summary>
    CreditsPosted,

    /// <summary>
    /// Someone else is reviewing it at this moment.
    /// </summary>
    Busy,
}
