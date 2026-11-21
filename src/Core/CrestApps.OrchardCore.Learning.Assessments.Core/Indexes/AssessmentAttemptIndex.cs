using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;

/// <summary>
/// Indexes attempts by learner, quiz, course, state and dates.
/// </summary>
public sealed class AssessmentAttemptIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the learner.</summary>
    public string UserId { get; set; }

    /// <summary>Gets or sets the quiz.</summary>
    public string QuizId { get; set; }

    /// <summary>Gets or sets the course.</summary>
    public string CourseId { get; set; }

    /// <summary>Gets or sets the enrollment.</summary>
    public string EnrollmentId { get; set; }

    /// <summary>Gets or sets the number.</summary>
    public string Number { get; set; }

    /// <summary>Gets or sets the state.</summary>
    public AttemptStatus Status { get; set; }

    /// <summary>Gets or sets whether a grader must mark it.</summary>
    public bool NeedsGrading { get; set; }

    /// <summary>Gets or sets when it started, in UTC.</summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>Gets or sets the deadline, in UTC.</summary>
    public DateTime? DeadlineUtc { get; set; }

    /// <summary>Gets or sets when it was submitted, in UTC.</summary>
    public DateTime? SubmittedUtc { get; set; }

    /// <summary>Gets or sets the score in percent.</summary>
    public decimal? Percent { get; set; }

    /// <summary>Gets or sets whether it passed.</summary>
    public bool? Passed { get; set; }
}

/// <summary>
/// One row per drawn item of a submitted attempt, for item analysis.
/// </summary>
public sealed class AttemptItemIndex : global::YesSql.Indexes.MapIndex
{
    /// <summary>Gets or sets the attempt.</summary>
    public string AttemptId { get; set; }

    /// <summary>Gets or sets the quiz.</summary>
    public string QuizId { get; set; }

    /// <summary>Gets or sets the question.</summary>
    public string QuestionId { get; set; }

    /// <summary>Gets or sets the question type.</summary>
    public string QuestionType { get; set; }

    /// <summary>Gets or sets the points earned, or <see langword="null"/>.</summary>
    public decimal? Score { get; set; }

    /// <summary>Gets or sets the points possible.</summary>
    public decimal Points { get; set; }

    /// <summary>Gets or sets the score of the whole attempt in percent, for discrimination.</summary>
    public decimal? AttemptPercent { get; set; }

    /// <summary>Gets or sets when the attempt was submitted, in UTC.</summary>
    public DateTime? SubmittedUtc { get; set; }
}

/// <summary>
/// Indexes overrides by learner and quiz.
/// </summary>
public sealed class AssessmentOverrideIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the learner.</summary>
    public string UserId { get; set; }

    /// <summary>Gets or sets the quiz.</summary>
    public string QuizId { get; set; }

    /// <summary>Gets or sets when it was given, in UTC.</summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// Indexes grade scales by name.
/// </summary>
public sealed class GradeScaleIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; }
}
