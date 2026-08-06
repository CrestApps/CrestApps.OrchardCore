using CrestApps.OrchardCore.Recipes.Core.Schemas.Placements;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Produces the JSON schema and metadata that describe a single placement node filter, such as <c>path</c>,
/// <c>contentType</c> or <c>contentPart</c>, on a placement node of the <c>Placements</c> recipe step.
/// </summary>
/// <remarks>
/// Implement this interface when a module contributes a custom
/// <c>IPlacementNodeFilterProvider</c> and wants the generated recipe schema to describe the filter's value.
/// Registering the implementation as <see cref="IPlacementNodeFilterSchemaDefinition"/> is enough for the
/// <c>Placements</c> recipe step to pick it up. Prefer deriving from
/// <see cref="CrestApps.OrchardCore.Recipes.Core.Schemas.Placements.PlacementNodeFilterSchemaDefinitionBase"/>.
/// </remarks>
public interface IPlacementNodeFilterSchemaDefinition
{
    /// <summary>
    /// Gets the filter key that this definition describes, matching the placement node filter provider key,
    /// for example <c>path</c>.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Builds the schema and metadata describing the placement node filter.
    /// </summary>
    /// <param name="context">The context describing the filter being documented.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<PlacementNodeFilterSchema> GetFilterSchemaAsync(PlacementNodeFilterSchemaContext context, CancellationToken cancellationToken = default);
}
