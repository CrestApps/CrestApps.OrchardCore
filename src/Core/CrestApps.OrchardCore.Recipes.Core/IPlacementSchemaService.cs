using CrestApps.OrchardCore.Recipes.Core.Schemas.Placements;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Builds JSON schemas describing the placement node filters available on the current tenant, used by the
/// <c>Placements</c> recipe step to describe each placement node.
/// </summary>
public interface IPlacementSchemaService
{
    /// <summary>
    /// Gets a descriptor for every placement node filter contributed through an
    /// <see cref="IPlacementNodeFilterSchemaDefinition"/>.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<IReadOnlyList<PlacementNodeFilterDescriptor>> GetFilterDescriptorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the schema describing a single placement node, including the shared members and every
    /// contributed filter.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetPlacementNodeSchemaAsync(CancellationToken cancellationToken = default);
}
