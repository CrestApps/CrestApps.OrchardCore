using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Lti.Models;

/// <summary>
/// An External tool course item: the registered tool it launches (an LTI resource link) and what the launch adds.
/// </summary>
public sealed class ExternalToolItemPart : ContentPart
{
    /// <summary>
    /// Gets or sets the id of the <see cref="ExternalTool"/>.
    /// </summary>
    public string ToolId { get; set; }

    /// <summary>
    /// Gets or sets the target link URI of this link, or <see langword="null"/> for the tool's default.
    /// </summary>
    public string TargetLinkUri { get; set; }

    /// <summary>
    /// Gets or sets the custom parameters of this link, one <c>name=value</c> per line, added to the tool's.
    /// </summary>
    public string CustomParameters { get; set; }

    /// <summary>
    /// Gets or sets the maximum score of the link's line item, or <see langword="null"/> when the link has no line item
    /// Learning made for it.
    /// </summary>
    public decimal? ScoreMaximum { get; set; }

    /// <summary>
    /// Gets or sets the label of the link's line item.
    /// </summary>
    public string LineItemLabel { get; set; }

    /// <summary>
    /// Gets or sets the resource id the tool gave the line item, if any.
    /// </summary>
    public string LineItemResourceId { get; set; }

    /// <summary>
    /// Gets or sets the tag the tool gave the line item, if any.
    /// </summary>
    public string LineItemTag { get; set; }

    /// <summary>
    /// Gets or sets the content item JSON of the Deep Linking response that made the link, kept as the tool sent it.
    /// </summary>
    public string DeepLinkedContent { get; set; }
}
