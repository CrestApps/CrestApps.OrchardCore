using CrestApps.OrchardCore.Learning.Assessments.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.ViewModels;

/// <summary>
/// The page of an attempt: answering it, or its result and review.
/// </summary>
public class AttemptPageViewModel
{
    /// <summary>Gets or sets the attempt.</summary>
    public AssessmentAttempt Attempt { get; set; }

    /// <summary>Gets or sets whether the right answers and feedback may be shown.</summary>
    public bool CanReview { get; set; }

    /// <summary>Gets or sets whether the score may be shown.</summary>
    public bool CanSeeScore { get; set; }

    /// <summary>Gets or sets the items shown on this page.</summary>
    public IList<AttemptItem> Items { get; set; } = [];

    /// <summary>Gets or sets whether the learner answers one question at a time.</summary>
    public bool Sequential { get; set; }

    /// <summary>Gets or sets the error to show.</summary>
    public string Error { get; set; }

    /// <summary>Gets or sets the course of the quiz, to go back to the player.</summary>
    public string CourseId { get; set; }
}
