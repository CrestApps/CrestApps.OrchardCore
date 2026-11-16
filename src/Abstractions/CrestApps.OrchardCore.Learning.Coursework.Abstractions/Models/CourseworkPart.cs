using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Coursework.Models;

/// <summary>
/// Coursework in a course outline: work a learner hands in as text, files or both, which a grader scores against a
/// rubric or out of a number of points. The instructions are the item's HTML body.
/// </summary>
public sealed class CourseworkPart : ContentPart
{
    /// <summary>
    /// Gets or sets when the work is due, in UTC; work handed in after it is marked late. Empty means no due date.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets the percentage taken off the score for each day, or part of a day, the work is late. Zero means no
    /// late penalty.
    /// </summary>
    public decimal LatePenaltyPercentPerDay { get; set; }

    /// <summary>
    /// Gets or sets the largest late penalty, as a percentage from 0 to 100.
    /// </summary>
    public decimal MaxLatePenaltyPercent { get; set; } = 100;

    /// <summary>
    /// Gets or sets how many whole days after the due date the coursework stops taking work, or
    /// <see langword="null"/> to take late work at any time. Work a grader returned can still be handed in again.
    /// </summary>
    public int? CloseAfterDays { get; set; }

    /// <summary>
    /// Gets or sets what the learner hands in.
    /// </summary>
    public CourseworkSubmissionType SubmissionType { get; set; } = CourseworkSubmissionType.TextAndFiles;

    /// <summary>
    /// Gets or sets the file extensions a learner may hand in, such as <c>.pdf</c>; empty allows any.
    /// </summary>
    public List<string> AllowedExtensions { get; set; } = [".pdf", ".docx", ".txt"];

    /// <summary>
    /// Gets or sets the largest file a learner may hand in, in megabytes.
    /// </summary>
    public int MaxFileSizeMb { get; set; } = 10;

    /// <summary>
    /// Gets or sets the most files one submission may have.
    /// </summary>
    public int MaxFiles { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether the learner may hand the work in again until it is graded. A grader can always return work
    /// for resubmission.
    /// </summary>
    public bool AllowResubmission { get; set; }

    /// <summary>
    /// Gets or sets the rubric the work is graded against, or <see langword="null"/> to grade out of
    /// <see cref="MaxPoints"/>.
    /// </summary>
    public string RubricId { get; set; }

    /// <summary>
    /// Gets or sets the points the work is graded out of when it has no rubric.
    /// </summary>
    public decimal MaxPoints { get; set; } = 100;
}

/// <summary>
/// What a learner hands in for coursework.
/// </summary>
public enum CourseworkSubmissionType
{
    /// <summary>
    /// Text typed in the page.
    /// </summary>
    Text,

    /// <summary>
    /// One or more files.
    /// </summary>
    Files,

    /// <summary>
    /// Text, files, or both.
    /// </summary>
    TextAndFiles,
}
