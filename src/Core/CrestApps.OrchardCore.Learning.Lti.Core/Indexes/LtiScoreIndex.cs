using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Indexes;

/// <summary>
/// Finds scores by line item, learner and course.
/// </summary>
public sealed class LtiScoreIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the line item id.
    /// </summary>
    public string LineItemId { get; set; }

    /// <summary>
    /// Gets or sets the course id.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the user id of the learner.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the tool id.
    /// </summary>
    public string ToolId { get; set; }
}
