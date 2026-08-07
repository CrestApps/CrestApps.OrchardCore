namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Placements;

/// <summary>
/// Provides contextual information about a placement node filter while its recipe schema is being built.
/// </summary>
public sealed class PlacementNodeFilterSchemaContext
{
    /// <summary>
    /// Gets the filter key as reported by the schema definition.
    /// </summary>
    public required string Key { get; init; }
}
