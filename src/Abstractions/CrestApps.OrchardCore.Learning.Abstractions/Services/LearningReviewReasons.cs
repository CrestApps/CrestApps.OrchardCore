namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// The reasons a completed enrollment is flagged for review.
/// </summary>
public static class LearningReviewReasons
{
    /// <summary>
    /// A result the completion rests on was voided or lowered after the course was completed.
    /// </summary>
    public const string ResultChangedAfterCompletion = "ResultChangedAfterCompletion";
}
