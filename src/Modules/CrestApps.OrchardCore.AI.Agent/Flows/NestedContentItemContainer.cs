using System.Text.Json.Nodes;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.AI.Agent.Flows;

/// <summary>
/// One list of nested content items inside a content item: the widgets of a Flow part, the items of a Bag part, or the
/// widgets of one zone of a widgets list part.
/// </summary>
internal sealed class NestedContentItemContainer
{
    /// <summary>
    /// Gets the kind of part that holds the list: flow, bag or widgetZone.
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// Gets the name of the part on the content item that owns the list.
    /// </summary>
    public string PartName { get; init; }

    /// <summary>
    /// Gets the zone of the list, for a widgets list part.
    /// </summary>
    public string Zone { get; init; }

    /// <summary>
    /// Gets the JSON array that holds the nested content items.
    /// </summary>
    public JsonArray Items { get; init; }

    /// <summary>
    /// Gets the JSON of the content item that owns the list.
    /// </summary>
    public JsonObject Owner { get; init; }

    /// <summary>
    /// Gets the ID of the nested content item that owns the list, or <see langword="null"/> when the top content item owns it.
    /// </summary>
    public string OwnerId { get; init; }

    /// <summary>
    /// Gets the definition of the part on the content type of the owner.
    /// </summary>
    public ContentTypePartDefinition TypePart { get; init; }

    /// <summary>
    /// Gets the name of the part of the top content item that this list is in, at any depth.
    /// </summary>
    public string TopLevelPartName { get; init; }

    /// <summary>
    /// Gets the depth of the items of the list: 1 for the items of the top content item.
    /// </summary>
    public int Depth { get; init; }
}
