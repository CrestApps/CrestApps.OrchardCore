using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Coursework.Models;

/// <summary>
/// A rubric: criteria, each with levels worth a number of points. A grader picks one level per criterion.
/// </summary>
public sealed class RubricPart : ContentPart
{
    /// <summary>
    /// Gets or sets the criteria, in order.
    /// </summary>
    public List<RubricCriterion> Criteria { get; set; } = [];
}

/// <summary>
/// One thing a rubric scores.
/// </summary>
public sealed class RubricCriterion
{
    /// <summary>
    /// Gets or sets the id, which a grade's choices refer to.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets what the grader looks for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the levels, in order.
    /// </summary>
    public List<RubricLevel> Levels { get; set; } = [];
}

/// <summary>
/// A level of a rubric criterion.
/// </summary>
public sealed class RubricLevel
{
    /// <summary>
    /// Gets or sets the id, which a grade's choices refer to.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the title, such as Excellent.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the points the level is worth.
    /// </summary>
    public decimal Points { get; set; }
}
