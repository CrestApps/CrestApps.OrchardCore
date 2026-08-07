using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Placements;

/// <summary>
/// Describes a placement node filter discovered from the registered
/// <see cref="IPlacementNodeFilterSchemaDefinition"/> contributions.
/// </summary>
public sealed class PlacementNodeFilterDescriptor
{
    /// <summary>
    /// Gets the filter key, matching the placement node filter provider key, for example <c>path</c>.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the human readable filter title.
    /// </summary>
    public string DisplayText { get; init; }

    /// <summary>
    /// Gets a description explaining what the filter matches.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Gets the schema of the filter value, added to the placement node under the filter key.
    /// </summary>
    public JsonSchemaBuilder ValueSchema { get; init; }
}
