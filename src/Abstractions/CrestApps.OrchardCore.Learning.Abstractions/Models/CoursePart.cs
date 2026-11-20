using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// Marks a content type as a course: its code, enrollment settings, completion criteria, prerequisites, pass mark and
/// staff. The outline is in <see cref="CourseOutlinePart"/>; the title, body, cover and categories are Orchard parts and
/// fields. The language comes from Orchard's localization part.
/// </summary>
public sealed class CoursePart : ContentPart
{
    /// <summary>
    /// Gets or sets the course code, such as <c>SAFE-101</c>.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets a short summary shown in the catalog.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets the level, an open string such as <c>Beginner</c>.
    /// </summary>
    public string Level { get; set; }

    /// <summary>
    /// Gets or sets the estimated effort, in minutes.
    /// </summary>
    public int? EstimatedMinutes { get; set; }

    /// <summary>
    /// Gets or sets how a learner gets into the course on their own.
    /// </summary>
    public EnrollmentMode EnrollmentMode { get; set; }

    /// <summary>
    /// Gets or sets how many days a self enrollment gives access, or <see langword="null"/> for no end.
    /// </summary>
    public int? AccessDays { get; set; }

    /// <summary>
    /// Gets or sets when the course starts, in UTC, or <see langword="null"/> when it is open from the start. Before it,
    /// learners see the course and may enroll, but cannot open its items.
    /// </summary>
    public DateTime? StartsUtc { get; set; }

    /// <summary>
    /// Gets or sets when the course ends, in UTC, or <see langword="null"/> for no end. As in Moodle, the end date does
    /// not lock the course: learners keep their access, and the course is shown as ended instead of in progress.
    /// </summary>
    public DateTime? EndsUtc { get; set; }

    /// <summary>
    /// Gets or sets where the course was imported from, such as <c>moodle:1a2b3c4d:course:12</c>, so importing it again
    /// updates it; <see langword="null"/> for a course made on the site.
    /// </summary>
    public string SourceIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the course pass mark as a percentage, or <see langword="null"/> when the course has no grade.
    /// </summary>
    public decimal? PassMark { get; set; }

    /// <summary>
    /// Gets or sets the gradebook categories. Empty means the course grade is the weighted mean of the gradable items.
    /// </summary>
    public IList<GradeCategory> GradeCategories { get; set; } = [];

    /// <summary>
    /// Gets or sets what a learner must hold before enrolling.
    /// </summary>
    public IList<CoursePrerequisite> Prerequisites { get; set; } = [];

    /// <summary>
    /// Gets or sets the completion criteria beyond "all required items". All of them must be met.
    /// </summary>
    public IList<LearningRuleSettings> CompletionCriteria { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the course has no completion criteria: it never completes, while the learner's progress is
    /// still kept (a Moodle course without completion criteria).
    /// </summary>
    public bool NeverCompletes { get; set; }

    /// <summary>
    /// Gets or sets the staff of the course.
    /// </summary>
    public IList<CourseStaffMember> Staff { get; set; } = [];
}
