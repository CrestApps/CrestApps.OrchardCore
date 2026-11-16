using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Coursework.Models;

/// <summary>
/// One hand-in of coursework by a learner: the text and files, when it came, whether it was late, and the grade and
/// feedback of a grader. A resubmission is a new submission with the next attempt number; the earlier one stays as it
/// was and is no longer current.
/// </summary>
public sealed class Submission : CatalogItem
{
    /// <summary>
    /// Gets or sets the enrollment the work was handed in under.
    /// </summary>
    public string EnrollmentId { get; set; }

    /// <summary>
    /// Gets or sets the learner.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the course.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the coursework course item.
    /// </summary>
    public string CourseworkId { get; set; }

    /// <summary>
    /// Gets or sets the title of the coursework when the work was handed in, for lists.
    /// </summary>
    public string CourseworkTitle { get; set; }

    /// <summary>
    /// Gets or sets the attempt number, from one.
    /// </summary>
    public int Attempt { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether this is the learner's latest submission for the coursework under the enrollment.
    /// </summary>
    public bool Current { get; set; } = true;

    /// <summary>
    /// Gets or sets the text the learner typed.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the files the learner handed in, kept in the protected file store.
    /// </summary>
    public List<SubmissionFile> Files { get; set; } = [];

    /// <summary>
    /// Gets or sets when the work was handed in, in UTC.
    /// </summary>
    public DateTime SubmittedUtc { get; set; }

    /// <summary>
    /// Gets or sets the due date of the coursework when the work was handed in, in UTC, or <see langword="null"/> when it
    /// had none. The late flag and the late penalty are worked out from it, so moving the due date afterwards changes
    /// neither.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the work came after the due date.
    /// </summary>
    public bool Late { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public SubmissionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the grade as a percentage, after the late penalty. This is the score that counts in the course
    /// grade.
    /// </summary>
    public decimal? Score { get; set; }

    /// <summary>
    /// Gets or sets the grade as a percentage before the late penalty.
    /// </summary>
    public decimal? RawScore { get; set; }

    /// <summary>
    /// Gets or sets the late penalty taken off the raw score, as a percentage of it.
    /// </summary>
    public decimal? LatePenaltyPercent { get; set; }

    /// <summary>
    /// Gets or sets how many days, counting a part of a day as a day, the work came after the due date when it was
    /// first graded.
    /// </summary>
    public int? DaysLate { get; set; }

    /// <summary>
    /// Gets or sets the points given.
    /// </summary>
    public decimal? Points { get; set; }

    /// <summary>
    /// Gets or sets the points the work was graded out of.
    /// </summary>
    public decimal? MaxPoints { get; set; }

    /// <summary>
    /// Gets or sets the rubric the work was graded against, or <see langword="null"/>.
    /// </summary>
    public string RubricId { get; set; }

    /// <summary>
    /// Gets or sets the level chosen for each rubric criterion, by criterion id.
    /// </summary>
    public Dictionary<string, string> RubricChoices { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the grader's feedback to the learner.
    /// </summary>
    public string Feedback { get; set; }

    /// <summary>
    /// Gets or sets the grader.
    /// </summary>
    public string GraderId { get; set; }

    /// <summary>
    /// Gets or sets when the work was graded, in UTC.
    /// </summary>
    public DateTime? GradedUtc { get; set; }

    /// <summary>
    /// Gets or sets who returned the work for resubmission.
    /// </summary>
    public string ReturnedBy { get; set; }

    /// <summary>
    /// Gets or sets when the work was returned for resubmission, in UTC.
    /// </summary>
    public DateTime? ReturnedUtc { get; set; }

    /// <summary>
    /// Gets or sets the earlier grades, oldest first, each kept when the work was graded again.
    /// </summary>
    public List<SubmissionGrade> History { get; set; } = [];
}

/// <summary>
/// The state of a submission.
/// </summary>
public enum SubmissionStatus
{
    /// <summary>
    /// Handed in and waiting for a grader.
    /// </summary>
    Submitted,

    /// <summary>
    /// Graded.
    /// </summary>
    Graded,

    /// <summary>
    /// Returned to the learner to hand in again.
    /// </summary>
    Returned,
}

/// <summary>
/// A file of a submission in the protected file store.
/// </summary>
public sealed class SubmissionFile
{
    /// <summary>
    /// Gets or sets the id of the file in the protected store.
    /// </summary>
    public string FileId { get; set; }

    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    public long Size { get; set; }
}

/// <summary>
/// A grade a submission had before it was graded again.
/// </summary>
public sealed class SubmissionGrade
{
    /// <summary>
    /// Gets or sets the grade as a percentage, after the late penalty.
    /// </summary>
    public decimal? Score { get; set; }

    /// <summary>
    /// Gets or sets the grade as a percentage before the late penalty.
    /// </summary>
    public decimal? RawScore { get; set; }

    /// <summary>
    /// Gets or sets the late penalty, as a percentage of the raw score.
    /// </summary>
    public decimal? LatePenaltyPercent { get; set; }

    /// <summary>
    /// Gets or sets the points.
    /// </summary>
    public decimal? Points { get; set; }

    /// <summary>
    /// Gets or sets the points the work was graded out of.
    /// </summary>
    public decimal? MaxPoints { get; set; }

    /// <summary>
    /// Gets or sets the rubric choices, by criterion id.
    /// </summary>
    public Dictionary<string, string> RubricChoices { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the feedback.
    /// </summary>
    public string Feedback { get; set; }

    /// <summary>
    /// Gets or sets the grader.
    /// </summary>
    public string GraderId { get; set; }

    /// <summary>
    /// Gets or sets when it was graded, in UTC.
    /// </summary>
    public DateTime? GradedUtc { get; set; }

    /// <summary>
    /// Gets or sets who replaced this grade.
    /// </summary>
    public string ReplacedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was replaced, in UTC.
    /// </summary>
    public DateTime ReplacedUtc { get; set; }

    /// <summary>
    /// Gets or sets why it was replaced.
    /// </summary>
    public string Reason { get; set; }
}
