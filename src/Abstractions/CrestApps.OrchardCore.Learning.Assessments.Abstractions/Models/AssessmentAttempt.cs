using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// One attempt of a quiz by a learner. It is self-contained: it holds a copy of every question as drawn, the responses
/// and the scores, so it never depends on a question version.
/// </summary>
public sealed class AssessmentAttempt : CatalogItem
{
    /// <summary>
    /// Gets or sets the learner.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the enrollment the attempt counts toward.
    /// </summary>
    public string EnrollmentId { get; set; }

    /// <summary>
    /// Gets or sets the course.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the quiz course item.
    /// </summary>
    public string QuizId { get; set; }

    /// <summary>
    /// Gets or sets the title of the quiz when the attempt started.
    /// </summary>
    public string QuizTitle { get; set; }

    /// <summary>
    /// Gets or sets the readable number, such as <c>A000001</c>.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the attempt number of the learner on this quiz, from one.
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public AttemptStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the attempt started, in UTC.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the deadline the server fixed, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? DeadlineUtc { get; set; }

    /// <summary>
    /// Gets or sets when the attempt was submitted, in UTC.
    /// </summary>
    public DateTime? SubmittedUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the server submitted it at the deadline.
    /// </summary>
    public bool AutoSubmitted { get; set; }

    /// <summary>
    /// Gets or sets the drawn items.
    /// </summary>
    public IList<AttemptItem> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets the points earned.
    /// </summary>
    public decimal? Score { get; set; }

    /// <summary>
    /// Gets or sets the points possible.
    /// </summary>
    public decimal MaxScore { get; set; }

    /// <summary>
    /// Gets or sets the score in percent.
    /// </summary>
    public decimal? Percent { get; set; }

    /// <summary>
    /// Gets or sets the pass mark that applied, in percent.
    /// </summary>
    public decimal? PassMark { get; set; }

    /// <summary>
    /// Gets or sets whether the attempt passed.
    /// </summary>
    public bool? Passed { get; set; }

    /// <summary>
    /// Gets or sets the review policy copied from the quiz when the attempt started.
    /// </summary>
    public ReviewPolicy ReviewPolicy { get; set; }

    /// <summary>
    /// Gets or sets when the quiz closed for everyone, copied from the quiz, for the review policy.
    /// </summary>
    public DateTime? QuizClosesUtc { get; set; }

    /// <summary>
    /// Gets or sets the review options copied from the quiz when the attempt started, or <see langword="null"/> to follow
    /// <see cref="ReviewPolicy"/>.
    /// </summary>
    public QuizReviewOptions ReviewOptions { get; set; }

    /// <summary>
    /// Gets or sets how many questions a page shows, copied from the quiz when the attempt started; 0 for every question
    /// on one page (one a page when the navigation is sequential).
    /// </summary>
    public int QuestionsPerPage { get; set; }

    /// <summary>
    /// Gets or sets the page the learner is on, from zero. A sequential quiz takes answers on this page only.
    /// </summary>
    public int CurrentPage { get; set; }

    /// <summary>
    /// Gets or sets when the learner gets feedback, copied from the quiz when the attempt started.
    /// </summary>
    public QuizFeedbackMode FeedbackMode { get; set; }

    /// <summary>
    /// Gets or sets the user id of the last grader.
    /// </summary>
    public string GradedBy { get; set; }

    /// <summary>
    /// Gets or sets when the attempt was graded, in UTC.
    /// </summary>
    public DateTime? GradedUtc { get; set; }

    /// <summary>
    /// Gets or sets the overall feedback of the grader.
    /// </summary>
    public string Feedback { get; set; }

    /// <summary>
    /// Gets or sets the statement the learner confirmed when starting.
    /// </summary>
    public string HonorStatement { get; set; }

    /// <summary>
    /// Gets or sets every score change, with who and why. Earlier scores are never lost.
    /// </summary>
    public IList<AttemptScoreChange> ScoreHistory { get; set; } = [];

    /// <summary>
    /// Gets whether the attempt can still be answered.
    /// </summary>
    public bool IsOpen => Status == AttemptStatus.InProgress;
}

/// <summary>
/// A change of an attempt score.
/// </summary>
public sealed class AttemptScoreChange
{
    /// <summary>
    /// Gets or sets when, in UTC.
    /// </summary>
    public DateTime ChangedUtc { get; set; }

    /// <summary>
    /// Gets or sets who.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets what kind of change: <c>Submitted</c>, <c>Graded</c>, <c>Regraded</c>, <c>Overridden</c> or <c>Voided</c>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the score before.
    /// </summary>
    public decimal? From { get; set; }

    /// <summary>
    /// Gets or sets the score after.
    /// </summary>
    public decimal? To { get; set; }

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason { get; set; }
}
