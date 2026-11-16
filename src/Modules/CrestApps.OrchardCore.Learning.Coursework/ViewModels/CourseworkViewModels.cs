using CrestApps.OrchardCore.Learning.Coursework.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Coursework.ViewModels;

/// <summary>
/// The editor of the coursework part.
/// </summary>
public class CourseworkPartViewModel
{
    /// <summary>Gets or sets the due date, in the editor's time zone.</summary>
    public DateTime? Due { get; set; }

    /// <summary>Gets or sets the percentage taken off a score for each day late.</summary>
    public decimal LatePenaltyPercentPerDay { get; set; }

    /// <summary>Gets or sets the largest late penalty, as a percentage.</summary>
    public decimal MaxLatePenaltyPercent { get; set; }

    /// <summary>Gets or sets how many whole days after the due date the coursework stops taking work.</summary>
    public int? CloseAfterDays { get; set; }

    /// <summary>Gets or sets what the learner hands in.</summary>
    public CourseworkSubmissionType SubmissionType { get; set; }

    /// <summary>Gets or sets the allowed file extensions, separated by commas.</summary>
    public string AllowedExtensions { get; set; }

    /// <summary>Gets or sets the largest file, in megabytes.</summary>
    public int MaxFileSizeMb { get; set; }

    /// <summary>Gets or sets the most files.</summary>
    public int MaxFiles { get; set; }

    /// <summary>Gets or sets whether the learner may hand the work in again until it is graded.</summary>
    public bool AllowResubmission { get; set; }

    /// <summary>Gets or sets the rubric.</summary>
    public string RubricId { get; set; }

    /// <summary>Gets or sets the points the work is graded out of without a rubric.</summary>
    public decimal MaxPoints { get; set; }

    /// <summary>Gets or sets the coursework item.</summary>
    [BindNever]
    public string CourseworkId { get; set; }

    /// <summary>Gets or sets whether the item is not saved yet.</summary>
    [BindNever]
    public bool IsNew { get; set; }

    /// <summary>Gets or sets the published rubrics.</summary>
    [BindNever]
    public IList<SelectListItem> RubricOptions { get; set; } = [];

    /// <summary>Gets or sets the submission types.</summary>
    [BindNever]
    public IList<SelectListItem> SubmissionTypeOptions { get; set; } = [];
}

/// <summary>
/// The editor of the rubric part.
/// </summary>
public class RubricPartViewModel
{
    /// <summary>Gets or sets the criteria.</summary>
    public List<RubricCriterionRow> Criteria { get; set; } = [];
}

/// <summary>
/// A criterion row of the rubric editor.
/// </summary>
public class RubricCriterionRow
{
    /// <summary>Gets or sets the id; empty for a new criterion.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets what the grader looks for.</summary>
    public string Description { get; set; }

    /// <summary>Gets or sets the levels.</summary>
    public List<RubricLevelRow> Levels { get; set; } = [];
}

/// <summary>
/// A level row of the rubric editor.
/// </summary>
public class RubricLevelRow
{
    /// <summary>Gets or sets the id; empty for a new level.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the points.</summary>
    public decimal? Points { get; set; }
}

/// <summary>
/// The model of one rubric criterion row partial.
/// </summary>
public class RubricCriterionRowModel
{
    /// <summary>Gets or sets the prefix of the criteria field names.</summary>
    public string Prefix { get; set; }

    /// <summary>Gets or sets the index of the criterion, or a placeholder in the template.</summary>
    public string Index { get; set; }

    /// <summary>Gets or sets the criterion.</summary>
    public RubricCriterionRow Criterion { get; set; }
}

/// <summary>
/// The coursework panel of the learner player.
/// </summary>
public class CourseworkPanelViewModel
{
    /// <summary>Gets or sets the coursework item.</summary>
    public string CourseworkId { get; set; }

    /// <summary>Gets or sets the coursework part.</summary>
    public CourseworkPart Part { get; set; }

    /// <summary>Gets or sets whether someone is signed in.</summary>
    public bool SignedIn { get; set; }

    /// <summary>Gets or sets whether the learner is enrolled in the course.</summary>
    public bool Enrolled { get; set; }

    /// <summary>Gets or sets whether the enrollment is active.</summary>
    public bool EnrollmentActive { get; set; }

    /// <summary>Gets or sets the learner's submissions, newest first.</summary>
    public IList<Submission> Submissions { get; set; } = [];

    /// <summary>Gets the learner's latest submission.</summary>
    public Submission Latest => Submissions.Count > 0 ? Submissions[0] : null;

    /// <summary>Gets or sets whether the learner can hand work in now.</summary>
    public bool CanSubmit { get; set; }

    /// <summary>Gets or sets whether the due date passed.</summary>
    public bool PastDue { get; set; }

    /// <summary>Gets or sets when the coursework stops taking work, in UTC, or <see langword="null"/> when it never does.</summary>
    public DateTime? CloseUtc { get; set; }

    /// <summary>Gets or sets whether the coursework stopped taking work.</summary>
    public bool Closed { get; set; }

    /// <summary>Gets or sets the rubric the work is graded against, or <see langword="null"/>.</summary>
    public RubricPart Rubric { get; set; }
}

/// <summary>
/// The submissions of a coursework item.
/// </summary>
public class CourseworkSubmissionsViewModel
{
    /// <summary>Gets or sets the coursework item.</summary>
    [BindNever]
    public ContentItem Coursework { get; set; }

    /// <summary>Gets or sets the submissions.</summary>
    [BindNever]
    public AdminListContext<SubmissionRow> List { get; set; }
}

/// <summary>
/// A row of the submissions list.
/// </summary>
public class SubmissionRow
{
    /// <summary>Gets or sets the submission.</summary>
    public Submission Submission { get; set; }

    /// <summary>Gets or sets the name of the learner.</summary>
    public string LearnerName { get; set; }
}

/// <summary>
/// The grading page of one submission.
/// </summary>
public class SubmissionGradingViewModel
{
    /// <summary>Gets or sets the submission.</summary>
    [BindNever]
    public Submission Submission { get; set; }

    /// <summary>Gets or sets the coursework item.</summary>
    [BindNever]
    public ContentItem Coursework { get; set; }

    /// <summary>Gets or sets the coursework part.</summary>
    [BindNever]
    public CourseworkPart Part { get; set; }

    /// <summary>Gets or sets the rubric, or <see langword="null"/> to grade out of points.</summary>
    [BindNever]
    public RubricPart Rubric { get; set; }

    /// <summary>Gets or sets the name of the learner.</summary>
    [BindNever]
    public string LearnerName { get; set; }

    /// <summary>Gets or sets the names of graders in the history, by user id.</summary>
    [BindNever]
    public IDictionary<string, string> Names { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets or sets the address of each file, by file id.</summary>
    [BindNever]
    public IDictionary<string, string> FileUrls { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets or sets the earlier attempts of the learner, newest first.</summary>
    [BindNever]
    public IList<Submission> EarlierAttempts { get; set; } = [];

    /// <summary>Gets or sets whether the submission can be graded now.</summary>
    [BindNever]
    public bool CanGrade { get; set; }

    /// <summary>Gets or sets the days the work was late: the ones kept at its first grade, or else the ones the policy gives now.</summary>
    [BindNever]
    public int DaysLate { get; set; }

    /// <summary>Gets or sets the late penalty the grade takes, as a percentage of the raw score.</summary>
    [BindNever]
    public decimal LatePenaltyPercent { get; set; }

    /// <summary>Gets or sets the level chosen for each criterion, by criterion id.</summary>
    public Dictionary<string, string> Choices { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the points without a rubric.</summary>
    public decimal? Points { get; set; }

    /// <summary>Gets or sets the feedback.</summary>
    public string Feedback { get; set; }

    /// <summary>Gets or sets why graded work is graded again.</summary>
    public string Reason { get; set; }
}
