using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// Marks a content type as a course item: its kind, how it is completed, whether it is graded, whether it is a free
/// preview and the rules that open it. Any content type can carry it; the kind is registered with
/// <c>AddCourseItemKind</c>.
/// </summary>
public sealed class CourseItemPart : ContentPart
{
    /// <summary>
    /// Gets or sets the kind, such as <c>Page</c>, <c>Video</c> or <c>Quiz</c>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets how the item is completed, one of the modes its kind supports.
    /// </summary>
    public string CompletionMode { get; set; }

    /// <summary>
    /// Gets or sets the minimum time on the item, in minutes, for the <c>MinimumTime</c> completion mode.
    /// </summary>
    public int? MinimumMinutes { get; set; }

    /// <summary>
    /// Gets or sets the estimated time the item takes, in minutes.
    /// </summary>
    public int? EstimatedMinutes { get; set; }

    /// <summary>
    /// Gets or sets whether the item's score counts toward the course grade.
    /// </summary>
    public bool Gradable { get; set; }

    /// <summary>
    /// Gets or sets the weight of the item's score in the course grade.
    /// </summary>
    public decimal Weight { get; set; } = 1;

    /// <summary>
    /// Gets or sets the gradebook category of the course the item counts in, one of
    /// <see cref="CoursePart.GradeCategories"/>, or <see langword="null"/> for none.
    /// </summary>
    public string GradeCategoryId { get; set; }

    /// <summary>
    /// Gets or sets the score, as a percentage, that passes the item for the <c>Passed</c> completion mode.
    /// </summary>
    public decimal? PassMark { get; set; }

    /// <summary>
    /// Gets or sets whether anyone, enrolled or not, can open the item.
    /// </summary>
    public bool FreePreview { get; set; }

    /// <summary>
    /// Gets or sets whether a result the browser reports counts toward a certification. Off by default.
    /// </summary>
    public bool AllowSelfReported { get; set; }

    /// <summary>
    /// Gets or sets where the item was imported from, such as <c>moodle:1a2b3c4d:course:12:cm:345</c>, so importing it
    /// again updates it; <see langword="null"/> for an item made on the site.
    /// </summary>
    public string SourceIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the rules that must all be met before the item opens.
    /// </summary>
    public IList<LearningRuleSettings> ReleaseRules { get; set; } = [];
}
