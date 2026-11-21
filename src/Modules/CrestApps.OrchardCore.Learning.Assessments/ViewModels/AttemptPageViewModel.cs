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

    /// <summary>Gets or sets the questions shown on this page, each with what the learner may see of it.</summary>
    public IList<AttemptQuestionViewModel> Questions { get; set; } = [];

    /// <summary>Gets or sets whether the learner moves one page after another, without going back.</summary>
    public bool Sequential { get; set; }

    /// <summary>Gets or sets the page shown, from zero.</summary>
    public int Page { get; set; }

    /// <summary>Gets or sets how many pages the attempt has.</summary>
    public int PageCount { get; set; } = 1;

    /// <summary>Gets or sets the error to show.</summary>
    public string Error { get; set; }

    /// <summary>Gets or sets the course of the quiz, to go back to the player.</summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets how many questions on the attempt's other pages have no saved answer.
    /// </summary>
    public int UnansweredElsewhere { get; set; }

    /// <summary>
    /// Gets or sets the course item after the quiz in the outline, for the result page's Continue.
    /// </summary>
    public string NextItemId { get; set; }

    public string NextItemTitle { get; set; }

    /// <summary>
    /// Gets or sets whether the learner completed the course, so Continue opens the completion panel.
    /// </summary>
    public bool CourseCompleted { get; set; }
}

/// <summary>
/// One question as the learner sees it on the attempt or review page.
/// </summary>
public class AttemptQuestionViewModel
{
    /// <summary>Gets or sets the question.</summary>
    public AttemptItem Item { get; set; }

    /// <summary>Gets or sets the number shown, which skips descriptions, or <see langword="null"/> for a description.</summary>
    public int? Number { get; set; }

    /// <summary>Gets or sets what the learner may see of the question now.</summary>
    public ReviewShows Shows { get; set; }

    /// <summary>Gets or sets whether the answer is shown read-only: a review, or a question checked or locked.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Gets or sets whether the learner may check the answer now, with immediate feedback.</summary>
    public bool CanCheck { get; set; }

    /// <summary>Gets or sets the address that checks the answer.</summary>
    public string CheckUrl { get; set; }
}
