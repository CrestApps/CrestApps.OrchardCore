namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// What an enrollment proves once it is completed: the course as it was and each required item with its result. It
/// copies what it needs, so it reads the same after Orchard Core prunes old content versions.
/// </summary>
public sealed class CompletionSnapshot
{
    /// <summary>
    /// Gets or sets the course title at completion.
    /// </summary>
    public string CourseTitle { get; set; }

    /// <summary>
    /// Gets or sets the course code at completion.
    /// </summary>
    public string CourseCode { get; set; }

    /// <summary>
    /// Gets or sets the content item version id of the course at completion, for reference only.
    /// </summary>
    public string CourseVersionId { get; set; }

    /// <summary>
    /// Gets or sets when the course was completed, in UTC.
    /// </summary>
    public DateTime CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the course grade as a percentage, when the course has one.
    /// </summary>
    public decimal? Grade { get; set; }

    /// <summary>
    /// Gets or sets whether the grade met the course pass mark, when the course has one.
    /// </summary>
    public bool? Passed { get; set; }

    /// <summary>
    /// Gets or sets whether nothing in the course gave the learner a grade at completion (no item counted toward it, or
    /// every one that did recorded the learner's completion only), which is why <see cref="Grade"/> is
    /// <see langword="null"/>. A minimum grade is met by such a completion; a missing grade for any other reason (an
    /// imported enrollment, a course that couldn't be loaded) is not.
    /// </summary>
    public bool NoGradeSource { get; set; }

    /// <summary>
    /// Gets or sets the total time spent on the course items, in seconds.
    /// </summary>
    public long SecondsSpent { get; set; }

    /// <summary>
    /// Gets or sets the person who completed the enrollment by hand, when it was not the completion criteria.
    /// </summary>
    public string CompletedBy { get; set; }

    /// <summary>
    /// Gets or sets why the enrollment was completed by hand.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the items of the outline with their results.
    /// </summary>
    public IList<CompletionSnapshotItem> Items { get; set; } = [];
}
