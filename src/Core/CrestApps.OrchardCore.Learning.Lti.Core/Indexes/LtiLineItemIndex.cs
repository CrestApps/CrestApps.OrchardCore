using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Indexes;

/// <summary>
/// Finds line items by course, tool, resource link, resource id and tag, the filters of the line items service.
/// </summary>
public sealed class LtiLineItemIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the tool id.
    /// </summary>
    public string ToolId { get; set; }

    /// <summary>
    /// Gets or sets the course id.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the resource link (course item) id.
    /// </summary>
    public string ResourceLinkId { get; set; }

    /// <summary>
    /// Gets or sets the tool's resource id.
    /// </summary>
    public string ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the tool's tag.
    /// </summary>
    public string Tag { get; set; }

    /// <summary>
    /// Gets or sets when the line item was made, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
