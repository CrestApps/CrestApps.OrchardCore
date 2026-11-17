using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AI.Agent.Flows;

/// <summary>
/// One nested content item and where it is.
/// </summary>
internal sealed class NestedContentItemEntry
{
    /// <summary>
    /// Gets the list that holds the nested content item.
    /// </summary>
    public NestedContentItemContainer Container { get; init; }

    /// <summary>
    /// Gets the position of the nested content item in its list, starting at 0.
    /// </summary>
    public int Position { get; init; }

    /// <summary>
    /// Gets the JSON of the nested content item.
    /// </summary>
    public JsonObject Item { get; init; }
}
