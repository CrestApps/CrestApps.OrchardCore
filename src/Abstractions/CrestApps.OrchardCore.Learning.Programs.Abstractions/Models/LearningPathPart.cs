using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Programs.Models;

/// <summary>
/// Makes a content item a learning path: ordered course sets. Sets joined by <see cref="LearningPathConnector.Then"/>
/// make stages that open one after another; inside a stage, sets joined by <see cref="LearningPathConnector.And"/>
/// all count and sets joined by <see cref="LearningPathConnector.Or"/> are alternatives (and binds tighter than or).
/// </summary>
public sealed class LearningPathPart : ContentPart
{
    /// <summary>
    /// Gets or sets the course sets, in order.
    /// </summary>
    public List<LearningPathCourseSet> Sets { get; set; } = [];

    /// <summary>
    /// Gets or sets whether a signed-in person can start the path from its page.
    /// </summary>
    public bool AllowSelfStart { get; set; }
}

/// <summary>
/// A set of courses in a learning path.
/// </summary>
public sealed class LearningPathCourseSet
{
    /// <summary>
    /// Gets or sets the id of the set within the path.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the title shown to learners.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets how this set joins the set before it; ignored for the first set.
    /// </summary>
    public LearningPathConnector Connector { get; set; }

    /// <summary>
    /// Gets or sets how many courses of the set must be completed.
    /// </summary>
    public LearningPathSetJoin Join { get; set; }

    /// <summary>
    /// Gets or sets the number of courses for <see cref="LearningPathSetJoin.AtLeast"/>.
    /// </summary>
    public int MinimumCount { get; set; }

    /// <summary>
    /// Gets or sets the content item ids of the courses.
    /// </summary>
    public List<string> CourseIds { get; set; } = [];
}

/// <summary>
/// How a course set joins the set before it.
/// </summary>
public enum LearningPathConnector
{
    /// <summary>
    /// The set opens once everything before it is done.
    /// </summary>
    Then,

    /// <summary>
    /// The set is open with the one before it, and both count.
    /// </summary>
    And,

    /// <summary>
    /// The set is open with the one before it, and either one counts.
    /// </summary>
    Or,
}

/// <summary>
/// How many courses of a set must be completed.
/// </summary>
public enum LearningPathSetJoin
{
    /// <summary>
    /// Every course.
    /// </summary>
    All,

    /// <summary>
    /// Any one course.
    /// </summary>
    Any,

    /// <summary>
    /// At least <see cref="LearningPathCourseSet.MinimumCount"/> courses.
    /// </summary>
    AtLeast,
}
