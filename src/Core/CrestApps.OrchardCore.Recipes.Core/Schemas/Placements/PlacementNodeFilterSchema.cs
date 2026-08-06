using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Placements;

/// <summary>
/// Describes the recipe payload of a single placement node filter on a placement node of the
/// <c>Placements</c> recipe step.
/// </summary>
public sealed class PlacementNodeFilterSchema
{
    /// <summary>
    /// Gets or sets the human readable filter title.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a description explaining what the filter matches.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the schema of the filter value, added to the placement node under the filter key.
    /// </summary>
    public JsonSchemaBuilder ValueSchema { get; set; }
}
