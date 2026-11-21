namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// One piece of work on a grading page, as other modules see it to add their help next to the grade fields: a quiz
/// essay answer or a coursework submission. A grading page renders it through
/// <c>IDisplayManager&lt;GradingAssistContext&gt;</c>, so a module adds a panel with a display driver; with no driver it
/// renders nothing. A panel may fill the page's fields, but only the grader's own save records a grade.
/// </summary>
public sealed class GradingAssistContext
{
    /// <summary>
    /// The kind of an essay answer in a quiz attempt.
    /// </summary>
    public const string AttemptItemKind = "AttemptItem";

    /// <summary>
    /// The kind of a coursework submission.
    /// </summary>
    public const string SubmissionKind = "Submission";

    /// <summary>
    /// Gets or sets the kind of work: <see cref="AttemptItemKind"/> or <see cref="SubmissionKind"/>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the attempt or submission id.
    /// </summary>
    public string RecordId { get; set; }

    /// <summary>
    /// Gets or sets the index of the question in the attempt, for an essay answer.
    /// </summary>
    public int? ItemIndex { get; set; }

    /// <summary>
    /// Gets or sets the most points the work can get.
    /// </summary>
    public decimal MaxPoints { get; set; }

    /// <summary>
    /// Gets or sets whether the page lets the grader change the grade now.
    /// </summary>
    public bool CanEdit { get; set; }

    /// <summary>
    /// Gets or sets the name of the form field that holds the points, or <see langword="null"/> when the work is graded
    /// with a rubric.
    /// </summary>
    public string PointsField { get; set; }

    /// <summary>
    /// Gets or sets the name of the form field that holds the feedback.
    /// </summary>
    public string FeedbackField { get; set; }

    /// <summary>
    /// Gets or sets the start of the names of the rubric's form fields, which end with <c>[criterionId]</c>, or
    /// <see langword="null"/> when the work is graded with points.
    /// </summary>
    public string RubricFieldPrefix { get; set; }

    /// <summary>
    /// Gets or sets the name of the form field a panel sets to <c>true</c> when the grader used its help, so the grade
    /// record says it was assisted.
    /// </summary>
    public string AssistedField { get; set; }
}
